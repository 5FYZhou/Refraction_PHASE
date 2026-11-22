using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>返回：朝巡逻框中心直线飞；半路撞墙就把当前位置立成新中心，两框尺寸都不变</summary>
    public sealed class SpliterReturnState : BaseState<EnemyStateTag, LogicContext>
    {
        public override EnemyStateTag Tag => EnemyStateTag.Return;

        private const float ArriveEps = 0.2f;

        private readonly SpliterLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly SpliterConfig _config;

        private bool _arrived;

        public SpliterReturnState(SpliterLogic owner, IMoveMotor motor, SpliterConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx) => _arrived = false;

        public override void Tick(in LogicContext ctx)
        {
            // 圆环里探到墙就没法直线回到中心：就地重新定义中心，不再硬顶
            if (ctx.WorldInfo.TouchingWall)
            {
                _owner.Recenter(ctx.WorldInfo.Pos);
                Stop();
                return;
            }

            Vector2 delta = _owner.PatrolArea.Center - ctx.WorldInfo.Pos;
            float distance = delta.magnitude;

            if (distance <= ArriveEps)
            {
                Stop();
                return;
            }

            _motor.SnapVelocity(delta / distance * _config.moveSpeed);
        }

        public override bool IsDone(in LogicContext ctx) => _arrived;

        public override void Exit() { }

        private void Stop()
        {
            _arrived = true;
            _motor.SnapVelocity(Vector2.zero);
        }
    }
}
