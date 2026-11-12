using Refraction.Foundation;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>子弹的外壳来源：按预制体造视图并交代开火者与命中回调，逻辑层只说要几发，不碰 Instantiate</summary>
    public sealed class BulletField : MonoBehaviour, IBulletViewFactory
    {
        private const int PoolSize = 32;
        private const int PrewarmCount = 8;

        private GameObject _prefab;
        private Transform _owner;
        private IHitLanded _hitSink;
        private Pool<Bullet> _pool;

        public void Init(GameObject prefab, Transform owner, IHitLanded hitSink)
        {
            _prefab = prefab;
            _owner = owner;
            _hitSink = hitSink;

            if (_prefab == null)
            {
                Debug.LogError("[BulletField] 子弹预制体为空，本次开火不会生成子弹。");
                return;
            }

            _pool = new Pool<Bullet>(
                CreateBullet,
                onGet: Take,
                onRelease: Put,
                name: nameof(Bullet),
                maxSize: PoolSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop);

            _pool.Prewarm(PrewarmCount);
        }

        public IBulletView Create(BulletLogic system, int id)
        {
            if (_pool == null || !_pool.TryGet(out Bullet bullet)) return null;

            bullet.Bind(this, system, id, _owner, _hitSink);

            return bullet;
        }

        /// <summary>子弹结算完或撞毁后回到这里：池要它，不是 Destroy</summary>
        public void Recycle(Bullet bullet) => _pool?.Release(bullet);

        private Bullet CreateBullet()
        {
            GameObject go = Instantiate(_prefab, transform);

            Bullet bullet = go.GetComponent<Bullet>();
            if (bullet == null) bullet = go.AddComponent<Bullet>();

            go.SetActive(false);

            return bullet;
        }

        private static void Take(Bullet bullet) => bullet.gameObject.SetActive(true);

        private static void Put(Bullet bullet)
        {
            bullet.Reset();
            bullet.gameObject.SetActive(false);
        }

        private void OnDestroy() => _pool?.Dispose();
    }
}
