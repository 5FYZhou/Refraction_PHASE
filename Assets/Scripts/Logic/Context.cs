using Refraction.Logic.Input;
using UnityEngine;

namespace Refraction.Logic
{
    public readonly struct InputSnapshot
    {
        public readonly Vector2 Move;
        public readonly bool Jump;
        public readonly bool Dash;
        public InputSnapshot(Vector2 move, bool jump, bool dash)
        {
            this.Move = move;
            this.Jump = jump;
            this.Dash = dash;
        }
    }

    public readonly struct WorldInfo
    {
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public WorldInfo(Vector2 position, Vector2 velocity)
        {
            Position = position;
            Velocity = velocity;
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