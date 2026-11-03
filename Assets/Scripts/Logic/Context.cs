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
        public InputSnapshot(Vector2 move, bool jump, bool dash, bool jumpHeld)
        {
            this.Move = move;
            this.Jump = jump;
            this.Dash = dash;
            this.JumpHeld = jumpHeld;
        }
    }

    public readonly struct WorldInfo
    {
        /// <summary>是否站在地面。</summary>
        public readonly bool Grounded;

        /// <summary>是否贴墙。</summary>
        public readonly bool TouchingWall;

        /// <summary>墙面所在方向：-1 在左，1 在右。</summary>
        public readonly int WallSide;

        public readonly Vector2 Velocity;
        public readonly int Face;

        public WorldInfo(bool grounded, bool touchingWall, int wallSide, Vector2 velocity, int face)
        {
            Grounded = grounded;
            TouchingWall = touchingWall;
            WallSide = wallSide;
            Velocity = velocity;
            Face = face;
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