using UnityEngine;

namespace Refraction.Logic.Combat
{
    public enum DamageSource
    {
        Default = 0,

        Bullet = 1,

        /// <summary>接触伤害，敌人贴身时触发</summary>
        Contact = 2,

        AfterimageLink = 3,
    }

    public readonly struct Damage
    {
        /// <summary>指向受害者的单位方向，重合时为 up</summary>
        public readonly Vector2 Direction;

        /// <summary>伤害值，0=纯效果不扣血</summary>
        public readonly float Amount;

        /// <summary>击退初速度（单位/秒），0 = 不击退。滑多远由受击者自己的 KnockbackDecay 决定</summary>
        public readonly float KnockbackSpeed;

        public readonly DamageSource Source;

        public bool HasKnockback => KnockbackSpeed > 0f && Direction.sqrMagnitude > 0f;

        public bool HasDamage => Amount > 0f;

        public Damage(
            float amount,
            DamageSource source,
            Vector2 direction = default,
            float knockbackSpeed = 0f)
        {
            Amount = amount;
            Source = source;
            Direction = direction;
            KnockbackSpeed = knockbackSpeed;
        }

        /// <remarks>正中命中给"上"，方向为零时归一化会出 NaN</remarks>
        public static Damage At(
            Vector2 victim,
            float amount,
            DamageSource source,
            float knockbackSpeed = 0f)
        {
            Vector2 delta = victim;

            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

            return new Damage(amount, source, direction, knockbackSpeed);
        }
    }
}
