using UnityEngine;

namespace Refraction.Data
{
    /// <summary>敌人展示态配色：6 个状态 × 黑白两套，三种敌人共用，改色只改这份资产</summary>
    /// <remarks>
    /// 资产放在 Resources/Data/EnemyVisualConfig.asset，路径是代码唯一入口，移走文件要同步常量。
    /// 没建资产时用脚本里的默认值（与旧硬编码色一致），不会变成白块。
    /// </remarks>
    [CreateAssetMenu(fileName = "EnemyVisualConfig", menuName = "Refraction/Settings/EnemyVisual")]
    public sealed class EnemyVisualConfig : ScriptableObject
    {
        private const string ResourcePath = "Data/EnemyVisualConfig";

        private static EnemyVisualConfig _default;

        [Header("白")]
        [Tooltip("巡逻：没有可追目标时。")]
        public Color patrolWhite = new(0.45f, 0.95f, 0.75f);

        [Tooltip("追击：盯着玩家飞过去时。")]
        public Color chaseWhite = new(1f, 0.84f, 0.35f);

        [Tooltip("返回：脱战后飞回家时。")]
        public Color returnWhite = new(0.62f, 0.55f, 1f);

        [Tooltip("前摇：攻击起手、清场蓄力时。")]
        public Color windupWhite = new(1f, 0.62f, 0.25f);

        [Tooltip("攻击：开火或冲刺的那一两帧。")]
        public Color strikeWhite = new(1f, 1f, 0.92f);

        [Tooltip("后摇：收招、还没回到巡逻时。")]
        public Color recoverWhite = new(0.55f, 0.62f, 0.78f);

        [Header("黑")]
        [Tooltip("巡逻：没有可追目标时。")]
        public Color patrolBlack = new(0.55f, 0.4f, 0.78f);

        [Tooltip("追击：盯着玩家飞过去时。")]
        public Color chaseBlack = new(0.72f, 0.48f, 0.24f);

        [Tooltip("返回：脱战后飞回家时。")]
        public Color returnBlack = new(0.42f, 0.34f, 0.6f);

        [Tooltip("前摇：攻击起手、清场蓄力时。")]
        public Color windupBlack = new(0.78f, 0.3f, 0.32f);

        [Tooltip("攻击：开火或冲刺的那一两帧。")]
        public Color strikeBlack = new(0.38f, 0.38f, 0.45f);

        [Tooltip("后摇：收招、还没回到巡逻时。")]
        public Color recoverBlack = new(0.3f, 0.48f, 0.52f);

        public static EnemyVisualConfig Default
        {
            get
            {
                if (_default != null) return _default;

                _default = Resources.Load<EnemyVisualConfig>(ResourcePath);

                if (_default == null)
                {
                    _default = CreateInstance<EnemyVisualConfig>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    Debug.LogWarning($"[敌人配色] 缺 Resources/{ResourcePath}.asset：临时用脚本默认值，资产里改不到。");
                }

                return _default;
            }
        }
    }
}
