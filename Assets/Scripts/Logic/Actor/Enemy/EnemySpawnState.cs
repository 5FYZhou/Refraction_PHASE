using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>敌人出生快照：只存刷新真正需要的量，每帧变化的 AI 状态、冷却与当前位置都不进来</summary>
    /// <remarks>生命取配置上限；朝向用 ±1，避免各敌人从缩放反推</remarks>
    public readonly struct EnemySpawnState
    {
        public readonly EnemyKind Kind;

        public readonly Vector2 Position;

        public readonly int FaceSign;

        public readonly bool InitialBlack;

        public readonly int InitialHealth;

        public EnemySpawnState(EnemyKind kind, Vector2 position, int faceSign, bool initialBlack, int initialHealth)
        {
            Kind = kind;
            Position = position;
            FaceSign = faceSign >= 0 ? 1 : -1;
            InitialBlack = initialBlack;
            InitialHealth = initialHealth > 0 ? initialHealth : 1;
        }
    }
}
