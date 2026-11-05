using System;
using System.Collections.Generic;
using Refraction.Logic.Level;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.Presentation.Level
{
    /// <summary>
    /// 本关的门索引：扫描场景里的所有 Door，按门 id 建表。
    /// 属于关卡场景自己的一份，随场景销毁，不会像常驻对象那样缓存上一个场景的门。
    ///
    /// 一扇门由「关卡 id + 门 id」唯一确定：本读取器绑定在当前场景解析出的关卡 id 上，
    /// 调 <see cref="TryGetPos"/> 前由调用方（LevelManager）核对关卡，避免拿错关的读取器查同号门。
    /// 索引在 Attach 或第一次查询时建立一次，之后不再刷新——运行期增删门不支持。
    /// </summary>
    public sealed class DoorReader : MonoBehaviour, ISceneRoot, IDoorReader
    {
        /// <summary>玩家刚被放到门的落点时，吞掉门的进入触发这么久，避免两扇门来回弹。</summary>
        private const float SuppressSeconds = 0.2f;

        private readonly Dictionary<int, Door> _doors = new();

        private GameRoot _root;
        private bool _indexed;
        private int _levelId;
        private bool _resolved;

        public int Order => SceneOrder.World;

        /// <summary>从本场景名解析出的关卡 id。非 Level_正整数 命名时为 0，本关不会索引任何门。</summary>
        public int LevelId 
        {
            get
            {
                if (!_resolved)
                {
                    _levelId = ResolveId();
                    _resolved = true;
                }
                return _levelId;
            }
        }

        /// <summary>已索引到的门数量，排查配置用。</summary>
        public int Count => _doors.Count;

        private void Awake()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            if (!SceneName.CheckLevelName(sceneName))
            {
                _levelId = 0;
                _resolved = true;
                throw new InvalidOperationException(
                    $"[DoorReader] 关卡场景名 \"{sceneName}\" 不合法，应命名为 {SceneName.GetLevelName(1)}、{SceneName.GetLevelName(2)}……");
            }

            _levelId = ResolveId();
            _resolved = true;
        }

        private void Start()
        {
            // 抓住实例而不是每次去问 GameRoot.Instance：销毁期再问会当场造一个新的出来
            _root = GameRoot.Instance;
            _root?.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            // 场景卸载时 GameRoot 已经清过表，这里只是兜底
            if (_root != null) _root.UnregisterSceneRoot(this);
        }
        private static int ResolveId()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return SceneName.TryGetLevelId(sceneName, out int id) ? id : 0;
        }

        public void Attach()
        {
            EnsureIndexed();
        }

        public bool TryGetPos(int doorId, out Vector2 position)
        {
            position = default;

            EnsureIndexed();

            if (!_doors.TryGetValue(doorId, out Door door)) return false;

            // 门在运行期被销毁（比如被别的逻辑回收）：当作没有，别把已销毁对象的位置当落点
            if (door == null)
            {
                _doors.Remove(doorId);
                return false;
            }

            position = door.SpawnPoint;

            // 落点压在门的触发器里，不吞掉接下来那次进入，这扇门会立刻把玩家再送走一次
            door.SuppressEnterFor(SuppressSeconds);
            return true;
        }

        private void EnsureIndexed()
        {
            if (_indexed) return;
            _indexed = true;

            if (_levelId <= 0)
            {
                Debug.LogError($"[DoorReader] 场景 \"{gameObject.scene.name}\" 不符合 Level_正整数 的命名，本关的门不会被索引。");
                return;
            }

            // 门可能挂在任意父节点的子物体里，所以从每个场景根往下找，不限定直接子物体；
            // includeInactive：停用的门同样可能是别的门的落点
            GameObject[] roots = gameObject.scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                Door[] doors = roots[i].GetComponentsInChildren<Door>(true);

                for (int j = 0; j < doors.Length; j++)
                {
                    Register(doors[j]);
                }
            }
        }

        private void Register(Door door)
        {
            if (door == null) return;

            int id = door.SelfId;

            // 0 与负数就是没配。这种门可以被人踩，但没法作为别人的落点——不拦下来会静默落到默认位置
            if (id <= 0)
            {
                Debug.LogError($"[DoorReader] 关卡 {_levelId} 里 {Describe(door)} 的 SelfId = {id} 非法（必须是正整数），已跳过。");
                return;
            }

            if (_doors.TryGetValue(id, out Door exists))
            {
                Debug.LogError(
                    $"[DoorReader] 关卡 {_levelId} 里门 id {id} 重复：{Describe(exists)} 与 {Describe(door)}。" +
                    "关卡 id + 门 id 必须唯一确定一扇门，这里只有先扫描到的生效。");
                return;
            }

            _doors.Add(id, door);
        }

        private static string Describe(Door door)
        {
            return $"{door.name}";
        }
    }
}
