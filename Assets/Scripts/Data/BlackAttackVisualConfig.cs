using UnityEngine;

namespace Refraction.Data
{
    /// <summary>黑形态的范围绘制配置：三块调试网格各自的颜色与总开关</summary>
    /// <remarks>
    /// 资产放在 Resources/Data/BlackAttackVisualConfig.asset，运行时由 BlackAttackView 自己加载。
    /// 只影响看得见的那三块网格，判定与弹反本身照常生效。
    /// </remarks>
    [CreateAssetMenu(fileName = "BlackAttackVisualConfig", menuName = "Refraction/Settings/BlackAttackVisual")]
    public sealed class BlackAttackVisualConfig : ScriptableObject
    {
        private const string ResourcePath = "Data/BlackAttackVisualConfig";

        private static BlackAttackVisualConfig _default;

        [Tooltip("判定范围的网格颜色：普攻与突进点亮的那几只盒，半透明紫。")]
        public Color attackColor = new(0.65f, 0.35f, 1f, 0.2f);

        [Tooltip("是否绘制判定范围：关掉就不生成网格，判定照常。")]
        public bool drawAttack;

        [Tooltip("弹反范围的网格颜色：BlackRebound 下那几只，半透明黄。")]
        public Color reboundColor = new(1f, 0.92f, 0.02f, 0.1f);

        [Tooltip("是否绘制弹反范围：关掉就不生成网格，弹反照常。")]
        public bool drawRebound = true;

        [Tooltip("突进扫掠面的网格颜色：起手形状沿位移扫出来的那一片，半透明绿。")]
        public Color sweepColor = new(0.2f, 1f, 0.35f, 0.22f);

        [Tooltip("是否绘制突进扫掠面：关掉就不生成那一片，只看判定盒本身。")]
        public bool drawSweep;

        public static BlackAttackVisualConfig Default
        {
            get
            {
                if (_default != null) return _default;

                _default = Resources.Load<BlackAttackVisualConfig>(ResourcePath);

                if (_default == null)
                {
                    _default = CreateInstance<BlackAttackVisualConfig>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    Debug.LogWarning($"[黑攻击范围] 缺 Resources/{ResourcePath}.asset：临时用脚本默认值，资产里改不到。");
                }

                return _default;
            }
        }
    }
}
