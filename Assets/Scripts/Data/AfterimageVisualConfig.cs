using Refraction.Foundation;
using UnityEngine;

namespace Refraction.Data
{
    /// <summary>残影与折跃连线的绘制数据：残影染色与透明度、淡出与闪烁、连线颜色与粗细</summary>
    /// <remarks>资产放 Resources/Data/Player/AfterimageVisualConfig.asset，AfterimageField 自己加载；缺资产时退回脚本默认值。连线粗细同时是判定方块宽度</remarks>
    [CreateAssetMenu(fileName = "AfterimageVisualConfig", menuName = "Refraction/Settings/AfterimageVisual")]
    public sealed class AfterimageVisualConfig : ScriptableObject
    {
        private static AfterimageVisualConfig _default;

        [Tooltip("残影的染色：RGB 与玩家 View 当前色相乘（白 = 完全跟随，黑世界残影自动变暗），A 是落地那一刻的透明度。")]
        public Color afterimageColor = new(1f, 1f, 1f, 0.35f);

        [Tooltip("残影淡到生命末尾时的透明度：从起始透明度线性淡到这里。")]
        [Range(0f, 1f)]
        public float fadeEndAlpha = 0.12f;

        [Tooltip("消失前开始闪烁的时长（秒）：残影只剩这么久时开始闪，提前看得出要没了。")]
        [Min(0f)]
        public float blinkWindow = 1f;

        [Tooltip("闪烁频率（次/秒）。")]
        [Min(0f)]
        public float blinkHz = 6f;

        [Tooltip("闪烁最暗时的不透明度比例：1 = 不闪，0 = 闪到全透明。")]
        [Range(0f, 1f)]
        public float blinkMinAlphaScale = 0.25f;

        [Tooltip("折跃连线的颜色（含透明度）。")]
        public Color linkColor = new(0.956f, 0.950f, 0.731f, 0.51f);

        [Tooltip("子弹正在穿越时连线的颜色：通常给一个更亮或更不透明的同色。")]
        public Color linkHotColor = new(0.956f, 0.950f, 0.731f, 1f);

        [Tooltip("连线粗细（米）：画面上线的宽度与判定方块宽度共用这一个值。")]
        [Min(0.01f)]
        public float linkThickness = 0.3f;

        public static AfterimageVisualConfig Default
        {
            get
            {
                if (_default != null) return _default;

                _default = Resources.Load<AfterimageVisualConfig>(ResPath.AfterimageVisualConfig);

                if (_default == null)
                {
                    _default = CreateInstance<AfterimageVisualConfig>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    Debug.LogWarning($"[残影表现] 缺 Resources/{ResPath.AfterimageVisualConfig}.asset：临时用脚本默认值，资产里改不到。");
                }

                return _default;
            }
        }
    }
}
