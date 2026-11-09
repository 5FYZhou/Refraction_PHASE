using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人组合根：把探测结果与世界采样喂给 EnemyLogic，巡逻/追踪/攻击的判定全在逻辑层</summary>
    public sealed class EnemyController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IAttack, IAttackable
    {
        public int Order => SceneOrder.World;

        [Tooltip("敌人参数资产。巡逻半径、索敌时长与三段攻击的数值都在这里，运行时只读不改。")]
        [SerializeField]
        private EnemyConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private EnemyMotor moveMotor;

        private const string DetectChildName = "DetectCollider";

        private GameRoot _root;
        private bool _attached;
        private EnemyLogic _logic;

        private PlayerController _target;
        private Transform _targetTransform;

        private void Awake()
        {
            CacheDetectZone();
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

            _logic = new EnemyLogic(moveMotor, config);

            var hurtBox = transform.GetComponentInChildren<HurtBox>();
            if (hurtBox != null) hurtBox.SetIAttackable(this);
            var attackBox = transform.GetComponentInChildren<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(this);

            _attached = true;
        }

        public void FixedTick(float deltaTime)
        {
            if (!_attached) return;

            // 世界信息由 EnemyMotor 的物理探测给出，本层只负责组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, Sense());
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

        /// <summary>触发器消息只保证送到碰撞体所在的那个 GameObject，碰撞体在子物体上，就地补一个转发器</summary>
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


        public Damage ProduceDamage()
        {
            return default;
        }

        public void TakeDamage(in Damage damage)
        {
            Debug.Log("EnemyDamage");
        }
    }
}
