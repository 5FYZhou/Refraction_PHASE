using UnityEngine;

namespace Refraction.MapEditor
{
    public static class BrushTool
    {
        public static RectInt Draw(Texture2D paint, Texture2D paintMask, Vector2 pixelPosition, float radius,
            Color color, PixelCommand command)
        {
            if (paint == null || radius <= 0f) return default;
            RectInt dirty = ApplyDisc(paint, pixelPosition, radius, color, command);
            if (paintMask != null && paintMask.width == paint.width && paintMask.height == paint.height)
            {
                dirty = MapToolContext.Union(dirty,
                    ApplyDisc(paintMask, pixelPosition, radius, Color.clear, command));
            }
            return dirty;
        }

        public static RectInt DrawLine(Texture2D paint, Texture2D paintMask, Vector2 from, Vector2 to, float radius,
            Color color, PixelCommand command)
        {
            float distance = Vector2.Distance(from, to);
            float step = Mathf.Max(1f, radius * 0.5f);
            int count = Mathf.Max(1, Mathf.CeilToInt(distance / step));
            RectInt dirty = default;
            for (int i = 1; i <= count; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)count);
                dirty = MapToolContext.Union(dirty, Draw(paint, paintMask, point, radius, color, command));
            }
            return dirty;
        }

        private static RectInt ApplyDisc(Texture2D texture, Vector2 center, float radius, Color color,
            PixelCommand command)
        {
            int width = texture.width;
            int height = texture.height;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(center.y + radius));
            int blockWidth = x1 - x0 + 1;
            int blockHeight = y1 - y0 + 1;
            if (blockWidth <= 0 || blockHeight <= 0) return default;

            Color[] pixels = texture.GetPixels(x0, y0, blockWidth, blockHeight);
            float radiusSqr = radius * radius;
            bool changed = false;
            for (int y = 0; y < blockHeight; y++)
            {
                for (int x = 0; x < blockWidth; x++)
                {
                    Vector2 delta = new Vector2(x0 + x + 0.5f, y0 + y + 0.5f) - center;
                    if (delta.sqrMagnitude > radiusSqr) continue;
                    int index = y * blockWidth + x;
                    if (pixels[index] == color) continue;
                    command?.Record(texture, (y0 + y) * width + x0 + x, pixels[index], color);
                    pixels[index] = color;
                    changed = true;
                }
            }
            if (!changed) return default;
            texture.SetPixels(x0, y0, blockWidth, blockHeight, pixels);
            return new RectInt(x0, y0, blockWidth, blockHeight);
        }
    }
}
