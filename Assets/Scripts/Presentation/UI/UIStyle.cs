using Refraction.Data;
using TMPro;

namespace Refraction.Presentation.UI
{
    public static class UIStyle
    {
        /// <summary>套一层样式；font 为 null 或条目标了 fontOnly 时，字号与字重保持预制体原样</summary>
        public static void Apply(TMP_Text text, UIStyleConfig.Entry entry, TMP_FontAsset font)
        {
            if (text == null || entry == null) return;

            if (!entry.fontOnly)
            {
                text.fontSize = entry.fontSize;
                text.fontStyle = entry.bold ? FontStyles.Bold : FontStyles.Normal;
            }

            if (font != null) text.font = font;
        }
    }
}
