using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Effects
{
    /// <summary>受击表现的 IHurtView 实现：闪白 + 挤压，闪多久由装配方给定</summary>
    public sealed class HurtFlash : IHurtView
    {
        private const string ViewName = "View";

        private readonly EffectModule _effects;
        private readonly Transform _target;
        private readonly Transform _view;
        private readonly float _duration;

        public HurtFlash(EffectModule effects, Transform target, float duration)
        {
            _effects = effects;
            _target = target;
            _view = target != null ? target.Find(ViewName) : null;
            _duration = duration;
        }

        public void OnHurt(int damage)
        {
            _effects?.Play(EffectType.DamageFlash, _target, _duration);

            if (_view != null) _effects?.PlaySqueeze(_view);
        }
    }
}
