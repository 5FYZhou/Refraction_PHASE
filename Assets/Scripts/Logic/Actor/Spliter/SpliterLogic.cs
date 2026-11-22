using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>Spliter：持有巡逻 / 追击两个框、攻击冷却与弹反冷却，状态推进交给 SpliterMoveGroup</summary>
    /// <remarks>追击框是领地：玩家出框即脱战，所以 Alert 不看视线也不看丢目标计时</remarks>
    public sealed class SpliterLogic : EnemyLogicBase
    {
        private readonly SpliterConfig _config;
        private readonly SpliterMoveGroup _moveGroup;
        private readonly Cooldown _attackCooldown = new();
        private readonly Cooldown _reboundCooldown = new();
        private readonly System.Random _rng = new();
        private readonly Vector2 _muzzle;

        private EnemyArea _patrolArea;
        private EnemyArea _chaseArea;
        private EnemyTargetInfo _target;

        public SpliterLogic(
            IMoveMotor motor,
            SpliterConfig config,
            IHurtView hurtView,
            in EnemyArea patrolArea,
            in EnemyArea chaseArea,
            bool black = false,
            bool aiEnabled = true,
            Vector2 muzzle = default)
            : base(motor, config, hurtView, aiEnabled, black)
        {
            _config = config;
            _patrolArea = patrolArea;
            _chaseArea = chaseArea;
            _muzzle = muzzle;
            _moveGroup = new SpliterMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        public SpliterAttackPhase Phase => _moveGroup.Phase;

        /// <summary>巡逻范围：也是返回态的落点</summary>
        public EnemyArea PatrolArea => _patrolArea;

        /// <summary>追击范围：玩家出框即脱战，随 Recenter 一起平移</summary>
        public EnemyArea ChaseArea => _chaseArea;

        /// <summary>领地：玩家还在追击框里（x、y 都要在框内）；框宽 0 时无界</summary>
        public override bool Alert => TargetKnown && _chaseArea.Contains(TargetPos);

        /// <summary>只有白形态起鼓蓄力；黑形态前摇接的是冲撞，没有开火</summary>
        public override bool ChargingShot =>
            !Black && Launcher != null && _config.bulletSpeed > 0f &&
            CurrentTag == EnemyStateTag.Attack && _moveGroup.Phase == SpliterAttackPhase.Windup;

        public override float ChargingTime => _config.barrageWindupTime;

        /// <summary>白形态攻击全程参与转向：前摇跟人，锁定之后不再动</summary>
        public bool BarrageAiming => !Black && CurrentTag == EnemyStateTag.Attack;

        /// <summary>转向要看的点：优先受击盒中心，缺了退回玩家枢轴</summary>
        public Vector2 AimPoint => _target.HalfSize.sqrMagnitude > 0f ? _target.BodyCenter : TargetPos;

        protected override float ContactDamage => _config.contactDamage;

        protected override float ContactInterval => _config.contactInterval;

        protected override float ContactKnockback => _config.contactKnockback;

        /// <summary>表现层唯一入口：写入本帧玩家信息，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target)
        {
            _target = target;
            SetSight(target.Known, target.Visible, target.Pos);
            FixedTick(ctx);
        }

        /// <summary>够不够近了：黑白共用同一个触发距离</summary>
        public bool InAttackRange(Vector2 pos) =>
            (TargetPos - pos).sqrMagnitude <= _config.attackRange * _config.attackRange;

        public bool CanAttack(float now) => _attackCooldown.CanUse(now);

        /// <summary>记一次起手：冷却从前摇那一帧算，整套动作都在间隔内；黑白的间隔各取各的</summary>
        public void BeginAttackCooldown(float now) =>
            _attackCooldown.MarkUsed(now, Black ? _config.chargeCooldown : _config.barrageCooldown);

        /// <summary>弹反：只有黑形态能弹，冷却之内只能弹一发</summary>
        public bool CanRebound(float now) => Black && _reboundCooldown.CanUse(now);

        public void MarkRebounded(float now) => _reboundCooldown.MarkUsed(now, _config.reboundCooldown);

        /// <summary>返回态撞墙：当前位置当成巡逻与追击两框的新中心，尺寸都不变</summary>
        public void Recenter(Vector2 center)
        {
            if (_patrolArea.HasBounds) _patrolArea = new EnemyArea(true, center, _patrolArea.HalfSize);
            if (_chaseArea.HasBounds) _chaseArea = new EnemyArea(true, center, _chaseArea.HalfSize);
        }

        /// <summary>后摇浮动：方向随机、距离在区间内随机，时长由距离 / 速度除出来</summary>
        public void RollFloat(out Vector2 dir, out float duration)
        {
            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
            dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float min = Mathf.Min(_config.floatDistanceMin, _config.floatDistanceMax);
            float max = Mathf.Max(_config.floatDistanceMin, _config.floatDistanceMax);
            float distance = Mathf.Lerp(min, max, (float)_rng.NextDouble());

            duration = distance / Mathf.Max(0.01f, _config.floatSpeed);
        }

        public Vector2 Muzzle(Vector2 pos, int sign) =>
            pos + new Vector2(Mathf.Abs(_muzzle.x) * sign, _muzzle.y);

        /// <summary>一波散射：以 dir 为中心左右对称张开，spreadDegrees 是相邻两发的夹角</summary>
        public void FireWave(Vector2 origin, Vector2 dir, int count, float spreadDegrees)
        {
            if (Launcher == null || _config.bulletSpeed <= 0f || count <= 0) return;

            float half = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Fire(origin, Rotate(dir, (i - half) * spreadDegrees));
            }
        }

        /// <summary>弹反的平行两发：沿弹道法线各偏一段，方向与被弹反的那发完全一致</summary>
        public void FireParallel(Vector2 origin, Vector2 dir)
        {
            if (Launcher == null || _config.bulletSpeed <= 0f) return;

            Vector2 perpendicular = new(-dir.y, dir.x);
            Vector2 side = perpendicular * _config.reboundSpread;

            Fire(origin + side, dir);
            Fire(origin - side, dir);
        }

        private void Fire(Vector2 origin, Vector2 dir)
        {
            Launcher.Launch(new BulletSpawn(
                origin,
                dir,
                _config.bulletSpeed,
                _config.bulletDamage,
                _config.bulletKnockback));
        }

        private static Vector2 Rotate(Vector2 dir, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            return new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
        }

        protected override void StopOnSpot(in LogicContext ctx) => Motor.SnapVelocity(Vector2.zero);

        protected override void OnAiTick(in LogicContext ctx) => _moveGroup.Tick(in ctx);
    }
}
