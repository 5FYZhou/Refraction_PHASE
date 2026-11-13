using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人组合根：只做装配与转发</summary>
    public sealed class EnemyController : MonoBehaviour, ISceneRoot, IPhysicsTicked
    {
        public int Order => SceneOrder.World;

        [Tooltip("敌人参数资产。巡逻、索敌与接触伤害的数值都在这里，运行时只读不改。")]
        [SerializeField]
        private EnemyConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private EnemyMotor moveMotor;

        [Tooltip("是否启用 AI。关掉即站桩靶子：巡逻/追踪都不推进、原地不动，受击表现、命中反馈与子弹回收照常，但伤害值恒为 0（打不死）。")]
        [SerializeField]
        private bool OpenAI = true;

        private const string DetectChildName = "DetectCollider";
        private const string PatrolChildName = "PatrolCollider";

        private GameRoot _root;
        private bool _attached;
        private EnemyLogic _logic;

        private PlayerController _target;
        private Transform _targetTransform;

        private Collider2D _patrolZone;
        private bool _hasPatrolBounds;
        private float _patrolMinX;
        private float _patrolMaxX;

        private void Awake()
        {
            CacheDetectZone();
            CachePatrolZone();
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
        
        public void Attach()
        {
            if (_attached) return;

            Require.Assigned(moveMotor, nameof(EnemyController), nameof(moveMotor));
            Require.Assigned(config, nameof(EnemyController), nameof(config));

            _logic = new EnemyLogic(moveMotor, config, OpenAI);

            var hurtBox = transform.GetComponentInChildren<HurtBox>();
            if (hurtBox != null) hurtBox.SetIAttackable(_logic);
            var attackBox = transform.GetComponentInChildren<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(_logic);

            _attached = true;
        }

        public bool AIEnabled => OpenAI;

        public bool HasTarget => _target != null;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        public void FixedTick(float deltaTime)
        {
            if (!_attached) return;

            // 世界信息由 EnemyMotor 探测，本层只组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, Sense(), SamplePatrolArea());

            // 血被打空：这具身体没有死亡演出，直接消失（销毁排在帧末，本帧后续逻辑不受影响）
            if (!_logic.IsAlive) Destroy(gameObject);
        }

        /// <summary>目标被销毁（换场景、死亡）后引用会变成伪 null，这里一律收成"看不见"，逻辑层不必再判空</summary>
        private EnemyTargetInfo Sense()
        {
            if (_target == null) return default;

            return new EnemyTargetInfo(true, _targetTransform.position);
        }

        /// <summary>探测触发器转发过来的进入：玩家的碰撞体可能挂在子物体上，往父级找（与 Door 同一套判法）</summary>
        public void OnDetectEnter(Collider2D other)
        {
            if (other == null) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _target = player;
            _targetTransform = player.transform;
        }

        public void OnDetectExit(Collider2D other)
        {
            if (_target == null || other == null) return;
            if (!other.transform.IsChildOf(_target.transform)) return;

            _target = null;
            _targetTransform = null;
        }

        /// <summary>触发器消息只送到碰撞体所在的 GameObject，就地补一个转发器</summary>
        private void CacheDetectZone()
        {
            Transform detect = transform.Find(DetectChildName);
            if (detect == null)
            {
                Debug.LogWarning($"[EnemyController] 找不到 {DetectChildName} 子物体：这只会一直巡逻，看不到玩家。");
                return;
            }

            Collider2D zone = detect.GetComponent<Collider2D>();
            if (zone == null || !zone.isTrigger)
            {
                Debug.LogWarning($"[EnemyController] {DetectChildName} 上没有触发器碰撞体（Is Trigger 未勾选）：侦测不到玩家。");
                return;
            }

            EnemyDetectZone forwarder = detect.GetComponent<EnemyDetectZone>();
            if (forwarder == null) forwarder = detect.gameObject.AddComponent<EnemyDetectZone>();

            forwarder.Init(this);
        }

        /// <summary>边界在装配时快照一次：子物体触发器跟着走，实时读等于当前位置</summary>
        private void CachePatrolZone()
        {
            Transform zone = transform.Find(PatrolChildName);
            if (zone == null) return;

            _patrolZone = zone.GetComponent<Collider2D>();
            if (_patrolZone == null)
            {
                Debug.LogWarning($"[EnemyController] {PatrolChildName} 上没有 Collider2D：巡逻范围按整平台处理。");
                return;
            }

            Bounds bounds = _patrolZone.bounds;
            _patrolMinX = bounds.min.x;
            _patrolMaxX = bounds.max.x;
            _hasPatrolBounds = _patrolMaxX - _patrolMinX > 0.01f;
        }

        private EnemyPatrolArea SamplePatrolArea()
        {
            if (_patrolZone == null || !_hasPatrolBounds) return default;

            Vector3 self = transform.position;
            Bounds bounds = _patrolZone.bounds;

            bool inside = self.x >= bounds.min.x && self.x <= bounds.max.x
                          && self.y >= bounds.min.y && self.y <= bounds.max.y;

            return inside ? new EnemyPatrolArea(true, _patrolMinX, _patrolMaxX) : default;
        }
    }
}
