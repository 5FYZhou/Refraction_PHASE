using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>飞行敌人 E_Second 的逻辑：持有玩家位置、巡逻领地与极性，状态推进交给 E_SecondMoveGroup</summary>
    /// <remarks>视线来自 DetectCollider 触发器，领地是编辑器画的追击框，都不留丢目标计时</remarks>
    public sealed class E_SecondLogic : ActorLogic, IAttacker, IIntervalAttacker
    {
        private readonly E_SecondConfig _config;
        private readonly E_SecondMoveGroup _moveGroup;
        private readonly IMoveMotor _motor;
        private readonly Cooldown _contactCooldown = new();
        private readonly System.Random _rng = new();

        private E_SecondTargetInfo _target;
        private E_SecondArea _patrolArea;
        private E_SecondArea _chaseArea;
        private bool _black;
        private IBulletLauncher _launcher;

        public E_SecondLogic(IMoveMotor motor, E_SecondConfig config, in E_SecondArea patrolArea, in E_SecondArea chaseArea, bool black)
            : base(motor, config)
        {
            _config = config;
            _motor = motor;
            _patrolArea = patrolArea;
            _chaseArea = chaseArea;
            _black = black;
            _moveGroup = new E_SecondMoveGroup(this, motor, config);

            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);
        }

        public void Dispose() => EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);

        public E_SecondStateTag CurrentTag => _moveGroup.Current;

        public E_SecondVisual Visual => _moveGroup.Visual;

        /// <summary>巡逻范围：也是返回态的落点</summary>
        public E_SecondArea PatrolArea => _patrolArea;

        /// <summary>追击范围：玩家出框即脱战，随 Recenter 与巡逻框一起平移</summary>
        public E_SecondArea ChaseArea => _chaseArea;

        /// <summary>当前世界极性是否为黑：黑出冲刺、白出子弹</summary>
        public bool Black => _black;

        public bool TargetKnown => _target.Known;

        public Vector2 TargetPos => _target.Pos;

        /// <summary>视线：DetectCollider 触发器里有玩家</summary>
        public bool Visible => _target.Known && _target.Visible;

        /// <summary>领地：玩家还在追击框里（x、y 都要在框内）；框宽 ≤ 0 时无界</summary>
        public bool Alert => _target.Known && _chaseArea.Contains(_target.Pos);

        /// <summary>表现层唯一入口：写入本帧玩家位置，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in E_SecondTargetInfo target)
        {
            _target = target;
            FixedTick(ctx);
        }

        public void BindLauncher(IBulletLauncher launcher) => _launcher = launcher;

        /// <summary>冲刺撞上玩家：身体圆与玩家受击盒相交；不看伤害回报，无敌窗里也算撞到</summary>
        public bool TouchingTarget(Vector2 pos)
        {
            if (!_target.Known) return false;

            float reach = Mathf.Max(0.01f, _motor.BodyHeight * 0.5f);

            return Mathf.Abs(pos.x - _target.BodyCenter.x) <= _target.HalfSize.x + reach
                && Mathf.Abs(pos.y - _target.BodyCenter.y) <= _target.HalfSize.y + reach;
        }

        /// <summary>返回态撞墙：把当前位置当新的巡逻中心，宽度不变；追击框按同一段位移跟着平移</summary>
        public void Recenter(Vector2 center)
        {
            Vector2 delta = center - _patrolArea.Center;

            _patrolArea = new E_SecondArea(true, center, _patrolArea.HalfSize);

            if (_chaseArea.HasBounds) _chaseArea = new E_SecondArea(true, _chaseArea.Center + delta, _chaseArea.HalfSize);
        }

        /// <summary>后摇上浮的随机距离，落在 [riseMin, riseMax]</summary>
        public float RollRiseDistance()
        {
            float min = Mathf.Min(_config.riseMin, _config.riseMax);
            float max = Mathf.Max(_config.riseMin, _config.riseMax);

            return Mathf.Lerp(min, max, (float)_rng.NextDouble());
        }

        public void FireWhite(Vector2 origin, Vector2 direction)
        {
            if (_launcher == null || _config.bulletSpeed <= 0f) return;

            _launcher.Launch(new BulletSpawn(
                origin,
                direction,
                _config.bulletSpeed,
                _config.bulletDamage,
                _config.bulletKnockback));
        }

        public Damage ProduceDamage() =>
            new(_config.contactDamage, DamageSource.Contact, Vector2.zero, _config.contactKnockback);

        public bool ReadyToHit(float now) => _contactCooldown.CanUse(now);

        public void MarkHit(float now) => _contactCooldown.MarkUsed(now, _config.contactInterval);

        /// <summary>敌人之间会挤在一起，接触伤害与敌弹只对玩家生效</summary>
        public override bool TakeDamage(in Damage damage)
        {
            if (damage.Source == DamageSource.Contact) return false;
            if (damage.Source == DamageSource.EnemyBullet) return false;

            return base.TakeDamage(in damage);
        }

        protected override void OnFixedTick(in LogicContext ctx) => _moveGroup.Tick(in ctx);

        protected override void OnTick(in LogicContext ctx) { }

        private void OnWorldPolarityChanged(WorldPolarityChanged evt) => _black = evt.World == WorldPolarity.Black;
    }
}
