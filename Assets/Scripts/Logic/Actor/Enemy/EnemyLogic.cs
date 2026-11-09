using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>敌人逻辑：持有索敌结果、丢目标计时与攻击冷却，状态推进交给 EnemyMoveGroup</summary>
    /// <remarks>索敌结果由表现层每物理帧注入；本类不碰场景对象，时间一律取 LogicContext.GameTime</remarks>
    public sealed class EnemyLogic : ActorLogic
    {
        private readonly EnemyConfig _config;
        private readonly EnemyMoveGroup _moveGroup;
        private readonly Cooldown _attackCooldown = new();

        private EnemyTargetInfo _target;
        private Vector2 _lastTargetPos;
        private float _lostTime;

        public EnemyLogic(IMoveMotor motor, EnemyConfig config) : base(motor, config)
        {
            _config = config;
            _moveGroup = new EnemyMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        public bool TargetVisible => _target.Visible;

        /// <summary>最后一次看到玩家的位置：看不见之后仍有效，追踪靠它继续往那边走</summary>
        public Vector2 TargetPos => _lastTargetPos;

        /// <summary>丢目标还没超时：三态共用同一个计时，谁也别自己再维护一份</summary>
        public bool Alert => _target.Visible || _lostTime < _config.loseTargetTime;

        public bool CanAttack(float now) => _attackCooldown.CanUse(now);

        public void MarkAttacked(float now) => _attackCooldown.MarkUsed(now, _config.attackCooldown);

        /// <summary>表现层唯一入口：先写入本帧索敌结果，再走 ActorLogic 的固定帧序（BeginStep → Tick → 重力 → Commit）</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target)
        {
            _target = target;
            FixedTick(ctx);
        }

        protected override void OnFixedTick(in LogicContext ctx)
        {
            UpdateTarget(in ctx);

            _moveGroup.Tick(in ctx);
        }

        protected override void OnTick(in LogicContext ctx) { }

        private void UpdateTarget(in LogicContext ctx)
        {
            if (_target.Visible)
            {
                _lostTime = 0f;
                _lastTargetPos = _target.Pos;
                return;
            }

            _lostTime += ctx.GameTime.deltaTime;
        }
    }
}
