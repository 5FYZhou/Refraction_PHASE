using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public sealed class PlayerLogic : ActorLogic
    {
        private MoveGroup _moveGroup;
        private PostureGroup _postureGroup;
        private AttackGroup _attackGroup;
        private TransitMotor _transit;
        private IMoveMotor _motor;
        private InputBuffer _buffer;

        private Vector2 _lastGroundedPos;
        private bool _hasGroundedPos;
        private Vector2 _curScale = Vector2.one;

        private DoorEntry _pendingEntry;
        private MovementStateTag _pendingEntryTag;
        private int _pendingEntryFace;
        private bool _hasPendingEntry;

        public PlayerLogic(IMoveMotor motor, PlayerConfig config, InputBuffer buffer, IBulletLauncher launcher) : base(motor, config)
        {
            _postureGroup = new(config, motor);
            _moveGroup = new(motor, config, buffer, _postureGroup);
            _attackGroup = new(config, motor, launcher);
            _transit = new(motor, config, _moveGroup);
            _motor = motor;
            _buffer = buffer;
        }

        /// <summary>还没踩过地（出生就在空中）时为 false</summary>
        public bool HasGroundedPos => _hasGroundedPos;

        /// <summary>最近一次踩地点，即"可以站住的地方"</summary>
        public Vector2 LastGroundedPos => _lastGroundedPos;

        /// <summary>逻辑层每帧算好，PlayerController 拿去应用</summary>
        public Vector2 CurViewScale { get => _curScale; }

        public MovementStateTag CurrentTag => _moveGroup.CurrentTag;

        public PostureStateTag PostureTag => _postureGroup.Current;

        public AttackStateTag AttackTag => _attackGroup.Current;

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

            _moveGroup.Tick(in tick);

            _attackGroup.Tick(in tick);

            _transit.AfterTick(in tick);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            _curScale = _postureGroup.ViewScale;
        }

        public void SpawnTo(Vector2 pos) => _motor.SpawnAt(pos, Vector2.zero);

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

        private void RememberGround(Vector2 pos)
        {
            _lastGroundedPos = pos;
            _hasGroundedPos = true;
        }

        public void Revive() => Conditions.Revive();

        public override void TakeDamage(in Damage damage)
        {
            if (damage.Source == DamageSource.Bullet) return;
            Conditions.TryApply(in damage);
        }
    }
}
