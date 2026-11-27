using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>弹反范围上的转发器：ReboundCollider 挂在子物体上，触发器消息只送到碰撞体所在的那个 GameObject</summary>
    public sealed class MmflyRebound : MonoBehaviour
    {
        private MmflyController _owner;

        public void Init(MmflyController owner) => _owner = owner;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_owner != null) _owner.OnReboundEnter(other);
        }
    }
}
