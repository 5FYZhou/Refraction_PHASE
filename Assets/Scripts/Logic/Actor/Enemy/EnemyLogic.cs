using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>敌人逻辑：持有索敌结果与丢目标计时，状态推进交给 EnemyMoveGroup</summary>
    /// <remarks>索敌结果由表现层每物理帧注入</remarks>
    public sealed class EnemyLogic : ActorLogic, IAttack
    {
        private readonly EnemyConfig _config;
        private readonly EnemyMoveGroup _moveGroup;
        private readonly bool _aiEnabled;
        private readonly IMoveMotor _motor;

        private EnemyTargetInfo _target;
        private EnemyPatrolArea _patrolArea;
        private Vector2 _lastTargetPos;
        private float _lostTime;

        public EnemyLogic(IMoveMotor motor, EnemyConfig config, bool aiEnabled) : base(motor, config)
        {
            _config = config;
            _aiEnabled = aiEnabled;
            _motor = motor;
            _moveGroup = new EnemyMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        public bool TargetVisible => _target.Visible;

        /// <summary>最后一次看到玩家的位置：看不见之后仍有效，追踪靠它继续往那边走</summary>
        public Vector2 TargetPos => _lastTargetPos;

        /// <summary>丢目标还没超时：两态共用同一个计时，谁也别自己再维护一份</summary>
        public bool Alert => _target.Visible || _lostTime < _config.loseTargetTime;

        /// <summary>本帧的巡逻范围：无界表示没有 PatrolCollider 或已经跑出去了，整平台随便走</summary>
        public EnemyPatrolArea PatrolArea => _patrolArea;

        /// <summary>表现层唯一入口：写入本帧索敌结果与巡逻范围，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target, in EnemyPatrolArea patrolArea)
        {
            _target = target;
            _patrolArea = patrolArea;
            FixedTick(ctx);
        }

        protected override void OnFixedTick(in LogicContext ctx)
        {
            if (!_aiEnabled)
            {
                // 站桩：两态不推进，但横向速度必须刹住——刚体没有线性阻尼，被撞一下会一直滑
                _motor.BrakeTowardsX();
                return;
            }

            UpdateTarget(in ctx);

            _moveGroup.Tick(in ctx);
        }

        /// <summary>站桩时受击表现照常（受伤态与击退都走基类），只把伤害值抹成 0，血掉不下去</summary>
        public override void TakeDamage(in Damage damage)
        {
            // 敌人之间会挤在一起，接触伤害只对玩家生效，否则巡逻时两只怪互相打死
            if (damage.Source == DamageSource.Contact) return;

            if (_aiEnabled)
            {
                base.TakeDamage(in damage);
                return;
            }

            base.TakeDamage(new Damage(0f, damage.Source, damage.Direction, damage.KnockbackSpeed));
        }

        /// <summary>接触伤害：挨打的人往自己的反方向退</summary>
        public Damage ProduceDamage()
        {
            if (_config == null) return default;

            Vector2 dir = _aiEnabled ? Vector2.zero : new Vector2(_motor.FaceSign, 0f);

            return new Damage(
                _config.contactDamage,
                DamageSource.Contact,
                dir,
                _config.contactKnockback);
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
