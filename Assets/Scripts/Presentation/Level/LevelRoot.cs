using System.Collections.Generic;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>关卡根：只在关卡场景存在，持有本关门索引、关卡推进与换色登记；没有它就不是关卡（开始场景）</summary>
    /// <remarks>Order 取 Entry + 1：必须晚于 SceneFlowConfig 写极性，否则 LevelManager 拿到的初值是上一关的</remarks>
    [DisallowMultipleComponent]
    public sealed class LevelRoot : MonoBehaviour, ISceneRoot
    {
        /// <summary>本场景的关卡根，随场景销毁置空；关卡对象靠它登记换色，开始场景为 null</summary>
        public static LevelRoot Current { get; private set; }

        [Tooltip("本关门的读取器，留空则在自身与子物体里找一次；找不到就无法解析进门口，同关传送也失效。")]
        [SerializeField]
        private DoorReader doorReader;

        private readonly List<IReverse> _pendingReverse = new();

        private GameRoot _root;
        private LevelManager _levelMgr;

        public int Order => SceneOrder.Entry + 1;

        public int LevelId => doorReader != null ? doorReader.LevelId : 0;

        public DoorReader DoorReader => doorReader;

        private void Awake()
        {
            if (doorReader == null) doorReader = GetComponentInChildren<DoorReader>(true);

            if (doorReader == null)
            {
                Debug.LogError($"[LevelRoot] 场景 \"{gameObject.scene.name}\" 的关卡根上没有 DoorReader，进门口解析与同关传送都不会生效。");
            }

            if (Current != null && Current != this)
            {
                Debug.LogError($"[LevelRoot] 已存在关卡根 {Current.name}，同时加载两个关卡会把门与推进弄串。");
            }

            Current = this;
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
            _pendingReverse.Clear();

            if (_root == null) return;

            _root.UnregisterSceneRoot(this);
            _root.UnbindLevelRoot(this);
        }

        public void Attach()
        {
            _root ??= GameRoot.Instance;

            if (_levelMgr != null) return;

            // 索引要在 GameRoot 解析进门落点之前建好：门是场景对象，晚一帧就可能已被别的脚本动过
            doorReader?.Initialize();

            _levelMgr = new LevelManager(LevelId, doorReader, _root != null ? _root.World : WorldPolarity.White, _root);
            _levelMgr.Init();

            for (int i = 0; i < _pendingReverse.Count; i++) _levelMgr.RegisterReverse(_pendingReverse[i]);

            _pendingReverse.Clear();

            _root?.BindLevelRoot(this);
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
    }
}
