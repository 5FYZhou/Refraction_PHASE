using Refraction.Generated;
using System;
using UnityEngine;

namespace Refraction.Logic.Input
{
    public class InputProvider
    {
        private InputSys _input = new();

        private Vector2 _move;
        private bool _jump;
        private bool _dash;
        private bool _jumpHeld;

        public InputProvider() { }

        public void Init()
        {
            _input.Enable();
        }
        public void Dispose()
        {
            _input.Disable();
        }

        public void Tick()
        {
            _move = Vector2.ClampMagnitude(
                _input.Player.Move.ReadValue<Vector2>(),
                1f
            );
            // 瞬时输入累积，物理帧侧取走
            _jump |= _input.Player.Jump.WasPressedThisFrame();
            _dash |= _input.Player.Dash.WasPressedThisFrame();

            _jumpHeld = _input.Player.Jump.IsPressed();
        }

        /// <summary>物理帧取走一次按下沿；取走即清零，不能留着，否则会被反复消费</summary>
        public InputSnapshot ConsumeSnapshot()
        {
            InputSnapshot snapshot = new(_move, _jump, _dash, _jumpHeld);

            _jump = false;
            _dash = false;

            return snapshot;
        }
    }
}
