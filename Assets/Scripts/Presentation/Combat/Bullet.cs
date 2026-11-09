using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>白色子弹：由组合根生成并初始化，靠刚体速度直线飞行，命中受击盒或离开 Bound 触发器时销毁自己</summary>
    /// <remarks>伤害由自己结算而不是走攻击盒：击退方向要用飞行方向，且必须排掉开火者自己（子弹出生在玩家中心）</remarks>
    public sealed class Bullet : MonoBehaviour, IAttack
    {
        private const string BoundTag = "Bound";

        private static readonly int GroundLayer = 6;

        private Damage _damage;
        private Transform _owner;

        /// <summary>生成后由组合根调用一次；预制体没挂刚体时当场补一个，Dynamic 才能与静态触发器产生回调</summary>
        public void Init(in BulletSpawn spawn, Transform owner)
        {
            _owner = owner;
            _damage = new Damage(spawn.Damage, DamageSource.Bullet, spawn.Direction, spawn.KnockbackSpeed);

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.velocity = spawn.Direction * spawn.Speed;

            // 默认朝右（+X），直接按飞行方向旋转
            float angle = Mathf.Atan2(spawn.Direction.y, spawn.Direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.position = new Vector3(spawn.Origin.x, spawn.Origin.y, transform.position.z);

            // 预制体上的攻击盒会再结算一遍伤害（还没有开火者信息），本弹自己算，关掉它
            AttackBox attackBox = GetComponent<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null && other.CompareTag(BoundTag)) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;

            // 出生点在开火者体内，先排自己人，否则一枪先把自己打了
            if (_owner != null && (other.transform == _owner || other.transform.IsChildOf(_owner))) return;

            if (other.CompareTag(BoundTag))
            {
                Destroy(gameObject);
                return;
            }

            // 打在墙地上一并消散
            if (other.gameObject.layer == GroundLayer)
            {
                Destroy(gameObject);
                return;
            }

            // 命中即消散：一颗子弹只结算一次，穿透弹以后再说
            Destroy(gameObject);
        }

        public Damage ProduceDamage()
        {
            return _damage;
        }
    }
}
