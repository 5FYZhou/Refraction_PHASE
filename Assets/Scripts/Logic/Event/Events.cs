

using Refraction.Logic.Actor;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Logic.Events
{
    public readonly struct RequestNextDoor
    {
        public readonly int nextLevel;
        public readonly int nextDoor;

        public readonly MovementStateTag enterTag;

        public readonly int enterFace;

        public RequestNextDoor(int nextLevel, int nextDoor, MovementStateTag enterTag, int enterFace)
        {
            this.nextLevel = nextLevel;
            this.nextDoor = nextDoor;
            this.enterTag = enterTag;
            this.enterFace = enterFace;
        }
    }

    public readonly struct PlayerHealthChanged
    {
        public readonly int cur;

        public readonly int max;

        public PlayerHealthChanged(int cur, int max)
        {
            this.cur = cur;
            this.max = max;
        }
    }

    public readonly struct WorldReversed
    {
    }

    public readonly struct WorldPolarityChanged
    {
        public readonly WorldPolarity World;

        public WorldPolarityChanged(WorldPolarity world)
        {
            World = world;
        }
    }

    public readonly struct PlayerPolarityChanged
    {
        public readonly int cur;

        public readonly int max;

        public PlayerPolarityChanged(int cur, int max)
        {
            this.cur = cur;
            this.max = max;
        }
    }

    public readonly struct RequestPlayerPosChange
    {
        public readonly DoorEntry Entry;

        public readonly MovementStateTag EnterTag;

        public readonly int EnterFace;

        public RequestPlayerPosChange(DoorEntry entry, MovementStateTag enterTag, int enterFace)
        {
            this.Entry = entry;
            this.EnterTag = enterTag;
            this.EnterFace = enterFace;
        }
    }
}
