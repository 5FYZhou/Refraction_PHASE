using System.Collections.Generic;
using Refraction.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace Refraction.Presentation.Effects
{
    public enum EffectType
    {
        None = 0,
        DamageFlash,
        Invert
    }

    public readonly struct EffectContext
    {
        public readonly Transform[] Transforms;
        public readonly float Duration;

        public EffectContext(Transform[] transforms, float duration)
        {
            Transforms = transforms;
            Duration = duration;
        }
    }

    public sealed class EffectModule
    {
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int OpenInvertId = Shader.PropertyToID("_OpenInvert");

        private const string HitSparkPath = "Effects/HitSpark";
        private const string AttackSparkPath = "Effects/AttackSpark";
        private const int HitSparkPoolSize = 16;
        private const int AttackSparkPoolSize = 8;
        private const int HitSparkPrewarm = 4;
        private const int AttackSparkPrewarm = 2;
        private const float SparkMaxLife = 5f;

        private readonly Dictionary<Transform, TargetState> _states = new();
        private readonly List<Transform> _expired = new();
        private readonly Dictionary<Graphic, UiState> _uiStates = new();
        private readonly List<Spark> _liveSparks = new();
        private readonly List<Spark> _liveAttackSparks = new();

        private Transform _effectRoot;
        private GameObject _sparkPrefab;
        private Pool<Spark> _sparkPool;
        private GameObject _attackSparkPrefab;
        private Pool<Spark> _attackSparkPool;

        private bool _warnedNoFlash;
        private bool _warnedNoInvert;
        private bool _warnedNoInvertUi;
        private bool _warnedNoSpark;
        private bool _warnedNoAttackSpark;

        public void Init(Transform parent)
        {
            if (_effectRoot != null) return;

            GameObject root = new("EffectField");
            root.transform.SetParent(parent, false);

            _effectRoot = root.transform;
        }

        public void Destroy() => Clear();

        public void Tick(float deltaTime)
        {
            TickSparks(_liveSparks, _sparkPool, deltaTime);
            TickSparks(_liveAttackSparks, _attackSparkPool, deltaTime);

            if (_states.Count == 0) return;

            foreach (KeyValuePair<Transform, TargetState> pair in _states)
            {
                if (pair.Value.Tick(deltaTime)) _expired.Add(pair.Key);
            }

            for (int i = 0; i < _expired.Count; i++)
            {
                _states[_expired[i]].Restore();
                _states.Remove(_expired[i]);
            }

            _expired.Clear();
        }

        public void Play(EffectType type, in EffectContext ctx)
        {
            if (type != EffectType.DamageFlash || ctx.Transforms == null) return;
            for (int i = 0; i < ctx.Transforms.Length; i++) PlayFlash(ctx.Transforms[i], ctx.Duration);
        }

        public void Play(EffectType type, Transform target, float duration)
        {
            if (type == EffectType.DamageFlash) PlayFlash(target, duration);
        }

        /// <summary>极性反转是常驻态、没有时长，所以不进闪白那套计时；和闪白共用同一份材质实例</summary>
        public void SetInvert(Transform target, bool open)
        {
            if (target == null) return;

            TargetState state = GetState(target);

            if (!state.HasInvertProperty && !_warnedNoInvert)
            {
                _warnedNoInvert = true;
                Debug.LogWarning($"[EffectModule] 目标 {target.name} 材质没有 _OpenInvert 属性！");
            }

            state.SetInvert(open);
        }

        /// <summary>UI Image 没有 SpriteRenderer：材质带 _OpenInvert 才走材质，否则退回顶点色取反</summary>
        public void SetInvert(Graphic graphic, bool open)
        {
            if (graphic == null) return;

            if (!_uiStates.TryGetValue(graphic, out UiState state))
            {
                state = new UiState(graphic);
                _uiStates[graphic] = state;

                if (state.Material == null && !_warnedNoInvertUi)
                {
                    _warnedNoInvertUi = true;
                    Debug.LogWarning($"[EffectModule] {graphic.name} 的材质没有 _OpenInvert，改走顶点色取反");
                }
            }

            state.Apply(open);
        }

        /// <summary>命中火花：位置是撞击点，方向是喷溅方向（一般为子弹来向的反向），颜色不传就用粒子自己的默认色</summary>
        public void PlayHitSpark(Vector2 position, Vector2 direction, Color? color = null)
        {
            if (!EnsureSparkPool()) return;

            if (!_sparkPool.TryGet(out Spark spark)) return;

            spark.Play(position, direction, color);
            _liveSparks.Add(spark);
        }

        /// <summary>攻击火花：玩家黑攻击砍到实体时播，方向沿用被击表面的外法线</summary>
        public void PlayAttackSpark(Vector2 position, Vector2 direction, Color? color = null)
        {
            if (!EnsureAttackSparkPool()) return;

            if (!_attackSparkPool.TryGet(out Spark spark)) return;

            spark.Play(position, direction, color);
            _liveAttackSparks.Add(spark);
        }

        private void TickSparks(List<Spark> live, Pool<Spark> pool, float deltaTime)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (!live[i].Tick(deltaTime)) continue;

                pool?.Release(live[i]);
                live.RemoveAt(i);
            }
        }

        private bool EnsureSparkPool()
        {
            if (_sparkPool != null) return true;

            _sparkPrefab = Resources.Load<GameObject>(HitSparkPath);

            if (_sparkPrefab == null)
            {
                if (!_warnedNoSpark)
                {
                    _warnedNoSpark = true;
                    Debug.LogWarning($"[EffectModule] 找不到粒子预制体 Resources/{HitSparkPath}，命中火花不播放。");
                }

                return false;
            }

            _sparkPool = new Pool<Spark>(
                CreateSpark,
                onGet: TakeSpark,
                onRelease: PutSpark,
                onDestroy: DestroySpark,
                name: nameof(Spark),
                maxSize: HitSparkPoolSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop);

            _sparkPool.Prewarm(HitSparkPrewarm);

            return true;
        }

        private Spark CreateSpark()
        {
            GameObject go = Object.Instantiate(_sparkPrefab, _effectRoot);
            go.SetActive(false);

            return new Spark(go);
        }

        private bool EnsureAttackSparkPool()
        {
            if (_attackSparkPool != null) return true;

            _attackSparkPrefab = Resources.Load<GameObject>(AttackSparkPath);

            if (_attackSparkPrefab == null)
            {
                if (!_warnedNoAttackSpark)
                {
                    _warnedNoAttackSpark = true;
                    Debug.LogWarning($"[EffectModule] 找不到粒子预制体 Resources/{AttackSparkPath}，攻击火花不播放。");
                }

                return false;
            }

            _attackSparkPool = new Pool<Spark>(
                CreateAttackSpark,
                onGet: TakeSpark,
                onRelease: PutSpark,
                onDestroy: DestroySpark,
                name: nameof(Spark),
                maxSize: AttackSparkPoolSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop);

            _attackSparkPool.Prewarm(AttackSparkPrewarm);

            return true;
        }

        private Spark CreateAttackSpark()
        {
            GameObject go = Object.Instantiate(_attackSparkPrefab, _effectRoot);
            go.SetActive(false);

            return new Spark(go);
        }

        private static void TakeSpark(Spark spark) => spark.Go.SetActive(true);

        private static void PutSpark(Spark spark) => spark.Sleep();

        private static void DestroySpark(Spark spark)
        {
            if (spark.Go != null) Object.Destroy(spark.Go);
        }

        private void PlayFlash(Transform target, float duration)
        {
            if (target == null || duration <= 0f) return;

            TargetState state = GetState(target);

            if (!state.HasFlashProperty && !_warnedNoFlash)
            {
                _warnedNoFlash = true;
                Debug.LogWarning($"[EffectModule] 目标 {target.name} 材质没有 _FlashAmount 属性！");
            }

            state.Restart(duration);
        }

        private TargetState GetState(Transform target)
        {
            if (_states.TryGetValue(target, out TargetState state)) return state;

            state = new TargetState(target);
            _states[target] = state;
            return state;
        }

        private void Clear()
        {
            foreach (TargetState state in _states.Values) state.Restore();
            _states.Clear();
            _expired.Clear();

            foreach (UiState state in _uiStates.Values) state.Dispose();
            _uiStates.Clear();

            for (int i = 0; i < _liveSparks.Count; i++) _sparkPool?.Release(_liveSparks[i]);
            _liveSparks.Clear();

            for (int i = 0; i < _liveAttackSparks.Count; i++) _attackSparkPool?.Release(_liveAttackSparks[i]);
            _liveAttackSparks.Clear();

            _sparkPool?.Dispose();
            _sparkPool = null;

            _attackSparkPool?.Dispose();
            _attackSparkPool = null;

            if (_effectRoot != null) Object.Destroy(_effectRoot.gameObject);
            _effectRoot = null;
        }

        private sealed class UiState
        {
            private readonly Graphic _graphic;
            private readonly Color _authored;

            public Material Material { get; }

            public UiState(Graphic graphic)
            {
                _graphic = graphic;
                _authored = graphic.color;

                Material source = graphic.material;

                if (source == null || !source.HasProperty(OpenInvertId)) return;

                Material instance = new(source) { name = source.name + "(Instance)" };
                graphic.material = instance;
                Material = instance;
            }

            public void Apply(bool open)
            {
                if (Material != null)
                {
                    Material.SetFloat(OpenInvertId, open ? 1f : 0f);
                    return;
                }

                _graphic.color = open
                    ? new Color(1f - _authored.r, 1f - _authored.g, 1f - _authored.b, _authored.a)
                    : _authored;
            }

            public void Dispose()
            {
                if (Material != null) Object.Destroy(Material);
            }
        }

        private sealed class TargetState
        {
            private readonly SpriteRenderer[] _renderers;
            private readonly Material[] _materials;
            // 闪白期间 color 会被压成白色，结束要按这份还原
            private readonly Color[] _originalColors;

            private float _duration;
            private float _elapsed;
            private bool _flashing;

            public bool HasFlashProperty { get; }
            public bool HasInvertProperty { get; }

            public TargetState(Transform target)
            {
                _renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
                _materials = new Material[_renderers.Length];
                _originalColors = new Color[_renderers.Length];

                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;

                    // .material 只为这个 Renderer 克隆一份实例，改它不影响同材质的其他怪
                    _materials[i] = _renderers[i].material;
                    _originalColors[i] = _renderers[i].color;

                    if (_materials[i] == null) continue;

                    if (_materials[i].HasProperty(FlashAmountId)) HasFlashProperty = true;
                    if (_materials[i].HasProperty(OpenInvertId)) HasInvertProperty = true;
                }
            }

            public void Restart(float duration)
            {
                _duration = duration;
                _elapsed = 0f;
                _flashing = true;
                Apply(1f);
            }

            public bool Tick(float deltaTime)
            {
                if (!_flashing) return false;

                _elapsed += deltaTime;
                if (_elapsed >= _duration)
                {
                    _flashing = false;
                    return true;
                }

                Apply(1f - _elapsed / _duration);
                return false;
            }

            public void SetInvert(bool open)
            {
                float value = open ? 1f : 0f;

                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null || _materials[i] == null) continue;

                    _materials[i].SetFloat(OpenInvertId, value);
                }
            }

            public void Restore()
            {
                // 先收颜色，再归零材质：反过来会先把颜色刷白，判断就失去依据了
                RestoreColors();
                ApplyMaterial(0f);
                _flashing = false;
            }

            private void Apply(float amount)
            {
                ApplyMaterial(amount);

                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;

                    // 只压 rgb，alpha 留给无敌闪烁这类自己驱动透明度的表现
                    _renderers[i].color = new Color(1f, 1f, 1f, _renderers[i].color.a);
                }
            }

            private void ApplyMaterial(float amount)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null || _materials[i] == null) continue;

                    _materials[i].SetFloat(FlashAmountId, amount);
                    _materials[i].SetColor(FlashColorId, Color.white);
                }
            }

            private void RestoreColors()
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;

                    // 还白着 = 没人抢过，按缓存还原；已被 EnemyTint 改成前摇色就留着
                    Color cur = _renderers[i].color;
                    if (cur.r < 0.999f || cur.g < 0.999f || cur.b < 0.999f) continue;

                    Color original = _originalColors[i];
                    _renderers[i].color = new Color(original.r, original.g, original.b, cur.a);
                }
            }
        }

        private sealed class Spark
        {
            private readonly GameObject _go;
            private readonly ParticleSystem _ps;
            // 预制体的默认起始色：不给颜色时按这份还原，否则复用会带着上一发的颜色
            private readonly ParticleSystem.MinMaxGradient _defaultColor;

            private float _elapsed;
            private bool _settled;

            public Spark(GameObject go)
            {
                _go = go;
                _ps = go.GetComponent<ParticleSystem>();

                if (_ps != null) _defaultColor = _ps.main.startColor;
            }

            public GameObject Go => _go;

            public void Play(Vector2 position, Vector2 direction, Color? color)
            {
                _elapsed = 0f;
                _settled = false;

                Transform t = _go.transform;
                t.position = new Vector3(position.x, position.y, 0f);

                // 预制体初始朝 +Y：把上方向转到这次的喷溅方向
                t.rotation = direction.sqrMagnitude > 1e-6f
                    ? Quaternion.FromToRotation(Vector3.up, direction)
                    : Quaternion.identity;

                if (_ps == null) return;

                ParticleSystem.MainModule main = _ps.main;
                main.startColor = color.HasValue ? new ParticleSystem.MinMaxGradient(color.Value) : _defaultColor;

                _ps.Clear(true);
                _ps.Play(true);
            }

            public bool Tick(float deltaTime)
            {
                _elapsed += deltaTime;

                if (_elapsed >= SparkMaxLife) return true;

                // Play 的当帧粒子还没吐出来，IsAlive 必然是 false，跳过一帧再判
                if (!_settled)
                {
                    _settled = true;
                    return false;
                }

                return _ps == null || !_ps.IsAlive(true);
            }

            public void Sleep()
            {
                if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                _go.SetActive(false);
            }
        }
    }
}
