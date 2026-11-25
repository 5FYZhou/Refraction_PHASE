using System;
using System.Collections;
using System.Collections.Generic;
using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Game;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Logic.Service;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Audio;
using Refraction.Presentation.Combat;
using Refraction.Presentation.Effects;
using Refraction.Presentation.Input;
using Refraction.Presentation.Level;
using Refraction.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.Presentation
{
    /// <summary>场景根：全场唯一常驻对象，持有输入源、关卡推进器与跨场景数据</summary>
    public class GameRoot : Singleton<GameRoot>, ISceneTransfer
    {
        private readonly List<ISceneRoot> _sceneRoots = new();
        private readonly List<ISceneRoot> _pendingAttach = new();
        private readonly List<IService> _services = new();
        private readonly SceneContext _scenectx = new();

        private InputProvider _inputProvider;
        private InputKeyMgr _inputKeys;
        private UIMgr _uiMgr;
        private ResMgr _resMgr;
        private PauseService _pauseService;
        private HitStopService _hitStopService;
        private LevelRoot _levelRoot;
        private EffectModule _effects;
        private AudioManager _audio;
        private GameManager _gameMgr;
        private UIInputProvider _uiInputProvider;

        private int _pendingDoorId;
        private MovementStateTag _pendingTag;
        private int _pendingFace;

        private bool _warnedNoLevel;

        private Coroutine _sceneLoad;
        private bool _levelReady;
        private bool _playerPlaced;

        /// <summary>给场景根用的输入源。玩家在自己的 Attach 里从这里取。</summary>
        public InputProvider Input => _inputProvider;

        public InputKeyMgr InputKeys => _inputKeys;

        public WorldPolarity World => _scenectx.World;

        public int LevelId => _levelRoot != null ? _levelRoot.LevelId : 0;

        public UIMgr UIMgr { get => _uiMgr ??= new UIMgr(); }
        public ResMgr ResMgr { get => _resMgr ??= new ResMgr(); }

        public PauseService Pause => _pauseService;

        public HitStopService HitStop => _hitStopService;

        public EffectModule Effects => _effects;

        public AudioManager Audio => _audio;

        public GameManager Game => _gameMgr;

        public CombatRoot Combat { get; private set; }

        public IReadOnlyList<ISceneRoot> SceneRoots => _sceneRoots;

        protected override void Awake()
        {
            base.Awake();

            // 重复实例要到帧末才销毁，Awake 仍会走完：必须原地退场，否则它会替真正的根把新场景再装配一遍，再带着刚建好的东西一起被销毁
            if (Instance != this)
            {
                enabled = false;
                return;
            }

            try
            {
                Assemble();
            }
            catch
            {
                enabled = false;
                throw;
            }
        }

        private void OnEnable()
        {
            // 手动 Disable 再 Enable 时输入源已被释放，必须靠这个空判断挡住
            if (_inputProvider == null) return;

            _inputProvider.Init();
            EventBus<RequestWorldReverse>.Subscribe(OnRequestWorldReverse);
            EventBus<RequestLoadScene>.Subscribe(OnRequestLoadScene);
            EventBus<PlayerHealthChanged>.Subscribe(OnPlayerHealthChanged);
            EventBus<PlayerPolarityChanged>.Subscribe(OnPlayerPolarityChanged);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EventBus<RequestWorldReverse>.Unsubscribe(OnRequestWorldReverse);
            EventBus<RequestLoadScene>.Unsubscribe(OnRequestLoadScene);
            EventBus<PlayerHealthChanged>.Unsubscribe(OnPlayerHealthChanged);
            EventBus<PlayerPolarityChanged>.Unsubscribe(OnPlayerPolarityChanged);

            DisposeServices();

            _inputProvider?.Dispose();
            _inputProvider = null;
            _inputKeys = null;

            _uiInputProvider?.Dispose();
            _uiInputProvider = null;

            _uiMgr?.Dispose();
            _uiMgr = null;

            _sceneRoots.Clear();
            _pendingAttach.Clear();
            _scenectx.Reset();

            _levelRoot = null;
            _pendingDoorId = 0;
            _pendingTag = MovementStateTag.None;
            _pendingFace = 0;

            Combat = null;

            _effects?.Destroy();
        }

        private void OnRequestWorldReverse(RequestWorldReverse evt)
        {
            if (evt.requestWhite)
            {
                _scenectx.SetWorld(WorldPolarity.White);
            }
            else
                _scenectx.ReverseWorld();
            EventBus<WorldPolarityChanged>.Publish(new(_scenectx.World));
        }

        private void OnRequestLoadScene(RequestLoadScene evt)
        {
            if (_sceneLoad != null)
            {
                Debug.LogError($"[GameRoot] 上一段转场还没结束，拒绝再次加载 \"{evt.sceneName}\"。");
                return;
            }

            _sceneLoad = StartCoroutine(CoSceneTransition(evt.sceneName));
        }

        private IEnumerator CoSceneTransition(string sceneName)
        {
            _levelReady = false;
            _playerPlaced = false;

            _gameMgr?.BeginSceneTransition();

            TransitionMask mask = _uiMgr != null ? _uiMgr.Transition : null;

            if (mask != null) yield return mask.CoFadeIn();

            yield return null;

            bool loaded = true;

            try
            {
                SceneManager.LoadScene(sceneName);
            }
            catch (Exception ex)
            {
                loaded = false;
                Debug.LogError($"[GameRoot] 加载场景 \"{sceneName}\" 失败：{ex.Message}");
            }

            if (!loaded)
            {
                if (mask != null) yield return mask.CoFadeOut();

                _gameMgr?.FinishSceneTransition(false);
                _sceneLoad = null;
                yield break;
            }

            float waited = 0f;
            float timeout = mask != null ? mask.ReadyTimeout : 3f;

            while (!TransitionReady() && waited < timeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!TransitionReady())
            {
                Debug.LogWarning($"[GameRoot] 场景 \"{sceneName}\" 在 {timeout} 秒内没等到关卡装配 + 玩家就位，按超时淡出。");
            }

            if (mask != null) yield return mask.CoFadeOut();

            _gameMgr?.FinishSceneTransition(true);

            if (_gameMgr != null && _gameMgr.CurState == GameState.SceneTransition)
            {
                Debug.LogError($"[GameRoot] 场景 \"{sceneName}\" 没有声明入口状态（SceneFlowConfig），转场收不了尾。");
            }

            _sceneLoad = null;
        }

        /// <summary>玩家只在关卡场景里等：开始场景没有 LevelRoot，等不到就该直接淡出</summary>
        private bool TransitionReady()
        {
            return _levelReady && (_playerPlaced || _levelRoot == null);
        }

        private void OnPlayerHealthChanged(PlayerHealthChanged evt)
        {
            _scenectx.SetHealth(evt.cur, evt.max);
        }

        private void OnPlayerPolarityChanged(PlayerPolarityChanged evt)
        {
            _scenectx.SetPolarity(evt.cur, evt.max);
        }

        /// <summary>首关装配：玩家的接入走场景根注册表，这里不碰</summary>
        private void Assemble()
        {
            _inputProvider = new();

            _inputKeys = new InputKeyMgr(_inputProvider);
            _inputKeys.Load();

            _pauseService = new PauseService(new Adapters.GameTimer());
            RegisterService(_pauseService);

            _hitStopService = new HitStopService(_pauseService);
            RegisterService(_hitStopService);

            _effects = new EffectModule();
            _effects.Init(transform);

            _audio = new AudioManager(transform);
            RegisterService(_audio);

            _gameMgr = new(_pauseService);
            _uiInputProvider = new();
            _uiInputProvider.Init();
            _uiMgr ??= new UIMgr();
            _uiMgr.Init();

            _audio.EnqueueBgm(AudioId.BgmScene3);
        }

        public void RegisterService(IService service)
        {
            if (service == null || _services.Contains(service)) return;

            _services.Add(service);
            service.Init();
        }

        public void BindCombatRoot(CombatRoot combat) => Combat = combat;

        public void UnbindCombatRoot(CombatRoot combat)
        {
            if (Combat == combat) Combat = null;
        }

        private void TickServices(float deltaTime, float unscaledDeltaTime)
        {
            for (int i = 0; i < _services.Count; i++)
            {
                _services[i].Tick(deltaTime, unscaledDeltaTime);
            }
        }

        private void DisposeServices()
        {
            for (int i = _services.Count - 1; i >= 0; i--)
            {
                try
                {
                    _services[i].Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            _services.Clear();
        }

        /// <summary>注册场景根（重复是 no-op）。真正的 Attach 推迟到下一个被驱动的帧，按 Order 顺序执行。</summary>
        public void RegisterSceneRoot(ISceneRoot root)
        {
            if (root == null || _sceneRoots.Contains(root)) return;

            InsertByOrder(_sceneRoots, root);
            InsertByOrder(_pendingAttach, root);
        }

        public void UnregisterSceneRoot(ISceneRoot root)
        {
            if (root == null) return;

            _sceneRoots.Remove(root);
            _pendingAttach.Remove(root);
        }

        /// <summary>关卡根装配完才报到：这里只用已就绪的关卡解析进门落点，不自己找、不自己建</summary>
        public void BindLevelRoot(LevelRoot levelRoot)
        {
            if (levelRoot == null || _levelRoot == levelRoot) return;

            _levelRoot = levelRoot;

            ResolveEntryPoint(levelRoot, _pendingDoorId, _pendingTag, _pendingFace);

            _pendingDoorId = 0;
            _pendingTag = MovementStateTag.None;
            _pendingFace = 0;
            _levelReady = true;
        }

        public void UnbindLevelRoot(LevelRoot levelRoot)
        {
            if (_levelRoot == levelRoot) _levelRoot = null;
        }

        /// <summary>跨关：记下这次加载要落到的门，LevelManager 在 LoadScene 之前通过它交棒</summary>
        public void RequestLevelLoad(int doorId, MovementStateTag enterTag, int enterFace)
        {
            _scenectx.SetNextDoor(doorId, enterTag, enterFace);
        }

        /// <summary>取走本关的进门口数据、进门时的移动状态与朝向，一次性。没有落点返回 false，调用方保持场景摆放</summary>
        public bool TryConsumeEntryPoint(out DoorEntry entry, out MovementStateTag enterTag, out int enterFace)
        {
            return _scenectx.TryConsumeEntry(out entry, out enterTag, out enterFace);
        }

        /// <summary>跨关血量：首关没有记录返回 false，玩家按满血出生</summary>
        public bool TryGetPlayerHealth(out int health)
        {
            return _scenectx.TryGetHealth(out health);
        }

        public bool TryGetPlayerPolarity(out int polarity)
        {
            return _scenectx.TryGetPolarity(out polarity);
        }

        public bool SaveCheckpoint(Vector2 pivot)
        {
            if (_scenectx.SaveCheckpoint(LevelId, pivot)) return true;

            if (!_warnedNoLevel)
            {
                _warnedNoLevel = true;

                string sceneName = SceneManager.GetActiveScene().name;

                if (_levelRoot == null)
                {
                    Debug.LogError($"[GameRoot] 场景 \"{sceneName}\" 里没有 LevelRoot，不算关卡，存档点在这里不生效。");
                }
                else
                {
                    Debug.LogError(
                        $"[GameRoot] 关卡根 \"{_levelRoot.name}\" 解不出关卡 id（{_levelRoot.LevelId}），" +
                        $"场景 \"{sceneName}\" 应命名为 Level_正整数，存档点在这里不生效。");
                }
            }

            return false;
        }

        public bool TryGetLocalCheckpoint(out Vector2 pivot)
        {
            pivot = default;

            if (!_scenectx.TryGetCheckpoint(out Vector2 point, out int level)) return false;
            if (level != LevelId) return false;

            pivot = point;
            return true;
        }

        public bool TryStartRespawnInCheckpointLevel()
        {
            if (!_scenectx.TryGetCheckpoint(out Vector2 point, out int level)) return false;

            string sceneName = SceneName.GetLevelName(level);

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameRoot] 存档关 \"{sceneName}\" 不在 Build Settings 里，无法回到存档点。");
                return false;
            }

            _scenectx.BeginRespawn(point);

            EventBus<RequestLoadScene>.Publish(new RequestLoadScene(sceneName));

            return true;
        }

        public bool TryConsumeRespawnPoint(out Vector2 pivot)
        {
            return _scenectx.TryConsumeRespawn(out pivot);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 只接单场景切换。附加加载（菜单、UI 叠层）不该动玩家和地面。
            if (mode != LoadSceneMode.Single) return;

            Rebind();
        }

        /// <summary>切场景后重置本次切换的临时状态。只在这个时机安全：新场景的 Awake / OnEnable 已跑完，场景根还没在 Start 里注册</summary>
        private void Rebind()
        {
            if (_inputProvider == null) return;

            _levelRoot = null;
            _levelReady = false;
            _playerPlaced = false;

            // 门索引随旧场景一起没了，落点只能等新场景的 LevelRoot 报到后再解析
            _pendingDoorId = _scenectx.TakeNextDoor(out _pendingTag, out _pendingFace);

            _scenectx.ClearEntry();
        }

        private void ResolveEntryPoint(LevelRoot levelRoot, int doorId, MovementStateTag enterTag, int enterFace)
        {
            if (doorId <= 0) return;

            // 落点压在门的触发器范围内：取门口时会顺手屏蔽该门，不然下一个物理步又会发一次切场景请求，两个场景来回弹
            if (!levelRoot.TryGetEntry(doorId, out DoorEntry entry))
            {
                Debug.LogError($"[GameRoot] 关卡 {levelRoot.LevelId} 里找不到 id = {doorId} 的门，玩家保持场景里的摆放位置。");
                return;
            }

            _scenectx.SetEntry(in entry, enterTag, enterFace);
        }

        /// <summary>把这一帧新注册的场景根按 Order 顺序 Attach 掉。Attach 里再注册的，下一帧继续。</summary>
        private void AttachPending()
        {
            if (_pendingAttach.Count > 0) AttachBatch();

            // 场景里没有 LevelRoot（开始场景）就没人来报到，别让转场一直等到超时
            if (!_levelReady && _levelRoot == null) _levelReady = true;
        }

        private void AttachBatch()
        {
            ISceneRoot[] batch = _pendingAttach.ToArray();
            _pendingAttach.Clear();

            for (int i = 0; i < batch.Length; i++)
            {
                // 注册后、Attach 前就被销毁的根要跳过：接口之间的 == null 不走 Unity 的伪 null 重载，必须先转成 Object
                UnityEngine.Object target = batch[i] as UnityEngine.Object;
                if (target == null) continue;

                // 单个根装配失败不该拖累后面的根：坏掉一个不该让全场景停摆
                try
                {
                    batch[i].Attach();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                // 玩家装配完立刻广播血量与极性：界面这一帧就有初值
                if (batch[i] is PlayerController player)
                {
                    player.Announce();
                    player.AnnouncePolarity();
                    _playerPlaced = true;
                }
            }
        }

        private static int CompareSceneRoots(ISceneRoot a, ISceneRoot b)
        {
            return a.Order.CompareTo(b.Order);
        }

        /// <summary>按 Order 升序插入：List.Sort 不稳定，同 Order 的根会被换位，这里用插入排序保住注册顺序</summary>
        private static void InsertByOrder(List<ISceneRoot> list, ISceneRoot root)
        {
            int i = list.Count;
            while (i > 0 && CompareSceneRoots(list[i - 1], root) > 0) i--;

            list.Insert(i, root);
        }

        private void Update()
        {
            AttachPending();
            _inputProvider?.Tick();
            TickUiInput();
            TickServices(Time.deltaTime, Time.unscaledDeltaTime);
            _effects?.Tick(Time.deltaTime);
            TickSceneRootsRender(Time.deltaTime);
        }

        private void TickUiInput()
        {
            if (_uiInputProvider == null) return;

            _uiInputProvider.Sample();

            if (!_uiInputProvider.ConsumeSnapshot().EscPressed) return;

            if (_uiMgr != null && _uiMgr.TryCloseTopmostPanel()) return;

            if (_gameMgr == null) return;

            switch (_gameMgr.CurState)
            {
                case GameState.Running:
                    _gameMgr.ChangeState(GameState.Paused);
                    return;

                case GameState.Paused:
                    _gameMgr.Resume();
                    return;
            }
        }

        private void FixedUpdate()
        {
            // 先装配再驱动：场景根的第一帧就要能跑
            AttachPending();
            TickSceneRootsPhysics(Time.fixedDeltaTime);
        }

        private void TickSceneRootsRender(float deltaTime)
        {
            for (int i = 0; i < _sceneRoots.Count; i++)
            {
                if (_sceneRoots[i] is IRenderTicked ticked) ticked.RenderTick(deltaTime);
            }
        }

        private void TickSceneRootsPhysics(float deltaTime)
        {
            for (int i = 0; i < _sceneRoots.Count; i++)
            {
                if (_sceneRoots[i] is IPhysicsTicked ticked) ticked.FixedTick(deltaTime);
            }
        }
    }
}
