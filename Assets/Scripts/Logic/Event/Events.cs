

using UnityEngine;

namespace Refraction.Logic.Events
{
    public readonly struct RequestNextDoor
    {
        public readonly int nextLevel;
        public readonly int nextDoor;

        public RequestNextDoor(int nextLevel, int nextDoor)
        {
            this.nextLevel = nextLevel;
            this.nextDoor = nextDoor;
        }
    }

    public readonly struct RequestPlayerPosChange
    {
        public readonly Vector2 Pos;

        public RequestPlayerPosChange(Vector2 pos) { this.Pos = pos; }
    }
}
