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
            _jump = _input.Player.Jump.WasPressedThisFrame();
            _dash = _input.Player.Dash.WasPressedThisFrame();
        }

        public InputSnapshot ConsumeSnapshot()
        {
            return new InputSnapshot(_move, _jump, _dash);
        }
    }
}
