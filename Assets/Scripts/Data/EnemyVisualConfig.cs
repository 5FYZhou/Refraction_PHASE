using UnityEngine;

namespace Refraction.Data
{
    /// <summary>敌人攻击配色：前摇 / 攻击 / 后摇三段 × 黑白两套，三种敌人共用</summary>
    /// <remarks>
    /// 只在这三段生效，其余展示态用 View 自己的初始色（黑世界取反 rgb）。
    /// 资产放在 Resources/Data/Enemy/EnemyVisualConfig.asset，路径是代码唯一入口，移走文件要同步常量。
    /// 没建资产时用脚本里的默认值，不会变成白块。
    /// </remarks>
    [CreateAssetMenu(fileName = "EnemyVisualConfig", menuName = "Refraction/Settings/EnemyVisual")]
    public sealed class EnemyVisualConfig : ScriptableObject
    {
        private const string ResourcePath = "Data/Enemy/EnemyVisualConfig";

        private static EnemyVisualConfig _default;

        [Header("白")]
        [Tooltip("前摇：攻击起手、清场蓄力时。")]
        public Color windupWhite = new(1f, 0.62f, 0.25f);

        [Tooltip("攻击：开火或冲刺的那一两帧。")]
        public Color strikeWhite = new(1f, 1f, 0.92f);

        [Tooltip("后摇：收招、还没回到巡逻时。")]
        public Color recoverWhite = new(0.55f, 0.62f, 0.78f);

        [Header("黑")]
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
