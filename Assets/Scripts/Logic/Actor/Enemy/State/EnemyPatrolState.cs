using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>巡逻：有 PatrolCollider 就在它的 x 范围内来回走，没有就走到墙或悬崖才掉头；离地不推进</summary>
    public sealed class EnemyPatrolState : BaseState<EnemyStateTag, LogicContext>
    {
        public override EnemyStateTag Tag => EnemyStateTag.Patrol;

        private readonly EnemyLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly EnemyConfig _config;

        private int _dir = 1;

        public EnemyPatrolState(EnemyLogic owner, IMoveMotor motor, EnemyConfig config)
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
            // 离地（坠落、被顶飞）不推进，否则会在空中左右飘
            if (!ctx.WorldInfo.Grounded)
            {
                _motor.BrakeTowardsX();
                return;
            }

            // 前方是墙或悬崖就掉头；只在面朝的正是前进方向时才认，掉头那一帧读到的还是旧一侧的数据
            bool blocked = _dir == ctx.WorldInfo.Face
                           && (ctx.WorldInfo.TouchingWall || ctx.WorldInfo.LedgeAhead);

            if (blocked)
            {
                _dir = -_dir;
            }
            else
            {
                EnemyPatrolArea area = _owner.PatrolArea;

                if (area.HasBounds && _dir > 0 && ctx.WorldInfo.Pos.x >= area.MaxX) _dir = -1;
                else if (area.HasBounds && _dir < 0 && ctx.WorldInfo.Pos.x <= area.MinX) _dir = 1;
            }

            _motor.MoveTowardsX(new Vector2(_dir, 0f), _config.patrolSpeed);
        }

        public override bool IsDone(in LogicContext ctx) => false;

        public override void Exit() { }
    }
}
