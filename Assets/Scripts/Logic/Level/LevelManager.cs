using Refraction.Logic.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.Logic.Level
{
    /// <summary>
    /// 关卡推进：收到 <see cref="RequestNextLevel"/> 后加载下一关场景。
    /// 下一关按 SceneName 的命名约定推导，场景不存在则报错、留在本关。
    /// </summary>
    public class LevelManager
    {
        private readonly int _curId;
        private bool _loading;

        public LevelManager(int id)
        {
            _curId = id;

            if (id <= 0)
            {
                Debug.LogError($"[LevelManager] 当前关卡 id 非法（{id}），无法推进到下一关。检查关卡场景名是否为 Level_正整数。");
            }
        }

        public void Init()
        {
            EventBus<RequestNextLevel>.Subscribe(OnRequestNextLevel);
        }

        public void Dispose()
        {
            EventBus<RequestNextLevel>.Unsubscribe(OnRequestNextLevel);
            _loading = false;
        }

        private void OnRequestNextLevel(RequestNextLevel evt)
        {
            // 门的触发器按碰撞体对触发，玩家身上有多个碰撞体时会连续进来多次，
            // 这里挡住重复请求，避免同一帧多次 LoadScene
            if (_loading) return;
            if (_curId <= 0) return;

            string nextName = SceneName.GetLevelName(_curId + 1);

            if (!Application.CanStreamedLevelBeLoaded(nextName))
            {
                Debug.LogError(
                    $"[LevelManager] 没有下一关场景 \"{nextName}\"（当前关卡 id = {_curId}）。" +
                    "确认场景已按 Level_正整数 命名并加入 Build Settings。");
                return;
            }

            _loading = true;
            SceneManager.LoadScene(nextName);
        }
    }
}
