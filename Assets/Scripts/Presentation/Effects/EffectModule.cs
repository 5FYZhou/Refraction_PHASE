using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.Effects
{
    public enum EffectType
    {
        None = 0,
        DamageFlash
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

        private readonly Dictionary<Transform, FlashState> _flashes = new();
        private readonly List<Transform> _expired = new();

        public void Init() { }
        public void Destroy() => Clear();

        public void Tick(float deltaTime)
        {
            if (_flashes.Count == 0) return;

            foreach (KeyValuePair<Transform, FlashState> pair in _flashes)
            {
                if (pair.Value.Tick(deltaTime)) _expired.Add(pair.Key);
            }

            for (int i = 0; i < _expired.Count; i++)
            {
                _flashes[_expired[i]].Restore();
                _flashes.Remove(_expired[i]);
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

        private void PlayFlash(Transform target, float duration)
        {
            if (target == null || duration <= 0f) return;

            if (!_flashes.TryGetValue(target, out FlashState state))
            {
                state = new FlashState(target);
                _flashes[target] = state;

                if (!state.HasFlashProperty)
                {
                    Debug.LogWarning($"[EffectModule] 目标 {target.name} 材质没有 _FlashAmount 属性！");
                }
            }

            state.Restart(duration);
        }

        private void Clear()
        {
            foreach (FlashState state in _flashes.Values) state.Restore();
            _flashes.Clear();
            _expired.Clear();
        }

        private sealed class FlashState
        {
            private readonly SpriteRenderer[] _renderers;
            private readonly Material[] _materials;
            // 闪白期间 color 会被压成白色，结束要按这份还原
            private readonly Color[] _originalColors;

            private float _duration;
            private float _elapsed;

            public bool HasFlashProperty { get; }

            public FlashState(Transform target)
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

                    if (_materials[i] != null && _materials[i].HasProperty(FlashAmountId))
                    {
                        HasFlashProperty = true;
                    }
                }
            }

            public void Restart(float duration)
            {
                _duration = duration;
                _elapsed = 0f;
                Apply(1f);
            }

            public bool Tick(float deltaTime)
            {
                _elapsed += deltaTime;
                if (_elapsed >= _duration) return true;

                Apply(1f - _elapsed / _duration);
                return false;
            }

            public void Restore()
            {
                // 先收颜色，再归零材质：反过来会先把颜色刷白，判断就失去依据了
                RestoreColors();
                ApplyMaterial(0f);
            }

            private void Apply(float amount)
            {
                ApplyMaterial(amount);

                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;

                    // 只压 rgb，alpha 留着——半透明的表现别被闪白顺手改成不透明
                    _renderers[i].color = new Color(1f, 1f, 1f, _originalColors[i].a);
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

                    _renderers[i].color = _originalColors[i];
                }
            }
        }
    }
}
