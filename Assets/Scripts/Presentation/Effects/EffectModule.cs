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
        private const string ChargeInPath = "Effects/ChargeIn";
        private const int HitSparkPoolSize = 16;
        private const int AttackSparkPoolSize = 8;
        private const int ChargeInPoolSize = 4;
        private const int HitSparkPrewarm = 4;
        private const int AttackSparkPrewarm = 2;
        private const int ChargeInPrewarm = 1;
        private const float SparkMaxLife = 5f;
        private const float ChargeInMinDuration = 0.05f;

        // 受击挤压：X 压、Y 拉，余弦回弹一轮后线性收回；乘在 View 的设计缩放上，朝向翻号的负号不动
        private const float SqueezeX = 0.08f;
        private const float SqueezeY = 0.12f;
        private const float SqueezeTime = 0.16f;
        private const float SqueezeCycles = 1f;

        // 蓄力起鼓：X、Y 一起涨，乘在 View 的设计缩放上，和挤压相加后同时生效
        private const float ChargeScaleX = 0.1f;
        private const float ChargeScaleY = 0.12f;
        // 爬升时间由前摇时长传进来，这里只是没给时长时的兜底
        private const float ChargeRampTime = 0.12f;

        // 蓄力闪烁：_FlashAmount 在 0 和这个峰值之间来回，每秒 ChargeFlashHz 次
        private const float ChargeFlashMax = 0.5f;
        private const float ChargeFlashHz = 5f;

        private readonly Dictionary<Transform, TargetState> _states = new();
        private readonly List<Transform> _expired = new();
        private readonly Dictionary<Transform, ShapeState> _shapes = new();
        private readonly List<Transform> _expiredShapes = new();
        private readonly Dictionary<Transform, ChargeInState> _chargeIns = new();
        private readonly List<Transform> _expiredChargeIns = new();
        private readonly Dictionary<Graphic, UiState> _uiStates = new();
        private readonly List<Spark> _liveSparks = new();
        private readonly List<Spark> _liveAttackSparks = new();

        private Transform _effectRoot;
        private GameObject _sparkPrefab;
        private Pool<Spark> _sparkPool;
        private GameObject _attackSparkPrefab;
        private Pool<Spark> _attackSparkPool;
        private GameObject _chargeInPrefab;
        private Pool<ChargeInState> _chargeInPool;

        private bool _warnedNoFlash;
        private bool _warnedNoInvert;
        private bool _warnedNoInvertUi;
        private bool _warnedNoSpark;
        private bool _warnedNoAttackSpark;
        private bool _warnedNoChargeIn;

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
            TickShapes(deltaTime);
            TickChargeIns(deltaTime);

            if (_states.Count == 0) return;

            foreach (KeyValuePair<Transform, TargetState> pair in _states)
            {
                if (!pair.Value.Tick(deltaTime)) _expired.Add(pair.Key);
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

        /// <summary>受击挤压：目标自己的 localScale 做 X 压 Y 拉，要传 View 这类纯视觉节点，根上有碰撞体不能缩</summary>
        public void PlaySqueeze(Transform target)
        {
            if (target == null) return;

            GetShape(target).Squeeze();
        }

        /// <summary>蓄力起鼓：target 上挂材质闪白，view 上做缩放；rampTime 一般直接给前摇时长，关掉当帧就落回</summary>
        public void SetCharge(Transform target, Transform view, bool on, float rampTime)
        {
            if (target == null) return;

            // 关的时候只在已有状态上收尾：每帧都新建会克隆材质，等于每帧一次 GC
            if (on)
            {
                GetState(target).SetCharging(true);
                if (view != null) GetShape(view).SetCharging(true, rampTime);
                return;
            }

            if (_states.TryGetValue(target, out TargetState state)) state.SetCharging(false);
            if (view != null && _shapes.TryGetValue(view, out ShapeState shape)) shape.SetCharging(false);
        }

        /// <summary>蓄力吸入：每帧贴到 follow 的枪口，播放时长（粒子 duration）就是前摇时长，开火当帧 on = false 收掉</summary>
        public void SetChargeIn(Transform follow, Vector2 offset, bool on, float duration)
        {
            if (follow == null) return;

            if (on)
            {
                if (!EnsureChargeInPool()) return;

                if (!_chargeIns.TryGetValue(follow, out ChargeInState state))
                {
                    if (!_chargeInPool.TryGet(out state)) return;

                    _chargeIns[follow] = state;
                }

                state.Play(follow, offset, duration);
                return;
            }

            if (!_chargeIns.TryGetValue(follow, out ChargeInState playing)) return;

            playing.Stop();
            _chargeInPool?.Release(playing);
            _chargeIns.Remove(follow);
        }

        private void TickChargeIns(float deltaTime)
        {
            if (_chargeIns.Count == 0) return;

            foreach (KeyValuePair<Transform, ChargeInState> pair in _chargeIns)
            {
                if (pair.Value.Tick(pair.Key, deltaTime)) _expiredChargeIns.Add(pair.Key);
            }

            for (int i = 0; i < _expiredChargeIns.Count; i++)
            {
                Transform key = _expiredChargeIns[i];

                if (!_chargeIns.TryGetValue(key, out ChargeInState state)) continue;

                state.Stop();
                _chargeInPool?.Release(state);
                _chargeIns.Remove(key);
            }

            _expiredChargeIns.Clear();
        }

        private ShapeState GetShape(Transform target)
        {
            if (_shapes.TryGetValue(target, out ShapeState state)) return state;

            state = new ShapeState(target);
            _shapes[target] = state;
            return state;
        }

        private void TickShapes(float deltaTime)
        {
            if (_shapes.Count == 0) return;

            foreach (KeyValuePair<Transform, ShapeState> pair in _shapes)
            {
                if (!pair.Value.Tick(deltaTime)) _expiredShapes.Add(pair.Key);
            }

            for (int i = 0; i < _expiredShapes.Count; i++)
            {
                _shapes[_expiredShapes[i]].Restore();
                _shapes.Remove(_expiredShapes[i]);
            }

            _expiredShapes.Clear();
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

        private bool EnsureChargeInPool()
        {
            if (_chargeInPool != null) return true;

            _chargeInPrefab = Resources.Load<GameObject>(ChargeInPath);

            if (_chargeInPrefab == null)
            {
                if (!_warnedNoChargeIn)
                {
                    _warnedNoChargeIn = true;
                    Debug.LogWarning($"[EffectModule] 找不到粒子预制体 Resources/{ChargeInPath}，蓄力吸入不播放。");
                }

                return false;
            }

            _chargeInPool = new Pool<ChargeInState>(
                CreateChargeIn,
                onGet: TakeChargeIn,
                onRelease: PutChargeIn,
                onDestroy: DestroyChargeIn,
                name: nameof(ChargeInState),
                maxSize: ChargeInPoolSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop);

            _chargeInPool.Prewarm(ChargeInPrewarm);

            return true;
        }

        private ChargeInState CreateChargeIn()
        {
            GameObject go = Object.Instantiate(_chargeInPrefab, _effectRoot);
            go.SetActive(false);

            return new ChargeInState(go);
        }

        private static void TakeChargeIn(ChargeInState state) => state.Go.SetActive(true);

        private static void PutChargeIn(ChargeInState state) => state.Stop();

        private static void DestroyChargeIn(ChargeInState state)
        {
            if (state.Go != null) Object.Destroy(state.Go);
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

            foreach (ShapeState shape in _shapes.Values) shape.Restore();
            _shapes.Clear();
            _expiredShapes.Clear();

            foreach (ChargeInState chargeIn in _chargeIns.Values) _chargeInPool?.Release(chargeIn);
            _chargeIns.Clear();
            _expiredChargeIns.Clear();

            _chargeInPool?.Dispose();
            _chargeInPool = null;

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
            private readonly Transform _target;
            private readonly SpriteRenderer[] _renderers;
            private readonly Material[] _materials;
            // 闪白期间 color 会被压成白色，结束要按这份还原
            private readonly Color[] _originalColors;

            private float _duration;
            private float _elapsed;
            private bool _flashing;
            private float _chargeElapsed;
            private bool _charging;
            // 只有闪白把 color 压白过才需要还原，纯蓄力态不能碰 EnemyTint 已经写好的前摇色
            private bool _whitened;

            public bool HasFlashProperty { get; }
            public bool HasInvertProperty { get; }

            public TargetState(Transform target)
            {
                _target = target;
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

                ApplyMaterial(FlashAmount());
                ApplyWhite();
            }

            /// <summary>蓄力闪烁：峰值只有 ChargeFlashMax，所以挨打时的 1 会盖住它，两者同占 _FlashAmount</summary>
            public void SetCharging(bool on)
            {
                if (_charging == on) return;

                _charging = on;
                _chargeElapsed = 0f;

                if (!on) ApplyMaterial(FlashAmount());
            }

            /// <summary>返回 false 表示闪白与蓄力都不再需要，状态可以摘掉了</summary>
            public bool Tick(float deltaTime)
            {
                // 敌人被击杀后 Transform 会先走掉，蓄力态得跟着结束，否则字典里永久留一条
                if (_target == null) return false;

                if (_flashing)
                {
                    _elapsed += deltaTime;

                    if (_elapsed >= _duration)
                    {
                        _flashing = false;
                        RestoreColors();
                    }
                }

                if (_charging) _chargeElapsed += deltaTime;

                if (!_flashing && !_charging) return false;

                ApplyMaterial(FlashAmount());
                if (_flashing) ApplyWhite();

                return true;
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
                _charging = false;
                _chargeElapsed = 0f;
            }

            private float FlashAmount()
            {
                float amount = _flashing ? 1f - _elapsed / _duration : 0f;

                if (!_charging) return amount;

                float blink = Mathf.Abs(Mathf.Sin(_chargeElapsed * ChargeFlashHz * Mathf.PI)) * ChargeFlashMax;

                return Mathf.Max(amount, blink);
            }

            private void ApplyWhite()
            {
                _whitened = true;

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
                if (!_whitened) return;

                _whitened = false;

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

        /// <summary>View 缩放的唯一写入口：挤压与蓄力各算一个系数，相加后乘在首次记录的设计缩放上，互相不覆盖</summary>
        private sealed class ShapeState
        {
            private readonly Transform _target;
            private readonly Vector3 _baseScale;

            private float _squeezeElapsed;
            private bool _squeezing;
            private float _chargeElapsed;
            private bool _charging;
            private float _rampTime = ChargeRampTime;

            public ShapeState(Transform target)
            {
                _target = target;
                _baseScale = target.localScale;
            }

            public void Squeeze()
            {
                _squeezeElapsed = 0f;
                _squeezing = true;
                Apply();
            }

            public void SetCharging(bool on, float rampTime = 0f)
            {
                if (on && rampTime > 0f) _rampTime = rampTime;

                if (_charging == on) return;

                _charging = on;
                _chargeElapsed = 0f;
                Apply();
            }

            public bool Tick(float deltaTime)
            {
                if (_target == null) return false;

                if (_squeezing)
                {
                    _squeezeElapsed += deltaTime;
                    if (_squeezeElapsed >= SqueezeTime) _squeezing = false;
                }

                if (_charging) _chargeElapsed += deltaTime;
                if (!_squeezing && !_charging) return false;

                Apply();
                return true;
            }

            public void Restore()
            {
                if (_target != null) _target.localScale = _baseScale;
            }

            private void Apply()
            {
                if (_target == null) return;

                float squeeze = 0f;

                if (_squeezing)
                {
                    float t = _squeezeElapsed / SqueezeTime;
                    squeeze = (1f - t) * Mathf.Cos(SqueezeCycles * Mathf.PI * t);
                }

                float charge = 0f;

                if (_charging)
                {
                    float t = Mathf.Clamp01(_chargeElapsed / _rampTime);
                    charge = t * t * (3f - 2f * t);
                }

                _target.localScale = new Vector3(
                    _baseScale.x * (1f - SqueezeX * squeeze + ChargeScaleX * charge),
                    _baseScale.y * (1f + SqueezeY * squeeze + ChargeScaleY * charge),
                    _baseScale.z);
            }
        }

        private sealed class ChargeInState
        {
            private readonly GameObject _go;
            private readonly ParticleSystem _ps;

            private Vector2 _offset;
            private float _duration;
            private float _elapsed;
            private bool _playing;
            // 时长到点只停发射、不收回池子：控制器还没说要收，重播会在准星上闪第二下
            private bool _emitted;

            public ChargeInState(GameObject go)
            {
                _go = go;
                _ps = go.GetComponent<ParticleSystem>();
            }

            public GameObject Go => _go;

            public void Play(Transform follow, Vector2 offset, float duration)
            {
                _offset = offset;
                _duration = Mathf.Max(duration, ChargeInMinDuration);

                if (_playing) return;

                _playing = true;
                _emitted = false;
                _elapsed = 0f;

                _go.SetActive(true);
                _go.transform.position = follow.position + new Vector3(offset.x, offset.y, 0f);

                if (_ps == null) return;

                // 粒子的发射窗口就是前摇时长，蓄满恰好播完
                ParticleSystem.MainModule main = _ps.main;
                main.duration = _duration;

                _ps.Clear(true);
                _ps.Play(true);
            }

            public bool Tick(Transform follow, float deltaTime)
            {
                if (follow == null || !_playing) return true;

                _go.transform.position = follow.position + new Vector3(_offset.x, _offset.y, 0f);

                if (_emitted) return false;

                _elapsed += deltaTime;
                if (_elapsed < _duration) return false;

                _emitted = true;

                if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                return false;
            }

            public void Stop()
            {
                if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                _playing = false;
                _emitted = false;
                _elapsed = 0f;

                if (_go != null) _go.SetActive(false);
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
