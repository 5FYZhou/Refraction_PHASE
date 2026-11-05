using Refraction.Foundation;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Level;
using UnityEngine;

namespace Refraction.Presentation
{
    public class GameRoot : Singleton<GameRoot>
    {
        private bool _initialized = false;

        [Tooltip("地面初始化器。进关时统一给地面子物体补 BoxCollider2D 并设为 Ground 层。")]
        [SerializeField]
        private GroundIniter groundIniter;

        [Tooltip("当前关卡自述。提供关卡 id、出生点、出口门。")]
        [SerializeField]
        private LevelReader levelReader;

        [Tooltip("玩家控制器。接收输入，驱动逻辑层。")]
        [SerializeField]
        private PlayerController playerController;

        private InputProvider _inputProvider;
        private LevelManager _levelMgr;

        protected override void Awake()
        {
            base.Awake();
            // 装配缺失直接抛错。这里不吞异常，但先把自己关掉：
            // 否则 OnEnable / Update 会继续跑并每帧刷空引用，把真正的那条错误淹掉
            try
            {
                Assemble();
            }
            catch
            {
                enabled = false;
                throw;
            }
        }

        private void OnEnable()
        {
            _inputProvider.Init();
            _levelMgr.Init();
        }

        private void OnDisable()
        {
            _inputProvider.Dispose();
            _levelMgr.Dispose();
        }

        /// <summary>
        /// 组装
        /// </summary>
        private void Assemble()
        {
            Require.Assigned(groundIniter, nameof(GameRoot), nameof(groundIniter));
            Require.Assigned(playerController, nameof(GameRoot), nameof(playerController));

            groundIniter.Init();
            _inputProvider = new();
            playerController.Attach(_inputProvider);
            if(levelReader != null)
                _levelMgr = new LevelManager(levelReader.Id);
        }

        /// <summary>注册场景根（重复是 no-op），真正装配推迟到第一个被驱动的帧</summary>
        public void RegisterSceneRoot(ISceneRoot root)
        {
            if (root == null || _sceneRoots.Contains(root)) return;

            _sceneRoots.Add(root);
            _sceneRoots.Sort(CompareSceneRoots);

            if (!_pendingAttach.Contains(root)) _pendingAttach.Add(root);
        }

        public void UnregisterSceneRoot(ISceneRoot root)
        {
            if (root == null) return;

            _sceneRoots.Remove(root);
            _pendingAttach.Remove(root);
        }

        private void Update()
        {
            _inputProvider.Tick();
        }

        private void FixedUpdate()
        {
            playerController.FixedTick();
        }
    }
}
