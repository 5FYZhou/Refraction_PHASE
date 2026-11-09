using Refraction.Data;
using Refraction.Foundation;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public class HurtState : BaseState<ConditionStateTag, LogicContext>
    {
        public override ConditionStateTag Tag => ConditionStateTag.Hurt;

        private CharacterConfig _config;
        private bool _justEntered;
        private Vector2 _direction = Vector2.up;
        private float _speed;
        public Vector2 ForcedVelocity => _direction * _speed;

        public HurtState(CharacterConfig config)
        {
            _config = config;
        }

        /// <summary>喂入一次性冲量（速度向量/秒），参数唯一写入口</summary>
        /// <remarks>零冲量也要写：不复位就沿用上一次的击退速度，受伤态会莫名滑一段</remarks>
        public void Configure(Vector2 impulse)
        {
            if (impulse.sqrMagnitude <= 0f)
            {
                _direction = Vector2.up;
                _speed = 0f;
                return;
            }

            _direction = impulse.normalized;
            _speed = impulse.magnitude;
        }

        public override void Enter(in LogicContext ctx)
        {
            // 冲量是瞬时量，进入那帧速度即冲量
            _justEntered = true;
        }

        public override void Tick(in LogicContext ctx)
        {
            if (_justEntered)
            {
                _justEntered = false;
                return;
            }

            if (_speed <= 0f) return;

            _speed = Mathf.Max(0f, _speed - _config.KnockbackDecay * ctx.GameTime.deltaTime);
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return _speed <= 0f;
        }

        public override void Exit() 
        {
            _direction = Vector2.zero;
            _speed = 0f;
        }
    }
}
