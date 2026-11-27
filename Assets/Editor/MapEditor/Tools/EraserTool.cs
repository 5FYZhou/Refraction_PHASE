using UnityEngine;

namespace Refraction.MapEditor
{
    public static class EraserTool
    {
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        private static readonly Color Erased = new Color(0f, 0f, 0f, 1f);

        public static void Erase(Texture2D paint, Texture2D mask, Vector2 pixelPosition, float radius)
        {
            if (paint == null || mask == null || radius <= 0f) return;
            if (paint.width != mask.width || paint.height != mask.height) return;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(pixelPosition.x - radius));
            int x1 = Mathf.Min(paint.width - 1, Mathf.CeilToInt(pixelPosition.x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(pixelPosition.y - radius));
            int y1 = Mathf.Min(paint.height - 1, Mathf.CeilToInt(pixelPosition.y + radius));
            float radiusSqr = radius * radius;

            Color[] paintPixels = paint.GetPixels();
            Color[] maskPixels = mask.GetPixels();
            bool changed = false;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 delta = new Vector2(x + 0.5f, y + 0.5f) - pixelPosition;
                    if (delta.sqrMagnitude > radiusSqr) continue;
                    int index = y * paint.width + x;
                    if (paintPixels[index] != Clear)
                    {
                        paintPixels[index] = Clear;
                        changed = true;
                    }
                    // 遮罩写 alpha：擦除原始 Ground 的同时不挡住后续绘制
                    if (maskPixels[index] != Erased)
                    {
                        maskPixels[index] = Erased;
                        changed = true;
                    }
                }
            }
            if (!changed) return;
            paint.SetPixels(paintPixels);
            paint.Apply(false, false);
            mask.SetPixels(maskPixels);
            mask.Apply(false, false);
        }
    }
}
