using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>探测触发器上的转发器：碰撞体挂在子物体上，触发器消息只送到碰撞体所在的那个 GameObject</summary>
    public sealed class EnemyDetectZone : MonoBehaviour
    {
        private IDetectSink _owner;

        /// <summary>由控制器在 Awake 里补挂并注入</summary>
        public void Init(IDetectSink owner) => _owner = owner;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_owner != null) _owner.OnDetectEnter(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_owner != null) _owner.OnDetectExit(other);
        }
    }
}
