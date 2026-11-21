using System.Collections.Generic;
using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Service;
using UnityEngine;

namespace Refraction.Presentation.Audio
{
    /// <summary>音效与 BGM 的唯一出口：请求只入队，统一在 Tick 消费，voice 走池复用</summary>
    /// <remarks>
    /// 由 GameRoot 注册成服务驱动，常驻不随场景销毁。单帧只消费 Tick 开始时已有的请求，
    /// 入队当帧就能听到，不会拖到下一帧；循环音（SetLoopSfx）不走队列，免得排队漏掉首帧。
    /// 时长一律取 clip.length。资源走 Resources.Load 取 Resources/Sound 下的文件并缓存，
    /// 同一个编号只读一次盘，之后全走内存。
    /// </remarks>
    public sealed class AudioManager : IService
    {
        private const string SoundFolder = "Sound/";

        // Unity AudioSource.priority：数值越小越优先，被偷时先偷数值大的
        private const int PriorityBgm = 0;
        private const int PriorityLoopSfx = 64;
        private const int PrioritySfx = 128;

        // 同时播放的一次性 SFX 软上限：防止 voice 耗尽、互相偷、听感糊
        private const int MaxConcurrentSfx = 16;

        private enum RequestType
        {
            Sfx,
            Bgm,
        }

        private readonly struct Request
        {
            public readonly RequestType Type;
            public readonly AudioId Id;
            public readonly bool Resume;
            public readonly bool Loop;
            public readonly bool Fade;

            public Request(RequestType type, AudioId id, bool resume = false, bool loop = true, bool fade = true)
            {
                Type = type;
                Id = id;
                Resume = resume;
                Loop = loop;
                Fade = fade;
            }
        }

        private enum FadeState
        {
            None,
            FadingOut,
            FadingIn,
        }

        private sealed class Playing
        {
            public GameObject Go;
            public AudioSource Source;
            public float Remaining;

            public void Reset()
            {
                Go = null;
                Source = null;
                Remaining = 0f;
            }
        }

        private readonly Transform _host;
        private readonly Dictionary<AudioId, string> _idToFile = new();
        private readonly Dictionary<AudioId, AudioClip> _cache = new();
        private readonly HashSet<AudioId> _warned = new();
        private readonly Queue<Request> _requests = new();
        private readonly List<Playing> _playing = new();
        private readonly Dictionary<AudioClip, float> _bgmProgress = new();

        private GameObject _rootGo;
        private Transform _audioRoot;
        private AudioSource _bgmSource;
        private AudioSource _loopSource;
        private Pool<GameObject> _voicePool;
        private Pool<Playing> _entryPool;

        private float _bgmVolume;
        private float _sfxVolume;
        private float _bgmFadeTime;
        private bool _initialized;

        private FadeState _fadeState;
        private float _fadeElapsed;
        private float _fadeFrom;
        private float _fadeTo;
        private AudioClip _pendingClip;
        private bool _pendingResume;
        private bool _pendingLoop;

        private bool _bgmShouldPlay;
        private bool _bgmStopWarned;

        public AudioManager(Transform host) => _host = host;

        public bool IsReady => _initialized;

        public float BgmVolume => _bgmVolume;

        public float SfxVolume => _sfxVolume;

        public void Init()
        {
            if (_initialized)
            {
                Debug.LogError("[音频] AudioManager.Init 被调用了两次：它只该由 GameRoot 调一次。");
                return;
            }

            LoadConfig();

            _rootGo = new GameObject("AudioRoot");
            _rootGo.transform.SetParent(_host, false);
            _audioRoot = _rootGo.transform;

            _voicePool = new Pool<GameObject>(
                CreateVoice,
                onGet: go => go.SetActive(true),
                onRelease: go => go.SetActive(false),
                name: "AudioVoice",
                maxSize: MaxConcurrentSfx);

            _entryPool = new Pool<Playing>(() => new Playing(), name: "AudioEntry");

            // 按并发上限预热：第一次音效不再现场建对象
            _voicePool.Prewarm(MaxConcurrentSfx);
            _entryPool.Prewarm(MaxConcurrentSfx);

            _bgmSource = CreateSource("MusicAudioSource", PriorityBgm, _bgmVolume);
            _loopSource = CreateSource("LoopSfxAudioSource", PriorityLoopSfx, _sfxVolume);

            _initialized = true;
        }

        /// <summary>先消费队列、再回收播完的音效，顺序反了会让本帧入队的音效晚一帧才播</summary>
        /// <remarks>用 unscaled 那个：音频不参与暂停冻结，否则 _playing 会在暂停时挂着播完的条目。</remarks>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!_initialized) return;

            ProcessRequests();

            UpdatePlaying(unscaledDeltaTime);

            UpdateBgmFade(unscaledDeltaTime);

            KeepBgmAlive();
        }

        public void EnqueueSfx(AudioId id) => _requests.Enqueue(new Request(RequestType.Sfx, id));

        /// <summary>请求播放 BGM，只入队。resumeFromLastTime 续播上次进度；isLoop=false 播完即停；fade=false 瞬时切换。</summary>
        public void EnqueueBgm(AudioId id, bool resumeFromLastTime = true, bool isLoop = true, bool fade = true)
            => _requests.Enqueue(new Request(RequestType.Bgm, id, resumeFromLastTime, isLoop, fade));

        /// <summary>开关一条循环音效（走路等持续音），同一时刻只保留一条</summary>
        /// <remarks>不走队列：排队会漏掉首帧。源常驻，持有者不再需要时必须关掉，否则切场景后还会响。</remarks>
        public void SetLoopSfx(AudioId id, bool play)
        {
            if (!_initialized || _loopSource == null) return;

            if (!play || id == AudioId.None)
            {
                if (_loopSource.isPlaying) _loopSource.Stop();

                return;
            }

            AudioClip clip = GetClip(id);

            if (clip == null) return;

            if (_loopSource.clip == clip && _loopSource.isPlaying) return;

            _loopSource.clip = clip;
            _loopSource.volume = _sfxVolume;
            _loopSource.Play();
        }

        public void StopAllSfx(bool clearPending = true)
        {
            if (!_initialized) return;

            if (clearPending) ClearPendingSfx();

            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                Playing entry = _playing[i];

                if (entry.Source != null)
                {
                    entry.Source.Stop();
                    entry.Source.clip = null;
                }

                if (entry.Go != null) _voicePool?.Release(entry.Go);

                entry.Reset();
                _entryPool?.Release(entry);
                _playing.RemoveAt(i);
            }

            if (_loopSource != null)
            {
                if (_loopSource.isPlaying) _loopSource.Stop();

                _loopSource.clip = null;
            }
        }

        public void SetBgmVolume(float value)
        {
            _bgmVolume = Mathf.Clamp01(value);

            if (_bgmSource == null) return;

            if (_fadeState == FadeState.FadingIn) _fadeTo = _bgmVolume;
            else if (_fadeState == FadeState.None) _bgmSource.volume = _bgmVolume;
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);

            for (int i = 0; i < _playing.Count; i++) _playing[i].Source.volume = _sfxVolume;

            if (_loopSource != null) _loopSource.volume = _sfxVolume;
        }

        public void Dispose()
        {
            if (!_initialized) return;

            _initialized = false;

            _requests.Clear();

            for (int i = 0; i < _playing.Count; i++)
            {
                Playing entry = _playing[i];

                if (entry.Source != null) entry.Source.clip = null;

                if (entry.Go != null) _voicePool?.Release(entry.Go);

                entry.Reset();
                _entryPool?.Release(entry);
            }

            _playing.Clear();
            _cache.Clear();
            _warned.Clear();
            _bgmProgress.Clear();

            _fadeState = FadeState.None;
            _bgmShouldPlay = false;
            _pendingClip = null;
            _pendingResume = false;
            _pendingLoop = true;
            _fadeElapsed = 0f;

            // 池里的 voice 都是音频根的子物体，销毁根即一并回收
            _voicePool?.Dispose();
            _entryPool?.Dispose();
            _voicePool = null;
            _entryPool = null;

            _bgmSource = null;
            _loopSource = null;

            if (_rootGo != null) Object.Destroy(_rootGo);

            _rootGo = null;
            _audioRoot = null;
        }

        private GameObject CreateVoice()
        {
            GameObject go = new("Voice");
            go.transform.SetParent(_audioRoot, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.priority = PrioritySfx;

            go.SetActive(false);

            return go;
        }

        private AudioSource CreateSource(string name, int priority, float volume)
        {
            GameObject go = new(name, typeof(AudioSource));
            go.transform.SetParent(_audioRoot, false);

            AudioSource source = go.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.priority = priority;
            source.volume = volume;

            return source;
        }

        private void ProcessRequests()
        {
            int count = _requests.Count;

            for (int i = 0; i < count; i++)
            {
                Request request = _requests.Dequeue();

                if (request.Type == RequestType.Sfx) PlaySfx(request.Id);
                else PlayBgm(request.Id, request.Resume, request.Loop, request.Fade);
            }
        }

        private void PlaySfx(AudioId id)
        {
            AudioClip clip = GetClip(id);

            if (clip == null) return;

            if (_playing.Count >= MaxConcurrentSfx) return;

            // 同 clip 去重：同一音效正在播时不重复叠加（防刷屏，也省 voice）
            for (int i = 0; i < _playing.Count; i++)
            {
                if (_playing[i].Source.clip == clip) return;
            }

            GameObject go = _voicePool.Get();

            if (go == null) return;

            AudioSource source = go.GetComponent<AudioSource>();

            if (source == null) return;

            // 池复用前保险：确保上一轮残留已停
            if (source.isPlaying) source.Stop();

            source.clip = clip;
            source.volume = _sfxVolume;
            source.priority = PrioritySfx;
            source.loop = false;
            source.Play();

            Playing entry = _entryPool.Get();
            entry.Go = go;
            entry.Source = source;
            entry.Remaining = clip.length;

            _playing.Add(entry);
        }

        private void PlayBgm(AudioId id, bool resumeLastTime, bool isLoop, bool fade)
        {
            AudioClip clip = GetClip(id);

            if (clip == null) return;

            // 非循环 BGM：每次都应从头播，续播参数无意义
            if (!isLoop) resumeLastTime = false;

            if (!fade)
            {
                _fadeState = FadeState.None;
                _fadeElapsed = 0f;
                _pendingClip = null;
                _pendingResume = false;
                _pendingLoop = true;

                SwitchBgmClip(clip, resumeLastTime, isLoop);
                _bgmSource.volume = _bgmVolume;

                return;
            }

            // 正在播同一个目标（含淡入中）忽略；淡出中允许覆盖待切目标
            if (isLoop && clip == _bgmSource.clip && _bgmSource.isPlaying && _fadeState != FadeState.FadingOut) return;

            if (_bgmSource.clip == null || !_bgmSource.isPlaying)
            {
                SwitchBgmClip(clip, resumeLastTime, isLoop);
                StartFadeIn();

                return;
            }

            _pendingClip = clip;
            _pendingResume = resumeLastTime;
            _pendingLoop = isLoop;
            StartFadeOut();
        }

        /// <summary>切换 BGM clip：从 0 音量起播，交给后续淡入推音量；非循环强制从头</summary>
        private void SwitchBgmClip(AudioClip clip, bool resumeLastTime, bool isLoop)
        {
            RememberBgmTime();

            _bgmSource.clip = clip;
            _bgmSource.volume = 0f;
            _bgmSource.loop = isLoop;
            _bgmSource.priority = PriorityBgm;

            if (isLoop && resumeLastTime && _bgmProgress.TryGetValue(clip, out float t) && t > 0f && t < clip.length)
                _bgmSource.time = t;
            else
                _bgmSource.time = 0f;

            _bgmSource.Play();

            _bgmShouldPlay = true;
        }

        private void RememberBgmTime()
        {
            if (_bgmSource == null) return;

            AudioClip current = _bgmSource.clip;

            if (current == null) return;

            _bgmProgress[current] = _bgmSource.time;
        }

        private void StartFadeIn()
        {
            _fadeState = FadeState.FadingIn;
            _fadeElapsed = 0f;
            _fadeFrom = _bgmSource.volume;
            _fadeTo = _bgmVolume;
            _pendingClip = null;
            _pendingResume = false;
            _pendingLoop = true;
        }

        private void StartFadeOut()
        {
            _fadeState = FadeState.FadingOut;
            _fadeElapsed = 0f;
            _fadeFrom = _bgmSource.volume;
            _fadeTo = 0f;
        }

        private void UpdateBgmFade(float unscaledDeltaTime)
        {
            if (_fadeState == FadeState.None) return;

            if (_bgmFadeTime <= 0f)
            {
                FinishBgmFade();

                return;
            }

            _fadeElapsed += unscaledDeltaTime;

            float t = Mathf.Clamp01(_fadeElapsed / _bgmFadeTime);
            _bgmSource.volume = Mathf.Lerp(_fadeFrom, _fadeTo, t);

            if (t >= 1f) FinishBgmFade();
        }

        private void FinishBgmFade()
        {
            if (_fadeState == FadeState.FadingIn)
            {
                _bgmSource.volume = _bgmVolume;
                _fadeState = FadeState.None;

                return;
            }

            if (_fadeState != FadeState.FadingOut)
            {
                _fadeState = FadeState.None;

                return;
            }

            _bgmSource.volume = 0f;

            if (_pendingClip != null)
            {
                AudioClip next = _pendingClip;
                bool resume = _pendingResume;
                bool loop = _pendingLoop;

                _pendingClip = null;
                _pendingResume = false;
                _pendingLoop = true;

                SwitchBgmClip(next, resume, loop);
                StartFadeIn();

                return;
            }

            RememberBgmTime();
            _bgmSource.Stop();
            _bgmShouldPlay = false;
            _fadeState = FadeState.None;
        }

        /// <summary>循环 BGM 被外部停掉时拉回来：音乐被踢走的最后一道保险</summary>
        private void KeepBgmAlive()
        {
            if (!_bgmShouldPlay || _fadeState != FadeState.None) return;
            if (_bgmSource == null || _bgmSource.clip == null || !_bgmSource.loop) return;
            if (_bgmSource.isPlaying) return;

            _bgmSource.Play();

            if (_bgmStopWarned) return;

            _bgmStopWarned = true;

            Debug.LogWarning("[音频] 循环 BGM 被外部停掉，已自动恢复：说明除 AudioManager 之外还有人在停 AudioSource。");
        }

        private void UpdatePlaying(float unscaledDeltaTime)
        {
            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                Playing entry = _playing[i];
                entry.Remaining -= unscaledDeltaTime;

                if (entry.Remaining > 0f && entry.Source.isPlaying) continue;

                entry.Source.clip = null;
                _voicePool.Release(entry.Go);
                _playing.RemoveAt(i);

                entry.Reset();
                _entryPool.Release(entry);
            }
        }

        private void ClearPendingSfx()
        {
            if (_requests.Count == 0) return;

            int count = _requests.Count;
            List<Request> kept = new(count);

            for (int i = 0; i < count; i++)
            {
                Request request = _requests.Dequeue();

                if (request.Type != RequestType.Sfx) kept.Add(request);
            }

            for (int i = 0; i < kept.Count; i++) _requests.Enqueue(kept[i]);
        }

        private void LoadConfig()
        {
            AudioConfig config = AudioConfig.Default;

            _bgmVolume = Mathf.Clamp01(config.bgmVolume);
            _sfxVolume = Mathf.Clamp01(config.sfxVolume);
            _bgmFadeTime = Mathf.Max(0f, config.bgmFadeDuration);

            _idToFile.Clear();

            AudioConfig.Entry[] entries = config.Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                AudioConfig.Entry entry = entries[i];

                if (string.IsNullOrEmpty(entry.fileName))
                {
                    Debug.LogWarning($"[音频] {entry.id} 没有配置文件名，这一条不会响。");

                    continue;
                }

                if (!_idToFile.TryAdd(entry.id, entry.fileName))
                    Debug.LogWarning($"[音频] AudioConfig 里 {entry.id} 重复配置，取第一条。");
            }
        }

        private AudioClip GetClip(AudioId id)
        {
            if (id == AudioId.None) return null;

            if (_cache.TryGetValue(id, out AudioClip cached)) return cached;

            if (!_idToFile.TryGetValue(id, out string fileName) || string.IsNullOrEmpty(fileName))
            {
                Warn(id, "AudioConfig 里没有它的文件名");

                return null;
            }

            AudioClip clip = Resources.Load<AudioClip>(SoundFolder + fileName);

            if (clip == null)
            {
                Warn(id, $"Resources/{SoundFolder}{fileName} 加载失败");

                return null;
            }

            _cache[id] = clip;

            return clip;
        }

        private void Warn(AudioId id, string reason)
        {
            if (!_warned.Add(id)) return;

            Debug.LogWarning($"[音频] {id}：{reason}。");
        }
    }
}
