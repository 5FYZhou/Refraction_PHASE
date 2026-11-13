using System;
using System.Collections.Generic;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Logic.Service;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Level;
using Refraction.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.Presentation
{
    /// <summary>场景根：全场唯一常驻对象，持有输入源、关卡推进器与跨场景数据</summary>
    public class GameRoot : Singleton<GameRoot>, ISceneTransfer
    {
        private readonly List<ISceneRoot> _sceneRoots = new();
        private readonly List<ISceneRoot> _pendingAttach = new();
        private readonly List<IService> _services = new();
        private readonly SceneContext _scenectx = new();

        private InputProvider _inputProvider;
        private DoorReader _doorReader;
        private UIMgr _uiMgr;
        private ResMgr _resMgr;
        private PauseService _pauseService;
        private HitStopService _hitStopService;

        private bool _warnedNoLevel;

        /// <summary>给场景根用的输入源。玩家在自己的 Attach 里从这里取。</summary>
        public InputProvider Input => _inputProvider;

        public IDoorReader DoorReader => _doorReader;

        public WorldPolarity World => _scenectx.World;

        public int LevelId => _doorReader != null ? _doorReader.LevelId : 0;

        public UIMgr UIMgr { get => _uiMgr ??= new UIMgr(); }
        public ResMgr ResMgr { get => _resMgr ??= new ResMgr(); }

        public PauseService Pause => _pauseService;

        public HitStopService HitStop => _hitStopService;

        public IReadOnlyList<ISceneRoot> SceneRoots => _sceneRoots;

        protected override void Awake()
        {
            base.Awake();

            // 重复实例要到帧末才销毁，Awake 仍会走完：必须原地退场，否则它会替真正的根把新场景再装配一遍，再带着刚建好的东西一起被销毁
            if (Instance != this)
            {
                enabled = false;
                return;
            }

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
            // 手动 Disable 再 Enable 时输入源已被释放，必须靠这个空判断挡住
            if (_inputProvider == null) return;

            _inputProvider.Init();
            EventBus<WorldReversed>.Subscribe(OnWorldReversed);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EventBus<WorldReversed>.Unsubscribe(OnWorldReversed);

            DisposeServices();

            _inputProvider?.Dispose();
            _inputProvider = null;

            _sceneRoots.Clear();
            _pendingAttach.Clear();
            _scenectx.Reset();
            _doorReader = null;
        }

        private void OnWorldReversed(WorldReversed evt)
        {
            _scenectx.ReverseWorld();
            EventBus<WorldPolarityChanged>.Publish(new(_scenectx.World));
        }

        /// <summary>首关装配：玩家的接入走场景根注册表，这里不碰</summary>
        private void Assemble()
        {
            _inputProvider = new();

            _pauseService = new PauseService(new Adapters.GameTimer());
            RegisterService(_pauseService);

            _hitStopService = new HitStopService(_pauseService);
            RegisterService(_hitStopService);

            // 不能用 gameObject.scene：base.Awake 已把自己 DontDestroyOnLoad，取到的是 DontDestroyOnLoad 场景
            _doorReader = FindOrCreateDoorReader(SceneManager.GetActiveScene());
        }

        public void RegisterService(IService service)
        {
            if (service == null || _services.Contains(service)) return;

            _services.Add(service);
            service.Init();
        }

        private void TickServices(float deltaTime, float unscaledDeltaTime)
        {
            for (int i = 0; i < _services.Count; i++)
            {
                _services[i].Tick(deltaTime, unscaledDeltaTime);
            }
        }

        private void DisposeServices()
        {
            for (int i = _services.Count - 1; i >= 0; i--)
            {
                try
                {
                    _services[i].Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            _services.Clear();
        }

        /// <summary>注册场景根（重复是 no-op）。真正的 Attach 推迟到下一个被驱动的帧，按 Order 顺序执行。</summary>
        public void RegisterSceneRoot(ISceneRoot root)
        {
            if (root == null || _sceneRoots.Contains(root)) return;

            InsertByOrder(_sceneRoots, root);
            InsertByOrder(_pendingAttach, root);
        }

        public void UnregisterSceneRoot(ISceneRoot root)
        {
            if (root == null) return;

            _sceneRoots.Remove(root);
            _pendingAttach.Remove(root);
        }

        /// <summary>场景配置覆盖世界极性，要在其他场景根装配前调用，否则地面会先按上一关的颜色刷一次</summary>
        public void ApplyWorldPolarity(WorldPolarity world)
        {
            _scenectx.SetWorld(world);
            EventBus<WorldPolarityChanged>.Publish(new(world));
        }

        /// <summary>跨关：记下这次加载要落到的门，LevelManager 在 LoadScene 之前通过它交棒</summary>
        public void RequestLevelLoad(int doorId, MovementStateTag enterTag, int enterFace)
        {
            _scenectx.SetNextDoor(doorId, enterTag, enterFace);
        }

        /// <summary>取走本关的进门口数据、进门时的移动状态与朝向，一次性。没有落点返回 false，调用方保持场景摆放</summary>
        public bool TryConsumeEntryPoint(out DoorEntry entry, out MovementStateTag enterTag, out int enterFace)
        {
            return _scenectx.TryConsumeEntry(out entry, out enterTag, out enterFace);
        }

        public bool SaveCheckpoint(Vector2 pivot)
        {
            if (_scenectx.SaveCheckpoint(LevelId, pivot)) return true;

            if (!_warnedNoLevel)
            {
                _warnedNoLevel = true;
                Debug.LogError($"[GameRoot] 场景 \"{SceneManager.GetActiveScene().name}\" 不是 Level_正整数，存档点在这里不生效。");
            }

            return false;
        }

        public bool TryGetLocalCheckpoint(out Vector2 pivot)
        {
            pivot = default;

            if (!_scenectx.TryGetCheckpoint(out Vector2 point, out int level)) return false;
            if (level != LevelId) return false;

            pivot = point;
            return true;
        }

        public bool TryStartRespawnInCheckpointLevel()
        {
            if (!_scenectx.TryGetCheckpoint(out Vector2 point, out int level)) return false;

            string sceneName = SceneName.GetLevelName(level);

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameRoot] 存档关 \"{sceneName}\" 不在 Build Settings 里，无法回到存档点。");
                return false;
            }

            _scenectx.BeginRespawn(point);

            try
            {
                SceneManager.LoadScene(sceneName);
                return true;
            }
            catch (Exception ex)
            {
                _scenectx.CancelRespawn();
                Debug.LogError($"[GameRoot] 加载存档关 \"{sceneName}\" 失败：{ex.Message}");
                return false;
            }
        }

        public bool TryConsumeRespawnPoint(out Vector2 pivot)
        {
            return _scenectx.TryConsumeRespawn(out pivot);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 只接单场景切换。附加加载（菜单、UI 叠层）不该动玩家和地面。
            if (mode != LoadSceneMode.Single) return;

            Rebind(scene);
        }

        /// <summary>切场景后重挂世界侧。只在这个时机安全：新场景的 Awake / OnEnable 已跑完，场景根还没在 Start 里注册</summary>
        private void Rebind(Scene scene)
        {
            if (_inputProvider == null) return;

            int pendingDoorId = _scenectx.TakeNextDoor(out MovementStateTag pendingTag, out int pendingFace);

            // 门索引同样每关一份：它持有的是上一个场景的 Door，切场景后必然失效
            _doorReader = FindOrCreateDoorReader(scene);

            // 落点先算好：玩家下个驱动帧才 Attach，那会儿再回来找门就晚了，也容易撞上“门已经销毁”
            _scenectx.ClearEntry();
            if (pendingDoorId > 0) ResolveEntryPoint(scene, pendingDoorId, pendingTag, pendingFace);
        }

        private void ResolveEntryPoint(Scene scene, int doorId, MovementStateTag enterTag, int enterFace)
        {
            if (_doorReader == null)
            {
                Debug.LogError($"[GameRoot] 没有门读取器，无法解析场景 \"{scene.name}\" 里 id = {doorId} 的门。");
                return;
            }

            // 落点压在门的触发器范围内：DoorReader 命中时会顺手屏蔽该门，不然下一个物理步又会发一次切场景请求，两个场景来回弹
            if (!_doorReader.TryGetEntry(doorId, out DoorEntry entry))
            {
                Debug.LogError($"[GameRoot] 关卡 {_doorReader.LevelId} 里找不到 id = {doorId} 的门，玩家保持场景里的摆放位置。");
                return;
            }

            _scenectx.SetEntry(in entry, enterTag, enterFace);
        }

        /// <summary>取本关的门读取器：场景里没挂就运行时补一个，门索引是装配期必需品，缺了玩家落不到指定的门</summary>
        private static DoorReader FindOrCreateDoorReader(Scene scene)
        {
            DoorReader reader = FindInScene<DoorReader>(scene);
            if (reader != null) return reader;

            GameObject go = new("[DoorReader]");
            SceneManager.MoveGameObjectToScene(go, scene);
            reader = go.AddComponent<DoorReader>();

            Debug.LogWarning($"[GameRoot] 场景 \"{scene.name}\" 里没挂 DoorReader，已在运行时补一个，建议在场景里显式挂上。");
            return reader;
        }

        /// <summary>只在指定场景里找组件。不能用 FindObjectOfType：常驻对象和别的场景也会被命中。</summary>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>把这一帧新注册的场景根按 Order 顺序 Attach 掉。Attach 里再注册的，下一帧继续。</summary>
        private void AttachPending()
        {
            if (_pendingAttach.Count == 0) return;

            ISceneRoot[] batch = _pendingAttach.ToArray();
            _pendingAttach.Clear();

            for (int i = 0; i < batch.Length; i++)
            {
                // 注册后、Attach 前就被销毁的根要跳过：接口之间的 == null 不走 Unity 的伪 null 重载，必须先转成 Object
                UnityEngine.Object target = batch[i] as UnityEngine.Object;
                if (target == null) continue;

                // 单个根装配失败不该拖累后面的根：坏掉一个不该让全场景停摆
                try
                {
                    batch[i].Attach();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                // 玩家装配完立刻广播血量与极性：界面这一帧就有初值
                if (batch[i] is PlayerController player)
                {
                    player.Announce();
                    player.AnnouncePolarity();
                }
            }
        }

        private static int CompareSceneRoots(ISceneRoot a, ISceneRoot b)
        {
            return a.Order.CompareTo(b.Order);
        }

        /// <summary>按 Order 升序插入：List.Sort 不稳定，同 Order 的根会被换位，这里用插入排序保住注册顺序</summary>
        private static void InsertByOrder(List<ISceneRoot> list, ISceneRoot root)
        {
            int i = list.Count;
            while (i > 0 && CompareSceneRoots(list[i - 1], root) > 0) i--;

            list.Insert(i, root);
        }

        private void Update()
        {
            AttachPending();
            _inputProvider?.Tick();
            TickServices(Time.deltaTime, Time.unscaledDeltaTime);
            TickSceneRootsRender(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            // 先装配再驱动：场景根的第一帧就要能跑
            AttachPending();
            TickSceneRootsPhysics(Time.fixedDeltaTime);
        }

        private void TickSceneRootsRender(float deltaTime)
        {
            for (int i = 0; i < _sceneRoots.Count; i++)
            {
                if (_sceneRoots[i] is IRenderTicked ticked) ticked.RenderTick(deltaTime);
            }
        }

        private void TickSceneRootsPhysics(float deltaTime)
        {
            for (int i = 0; i < _sceneRoots.Count; i++)
            {
                if (_sceneRoots[i] is IPhysicsTicked ticked) ticked.FixedTick(deltaTime);
            }
        }
    }
}
