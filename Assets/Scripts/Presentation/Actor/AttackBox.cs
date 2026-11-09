using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>攻击盒：碰到对方的受击盒就结算一次伤害，数值由挂上来的 IAttack 给</summary>
    public sealed class AttackBox : MonoBehaviour
    {
        public IAttack Attack { get; private set; }

        public void SetAttack(in IAttack attack) { Attack = attack; }

        private void Awake()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Attack == null || other == null) return;

            // 攻击盒多半就罩在自己身体外面，不排自己会当场自伤
            if (other.transform.root == transform.root) return;

            var target = other.GetComponent<HurtBox>();
            if (target == null) return;

            target.Attackable.TakeDamage(Attack.ProduceDamage());
        }
    }
}
