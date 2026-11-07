
using TMPro;

namespace Refraction.Presentation.UI
{
    public enum TxtStyle
    {
        // 不修改，保留编辑器内的
        Default,
        Title,
        Body
    }

    public static class UIStyle
    {
        // 字号单位是 TMP 的磅值，只对走样式的面板生效
        private const float BodySize = 20f;
        private const float TitleSize = 28f;

        /// <summary>套一层样式：字号、字重、字资产一起改；font 传 null 表示 UIMgr 那边没取到资产，保留原本的字体</summary>
        public static void SetTxtStyle(TMP_Text text, TxtStyle style, TMP_FontAsset font)
        {
            if (text == null || style == TxtStyle.Default) return;

            bool title = style == TxtStyle.Title;

            text.fontSize = title ? TitleSize : BodySize;
            text.fontStyle = title ? FontStyles.Bold : FontStyles.Normal;

            if (font != null) text.font = font;
        }
    }
}
