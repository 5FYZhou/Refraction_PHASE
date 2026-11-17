using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>巡逻：只在踩地时沿 x 走，到框边、贴墙或前头是悬崖就掉头</summary>
    public sealed class WarriorPatrolState : BaseState<EnemyStateTag, LogicContext>
    {
        public override EnemyStateTag Tag => EnemyStateTag.Patrol;

        private readonly WarriorLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly WarriorConfig _config;

        private int _dir = 1;

        public WarriorPatrolState(WarriorLogic owner, IMoveMotor motor, WarriorConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx) => _dir = ctx.WorldInfo.Face >= 0 ? 1 : -1;

        public override void Tick(in LogicContext ctx)
        {
            // 离地（坠落、被顶飞）不推进，否则会在空中左右飘
            if (!ctx.WorldInfo.Grounded)
            {
                _motor.BrakeTowardsX();
                return;
            }

            // 只在面朝的正是前进方向时才认墙与悬崖，掉头那一帧读到的还是旧一侧的数据
            bool facingAhead = _dir == ctx.WorldInfo.Face;

            if (facingAhead && (ctx.WorldInfo.TouchingWall || ctx.WorldInfo.LedgeAhead))
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
