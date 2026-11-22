using Refraction.Logic;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>入口状态：Attach 时推一次</summary>
    /// <remarks>Order 取 SceneOrder.Entry：其余场景根的 Attach 都排在它之后。</remarks>
    [DisallowMultipleComponent]
    public sealed class SceneFlowConfig : MonoBehaviour, ISceneRoot
    {
        [Tooltip("本场景的入口状态：游戏场景填 Running，开始场景填 Menu")]
        [SerializeField] private GameState entryState = GameState.Running;

        private GameRoot _root;

        public int Order => SceneOrder.Entry;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root?.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public void Attach()
        {
            _root ??= GameRoot.Instance;
            _root?.Game.SetSceneTransitionTargetState(entryState);

            Debug.Log($"[Flow] 场景声明入口状态 {entryState}（{gameObject.scene.name}）");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entryState == GameState.Paused)
            {
                Debug.LogWarning(
                    "[Flow] SceneFlowConfig.entryState = Paused：进关即 timeScale=0，只能靠 Resume 解开。" +
                    "游戏关卡场景请填 Running。",
                    this);
            }

            if (entryState == GameState.None
                || entryState == GameState.Exit
                || entryState == GameState.SceneTransition)
            {
                Debug.LogWarning(
                    $"[Flow] SceneFlowConfig.entryState = {entryState} 非法：只允许 Menu / CG / Running / Paused。",
                    this);
            }
        }
#endif
    }
}
