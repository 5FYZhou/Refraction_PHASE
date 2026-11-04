using System;
using Refraction.Foundation;
using Refraction.Logic.Level;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.Presentation.Level
{
    /// <summary>
    /// 关卡场景自述：从场景名读出关卡 id，并提供出生点与门的位置。
    /// 场景名必须是 Level_正整数，否则视为关卡配置错误。
    /// </summary>
    public class LevelReader : MonoBehaviour
    {
        [Tooltip("玩家进入关卡时的出生点。")]
        [SerializeField]
        private Transform spawnT;

        [Tooltip("关卡出口的门。Awake 时会在这个 Transform 上挂 Door 组件。")]
        [SerializeField]
        private Transform doorT;

        private int _id;
        private bool _resolved;

        /// <summary>
        /// 当前关卡 id，场景名非法时为 0。
        /// 首次访问才解析：GameRoot 在 Awake 里就要用 Id，
        /// 而两个 Awake 的先后顺序 Unity 不保证，靠 Awake 赋值会读到 0。
        /// </summary>
        public int Id
        {
            get
            {
                if (!_resolved)
                {
                    _id = ResolveId();
                    _resolved = true;
                }
                return _id;
            }
        }

        /// <summary>玩家出生点。</summary>
        public Transform SpawnPoint => spawnT;

        /// <summary>关卡出口的门。</summary>
        public Transform DoorPoint => doorT;

        private void Awake()
        {
            Require.Assigned(spawnT, nameof(LevelReader), nameof(spawnT));
            Require.Assigned(doorT, nameof(LevelReader), nameof(doorT));

            string sceneName = SceneManager.GetActiveScene().name;

            if (!SceneName.CheckLevelName(sceneName))
            {
                _id = 0;
                _resolved = true;
                throw new InvalidOperationException(
                    $"[LevelReader] 关卡场景名 \"{sceneName}\" 不合法，应命名为 {SceneName.GetLevelName(1)}、{SceneName.GetLevelName(2)}……");
            }

            _id = ResolveId();
            _resolved = true;

            InitDoor();
        }

        private static int ResolveId()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return SceneName.TryGetLevelId(sceneName, out int id) ? id : 0;
        }

        private void InitDoor()
        {
            doorT.AddComponent<Door>();
        }
    }
}
