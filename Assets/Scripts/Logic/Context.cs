using Refraction.Logic.Input;
using UnityEngine;

namespace Refraction.Logic
{
    public readonly struct InputSnapshot
    {
        public readonly Vector2 Move;
        public readonly bool Jump;
        public readonly bool Dash;
        public readonly bool JumpHeld;

        /// <summary>下蹲键是否按住：姿态是持续状态而不是按下沿</summary>
        public readonly bool CrouchHeld;

        public readonly bool Attack;

        public readonly bool ChargePressed;

        public InputSnapshot(Vector2 move, bool jump, bool dash, bool jumpHeld, bool crouchHeld, bool attack, bool chargePressed)
        {
            this.Move = move;
            this.Jump = jump;
            this.Dash = dash;
            this.JumpHeld = jumpHeld;
            this.CrouchHeld = crouchHeld;
            this.Attack = attack;
            this.ChargePressed = chargePressed;
        }
    }

    /// <summary>当前帧开始时的世界快照</summary>
    public readonly struct WorldInfo
    {
        public readonly bool Grounded;

        public readonly bool TouchingWall;

        /// <summary>墙面所在方向：-1 在左，1 在右</summary>
        public readonly int WallSide;

        public readonly Vector2 FrameStartVelocity;
        public readonly int Face;
        public readonly Vector2 Pos;

        public readonly bool LedgeAhead;

        public WorldInfo(bool grounded, bool touchingWall, int wallSide, Vector2 velocity, int face, Vector2 pos, bool ledgeAhead = false)
        {
            Grounded = grounded;
            TouchingWall = touchingWall;
            WallSide = wallSide;
            FrameStartVelocity = velocity;
            Face = face;
            Pos = pos;
            LedgeAhead = ledgeAhead;
        }
    }

    public readonly struct GameTime
    {
        public readonly float now;
        public readonly float deltaTime;
        public GameTime(float n, float d)
        {
            now = n;
            deltaTime = d;
        }
    }

    public readonly struct LogicContext
    {
        public readonly InputSnapshot InputSnapshot;
        public readonly WorldInfo WorldInfo;
        public readonly GameTime GameTime;

        public LogicContext(InputSnapshot inputSnapshot, WorldInfo worldInfo, GameTime gameTime)
        {
            InputSnapshot = inputSnapshot;
            WorldInfo = worldInfo;
            GameTime = gameTime;
        }
    }
}