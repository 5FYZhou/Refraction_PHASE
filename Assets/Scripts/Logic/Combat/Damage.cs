using UnityEngine;

namespace Refraction.Logic.Combat
{
    public enum DamageSource
    {
        Default = 0,

        Bullet = 1,

        /// <summary>接触伤害，敌人贴身时触发</summary>
        Contact = 2,
    }

    public readonly struct Damage
    {
        public readonly Vector2 Point;

        /// <summary>指向受害者的单位方向，重合时为 up</summary>
        public readonly Vector2 Direction;

        /// <summary>伤害值，0=纯效果不扣血</summary>
        public readonly float Amount;

        /// <summary>击退位移，格；0=不击退。v0 由受击者按自身阻尼折算</summary>
        public readonly float KnockbackCells;

        public readonly DamageSource Source;

        public bool HasKnockback => KnockbackCells > 0f && Direction.sqrMagnitude > 0f;

        public bool HasDamage => Amount > 0f;

        public Damage(
            Vector2 point,
            float amount,
            DamageSource source,
            Vector2 direction = default,
            float knockbackCells = 0f)
        {
            Point = point;
            Amount = amount;
            Source = source;
            Direction = direction;
            KnockbackCells = knockbackCells;
        }

        /// <remarks>正中命中给"上"，方向为零时归一化会出 NaN</remarks>
        public static Damage At(
            Vector2 point,
            Vector2 victim,
            float amount,
            DamageSource source,
            float knockbackCells = 0f)
        {
            Vector2 delta = victim - point;

            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

            return new Damage(point, amount, source, direction, knockbackCells);
        }
    }
}
