using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    public sealed class PlayerController : MonoBehaviour, ISceneRoot
    {
        public int Order => SceneOrder.Player;

        [SerializeField]
        private float _spawnY = -10f;

        private GameRoot _root;
        private bool _attached = false;
        private bool _warnedNoGrounded;
        private InputProvider _inputProvider;
        private InputBuffer _buffer;
        private PlayerLogic _logic;

        [Tooltip("驱动刚体的执行器。逻辑层的移动/跳跃/冲刺指令最终都落到这里。")]
        [SerializeField]
        private MoveMotor moveMotor;

        [Tooltip("玩家参数资产。应与 MoveMotor 上引用的是同一份配置。")]
        [SerializeField]
        private PlayerConfig config;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
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

        /// <summary>
        /// 场景根装配：接上场景根持有的输入源，再按上一场景那扇门指定的落点摆好。
        /// GameRoot 在注册后的下一个驱动帧调用，必须幂等。
        /// </summary>
        public void Attach()
        {
            if (_attached) return;

            _root ??= GameRoot.Instance;
            if (_root == null || _root.Input == null)
            {
                Debug.LogError("[PlayerController] GameRoot 还没装配好输入源，无法接入。");
                return;
            }

            Attach(_root.Input);

            // 从门里进来就落到那扇门的位置；首关没有落点，保持场景里摆的位置
            if (_root.TryConsumeEntryPoint(out Vector2 entry))
            {
                _logic.SpawnTo(entry);
            }
        }

        private void Attach(InputProvider inputProvider)
        {
            Require.Assigned(moveMotor, nameof(PlayerController), nameof(moveMotor));
            Require.Assigned(config, nameof(PlayerController), nameof(config));

            _inputProvider = inputProvider;
            _buffer = new (
                Mathf.Max(config.inputBufferTime, config.dashBufferTime),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );
            _logic = new(moveMotor, config, _buffer);
            _attached = true;
        }

        public void FixedTick()
        {
            if (!_attached) return;

            InputSnapshot snapshot = _inputProvider.ConsumeSnapshot();
            // 世界信息由 MoveMotor 的物理探测给出，本层只负责组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            GameTime gameTime = new(Time.fixedTime, Time.fixedDeltaTime);
            LogicContext context = new(snapshot, worldInfo, gameTime);

            _buffer.Push(in snapshot, Time.fixedTime);

            _logic.FixedTick(context);

            // 掉出世界：回到逻辑层记下的最后一次落脚点
            if (context.WorldInfo.Pos.y < _spawnY && !_logic.RespawnToLastGrounded())
            {
                WarnNoGroundedPoint();
            }
        }

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

            _logic.SpawnTo(evt.Pos);
        }
    }
}
