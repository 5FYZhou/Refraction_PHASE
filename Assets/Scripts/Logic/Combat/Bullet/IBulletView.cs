using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>子弹的两种状态：飞行由物理推进，传输中由逻辑层逐段摆位</summary>
    public enum BulletState
    {
        Flying,
        Transporting
    }

    /// <summary>一帧的姿势：位置、朝向、飞行速度、状态与正在穿的那条连线；SegFrom 为 -1 表示没在穿线</summary>
    public readonly struct BulletPose
    {
        public readonly Vector2 Pos;
        public readonly Vector2 Dir;
        public readonly float Speed;
        public readonly BulletState State;
        public readonly int SegFrom;
        public readonly int SegTo;

        public BulletPose(Vector2 pos, Vector2 dir, float speed, BulletState state, int segFrom, int segTo)
        {
            Pos = pos;
            Dir = dir;
            Speed = speed;
            State = state;
            SegFrom = segFrom;
            SegTo = segTo;
        }
    }

    /// <summary>子弹表现层端口：逻辑层只下发姿势与生死，刚体、碰撞体、回收都归实现侧</summary>
    public interface IBulletView
    {
        /// <summary>飞行中位置归物理，逻辑层跟读而不自己积分</summary>
        Vector2 Position { get; }

        void Apply(in BulletPose pose);

        void Kill();
    }

    /// <summary>造视图的端口：预制体与挂点都是表现层的事，逻辑层只说"要一发"</summary>
    public interface IBulletViewFactory
    {
        IBulletView Create(BulletLogic system, int id);
    }
}
