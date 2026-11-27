using Refraction.Foundation;
using UnityEngine;

namespace Refraction.Data
{
    /// <summary>世界反转演出的时长配置：反转背景非 BG 层、粒子与所有地面从全透明淡回不透明的秒数</summary>
    /// <remarks>资产放在 Resources/Data/Level/WorldReverseVisualConfig.asset，由 LevelRoot 加载；缺资产时用脚本默认值并告警</remarks>
    [CreateAssetMenu(fileName = "WorldReverseVisualConfig", menuName = "Refraction/Settings/WorldReverseVisual")]
    public sealed class WorldReverseVisualConfig : ScriptableObject
    {
        private static WorldReverseVisualConfig _default;

        [Tooltip("反转后淡入的时长（秒）：0 = 不演出，颜色直接到位。所有参与对象共用这一个时长。")]
        [Min(0f)]
        public float fadeInDuration = 0.3f;

        public static WorldReverseVisualConfig Default
        {
            get
            {
                if (_default != null) return _default;

                _default = Resources.Load<WorldReverseVisualConfig>(ResPath.WorldReverseVisualConfig);

                if (_default == null)
                {
                    _default = CreateInstance<WorldReverseVisualConfig>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    Debug.LogWarning($"[反转演出] 缺 Resources/{ResPath.WorldReverseVisualConfig}.asset：临时用脚本默认值，资产里改不到。");
                }

                return _default;
            }
        }
    }
}
