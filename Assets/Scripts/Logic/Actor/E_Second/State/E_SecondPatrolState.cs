using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>巡逻：在巡逻框的 x 区间里来回飞，只动 x 轴，越界或撞墙就掉头</summary>
    public sealed class E_SecondPatrolState : BaseState<E_SecondStateTag, LogicContext>
    {
        public override E_SecondStateTag Tag => E_SecondStateTag.Patrol;

        private readonly E_SecondLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly E_SecondConfig _config;

        private int _dir = 1;

        public E_SecondPatrolState(E_SecondLogic owner, IMoveMotor motor, E_SecondConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx)
        {
            _dir = ctx.WorldInfo.Face >= 0 ? 1 : -1;
        }

        public override void Tick(in LogicContext ctx)
        {
            // 只动 x：上浮与击退留下的竖直速度每帧清掉，巡逻高度就是进状态时的高度
            _motor.SetVelocityY(0f);

            // 只在面朝的正是前进方向时才认墙，掉头那一帧读到的还是旧一侧的数据
            if (_dir == ctx.WorldInfo.Face && ctx.WorldInfo.TouchingWall)
            {
                _dir = -_dir;
            }
            else if (_owner.PatrolArea.HasBounds)
            {
                if (_dir > 0 && ctx.WorldInfo.Pos.x >= _owner.PatrolArea.MaxX) _dir = -1;
                else if (_dir < 0 && ctx.WorldInfo.Pos.x <= _owner.PatrolArea.MinX) _dir = 1;
            }

            _motor.MoveTowardsX(new Vector2(_dir, 0f), _config.patrolSpeed);
        }

        public override bool IsDone(in LogicContext ctx) => false;

        public override void Exit() { }
    }
}
