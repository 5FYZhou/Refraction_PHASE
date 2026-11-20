using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>带命中体的命中回报：AttackBox 优先派发这条，实现方可以按被击碰撞体算表现</summary>
    public interface IHitBodyLanded
    {
        void OnHitLanded(Collider2D other);
    }
}
