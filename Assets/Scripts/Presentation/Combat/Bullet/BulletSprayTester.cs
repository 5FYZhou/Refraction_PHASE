using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>临时测试用：按固定间隔朝固定方向连发敌籍子弹，给弹反与受击判定喂靶子，验完即删</summary>
    /// <remarks>发出去的子弹 PlayerOwned = false，和敌人开火同籍，弹反才会把它当敌弹打回去</remarks>
    public sealed class BulletSprayTester : MonoBehaviour, ISceneRoot, IPhysicsTicked
    {
        private const string DefaultBulletPath = "Prefabs/Bullet";
        private const int PoolSize = 8;
        private const float MinInterval = 0.05f;

        public int Order => SceneOrder.World;

        [Tooltip("要发射的子弹预制体。留空则退到 Resources/Prefabs/Bullet。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("两发之间的间隔（秒）。填 0 或负数按 0.05 处理，免得一帧里连发到卡死。")]
        [SerializeField]
        private float interval = 1f;

        [Tooltip("发射方向（世界坐标，会被归一化成单位向量）。零向量按向左处理。")]
        [SerializeField]
        private Vector2 direction = Vector2.left;

        [Tooltip("出膛速度（单位/秒）。")]
        [SerializeField]
        private float speed = 12f;

        [Tooltip("这一发的伤害值。只影响被打中的受击盒，被弹反后会换成玩家自己的数值。")]
        [SerializeField]
        private float damage;

        private GameRoot _root;
        private BulletLogic _bullets;
        private BulletField _field;
        private float _wait;

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_field != null) Destroy(_field.gameObject);

            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public void Attach()
        {
            if (_bullets != null) return;

            GameObject prefab = bulletPrefab != null ? bulletPrefab : Resources.Load<GameObject>(DefaultBulletPath);

            if (prefab == null)
            {
                Debug.LogError("[BulletSprayTester] 没给子弹预制体，Resources 里也没找到，本次不会开火。");
                return;
            }

            GameObject host = new(nameof(BulletField));

            _field = host.AddComponent<BulletField>();
            _field.Init(prefab, transform, null, PoolSize, 1);

            _bullets = new BulletLogic(_field, false);
        }

        public void FixedTick(float deltaTime)
        {
            // 子弹系统要每物理帧推一次姿势，缺了它会停在出膛位置
            _bullets?.Tick(deltaTime);

            if (_bullets == null) return;

            _wait -= deltaTime;

            while (_wait <= 0f)
            {
                Fire();
                _wait += Mathf.Max(MinInterval, interval);
            }
        }

        private void Fire()
        {
            _bullets.Launch(new BulletSpawn(transform.position, Heading, speed, damage));
        }

        private Vector2 Heading
        {
            get
            {
                Vector2 dir = direction;

                if (dir.sqrMagnitude < 1e-6f) return Vector2.left;

                return dir.normalized;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
            Gizmos.DrawRay(transform.position, Heading * 2f);
        }
    }
}
