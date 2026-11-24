using System.Collections.Generic;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>关卡根：只在关卡场景存在，持有本关门索引、关卡推进与换色登记；没有它就不是关卡（开始场景）</summary>
    /// <remarks>Order 取 Entry + 1：必须晚于 SceneFlowConfig 写极性，否则 LevelManager 拿到的初值是上一关的；门索引靠 Door 自己注册，运行期增删门不支持</remarks>
    [DisallowMultipleComponent]
    public sealed class LevelRoot : MonoBehaviour, ISceneRoot, IDoorReader
    {
        /// <summary>落点放下后吞掉门的进入触发这么久（秒），否则两扇门会来回弹</summary>
        private const float SuppressSeconds = 0.2f;

        private static readonly List<Door> Pending = new();

        /// <summary>本场景的关卡根，随场景销毁置空；门与换色对象靠它登记，开始场景为 null</summary>
        public static LevelRoot Current { get; private set; }

        private readonly Dictionary<int, Door> _doors = new();
        private readonly List<IReverse> _pendingReverse = new();

        private GameRoot _root;
        private LevelManager _levelMgr;
        private int _levelId;

        public int Order => SceneOrder.Entry + 1;

        /// <summary>本场景解析出的关卡 id；命名不合 Level_正整数 时为 0，本关不索引任何门</summary>
        public int LevelId => _levelId;

        private void Awake()
        {
            _levelId = SceneName.TryGetLevelId(gameObject.scene.name, out int id) ? id : 0;

            if (_levelId <= 0)
            {
                Debug.LogError(
                    $"[LevelRoot] 场景 \"{gameObject.scene.name}\" 不符合命名约定，应命名为 {SceneName.GetLevelName(1)}、{SceneName.GetLevelName(2)}……" +
                    "本关的门不会被索引，也无法推进到下一关。");
            }

            if (Current != null && Current != this)
            {
                Debug.LogError($"[LevelRoot] 已存在关卡根 {Current.name}，同时加载两个关卡会把门与推进弄串。");
            }

            Current = this;

            // 门的 Awake 可能早于本组件（组件顺序不定），那会儿只能先排在静态表里
            for (int i = 0; i < Pending.Count; i++) AddDoor(Pending[i]);

            Pending.Clear();
        }

        private void Start()
        {
            _root = GameRoot.Instance;
            _root?.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;

            _levelMgr?.Dispose();
            _levelMgr = null;

            _doors.Clear();
            _pendingReverse.Clear();

            if (_root == null) return;

            _root.UnregisterSceneRoot(this);
            _root.UnbindLevelRoot(this);
        }

        public void Attach()
        {
            _root ??= GameRoot.Instance;

            if (_levelMgr != null) return;

            _levelMgr = new LevelManager(_levelId, this, _root != null ? _root.World : WorldPolarity.White, _root);
            _levelMgr.Init();

            for (int i = 0; i < _pendingReverse.Count; i++) _levelMgr.RegisterReverse(_pendingReverse[i]);

            _pendingReverse.Clear();

            _root?.BindLevelRoot(this);
        }

        /// <summary>门在 Awake 里登记自己；本根还没建时排进静态表，建好后由 Awake 统一收进来</summary>
        public static void RegisterDoor(Door door)
        {
            if (door == null) return;

            if (Current != null)
            {
                Current.AddDoor(door);
                return;
            }

            if (!Pending.Contains(door)) Pending.Add(door);
        }

        public static void UnregisterDoor(Door door)
        {
            if (door == null) return;

            Pending.Remove(door);
            Current?.RemoveDoor(door);
        }

        public bool TryGetEntry(int doorId, out DoorEntry entry)
        {
            entry = default;

            if (!_doors.TryGetValue(doorId, out Door door)) return false;

            // 门被销毁：清掉这条记录并当作没有，别拿已销毁对象的位置当落点
            if (door == null)
            {
                _doors.Remove(doorId);
                return false;
            }

            entry = door.ToEntry();

            // 落点压在门的触发器里，不吞掉接下来那次进入，这扇门会立刻把玩家再送走一次
            door.SuppressEnterFor(SuppressSeconds);
            return true;
        }

        /// <summary>换色对象先到、关卡后到是常态（场景对象 Start 早于本根的 Attach），所以这里替它排队</summary>
        public void RegisterReverse(IReverse reverse)
        {
            if (reverse == null) return;

            if (_levelMgr != null)
            {
                _levelMgr.RegisterReverse(reverse);
                return;
            }

            if (!_pendingReverse.Contains(reverse)) _pendingReverse.Add(reverse);
        }

        public void UnregisterReverse(IReverse reverse)
        {
            if (reverse == null) return;

            _pendingReverse.Remove(reverse);
            _levelMgr?.RemoveReverse(reverse);
        }

        private void AddDoor(Door door)
        {
            if (door == null || _levelId <= 0) return;

            int id = door.SelfId;

            // 0 与负数是没配：这种门能踩，但不能当别人的落点，不拦会静默落到默认位置
            if (id <= 0)
            {
                Debug.LogError($"[LevelRoot] 关卡 {_levelId} 里 {door.name} 的 SelfId = {id} 非法（必须是正整数），已跳过。");
                return;
            }

            if (_doors.TryGetValue(id, out Door exists))
            {
                Debug.LogError(
                    $"[LevelRoot] 关卡 {_levelId} 里门 id {id} 重复：{exists.name} 与 {door.name}。" +
                    "关卡 id + 门 id 必须唯一确定一扇门，这里只有先注册到的生效。");
                return;
            }

            _doors.Add(id, door);
        }

        private void RemoveDoor(Door door)
        {
            if (door == null) return;

            if (_doors.TryGetValue(door.SelfId, out Door exists) && exists == door) _doors.Remove(door.SelfId);
        }
    }
}
