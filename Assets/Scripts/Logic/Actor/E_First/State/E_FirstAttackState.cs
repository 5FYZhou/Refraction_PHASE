using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>射击：前摇立定瞄准，开火后仍站着，后摇才朝玩家爬；不设冷却，看得见就接着打</summary>
    public sealed class E_FirstAttackState : BaseState<E_FirstStateTag, LogicContext>
    {
        public override E_FirstStateTag Tag => E_FirstStateTag.Attack;

        public E_FirstAttackPhase Phase => _phase;

        private const float FaceDeadZone = 0.05f;
        private const float DirEps = 1e-6f;

        private readonly E_FirstLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly E_FirstConfig _config;

        private E_FirstAttackPhase _phase;
        private float _phaseStart;
        private Vector2 _lockedDir = Vector2.right;
        private bool _done;

        public E_FirstAttackState(E_FirstLogic owner, IMoveMotor motor, E_FirstConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx) => BeginWindup(in ctx);

        public override void Tick(in LogicContext ctx)
        {
            // 转黑当帧收手：黑世界对玩家没有反应
            if (!_owner.Hostile)
            {
                _done = true;
                return;
            }

            float elapsed = ctx.GameTime.now - _phaseStart;

            switch (_phase)
            {
                case E_FirstAttackPhase.Windup: TickWindup(in ctx, elapsed); break;
                case E_FirstAttackPhase.Strike: TickStrike(in ctx, elapsed); break;
                case E_FirstAttackPhase.Recover: TickRecover(in ctx, elapsed); break;
            }
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }

        private void BeginWindup(in LogicContext ctx)
        {
            _phase = E_FirstAttackPhase.Windup;
            _phaseStart = ctx.GameTime.now;
            _done = false;

            _motor.SetVelocityX(0f);
        }

        /// <summary>前摇：立定跟着玩家转身，方向到结束那一帧才锁</summary>
        private void TickWindup(in LogicContext ctx, float elapsed)
        {
            _motor.SetVelocityX(0f);

            FaceTarget(_owner.TargetPos - ctx.WorldInfo.Pos);

            if (elapsed >= _config.windupTime) BeginStrike(in ctx);
        }

        private void BeginStrike(in LogicContext ctx)
        {
            Vector2 pos = ctx.WorldInfo.Pos;
            Vector2 delta = _owner.TargetPos - pos;

            int sign = ctx.WorldInfo.Face >= 0 ? 1 : -1;

            _lockedDir = delta.sqrMagnitude > DirEps
                ? delta.normalized
                : new Vector2(sign, 0f);

            _phase = E_FirstAttackPhase.Strike;
            _phaseStart = ctx.GameTime.now;

            _motor.SetVelocityX(0f);
            _motor.FaceTowards(new Vector2(Mathf.Abs(_lockedDir.x) > FaceDeadZone ? Mathf.Sign(_lockedDir.x) : sign, 0f));

            _owner.Fire(_owner.Muzzle(pos, sign), _lockedDir);
        }

        private void TickStrike(in LogicContext ctx, float elapsed)
        {
            _motor.SetVelocityX(0f);

            if (elapsed >= _config.strikeTime) BeginRecover(in ctx);
        }

        private void BeginRecover(in LogicContext ctx)
        {
            _phase = E_FirstAttackPhase.Recover;
            _phaseStart = ctx.GameTime.now;
        }

        /// <summary>后摇：看得见就往玩家爬，被框边、墙或悬崖挡住就原地；走完还在视线里直接回前摇</summary>
        private void TickRecover(in LogicContext ctx, float elapsed)
        {
            if (_owner.Visible) CrawlTowards(in ctx);
            else _motor.SetVelocityX(0f);

            if (elapsed >= _config.recoverTime) EndRecover(in ctx);
        }

        private void EndRecover(in LogicContext ctx)
        {
            if (_owner.Visible)
            {
                BeginWindup(in ctx);
                return;
            }

            _done = true;
        }

        private void CrawlTowards(in LogicContext ctx)
        {
            float dx = _owner.TargetPos.x - ctx.WorldInfo.Pos.x;

            // 已经贴到守住的横向距离内就站住：再爬会把玩家顶走
            if (Mathf.Abs(dx) <= _config.recoverChaseRadius)
            {
                _motor.SetVelocityX(0f);
                return;
            }

            int dir = dx > 0f ? 1 : -1;

            // 只在面朝的正是前进方向时才认墙与悬崖，掉头那一帧读到的还是旧一侧的数据
            bool facingAhead = dir == ctx.WorldInfo.Face;

            if (!ctx.WorldInfo.Grounded
                || (facingAhead && (ctx.WorldInfo.TouchingWall || ctx.WorldInfo.LedgeAhead))
                || OutsideArea(ctx.WorldInfo.Pos.x, dir))
            {
                _motor.SetVelocityX(0f);
                return;
            }

            // 恒速：直接写速度，不走 moveAcceleration 逼近，后摇爬行不会起步冲刺
            _motor.FaceTowards(new Vector2(dir, 0f));
            _motor.SetVelocityX(dir * _config.recoverChaseSpeed);
        }

        private bool OutsideArea(float x, int dir)
        {
            E_FirstArea area = _owner.PatrolArea;

            if (!area.HasBounds) return false;

            return dir > 0 ? x >= area.MaxX : x <= area.MinX;
        }

        /// <summary>玩家正上 / 正下方时保持原朝向，别让 x 分量接近 0 的抖动把朝向翻来覆去</summary>
        private void FaceTarget(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) <= FaceDeadZone) return;

            _motor.FaceTowards(new Vector2(Mathf.Sign(delta.x), 0f));
        }
    }
}
