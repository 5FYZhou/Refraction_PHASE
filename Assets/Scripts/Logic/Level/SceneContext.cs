using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Logic.Level
{
    /// <summary>跨场景数据：世界黑白、玩家血量与极性值、下一扇门与进门口、存档点、待复活点，由常驻的 GameRoot 持有，切场景不丢</summary>
    /// <remarks>门落点、进门口、待复活点都是取一次就清空——残留值会把之后的重载丢到错的位置</remarks>
    public sealed class SceneContext
    {
        private readonly CheckpointBook _checkpoints = new();

        private int _nextDoorId;
        private MovementStateTag _enterTag;
        private int _enterFace;

        private DoorEntry _entry;
        private MovementStateTag _entryTag;
        private int _entryFace;
        private bool _hasEntry;

        private Vector2 _respawnPoint;
        private bool _hasRespawn;

        private int _health;
        private bool _hasHealth;

        private int _polarity;
        private bool _hasPolarity;

        /// <summary>世界极性：跨关保留，新场景的 LevelManager 从这里接住</summary>
        public WorldPolarity World { get; private set; }

        public bool IsBlackWorld => World == WorldPolarity.Black;

        public void ReverseWorld()
        {
            World = World == WorldPolarity.White ? WorldPolarity.Black : WorldPolarity.White;
        }

        public void SetWorld(WorldPolarity world)
        {
            World = world;
        }

        /// <summary>记下这次加载要落到的门，必须在 LoadScene 之前调用：发起方随旧场景一起销毁</summary>
        public void SetNextDoor(int doorId, MovementStateTag enterTag, int enterFace)
        {
            _nextDoorId = doorId;
            _enterTag = enterTag;
            _enterFace = enterFace;
        }

        public int TakeNextDoor(out MovementStateTag enterTag, out int enterFace)
        {
            enterTag = _enterTag;
            enterFace = _enterFace;

            int doorId = _nextDoorId;
            _nextDoorId = 0;
            _enterTag = MovementStateTag.None;
            _enterFace = 0;

            return doorId;
        }

        public void SetEntry(in DoorEntry entry, MovementStateTag enterTag, int enterFace)
        {
            _entry = entry;
            _entryTag = enterTag;
            _entryFace = enterFace;
            _hasEntry = true;
        }

        public void ClearEntry()
        {
            _hasEntry = false;
        }

        public bool TryConsumeEntry(out DoorEntry entry, out MovementStateTag enterTag, out int enterFace)
        {
            entry = _entry;
            enterTag = _entryTag;
            enterFace = _entryFace;

            if (!_hasEntry) return false;

            _hasEntry = false;
            return true;
        }

        public bool SaveCheckpoint(int levelId, Vector2 pivot)
        {
            return _checkpoints.Save(levelId, pivot);
        }

        /// <summary>跨关血量：受击、反转回满、复活都会覆盖它，上界钳到配表上限</summary>
        public void SetHealth(int cur, int max)
        {
            _health = Mathf.Clamp(cur, 0, Mathf.Max(1, max));
            _hasHealth = true;
        }

        public bool TryGetHealth(out int health)
        {
            health = _health;
            return _hasHealth;
        }

        /// <summary>跨关极性：加值、反转清空、复活都会覆盖它，上界钳到配表上限</summary>
        public void SetPolarity(int cur, int max)
        {
            _polarity = Mathf.Clamp(cur, 0, Mathf.Max(1, max));
            _hasPolarity = true;
        }

        public bool TryGetPolarity(out int polarity)
        {
            polarity = _polarity;
            return _hasPolarity;
        }

        public bool TryGetCheckpoint(out Vector2 pivot, out int levelId)
        {
            return _checkpoints.TryGetRespawn(out pivot, out levelId);
        }

        public void BeginRespawn(Vector2 pivot)
        {
            _respawnPoint = pivot;
            _hasRespawn = true;
        }

        public void CancelRespawn()
        {
            _hasRespawn = false;
        }

        public bool TryConsumeRespawn(out Vector2 pivot)
        {
            pivot = _respawnPoint;

            if (!_hasRespawn) return false;

            _hasRespawn = false;
            return true;
        }

        /// <summary>退出播放时清掉只对本轮有效的跨关数据；存档点保留，重进仍从上次存档继续</summary>
        public void Reset()
        {
            World = WorldPolarity.White;
            _nextDoorId = 0;
            _enterTag = MovementStateTag.None;
            _enterFace = 0;
            _hasEntry = false;
            _hasRespawn = false;
            _hasHealth = false;
            _hasPolarity = false;
        }
    }
}
