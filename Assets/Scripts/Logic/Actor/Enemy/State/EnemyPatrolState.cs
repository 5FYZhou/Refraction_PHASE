using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>巡逻：在 [巡逻中心 ± 半径] 之间来回走，只在地面上推进；越界或贴墙就掉头</summary>
    public sealed class EnemyPatrolState : BaseState<EnemyStateTag, LogicContext>
    {
        public override EnemyStateTag Tag => EnemyStateTag.Patrol;

        private readonly IMoveMotor _motor;
        private readonly EnemyConfig _config;

        private int _dir = 1;
        private float _originX;

        public EnemyPatrolState(IMoveMotor motor, EnemyConfig config)
        {
            _motor = motor;
            _config = config;
        }

        /// <summary>每次进来都以当前站位为中心：出生时就是出生点，追远之后也不会一路走回原处</summary>
        public override void Enter(in LogicContext ctx)
        {
            _originX = ctx.WorldInfo.Pos.x;
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

            if (blocked) _dir = -_dir;
            else if (_dir > 0 && ctx.WorldInfo.Pos.x >= _originX + _config.patrolRadius) _dir = -1;
            else if (_dir < 0 && ctx.WorldInfo.Pos.x <= _originX - _config.patrolRadius) _dir = 1;

            _motor.MoveTowardsX(new Vector2(_dir, 0f), _config.patrolSpeed);
        }

        public override bool IsDone(in LogicContext ctx) => false;

        public override void Exit() { }
    }
}
