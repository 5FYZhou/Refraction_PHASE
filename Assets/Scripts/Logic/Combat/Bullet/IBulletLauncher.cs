using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>开火参数：Origin 出膛点，Direction 单位向量，Speed 每秒，Damage 伤害，KnockbackSpeed 击退初速，TransferSpeed 残影间传输速度</summary>
    public readonly struct BulletSpawn
    {
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;
        public readonly float Speed;
        public readonly float Damage;
        public readonly float KnockbackSpeed;
        public readonly float TransferSpeed;

        public BulletSpawn(Vector2 origin, Vector2 direction, float speed, float damage, float knockbackSpeed = 0f, float transferSpeed = 0f)
        {
            Origin = origin;
            Direction = direction;
            Speed = speed;
            Damage = damage;
            KnockbackSpeed = knockbackSpeed;
            TransferSpeed = transferSpeed;
        }
    }

    /// <summary>开火唯一出口：逻辑层只交意图，实例与池归表现层</summary>
    public interface IBulletLauncher
    {
        void Launch(in BulletSpawn spawn);
    }
}
