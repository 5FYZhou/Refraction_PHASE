using Refraction.Generated;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Refraction.Logic.Input
{
    public class InputProvider
    {
        private const string KeyPathPrefix = "<Keyboard>/";

        private InputSys _input = new();

        private Vector2 _move;
        private bool _jump;
        private bool _dash;
        private bool _jumpHeld;
        private bool _crouchHeld;
        private bool _attack;
        private bool _attackHeld;
        private bool _charge;
        private bool _reverse;
        private bool _save;
        private bool _upHeld;

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
            _jump |= _input.Player.Jump.WasPressedThisFrame();
            _dash |= _input.Player.Dash.WasPressedThisFrame();
            _attack |= _input.Player.Attack.WasPressedThisFrame();
            _charge |= _input.Player.Charge.WasPressedThisFrame();
            _reverse |= _input.Player.Reverse.WasPressedThisFrame();

            bool upHeld = _move.y > 0.5f;
            _save |= upHeld && !_upHeld;
            _upHeld = upHeld;

            _jumpHeld = _input.Player.Jump.IsPressed();
            _crouchHeld = _input.Player.Crouch.IsPressed();
            _attackHeld = _input.Player.Attack.IsPressed();
        }

        /// <summary>取走即清零，否则反复消费</summary>
        public InputSnapshot ConsumeSnapshot()
        {
            InputSnapshot snapshot = new(_move, _jump, _dash, _jumpHeld, _crouchHeld, _attack, _charge, _save, _reverse, _attackHeld);

            _jump = false;
            _dash = false;
            _attack = false;
            _charge = false;
            _reverse = false;
            _save = false;

            return snapshot;
        }

        public string GetKey(PlayerKeyTag tag)
        {
            InputAction action = ActionOf(tag);
            if (action == null) return null;

            string part = PartOf(tag);
            ReadOnlyArray<InputBinding> bindings = action.bindings;

            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (!IsKeyboard(binding) || !MatchesPart(binding, part)) continue;

                string path = binding.effectivePath;

                return string.IsNullOrEmpty(path) ? null : path.Substring(path.LastIndexOf('/') + 1);
            }

            return null;
        }

        /// <summary>空键名=空路径，不触发</summary>
        public void SetKey(PlayerKeyTag tag, string keyName)
        {
            InputAction action = ActionOf(tag);
            if (action == null) return;

            string part = PartOf(tag);
            string path = string.IsNullOrEmpty(keyName) ? string.Empty : KeyPathPrefix + keyName;

            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();

            ReadOnlyArray<InputBinding> bindings = action.bindings;

            for (int i = 0; i < bindings.Count; i++)
            {
                if (!IsKeyboard(bindings[i]) || !MatchesPart(bindings[i], part)) continue;

                action.ApplyBindingOverride(i, path);
            }

            if (wasEnabled) action.Enable();
        }

        private InputAction ActionOf(PlayerKeyTag tag)
        {
            switch (tag)
            {
                case PlayerKeyTag.MoveUp:
                case PlayerKeyTag.MoveDown:
                case PlayerKeyTag.MoveLeft:
                case PlayerKeyTag.MoveRight:
                    return _input.Player.Move;
                case PlayerKeyTag.Jump:
                    return _input.Player.Jump;
                case PlayerKeyTag.Dash:
                    return _input.Player.Dash;
                case PlayerKeyTag.Attack:
                    return _input.Player.Attack;
                case PlayerKeyTag.Crouch:
                    return _input.Player.Crouch;
                case PlayerKeyTag.Charge:
                    return _input.Player.Charge;
                case PlayerKeyTag.Reverse:
                    return _input.Player.Reverse;
                default:
                    return null;
            }
        }

        private static string PartOf(PlayerKeyTag tag)
        {
            switch (tag)
            {
                case PlayerKeyTag.MoveUp: return "up";
                case PlayerKeyTag.MoveDown: return "down";
                case PlayerKeyTag.MoveLeft: return "left";
                case PlayerKeyTag.MoveRight: return "right";
                default: return null;
            }
        }

        private static bool IsKeyboard(in InputBinding binding)
        {
            return binding.path != null && binding.path.StartsWith(KeyPathPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesPart(in InputBinding binding, string part)
        {
            if (part == null) return !binding.isPartOfComposite;

            return binding.isPartOfComposite && string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase);
        }
    }
}
