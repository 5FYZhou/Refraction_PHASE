using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public sealed class PlayerLogic : ActorLogic, IHitLanded
    {
        private const int FallDamage = 1;

        private MoveGroup _moveGroup;
        private PostureGroup _postureGroup;
        private AttackGroup _attackGroup;
        private TransitMotor _transit;
        private IMoveMotor _motor;
        private InputBuffer _buffer;
        private PlayerConfig _config;
        private readonly PolarityGauge _polarity;

        private IAfterimageSpawner _afterimages;
        private readonly Cooldown _chargeCooldown = new();
        private ChargePhase _chargePhase;
        private float _chargeEnterTime;
        private float _chargeNow;

        private Vector2 _lastGroundedPos;
        private bool _hasGroundedPos;
        private Vector2 _curScale = Vector2.one;

        private DoorEntry _pendingEntry;
        private MovementStateTag _pendingEntryTag;
        private int _pendingEntryFace;
        private bool _hasPendingEntry;

        public PlayerLogic(IMoveMotor motor, PlayerConfig config, InputBuffer buffer, IBulletLauncher launcher, IBlackAttackView blackAttack = null) : base(motor, config)
        {
            _postureGroup = new(config, motor);
            _moveGroup = new(motor, config, buffer, _postureGroup);
            _attackGroup = new(config, motor, launcher, _postureGroup, blackAttack, Conditions);
            _transit = new(motor, config, _moveGroup);
            _motor = motor;
            _buffer = buffer;
            _config = config;
            _polarity = new(config.maxPolarity);
        }

        /// <summary>还没踩过地（出生就在空中）时为 false</summary>
        public bool HasGroundedPos => _hasGroundedPos;

        /// 最近一次踩地点
        public Vector2 LastGroundedPos => _lastGroundedPos;

        /// PlayerController 每帧拿去应用
        public Vector2 CurViewScale { get => _curScale; }

        /// 广播当前血量：装配时给初值，掉血与复活后再发
        public void Announce()
        {
            EventBus<PlayerHealthChanged>.Publish(new(CurHealth, MaxHealth));
        }

        public MovementStateTag CurrentTag => _moveGroup.CurrentTag;

        public PostureStateTag PostureTag => _postureGroup.Current;

        public AttackStateTag AttackTag => _attackGroup.Current;

        public int Polarity => _polarity.Value;

        public int MaxPolarity => _polarity.Max;

        public void OnHitLanded()
        {
            int before = _polarity.Value;
            _polarity.Add(_config.polarityPerHit);

            if (_polarity.Value != before) AnnouncePolarity();
        }

        public void OnBlackAttackHitLanded()
        {
            OnHitLanded();
            EventBus<HitStopRequested>.Publish(new(_config.blackAttackHitStopTime));
        }

        public void AnnouncePolarity()
        {
            EventBus<PlayerPolarityChanged>.Publish(new(_polarity.Value, _polarity.Max));
        }

        public ChargePhase ChargeStage => _chargePhase;

        /// <summary>0 没在充能，1 前摇走满</summary>
        public float ChargeProgress
        {
            get
            {
                if (_chargePhase == ChargePhase.Charged) return 1f;
                if (_chargePhase != ChargePhase.Charging || _config.chargePrepareTime <= 0f) return 0f;

                return Mathf.Clamp01((_chargeNow - _chargeEnterTime) / _config.chargePrepareTime);
            }
        }

        public bool IsCharged => _chargePhase == ChargePhase.Charged;

        public ConditionStateTag ConditionTag => Conditions.Current;

        public bool IsInvulnerable => Conditions.IsInvulnerable;

        public Vector2 HurtVelocity => Conditions.HurtVelocity;

        public Vector2 Position => Vector2.zero;

        protected override void OnFixedTick(in LogicContext ctx)
        {
            // 记在移动之前：帧末位置可能已被推出平台边缘
            if (ctx.WorldInfo.Grounded) RememberGround(ctx.WorldInfo.Pos);

            // 过门运动必须等帧首（BeginStep 之后）才能动速度
            if (_hasPendingEntry)
            {
                _hasPendingEntry = false;
                _transit.Begin(in _pendingEntry, _pendingEntryTag, _pendingEntryFace, in ctx);
            }

            LogicContext tick = _transit.Apply(in ctx);

            // 入缓冲的必须是最终生效的输入
            _buffer.Push(in tick.InputSnapshot, tick.GameTime.now);

            // 姿态先于移动：反过来会晚一帧，按下蹲键的那帧还能跳
            _postureGroup.Tick(in tick);

            MovementStateTag beforeMove = _moveGroup.CurrentTag;
            _moveGroup.Tick(in tick);

            bool dashStarted = beforeMove != MovementStateTag.Dash && _moveGroup.CurrentTag == MovementStateTag.Dash;

            _attackGroup.Tick(in tick);

            TickCharge(in tick, dashStarted);

            TryReverseWorld(in tick);

            _transit.AfterTick(in tick);
        }

        private void TickCharge(in LogicContext ctx, bool dashStarted)
        {
            float now = ctx.GameTime.now;
            _chargeNow = now;

            if (dashStarted && _afterimages != null && _afterimages.TrySpawn(ctx.WorldInfo.Pos, now, ctx.WorldInfo.Face))
            {
                _chargeCooldown.MarkUsed(now, _config.chargeCooldown);
                _chargePhase = ChargePhase.None;
                return;
            }

            if (_chargePhase == ChargePhase.Charged) return;

            if (_chargePhase == ChargePhase.Charging)
            {
                if (now - _chargeEnterTime >= _config.chargePrepareTime) GoCharged();
                return;
            }

            if (!ctx.InputSnapshot.ChargePressed) return;
            if (!_chargeCooldown.CanUse(now)) return;

            if (_config.chargePrepareTime <= 0f)
            {
                GoCharged();
                return;
            }

            _chargeEnterTime = now;
            _chargePhase = ChargePhase.Charging;
        }

        private void GoCharged()
        {
            _chargePhase = ChargePhase.Charged;
            _afterimages?.Arm();
        }

        private void TryReverseWorld(in LogicContext ctx)
        {
            if (!ctx.InputSnapshot.ReversePressed) return;
            if (_polarity.Value < _polarity.Max) return;

            _polarity.Clear();
            ResetHealth();
            Announce();
            AnnouncePolarity();
            EventBus<WorldReversed>.Publish(default);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            _curScale = _postureGroup.ViewScale;
        }

        public void SpawnTo(Vector2 pos)
        {
            _motor.SpawnAt(pos, Vector2.zero);
            _afterimages?.Clear();

            _chargePhase = ChargePhase.None;
            _chargeEnterTime = 0f;
        }

        public void BindAfterimageSpawner(IAfterimageSpawner spawner)
        {
            _afterimages = spawner;

            if (_chargePhase == ChargePhase.Charged) spawner?.Arm();
        }

        public void BindBulletLauncher(IBulletLauncher launcher) => _attackGroup.BindLauncher(launcher);

        public void Dispose() => _attackGroup.Dispose();

        public void BeginTransit(in DoorEntry entry, MovementStateTag enterTag, int enterFace)
        {
            _pendingEntry = entry;
            _pendingEntryTag = enterTag;
            _pendingEntryFace = enterFace;
            _hasPendingEntry = true;
        }

        /// <summary>打断下蹲前摇：立刻回站立并恢复移速乘数与缩放；不在前摇里调用无副作用</summary>
        public bool InterruptCrouch(in LogicContext ctx) => _postureGroup.InterruptToStand(ctx);

        /// <summary>回到最近踩地点；没记下过返回 false（调用方自行处理），且不清空记录，连续掉落仍回同一点</summary>
        public bool RespawnToLastGrounded()
        {
            if (!_hasGroundedPos) return false;

            SpawnTo(_lastGroundedPos);
            return true;
        }

        public void TakeFallDamage() => TakeDamage(new Damage(FallDamage, DamageSource.Fall));

        private void RememberGround(Vector2 pos)
        {
            _lastGroundedPos = pos;
            _hasGroundedPos = true;
        }

        public void Revive()
        {
            ResetHealth();
            Conditions.ClearHurt();
            _polarity.Clear();
            Announce();
            AnnouncePolarity();
        }

        public override bool TakeDamage(in Damage d)
        {
            if (d.Source == DamageSource.Bullet) return false;
            if (!base.TakeDamage(in d)) return false;

            Announce();

            if (d.Source == DamageSource.Contact) EventBus<HitStopRequested>.Publish(new(_config.contactHitStopTime));

            return true;
        }
    }
}
