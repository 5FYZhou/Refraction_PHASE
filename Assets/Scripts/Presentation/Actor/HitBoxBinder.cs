using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>把根上与所有子物体上的受击盒 / 攻击盒接到同一份逻辑</summary>
    /// <remarks>逐个遍历而不是 GetComponentInChildren：后者只回第一个，挂在根上的那个会把子物体上的全挡住。
    /// 攻击盒顺手记下归属根，盒子挂在根下任意深度时自身层级的排除才准。</remarks>
    public static class HitBoxBinder
    {
        public static void Bind(Transform root, IHurtable hurtable, IAttacker attacker)
        {
            HurtBox[] hurtBoxes = root.GetComponentsInChildren<HurtBox>();
            for (int i = 0; i < hurtBoxes.Length; i++) hurtBoxes[i].SetIAttackable(in hurtable);

            AttackBox[] attackBoxes = root.GetComponentsInChildren<AttackBox>();
            for (int i = 0; i < attackBoxes.Length; i++)
            {
                attackBoxes[i].SetOwnerRoot(root);
                attackBoxes[i].SetAttack(in attacker);
            }
        }
    }
}
