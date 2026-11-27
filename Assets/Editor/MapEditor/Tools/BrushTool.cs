using UnityEngine;

namespace Refraction.MapEditor
{
    public static class BrushTool
    {
        public static void Draw(Texture2D texture, Vector2 pixelPosition, float radius, Color color)
        {
            if (texture == null || radius <= 0f) return;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(pixelPosition.x - radius));
            int x1 = Mathf.Min(texture.width - 1, Mathf.CeilToInt(pixelPosition.x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(pixelPosition.y - radius));
            int y1 = Mathf.Min(texture.height - 1, Mathf.CeilToInt(pixelPosition.y + radius));
            float radiusSqr = radius * radius;

            Color[] pixels = texture.GetPixels();
            bool changed = false;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 delta = new Vector2(x + 0.5f, y + 0.5f) - pixelPosition;
                    if (delta.sqrMagnitude > radiusSqr) continue;
                    int index = y * texture.width + x;
                    if (pixels[index] == color) continue;
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
