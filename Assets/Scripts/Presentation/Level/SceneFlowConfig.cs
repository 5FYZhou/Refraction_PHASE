using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>关卡配置：场景里挂上就以这里的世界极性为准，覆盖上一关留下的值；没挂则沿用</summary>
    public sealed class SceneFlowConfig : MonoBehaviour, ISceneRoot
    {
        [SerializeField]
        [Tooltip("本关起始世界极性：覆盖跨关保留的值，决定地面配色与玩家攻击链")]
        private WorldPolarity worldPolarity = WorldPolarity.White;

        private GameRoot _root;
        private bool _applied;

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
            if (_applied) return;
            _applied = true;

            _root ??= GameRoot.Instance;
            _root?.ApplyWorldPolarity(worldPolarity);
        }
    }
}
