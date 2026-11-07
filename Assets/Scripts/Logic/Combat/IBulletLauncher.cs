using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>开火参数：Origin 是玩家中心的世界坐标，Face 只取 -1 或 1，Speed 单位为每秒，Damage 为伤害点数</summary>
    public readonly struct BulletSpawn
    {
        public readonly Vector2 Origin;
        public readonly int Face;
        public readonly float Speed;
        public readonly float Damage;

        public BulletSpawn(Vector2 origin, int face, float speed, float damage)
        {
            Origin = origin;
            Face = face;
            Speed = speed;
            Damage = damage;
        }
    }

    /// <summary>逻辑层触发开火的唯一出口：表现层负责实例化预制体，逻辑层不得碰场景对象</summary>
    public interface IBulletLauncher
    {
        void Launch(in BulletSpawn spawn);
    }
}
