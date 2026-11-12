using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>攻击盒：碰到对方的受击盒就结算一次伤害，数值由挂上来的 IAttack 给</summary>
    public sealed class AttackBox : MonoBehaviour
    {
        public IAttacker Attacker { get; private set; }

        public void SetAttack(in IAttacker attack) { Attacker = attack; }

        private void Awake()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Attacker == null || other == null) return;

            // 攻击盒多半就罩在自己身体外面，不排自己会当场自伤
            if (other.transform.root == transform.root) return;

            var target = other.GetComponent<HurtBox>();
            if (target == null) return;

            Damage damage = Attacker.ProduceDamage();

            // 接触伤害的方向按几何现算：用攻击者朝向会把人往怀里推
            if (damage.Source == DamageSource.Contact || damage.Direction.sqrMagnitude <= 0f)
            {
                Vector2 away = (Vector2)other.bounds.center - (Vector2)transform.position;

                if (away.sqrMagnitude > 1e-6f)
                {
                    damage = new Damage(damage.Amount, damage.Source, away.normalized, damage.KnockbackSpeed);
                }
            }

            if (!target.Attackable.TakeDamage(in damage)) return;

            if (Attacker is IHitLanded landed) landed.OnHitLanded();
        }
    }
}
