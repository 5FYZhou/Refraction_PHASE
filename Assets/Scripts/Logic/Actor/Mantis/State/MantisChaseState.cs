using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>追击：朝玩家最后已知位置走，进到停止距离就刹住，起手由状态组抢占</summary>
    public sealed class MantisChaseState : BaseState<EnemyStateTag, LogicContext>
    {
        public override EnemyStateTag Tag => EnemyStateTag.Chase;

        private readonly MantisLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly MantisConfig _config;

        public MantisChaseState(MantisLogic owner, IMoveMotor motor, MantisConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx) { }

        public override void Tick(in LogicContext ctx)
        {
            // 看得见之后用最后已知位置：玩家刚出范围时仍往那边走，不会拐去原点
            float dx = _owner.LastSeenPos.x - ctx.WorldInfo.Pos.x;

            if (Mathf.Abs(dx) <= _owner.StopDistance)
            {
                _motor.BrakeTowardsX();
                return;
            }

            int dir = dx > 0f ? 1 : -1;

            // 玩家在悬崖对面就停在边上：只在正要朝崖边走时拦，掉头那一帧不为难它
            if (ctx.WorldInfo.Grounded && ctx.WorldInfo.LedgeAhead && dir == ctx.WorldInfo.Face)
            {
                _motor.BrakeTowardsX();
                return;
            }

            _motor.MoveTowardsX(new Vector2(dir, 0f), _config.moveSpeed);
        }

        /// <summary>丢目标超时即结束，回巡逻由 Fallback 决定</summary>
        public override bool IsDone(in LogicContext ctx) => !_owner.Alert;

        public override void Exit() { }
    }
}
