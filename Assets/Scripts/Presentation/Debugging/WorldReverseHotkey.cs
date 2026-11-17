#if UNITY_EDITOR
using Refraction.Logic.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Refraction.Presentation.Debugging
{
    /// <summary>编辑器专用：按 R 发一次世界反转请求，不进包、不占 InputSys 的按键表</summary>
    /// <remarks>整份文件包在 UNITY_EDITOR 里；宿主对象自己造、自己常驻，换关不用逐关挂</remarks>
    public sealed class WorldReverseHotkey : MonoBehaviour, ISceneRoot, IRenderTicked
    {
        private const string HostName = "[WorldReverseHotkey]";

        public int Order => SceneOrder.World;

        private GameRoot _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            if (FindObjectOfType<WorldReverseHotkey>() != null) return;

            GameObject go = new(HostName);
            DontDestroyOnLoad(go);
            go.AddComponent<WorldReverseHotkey>();
        }

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public void Attach() { }

        public void RenderTick(float deltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.rKey.wasPressedThisFrame) return;

            EventBus<RequestWorldReverse>.Publish(default);
        }
    }
}
#endif
