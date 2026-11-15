using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>敌人逻辑公共底盘：黑白切换、站桩、视线与丢目标计时、接触伤害；状态推进留给子类</summary>
    /// <remarks>帧序由 ActorLogic 固定：站桩时整个 AI 不推进，连丢目标计时都冻住</remarks>
    public abstract class EnemyLogicBase : ActorLogic, IAttacker, IIntervalAttacker
    {
        private readonly Cooldown _contactCooldown = new();
        private readonly bool _aiEnabled;

        private IBulletLauncher _launcher;
        private bool _black;
        private bool _targetKnown;
        private bool _targetVisible;
        private Vector2 _targetPos;
        private Vector2 _lastSeenPos;
        private float _lostTime;

        protected EnemyLogicBase(IMoveMotor motor, CharacterConfig config, bool aiEnabled, WorldPolarity world)
            : base(motor, config)
        {
            Motor = motor;
            _aiEnabled = aiEnabled;
            _black = world == WorldPolarity.Black;

            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);
        }

        public IMoveMotor Motor { get; }

        public bool Black => _black;

        /// <summary>视线：本帧探测器里确实有玩家（Known 为 false 时一律算看不见）</summary>
        public bool Visible => _targetKnown && _targetVisible;

        public bool TargetKnown => _targetKnown;

        /// <summary>玩家当前所在位置：看不见时仍是上一帧的位置</summary>
        public Vector2 TargetPos => _targetPos;

        /// <summary>最后一次看到玩家的位置：看不见之后仍有效，追击靠它继续往那边走</summary>
        public Vector2 LastSeenPos => _lastSeenPos;

        /// <summary>丢目标还没超时：两态共用同一个计时，谁也别自己再维护一份</summary>
        public virtual bool Alert => Visible || _lostTime < LoseTargetTime;

        public virtual bool ChaseAllowed => true;

        protected abstract float ContactDamage { get; }

        protected abstract float ContactInterval { get; }

        protected abstract float ContactKnockback { get; }

        protected virtual float LoseTargetTime => 0f;

        protected IBulletLauncher Launcher => _launcher;

        public void Dispose() => EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);

        public void BindLauncher(IBulletLauncher launcher) => _launcher = launcher;

        /// <summary>接触伤害：站桩时朝朝向推，AI 打开时不带方向，免得把玩家一路顶走</summary>
        public Damage ProduceDamage() => new(
            ContactDamage,
            DamageSource.Contact,
            _aiEnabled ? Vector2.zero : new Vector2(Motor.FaceSign, 0f),
            ContactKnockback);

        public bool ReadyToHit(float now) => _contactCooldown.CanUse(now);

        public void MarkHit(float now) => _contactCooldown.MarkUsed(now, ContactInterval);

        /// <summary>站桩：伤害值抹成 0、血量不动，但受击表现与命中反馈照常，所以照样算命中</summary>
        /// <remarks>敌人之间会挤在一起，接触伤害与敌弹都只对玩家生效，否则怪物会互相打死</remarks>
        public override bool TakeDamage(in Damage damage)
        {
            if (damage.Source == DamageSource.Contact) return false;
            if (damage.Source == DamageSource.EnemyBullet) return false;

            if (_aiEnabled) return base.TakeDamage(in damage);

            base.TakeDamage(new Damage(0f, damage.Source, damage.Direction, damage.KnockbackSpeed));

            return IsAlive && !Conditions.IsInvulnerable;
        }

        /// <summary>表现层唯一入口：写入本帧索敌结果，再由子类走基类帧序</summary>
        protected void SetSight(bool known, bool visible, Vector2 pos)
        {
            _targetKnown = known;
            _targetVisible = visible;
            _targetPos = pos;
        }

        protected override void OnFixedTick(in LogicContext ctx)
        {
            if (!_aiEnabled)
            {
                StopOnSpot(in ctx);
                return;
            }

            TrackTarget(in ctx);
            OnAiTick(in ctx);
        }

        protected override void OnTick(in LogicContext ctx) { }

        protected abstract void StopOnSpot(in LogicContext ctx);

        protected abstract void OnAiTick(in LogicContext ctx);

        private void TrackTarget(in LogicContext ctx)
        {
            if (Visible)
            {
                _lostTime = 0f;
                _lastSeenPos = _targetPos;
                return;
            }

            _lostTime += ctx.GameTime.deltaTime;
        }

        private void OnWorldPolarityChanged(WorldPolarityChanged evt) => _black = evt.World == WorldPolarity.Black;
    }
}
