using UnityEngine;

namespace Refraction.MapEditor
{
    public static class EraserTool
    {
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        private static readonly Color Erased = new Color(0f, 0f, 0f, 1f);

        public static RectInt Erase(Texture2D paint, Texture2D groundMask, Texture2D paintMask,
            Vector2 pixelPosition, float radius, PixelCommand command)
        {
            if (paint == null || groundMask == null || paintMask == null || radius <= 0f) return default;
            if (paint.width != groundMask.width || paint.height != groundMask.height
                || paint.width != paintMask.width || paint.height != paintMask.height) return default;

            RectInt dirty = ApplyDisc(paint, pixelPosition, radius, Clear, command);
            dirty = MapToolContext.Union(dirty, ApplyDisc(groundMask, pixelPosition, radius, Erased, command));
            dirty = MapToolContext.Union(dirty, ApplyDisc(paintMask, pixelPosition, radius, Erased, command));
            return dirty;
        }

        public static RectInt EraseLine(Texture2D paint, Texture2D groundMask, Texture2D paintMask,
            Vector2 from, Vector2 to, float radius, PixelCommand command)
        {
            float distance = Vector2.Distance(from, to);
            float step = Mathf.Max(1f, radius * 0.5f);
            int count = Mathf.Max(1, Mathf.CeilToInt(distance / step));
            RectInt dirty = default;
            for (int i = 1; i <= count; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)count);
                dirty = MapToolContext.Union(dirty, Erase(paint, groundMask, paintMask, point, radius, command));
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
