using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>子弹当前属于哪一边：只认运行时这一份，预制体与初始发射者都不作数</summary>
    public enum BulletFaction
    {
        Player = 0,
        Enemy = 1,
    }

    /// <summary>发射者身份：CombatRoot 登记的编号 + 该发射者配置里的第几类子弹</summary>
    public readonly struct BulletShooter
    {
        public readonly int OwnerId;

        public readonly int BulletIndex;

        public readonly BulletFaction Faction;

        public BulletShooter(int ownerId, int bulletIndex, BulletFaction faction)
        {
            OwnerId = ownerId;
            BulletIndex = bulletIndex;
            Faction = faction;
        }
    }

    /// <summary>一次发射请求：逻辑层只说打什么，取哪个预制体、从哪个池拿实例归 CombatRoot</summary>
    public readonly struct BulletRequest
    {
        public readonly int Id;

        public readonly BulletShooter Shooter;

        public readonly Damage Damage;

        public readonly Vector2 Origin;

        public readonly Vector2 Direction;

        public readonly float Speed;

        public readonly float TransferSpeed;

        public BulletRequest(
            int id, in BulletShooter shooter, in Damage damage, Vector2 origin, Vector2 direction, float speed,
            float transferSpeed)
        {
            Id = id;
            Shooter = shooter;
            Damage = damage;
            Origin = origin;
            Direction = direction;
            Speed = speed;
            TransferSpeed = transferSpeed;
        }
    }

    /// <summary>弹反要覆盖的那几项：阵营、伤害、击退、残影传输速度；外观与池归属一概不动</summary>
    public readonly struct BulletRebind
    {
        public readonly BulletFaction Faction;

        public readonly float Damage;

        public readonly float Knockback;

        public readonly float TransferSpeed;

        public BulletRebind(BulletFaction faction, float damage, float knockback, float transferSpeed = 0f)
        {
            Faction = faction;
            Damage = damage;
            Knockback = knockback;
            TransferSpeed = transferSpeed;
        }
    }
}
