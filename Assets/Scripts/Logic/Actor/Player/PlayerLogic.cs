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

        /// <summary>是否已经记下过落脚点。还没踩过地（出生就在空中）时为 false。</summary>
        public bool HasGroundedPos => _hasGroundedPos;

        /// <summary>最近一次判定踩地时所在的位置，即"可以站住的地方"。</summary>
        public Vector2 LastGroundedPos => _lastGroundedPos;

        /// <summary>View 该用的本地缩放。逻辑层每帧算好，PlayerController 拿去应用。</summary>
        public Vector2 CurViewScale { get => _curScale; }

        protected override void OnFixedTick(in LogicContext ctx)
        {
            // 记在移动之前：这一帧判定踩地，说明帧首位置一定是可站立的，
            // 用它而不是帧末位置——帧末可能已经被推出平台边缘
            if (ctx.WorldInfo.Grounded) RememberGround(ctx.WorldInfo.Pos);

            // 姿态先推进：同帧里移动侧读到的禁令必须是这一帧的姿态，
            // 反过来顺序就会晚一帧（按下下蹲键的那帧还能跳）
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

        /// <summary>
        /// 打断当前的下蹲前摇：立刻回站立，移速乘数与身体/View 缩放瞬间恢复。
        /// 预留给受击、强制起身、被其他系统抢占姿态这类外部打断；不在前摇里调用无副作用。
        /// </summary>
        public bool InterruptCrouch(in LogicContext ctx) => _postureGroup.InterruptToStand(ctx);

        /// <summary>
        /// 掉出世界后回到最近一次踩地的位置。
        /// 一次落脚点都没记下时返回 false，调用方自行决定怎么处理。
        /// 记录不清空：连续掉落时每次都能回到同一个安全点。
        /// </summary>
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
