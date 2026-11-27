using UnityEngine;

namespace Refraction.MapEditor
{
    public static class BrushTool
    {
        public static void Draw(Texture2D paint, Texture2D paintMask, Vector2 pixelPosition, float radius,
            Color color, PixelCommand command)
        {
            if (paint == null || radius <= 0f) return;
            ApplyDisc(paint, paint.width, paint.height, pixelPosition, radius, color, command);
            if (paintMask != null && paintMask.width == paint.width && paintMask.height == paint.height)
            {
                ApplyDisc(paintMask, paintMask.width, paintMask.height, pixelPosition, radius,
                    new Color(0f, 0f, 0f, 0f), command);
            }
        }

        public static void DrawLine(Texture2D paint, Texture2D paintMask, Vector2 from, Vector2 to, float radius,
            Color color, PixelCommand command)
        {
            float distance = Vector2.Distance(from, to);
            float step = Mathf.Max(1f, radius * 0.5f);
            int count = Mathf.Max(1, Mathf.CeilToInt(distance / step));
            for (int i = 1; i <= count; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)count);
                Draw(paint, paintMask, point, radius, color, command);
            }
        }

        private static void ApplyDisc(Texture2D texture, int width, int height, Vector2 pixelPosition, float radius,
            Color color, PixelCommand command)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(pixelPosition.x - radius));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(pixelPosition.x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(pixelPosition.y - radius));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(pixelPosition.y + radius));
            float radiusSqr = radius * radius;
            Color[] pixels = texture.GetPixels();
            bool changed = false;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 delta = new Vector2(x + 0.5f, y + 0.5f) - pixelPosition;
                    if (delta.sqrMagnitude > radiusSqr) continue;
                    int index = y * width + x;
                    if (pixels[index] == color) continue;
                    command?.Record(texture, index, pixels[index], color);
                    pixels[index] = color;
                    changed = true;
                }
            }
            if (!changed) return;
            texture.SetPixels(pixels);
            texture.Apply(false, false);
        }
    }
}
