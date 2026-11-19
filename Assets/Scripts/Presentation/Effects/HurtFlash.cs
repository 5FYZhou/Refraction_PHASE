using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Effects
{
    /// <summary>受击闪白的 IHurtView 实现：逻辑层只管喊一声，闪多久、闪哪一层由装配方给定</summary>
    public sealed class HurtFlash : IHurtView
    {
        private readonly EffectModule _effects;
        private readonly Transform _target;
        private readonly float _duration;

        public HurtFlash(EffectModule effects, Transform target, float duration)
        {
            _effects = effects;
            _target = target;
            _duration = duration;
        }

        public void OnHurt(int damage) => _effects?.Play(EffectType.DamageFlash, _target, _duration);
    }
}
