using Refraction.Data;
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

    /// <summary>展示态 + 黑白极性 → View 颜色：色值在 EnemyVisualConfig 资产里，这里只做映射</summary>
    public static class EnemyVisualColors
    {
        public static Color Of(EnemyVisualConfig config, EnemyVisual visual, bool black)
        {
            switch (visual)
            {
                case EnemyVisual.Patrol: return black ? config.patrolBlack : config.patrolWhite;
                case EnemyVisual.Chase: return black ? config.chaseBlack : config.chaseWhite;
                case EnemyVisual.Return: return black ? config.returnBlack : config.returnWhite;
                case EnemyVisual.Windup: return black ? config.windupBlack : config.windupWhite;
                case EnemyVisual.Strike: return black ? config.strikeBlack : config.strikeWhite;
            }

            return black ? config.recoverBlack : config.recoverWhite;
        }
    }
}
