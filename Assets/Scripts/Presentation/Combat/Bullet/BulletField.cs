using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>子弹的外壳来源：按预制体造视图并交代开火者与命中回调，逻辑层只说要几发，不碰 Instantiate</summary>
    public sealed class BulletField : MonoBehaviour, IBulletViewFactory
    {
        private GameObject _prefab;
        private Transform _owner;
        private IHitLanded _hitSink;

        public void Init(GameObject prefab, Transform owner, IHitLanded hitSink)
        {
            _prefab = prefab;
            _owner = owner;
            _hitSink = hitSink;

            if (_prefab == null) Debug.LogError("[BulletField] 子弹预制体为空，本次开火不会生成子弹。");
        }

        public IBulletView Create(BulletLogic system, int id)
        {
            if (_prefab == null) return null;

            GameObject go = Instantiate(_prefab);

            Bullet bullet = go.GetComponent<Bullet>();
            if (bullet == null) bullet = go.AddComponent<Bullet>();

            bullet.Bind(system, id, _owner, _hitSink);

            return bullet;
        }
    }
}
