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

        /// <summary>蓄力起鼓与挤压共用 View 的缩放，由表现层按逻辑的 ChargingShot 每帧灌进来</summary>
        public void SetCharging(bool on, float rampTime, Vector2 muzzleOffset, Color chargeInColor)
        {
            _effects?.SetCharge(_target, _view, on, rampTime);
            _effects?.SetChargeIn(_target, muzzleOffset, on, rampTime, chargeInColor);
        }
    }
}
