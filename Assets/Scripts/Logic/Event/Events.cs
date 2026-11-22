

using Refraction.Logic.Actor;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Logic.Events
{
    #region 请求
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

    public readonly struct RequestHitStop
    {
        public readonly float seconds;

        public RequestHitStop(float seconds)
        {
            this.seconds = seconds;
        }
    }

    public enum CameraShakeMode
    {
        Directional = 0,

        Omni = 1,
    }

    public readonly struct RequestCameraShake
    {
        public readonly Vector2 impulse;

        public readonly float duration;

        public readonly CameraShakeMode mode;

        public RequestCameraShake(Vector2 impulse, float duration, CameraShakeMode mode = CameraShakeMode.Directional)
        {
            this.impulse = impulse;
            this.duration = duration;
            this.mode = mode;
        }
    }

    public readonly struct RequestWorldReverse
    {
    }
    #endregion

    #region 事实
    public readonly struct GameStateChanged
    {
        public readonly GameState state;
        public readonly bool inputAllowed;
        public readonly bool sceneBusy;
        public readonly bool paused;

        public GameStateChanged(GameState state, bool inputAllowed, bool sceneBusy, bool paused)
        {
            this.state = state;
            this.inputAllowed = inputAllowed;
            this.sceneBusy = sceneBusy;
            this.paused = paused;
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

    public readonly struct AfterimageLinkHit
    {
        public readonly Vector2 Pos;

        public readonly float Damage;

        public AfterimageLinkHit(Vector2 pos, float damage)
        {
            Pos = pos;
            Damage = damage;
        }
    }

    #endregion
}
