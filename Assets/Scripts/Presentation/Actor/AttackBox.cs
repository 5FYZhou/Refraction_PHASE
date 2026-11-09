using Refraction.Logic.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    public sealed class AttackBox : MonoBehaviour
    {
        public IAttack Attack { get; private set; }

        public void SetAttack(in IAttack attack) { Attack = attack; }

        private void Awake()
        {
            var c = GetComponent<Collider2D>();
            c.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if(other != null)
            {
                var hurtBox = other.GetComponent<HurtBox>();
                if(hurtBox != null)
                {
                    hurtBox.Attackable.TakeDamage(Attack.ProduceDamage());
                }
            }
        }
    }
}
