using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>表现层换色用的展示态：巡逻 / 追击 / 返回 + 攻击三段；没有对应态的敌人不会产出它</summary>
    public enum EnemyVisual
    {
        Patrol,
        Chase,
        Return,
        Windup,
        Strike,
        Recover,
    }

    /// <summary>展示态 + 黑白极性 → View 颜色：三种敌人共用一套色阶，改配色只改这里</summary>
    public static class EnemyVisualColors
    {
        private static readonly Color PatrolWhite = new(0.45f, 0.95f, 0.75f);
        private static readonly Color ChaseWhite = new(1f, 0.84f, 0.35f);
        private static readonly Color ReturnWhite = new(0.62f, 0.55f, 1f);
        private static readonly Color WindupWhite = new(1f, 0.62f, 0.25f);
        private static readonly Color StrikeWhite = new(1f, 1f, 0.92f);
        private static readonly Color RecoverWhite = new(0.55f, 0.62f, 0.78f);

        private static readonly Color PatrolBlack = new(0.55f, 0.4f, 0.78f);
        private static readonly Color ChaseBlack = new(0.72f, 0.48f, 0.24f);
        private static readonly Color ReturnBlack = new(0.42f, 0.34f, 0.6f);
        private static readonly Color WindupBlack = new(0.78f, 0.3f, 0.32f);
        private static readonly Color StrikeBlack = new(0.38f, 0.38f, 0.45f);
        private static readonly Color RecoverBlack = new(0.3f, 0.48f, 0.52f);

        public static Color Of(EnemyVisual visual, bool black)
        {
            switch (visual)
            {
                case EnemyVisual.Patrol: return black ? PatrolBlack : PatrolWhite;
                case EnemyVisual.Chase: return black ? ChaseBlack : ChaseWhite;
                case EnemyVisual.Return: return black ? ReturnBlack : ReturnWhite;
                case EnemyVisual.Windup: return black ? WindupBlack : WindupWhite;
                case EnemyVisual.Strike: return black ? StrikeBlack : StrikeWhite;
            }

            return black ? RecoverBlack : RecoverWhite;
        }
    }
}
