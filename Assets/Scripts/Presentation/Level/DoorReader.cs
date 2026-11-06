using System;
using System.Collections.Generic;
using Refraction.Logic.Level;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.Presentation.Level
{
    /// <summary>本关门索引：扫场景内所有 Door 按门 id 建表，随场景销毁，不会串到上一个场景</summary>
    /// <remarks>索引只在 Attach 或首次查询时建一次，运行期增删门不支持；查门前调用方要先核对关卡 id，否则会拿错关的表查同号门</remarks>
    public sealed class DoorReader : MonoBehaviour, ISceneRoot, IDoorReader
    {
        /// <summary>落点放下后吞掉门的进入触发这么久（秒），否则两扇门会来回弹</summary>
        private const float SuppressSeconds = 0.2f;

        private readonly Dictionary<int, Door> _doors = new();

        private GameRoot _root;
        private bool _indexed;
        private int _levelId;
        private bool _resolved;

        public int Order => SceneOrder.World;

        /// <summary>本场景解析出的关卡 id；命名不合 Level_正整数 时为 0，本关不索引任何门</summary>
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
            // 抓住实例：销毁期再问 GameRoot.Instance 会当场造一个新的出来
            _root = GameRoot.Instance;
            _root?.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            // GameRoot 在场景卸载时已清过表，这里只是兜底
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

            // 门被销毁：清掉这条记录并当作没有，别拿已销毁对象的位置当落点
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

            // 从每个场景根往下扫（门可挂在任意层），含未激活：停用的门也可能是别人的落点
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

            // 0 与负数是没配：这种门能踩，但不能当别人的落点，不拦会静默落到默认位置
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
