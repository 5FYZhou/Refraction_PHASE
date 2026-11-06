using Refraction.Data;
using Refraction.Logic.Input;
using Refraction.Logic.Move;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public sealed class PlayerLogic : ActorLogic
    {
        private MoveGroup _moveGroup;
        private PostureGroup _postureGroup;
        private IMoveMotor _motor;

        private Vector2 _lastGroundedPos;
        private bool _hasGroundedPos;
        private Vector2 _curScale = Vector2.one;

        public PlayerLogic(IMoveMotor motor, PlayerConfig config, InputBuffer buffer) : base(motor)
        {
            _postureGroup = new(config, motor);
            _moveGroup = new(motor, config, buffer, _postureGroup);
            _motor = motor;
        }

        /// <summary>还没踩过地（出生就在空中）时为 false</summary>
        public bool HasGroundedPos => _hasGroundedPos;

        /// <summary>最近一次踩地点，即"可以站住的地方"</summary>
        public Vector2 LastGroundedPos => _lastGroundedPos;

        /// <summary>逻辑层每帧算好，PlayerController 拿去应用</summary>
        public Vector2 CurViewScale { get => _curScale; }

        protected override void OnFixedTick(in LogicContext ctx)
        {
            // 记在移动之前：帧末位置可能已被推出平台边缘
            if (ctx.WorldInfo.Grounded) RememberGround(ctx.WorldInfo.Pos);

            // 姿态先于移动：反过来会晚一帧，按下蹲键的那帧还能跳
            _postureGroup.Tick(ctx);

            _moveGroup.Tick(ctx);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            _curScale = _postureGroup.ViewScale;
        }

        public void SpawnTo(Vector2 pos)
        {
            _motor.SetPosition(pos);
            _motor.SetVelocityX(0);
            _motor.SetVelocityY(0);
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
    }
}
