using Refraction.Foundation;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>一种子弹预制体一个池：键就是预制体本身，外观不同绝不串池</summary>
    /// <remarks>取出的实例只改运行时归属，外观、碰撞体与母池都不变，所以弹反不必换实例</remarks>
    public sealed class BulletPool
    {
        private const int MaxSize = 32;
        private const int PrewarmCount = 4;

        private readonly GameObject _prefab;
        private readonly Transform _root;
        private readonly Pool<Bullet> _pool;

        public BulletPool(GameObject prefab, Transform root)
        {
            _prefab = prefab;
            _root = root;

            _pool = new Pool<Bullet>(
                Create,
                onGet: Take,
                onRelease: Put,
                name: prefab.name,
                maxSize: MaxSize,
                overflowPolicy: PoolOverflowPolicy.CreateOrDrop);

            _pool.Prewarm(PrewarmCount);
        }

        public GameObject Prefab => _prefab;

        /// <summary>借一发并写入这一发的归属：池空了返回 null，由调用方把逻辑账目丢掉</summary>
        public Bullet Take(BulletField field, BulletLogic system, in BulletRequest request, Transform owner,
            IHitLanded hitSink)
        {
            if (!_pool.TryGet(out Bullet bullet)) return null;

            bullet.Initialize(this, field, system, in request, owner, hitSink);

            return bullet;
        }

        public void Release(Bullet bullet) => _pool.Release(bullet);

        public void Dispose() => _pool.Dispose();

        private Bullet Create()
        {
            GameObject instance = Object.Instantiate(_prefab, _root);

            // 预制体根节点带着自己的局部坐标，池里复用前先落回容器原点，出膛位置一律由 Apply 决定
            instance.transform.localPosition = Vector3.zero;

            Bullet bullet = instance.GetComponent<Bullet>();

            if (bullet == null) bullet = instance.AddComponent<Bullet>();

            instance.SetActive(false);

            return bullet;
        }

        private static void Take(Bullet bullet) => bullet.gameObject.SetActive(true);

        private static void Put(Bullet bullet)
        {
            bullet.Reset();
            bullet.gameObject.SetActive(false);
        }
    }
}
