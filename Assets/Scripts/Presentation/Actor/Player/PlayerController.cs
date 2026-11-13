using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Presentation.Combat;
using Refraction.Presentation.Level;
using Refraction.Presentation.UI;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    public sealed class PlayerController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IRenderTicked
    {
        private const float BlinkHz = 5f;
        private const float BlinkMinAlpha = 0.3f;

        public int Order => SceneOrder.Player;

        [SerializeField]
        private float _spawnY = -10f;

        [Tooltip("玩家参数资产。应与 PlayerMotor 上引用的是同一份配置。")]
        [SerializeField]
        private PlayerConfig config;

        [Tooltip("驱动刚体的执行器。逻辑层的移动/跳跃/冲刺指令最终都落到这里。")]
        [SerializeField]
        private MoveMotor moveMotor;


        [Tooltip("存档点锚点：场景里的 Spawn 对象，玩家站在它的碰撞体范围内按 W 存档。留空表示本关没有存档点。")]
        [SerializeField]
        private Transform spawnT;

        private GameRoot _root;
        private bool _attached = false;
        private bool _warnedNoGrounded;
        private InputProvider _inputProvider;
        private InputBuffer _buffer;
        private PlayerLogic _logic;
        private IAfterimageSpawner _afterimageSpawner;
        private Spawn _checkpoint;
        private IBulletLauncher _launcher;
        private BlackAttackView _blackAttack;

        // View 与脚底参考点：都按名字找（Player/View、View/Bottom），不在 Inspector 里连线
        private Transform view;
        private Transform bottom;

        // View 的原始姿态与底边：缩放要以脚底为锚点，必须知道它站立时站在哪儿
        private Vector3 _viewBasePos;
        private Vector3 _viewBaseScale;
        private float _viewBottomOffset;
        private Vector2 _curViewScale;
        private bool _warnedNoView;

        private SpriteRenderer _viewRenderer;
        private Color _viewBaseColor = Color.white;
        private Sprite _viewBaseSprite;
        private float _lastChargeProgress = -1f;
        private bool _lastCharged;
        private float _blinkElapsed;

        private static readonly Color ChargeYellow = new(1f, 0.92f, 0.3f, 1f);
        private static readonly Color ChargeOrange = new(1f, 0.5f, 0.08f, 1f);

        private void Awake()
        {
            // 必须放 Awake：GameRoot 可能在 Start 前就调 Attach/Tick，那帧 view 还没解析，会先报一次“找不到 View”
            CacheView();
        }
        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }
        private void OnDestroy()
        {
            _logic?.Dispose();

            // 用 Start 里抓住的引用：销毁期再问 GameRoot.Instance 可能当场造一个新的出来
            if (_root != null) _root.UnregisterSceneRoot(this);
        }
        private void OnEnable()
        {
            EventBus<RequestPlayerPosChange>.Subscribe(OnRequestPlayerPosChange);
        }
        private void OnDisable()
        {
            EventBus<RequestPlayerPosChange>.Unsubscribe(OnRequestPlayerPosChange);
        }

        /// <summary>场景根装配：接上输入源，再按上一场景那扇门指定的落点摆好；GameRoot 在注册后的下一个驱动帧调用，必须幂等</summary>
        public void Attach()
        {
            if (_attached) return;

            _root ??= GameRoot.Instance;
            if (_root == null || _root.Input == null)
            {
                Debug.LogError("[PlayerController] GameRoot 还没装配好输入源，无法接入。");
                return;
            }

            Require.Assigned(moveMotor, nameof(PlayerController), nameof(moveMotor));
            Require.Assigned(config, nameof(PlayerController), nameof(config));

            _inputProvider = _root.Input;
            _buffer = new(
                Mathf.Max(config.inputBufferTime, config.dashBufferTime),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );

            _blackAttack ??= new BlackAttackView(transform, config, () => moveMotor.FaceSign, () => _logic?.OnHitLanded());

            _logic = new(moveMotor, config, _buffer, _launcher, _blackAttack);

            if (_afterimageSpawner != null) _logic.BindAfterimageSpawner(_afterimageSpawner);

            var hurtBox = transform.GetComponentInChildren<HurtBox>();
            if (hurtBox != null) hurtBox.SetIAttackable(_logic);

            _checkpoint = spawnT != null ? spawnT.GetComponent<Spawn>() : null;
            if (spawnT != null && _checkpoint == null)
            {
                Debug.LogWarning($"[PlayerController] 存档点 {spawnT.name} 上没挂 Spawn 脚本，按 W 不会存档。");
            }

            _attached = true;

            // 跨关回档的落点优先于门；首关两者都没有，保持场景里摆的位置
            if (_root.TryConsumeRespawnPoint(out Vector2 respawn))
            {
                _logic.SpawnTo(respawn);
            }
            else if (_root.TryConsumeEntryPoint(out DoorEntry entry, out MovementStateTag enterTag, out int enterFace))
            {
                SpawnAtEntry(in entry, enterTag, enterFace);
            }
        }

        public MovementStateTag CurrentMovementTag =>
            _attached && _logic != null ? _logic.CurrentTag : MovementStateTag.None;

        /// -1 朝左，1 朝右；跨场景后新玩家还没有朝向
        public int CurrentFace => _attached && moveMotor != null ? moveMotor.FaceSign : 1;

        public void Announce() => _logic?.Announce();

        public void AnnouncePolarity() => _logic?.AnnouncePolarity();

        public void BindAfterimageSpawner(IAfterimageSpawner spawner)
        {
            _afterimageSpawner = spawner;
            _logic?.BindAfterimageSpawner(spawner);
        }

        public void BindBulletLauncher(IBulletLauncher launcher)
        {
            _launcher = launcher;
            _logic?.BindBulletLauncher(launcher);
        }

        public Transform View => view;

        public PlayerConfig Config => config;

        public IHitLanded HitSink => _logic;

        private void SpawnAtEntry(in DoorEntry entry, MovementStateTag enterTag, int enterFace)
        {
            Vector2 pivot = new(entry.Point.x, entry.Point.y - moveMotor.FeetOffsetY);

            _logic.SpawnTo(pivot);
            _logic.BeginTransit(in entry, enterTag, enterFace);
        }

        public void FixedTick(float deltaTime)
        {
            if (!_attached) return;

            InputSnapshot snapshot = _inputProvider.ConsumeSnapshot();
            // 世界信息由 PlayerMotor 的物理探测给出，本层只负责组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            GameTime gameTime = new(Time.fixedTime, Time.fixedDeltaTime);
            LogicContext context = new(snapshot, worldInfo, gameTime);

            _logic.FixedTick(context);

            EventBus<PlayerDebugSnapshot>.Publish(new(
                context.WorldInfo.FrameStartVelocity,
                moveMotor.LedgerVelocity,
                _logic.HurtVelocity,
                context.WorldInfo.Grounded,
                _logic.ConditionTag,
                _logic.CurrentTag,
                _logic.PostureTag,
                _logic.AttackTag,
                _logic.Polarity,
                _logic.MaxPolarity));

            // 掉血到 0：回存档点，连存档记录都没有才退回原地满血
            if (!_logic.IsAlive)
            {
                HandleDeath();
                return;
            }

            // 掉出世界：扣坠落伤害并回上次落脚点，扣到没血就走死亡分支
            if (context.WorldInfo.Pos.y < _spawnY)
            {
                if (!_logic.HasGroundedPos)
                {
                    WarnNoGroundedPoint();
                }
                else
                {
                    _logic.TakeFallDamage();

                    if (!_logic.IsAlive) HandleDeath();
                    else _logic.RespawnToLastGrounded();
                }
            }

            if (context.InputSnapshot.SavePressed) TrySaveCheckpoint();
        }

        private void HandleDeath()
        {
            if (_root.TryGetLocalCheckpoint(out Vector2 checkpoint))
            {
                _logic.Revive();
                _logic.SpawnTo(checkpoint);
                return;
            }

            if (_root.TryStartRespawnInCheckpointLevel()) return;

            _logic.Revive();
            if (!_logic.RespawnToLastGrounded()) WarnNoGroundedPoint();
        }

        private void TrySaveCheckpoint()
        {
            if (_checkpoint == null || !_checkpoint.HasPlayer) return;

            Vector2 pivot = _checkpoint.Point;
            pivot.y -= moveMotor.FeetOffsetY;

            if (_root.SaveCheckpoint(pivot)) _checkpoint.Flash();
        }

        public void RenderTick(float deltaTime)
        {
            if (!_attached) return;

            _logic.Tick(new());
            if (_curViewScale != _logic.CurViewScale)
            {
                _curViewScale = _logic.CurViewScale;
                SetViewScale(_curViewScale);
            }

            ApplyChargeVisual(_logic.ChargeProgress, _logic.IsCharged);
            ApplyBlinkVisual(deltaTime);
        }

        private void ApplyBlinkVisual(float deltaTime)
        {
            float alpha = _viewBaseColor.a;

            if (_logic.IsInvulnerable)
            {
                _blinkElapsed += deltaTime;
                if (Mathf.Repeat(_blinkElapsed * BlinkHz, 1f) < 0.5f) alpha = BlinkMinAlpha * _viewBaseColor.a;
            }
            else
            {
                _blinkElapsed = 0f;
            }

            if (_viewRenderer == null) return;

            Color color = _viewRenderer.color;
            if (Mathf.Approximately(color.a, alpha)) return;

            color.a = alpha;
            _viewRenderer.color = color;
        }

        private void ApplyChargeVisual(float progress, bool charged)
        {
            if (Mathf.Approximately(progress, _lastChargeProgress) && charged == _lastCharged) return;

            _lastChargeProgress = progress;
            _lastCharged = charged;

            if (_viewRenderer == null) return;

            if (charged)
            {
                _viewRenderer.color = ChargeOrange;
                _viewRenderer.sprite =  _viewBaseSprite;
                return;
            }

            if (progress <= 0f)
            {
                _viewRenderer.color = _viewBaseColor;
                _viewRenderer.sprite = _viewBaseSprite;
                return;
            }

            _viewRenderer.color = Color.Lerp(_viewBaseColor, ChargeYellow, progress);
            _viewRenderer.sprite = ChargeSprite;
        }

        private Sprite ChargeSprite => _viewBaseSprite;

        private void WarnNoGroundedPoint()
        {
            if (_warnedNoGrounded) return;

            _warnedNoGrounded = true;
            Debug.LogWarning("[PlayerController] 掉出世界，但还没有任何落脚点记录（出生后一次都没踩过地），无处可回。");
        }

        public void OnRequestPlayerPosChange(RequestPlayerPosChange evt)
        {
            // 还没 Attach 就被点名传送（切场景时事件可能早到），忽略而不是空引用
            if (!_attached) return;

            SpawnAtEntry(in evt.Entry, evt.EnterTag, evt.EnterFace);
        }

        #region View
        /// <summary>应用逻辑层给的 View 缩放：枢轴在身体中心，只改 scale 脚会离地（蹲下抬起 0.65），所以同时下移 (1 − scale.y) × 脚底到枢轴的距离</summary>
        public void SetViewScale(Vector2 scale)
        {
            if (view == null)
            {
                WarnNoView();
                return;
            }

            view.localScale = new Vector3(
                _viewBaseScale.x * scale.x,
                _viewBaseScale.y * scale.y,
                _viewBaseScale.z);

            Vector3 pos = _viewBasePos;
            pos.y = _viewBasePos.y + _viewBottomOffset * (1f - scale.y);
            view.localPosition = pos;
        }

        /// <summary>层级里约定好的两个名字：Player/View 与 View/Bottom（脚底空物体）；PlayerMotor 找的也是这两个名字，两边必须一致</summary>
        private const string ViewChildName = "View";
        private const string BottomChildName = "Bottom";

        /// <summary>记下 View 站立时的本地姿态与脚底相对枢轴的高度，蹲下缩放围着脚底做</summary>
        /// <remarks>Bottom 是 View 的直接子节点，必须 view.Find 而不是 transform.Find——后者只扫 Player 的一级子物体，找不到就以身体中心为锚点（脚浮起来）；基准取 localPosition / localScale，存世界坐标会在关卡根节点不在原点时把人整体甩出去</remarks>
        private void CacheView()
        {
            view = transform.Find(ViewChildName);
            if (view == null)
            {
                WarnNoView();
                return;
            }

            bottom = view.Find(BottomChildName);

            _viewBasePos = view.localPosition;
            _viewBaseScale = view.localScale;

            _viewRenderer = view.GetComponent<SpriteRenderer>();
            if (_viewRenderer != null)
            {
                _viewBaseColor = _viewRenderer.color;
                _viewBaseSprite = _viewRenderer.sprite;
            }

            // y 压成 0 就再也换不回来了，宁可不做缩放
            if (Mathf.Abs(_viewBaseScale.y) < 1e-4f)
            {
                _viewBottomOffset = 0f;
                Debug.LogWarning($"[PlayerController] {ViewChildName} 的基础 y 缩放是 0，下蹲的身高变化不会生效。");
                return;
            }

            if (bottom == null)
            {
                _viewBottomOffset = 0f;
                Debug.LogWarning($"[PlayerController] 找不到 {ViewChildName}/{BottomChildName}，" +
                                 "下蹲会以 View 枢轴（身体中心）为锚点缩放，脚会浮起来。");
                return;
            }

            // 底边的本地 y 乘 View 基础缩放，就是脚底到枢轴的本地距离（正常为负）
            _viewBottomOffset = bottom.localPosition.y * _viewBaseScale.y;

            // 初始就当"已经应用过站姿"：第一帧逻辑层给的也是 1，不必重复写一次 Transform
            _curViewScale = Vector2.one;
        }

        private void WarnNoView()
        {
            if (_warnedNoView) return;

            _warnedNoView = true;
            Debug.LogWarning($"[PlayerController] 层级里找不到 {ViewChildName} 子物体：下蹲不会反映到画面上" +
                             "（碰撞体与移速仍会正常生效）。");
        }
        #endregion

    }
}
