using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>追击：朝玩家直线飞，贴到 chaseStopDistance 就悬停，始终面朝玩家</summary>
    public sealed class E_SecondChaseState : BaseState<E_SecondStateTag, LogicContext>
    {
        public override E_SecondStateTag Tag => E_SecondStateTag.Chase;

        private const float FaceDeadZone = 0.05f;

        private readonly E_SecondLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly E_SecondConfig _config;

        public E_SecondChaseState(E_SecondLogic owner, IMoveMotor motor, E_SecondConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx) { }

        public override void Tick(in LogicContext ctx)
        {
            Vector2 delta = _owner.TargetPos - ctx.WorldInfo.Pos;
            float distance = delta.magnitude;

            if (distance <= _config.chaseStopDistance || distance <= 1e-4f)
            {
                _motor.SnapVelocity(Vector2.zero);
                FaceTarget(delta);
                return;
            }

            _motor.SnapVelocity(delta / distance * _config.moveSpeed);

            // SnapVelocity 会按速度方向改朝向，飞的是斜线时得盖回水平朝向
            FaceTarget(delta);
        }

        /// <summary>丢目标/出领地即结束，回巡逻还是回返回态由 Fallback 决定</summary>
        public override bool IsDone(in LogicContext ctx) => !_owner.Alert;

        public override void Exit() { }

        /// <summary>玩家正上/正下方时保持原朝向，别让 x 分量接近 0 的抖动把朝向翻来覆去</summary>
        private void FaceTarget(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) <= FaceDeadZone) return;

            _motor.FaceTowards(new Vector2(Mathf.Sign(delta.x), 0f));
        }
    }
}
