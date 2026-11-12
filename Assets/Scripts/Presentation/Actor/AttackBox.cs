using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>攻击盒：碰到受击盒就按 IAttacker 的数值结算</summary>
    public sealed class AttackBox : MonoBehaviour
    {
        public IAttacker Attacker { get; private set; }

        public void SetAttack(in IAttacker attack) { Attacker = attack; }

        private Collider2D _cachedOther;
        private HurtBox _cachedBox;

        private void Awake()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other, false);

        private void OnTriggerStay2D(Collider2D other) => TryHit(other, true);

        private void TryHit(Collider2D other, bool repeat)
        {
            if (Attacker == null || other == null) return;

            // 攻击盒多半就罩在自己身体外面，不排自己会当场自伤
            if (other.transform.root == transform.root) return;

            HurtBox target = TargetOf(other);
            if (target == null) return;

            // 间隔在攻击者身上：被无敌窗挡掉的那次不消耗间隔
            IIntervalAttacker interval = Attacker as IIntervalAttacker;
            if (repeat && interval == null) return;
            if (interval != null && !interval.ReadyToHit(Time.fixedTime)) return;

            Damage damage = Attacker.ProduceDamage();

            // 接触伤害的方向按几何现算：用攻击者朝向会把人往怀里推
            if (damage.Source == DamageSource.Contact || damage.Direction.sqrMagnitude <= 0f)
            {
                Vector2 away = (Vector2)other.bounds.center - (Vector2)transform.position;

                if (away.sqrMagnitude > 1e-6f)
                {
                    Vector2 dir = away.normalized;
                    if(repeat) dir.y = 0f;
                    dir.x = dir.x > 0f ? 1f : -1f;
                    damage = new Damage(damage.Amount, damage.Source, dir, damage.KnockbackSpeed);
                }
            }

            if (!target.Attackable.TakeDamage(in damage)) return;

            if (interval != null) interval.MarkHit(Time.fixedTime);
            if (Attacker is IHitLanded landed) landed.OnHitLanded();
        }

        private HurtBox TargetOf(Collider2D other)
        {
            if (other == _cachedOther) return _cachedBox;

            _cachedOther = other;
            _cachedBox = other.GetComponent<HurtBox>();
            return _cachedBox;
        }
    }
}
