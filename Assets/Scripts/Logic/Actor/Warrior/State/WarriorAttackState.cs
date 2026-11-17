using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>扑击：黑走前摇 → 抛物线扑向玩家 → 后摇；白走反向抛物线 → 顶点悬停 → 朝玩家开一枪</summary>
    /// <remarks>状态一进来就把形态锁死，中途翻极性也不改动作；轨迹里撞墙就地收手并进冷却</remarks>
    public sealed class WarriorAttackState : BaseState<EnemyStateTag, LogicContext>
    {
        public override EnemyStateTag Tag => EnemyStateTag.Attack;

        public WarriorAttackPhase Phase => _phase;

        /// <summary>切线模长的下限：s 推进要除以它，接近 0 时一步跨掉整条曲线</summary>
        private const float MinTangent = 0.05f;

        private const float DirEps = 1e-6f;

        private const float FaceDeadZone = 0.05f;

        private readonly WarriorLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly WarriorConfig _config;

        private WarriorAttackPhase _phase;
        private float _phaseStart;
        private bool _blackForm;
        private Vector2 _lockedTarget;
        private WarriorArc _arc;
        private float _s;
        private bool _hovered;
        private bool _done;

        public WarriorAttackState(WarriorLogic owner, IMoveMotor motor, WarriorConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx)
        {
            _blackForm = _owner.Black;
            _lockedTarget = _owner.TargetPos;
            _s = 0f;
            _done = false;

            if (_blackForm) BeginWindup(in ctx);
            else BeginLeap(in ctx);
        }

        public override void Tick(in LogicContext ctx)
        {
            float elapsed = ctx.GameTime.now - _phaseStart;

            switch (_phase)
            {
                case WarriorAttackPhase.Windup: TickWindup(in ctx, elapsed); break;
                case WarriorAttackPhase.Pounce: TickPounce(in ctx); break;
                case WarriorAttackPhase.Recover: TickRecover(in ctx, elapsed); break;
                case WarriorAttackPhase.Leap: TickLeap(in ctx); break;
                case WarriorAttackPhase.Hover: TickHover(in ctx, elapsed); break;
            }
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() => _motor.SetGravityEnable(true);

        private void BeginWindup(in LogicContext ctx)
        {
            _phase = WarriorAttackPhase.Windup;
            _phaseStart = ctx.GameTime.now;

            _motor.SetVelocityX(0f);
        }

        private void TickWindup(in LogicContext ctx, float elapsed)
        {
            _motor.SetVelocityX(0f);

            if (elapsed >= _config.windupTime) BeginPounce(in ctx);
        }

        private void BeginPounce(in LogicContext ctx)
        {
            _phase = WarriorAttackPhase.Pounce;
            _phaseStart = ctx.GameTime.now;
            _s = 0f;

            // 轨迹自己算，不吃重力：否则抛物线会被引擎再拉一遍，落点对不上
            _motor.SetGravityEnable(false);

            _arc = new WarriorArc(ctx.WorldInfo.Pos, _lockedTarget, _config.pounceApexHeight);
        }

        private void TickPounce(in LogicContext ctx)
        {
            if (ctx.WorldInfo.TouchingWall)
            {
                End(in ctx);
                return;
            }

            if (Advance(in ctx, 1f)) BeginRecover(in ctx);
        }

        private void BeginRecover(in LogicContext ctx)
        {
            _phase = WarriorAttackPhase.Recover;
            _phaseStart = ctx.GameTime.now;

            _motor.SetVelocityX(0f);
            _motor.SetGravityEnable(true);
        }

        private void TickRecover(in LogicContext ctx, float elapsed)
        {
            _motor.SetVelocityX(0f);

            if (elapsed >= _config.recoverTime) End(in ctx);
        }

        private void BeginLeap(in LogicContext ctx)
        {
            _phase = WarriorAttackPhase.Leap;
            _phaseStart = ctx.GameTime.now;
            _s = 0f;
            _hovered = false;

            _motor.SetGravityEnable(false);

            Vector2 start = ctx.WorldInfo.Pos;

            // 水平位移反向、落回起跳高度：跳开的距离与原本要扑的距离相等，方向背着玩家
            Vector2 landing = new(2f * start.x - _lockedTarget.x, start.y);

            _arc = new WarriorArc(start, landing, _config.pounceApexHeight);

            // 起跳就面朝玩家：整段后跃都反着方向走，悬停那一枪不必再转身
            FaceDirection(_lockedTarget.x - start.x);
        }

        private void TickLeap(in LogicContext ctx)
        {
            if (ctx.WorldInfo.TouchingWall)
            {
                End(in ctx);
                return;
            }

            if (!Advance(in ctx, _hovered ? 1f : WarriorArc.ApexS)) return;

            if (_hovered) End(in ctx);
            else BeginHover(in ctx);
        }

        private void BeginHover(in LogicContext ctx)
        {
            _phase = WarriorAttackPhase.Hover;
            _phaseStart = ctx.GameTime.now;

            _motor.SetVelocityX(0f);
            _motor.SetVelocityY(0f);
            _motor.SetGravityEnable(false);
        }

        private void TickHover(in LogicContext ctx, float elapsed)
        {
            _motor.SetVelocityX(0f);
            _motor.SetVelocityY(0f);

            // 每帧跟着玩家转：只在校正那一刻定朝向，玩家横移就会对着空气开枪
            FaceDirection(_owner.TargetPos.x - ctx.WorldInfo.Pos.x);

            if (elapsed < _config.hoverTime) return;

            Fire(in ctx);

            // 射击后接着走完抛物线后半程：重力仍然关着，落回起跳高度那一刻才收手
            _hovered = true;
            _phase = WarriorAttackPhase.Leap;
            _phaseStart = ctx.GameTime.now;
        }

        /// <summary>按弧长推进 s：速度恒为 pounceSpeed，陡的地方 s 走得慢</summary>
        private bool Advance(in LogicContext ctx, float stopAt)
        {
            Vector2 tangent = _arc.TangentAt(_s);
            float bend = Mathf.Max(MinTangent, tangent.magnitude);

            _s = Mathf.Min(stopAt, _s + _config.pounceSpeed * ctx.GameTime.deltaTime / bend);

            Vector2 heading = _arc.TangentAt(_s);
            Vector2 dir = heading.sqrMagnitude > DirEps ? heading.normalized : Vector2.right;

            Drive(dir);

            return _s >= stopAt - 1e-4f;
        }

        /// <summary>黑的朝向跟着切线走（撞墙探针也随它）；白的朝向锁在开跳那一刻，只写速度</summary>
        private void Drive(Vector2 dir)
        {
            if (_blackForm)
            {
                _motor.SnapVelocity(dir * _config.pounceSpeed);
                return;
            }

            _motor.SetVelocityX(dir.x * _config.pounceSpeed);
            _motor.SetVelocityY(dir.y * _config.pounceSpeed);
        }

        private void End(in LogicContext ctx)
        {
            _done = true;

            _motor.SetVelocityX(0f);
            _motor.SetGravityEnable(true);

            // 撞墙中断与正常走完都从这里进冷却
            _owner.MarkPounced(ctx.GameTime.now);
        }

        private void Fire(in LogicContext ctx)
        {
            Vector2 pos = ctx.WorldInfo.Pos;
            Vector2 delta = _owner.TargetPos - pos;

            if (delta.sqrMagnitude <= DirEps) return;

            Vector2 dir = delta.normalized;
            int sign = dir.x >= 0f ? 1 : -1;

            _owner.Fire(_owner.Muzzle(pos, sign), dir);
        }

        /// <summary>只认方向不看速度：白形态靠它决定这一段摆哪张脸（玩家正好同 x 时保持原朝向）</summary>
        private void FaceDirection(float dx)
        {
            if (Mathf.Abs(dx) <= FaceDeadZone) return;

            _motor.FaceTowards(new Vector2(Mathf.Sign(dx), 0f));
        }
    }
}
