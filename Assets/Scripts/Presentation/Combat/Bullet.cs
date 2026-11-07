using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>白色子弹：由组合根生成并初始化，靠刚体速度直线飞行，离开 Bound 触发器时销毁自己</summary>
    public sealed class Bullet : MonoBehaviour
    {
        private const string BoundTag = "Bound";

        public float Damage { get; private set; }

        /// <summary>生成后由组合根调用一次；预制体没挂刚体时当场补一个，Dynamic 才能与静态触发器产生回调</summary>
        public void Init(in BulletSpawn spawn)
        {
            Damage = spawn.Damage;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.velocity = new Vector2(spawn.Face * spawn.Speed, 0f);

            transform.position = new Vector3(spawn.Origin.x, spawn.Origin.y, transform.position.z);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null && other.CompareTag(BoundTag)) Destroy(gameObject);
        }
    }
}
