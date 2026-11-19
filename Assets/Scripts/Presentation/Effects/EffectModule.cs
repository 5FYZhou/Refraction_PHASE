using System.Collections.Generic;
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

        private readonly Dictionary<Transform, TargetState> _states = new();
        private readonly List<Transform> _expired = new();
        private readonly Dictionary<Graphic, UiState> _uiStates = new();

        private bool _warnedNoFlash;
        private bool _warnedNoInvert;
        private bool _warnedNoInvertUi;

        public void Init() { }
        public void Destroy() => Clear();

        public void Tick(float deltaTime)
        {
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
    }
}
