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

    /// <summary> 当前帧开始时的世界快照 </summary>
    public readonly struct WorldInfo
    {
        /// <summary>是否站在地面。</summary>
        public readonly bool Grounded;

        /// <summary>是否贴墙。</summary>
        public readonly bool TouchingWall;

        /// <summary>墙面所在方向：-1 在左，1 在右。</summary>
        public readonly int WallSide;

        /// <summary> 当前帧开始时的速度  </summary>
        public readonly Vector2 FrameStartVelocity;
        public readonly int Face;
        public readonly Vector2 Pos;

        public WorldInfo(bool grounded, bool touchingWall, int wallSide, Vector2 velocity, int face, Vector2 pos)
        {
            Grounded = grounded;
            TouchingWall = touchingWall;
            WallSide = wallSide;
            FrameStartVelocity = velocity;
            Face = face;
            Pos = pos;
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