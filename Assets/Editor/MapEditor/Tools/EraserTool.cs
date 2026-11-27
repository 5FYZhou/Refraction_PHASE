using UnityEngine;

namespace Refraction.MapEditor
{
    public static class EraserTool
    {
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        private static readonly Color Erased = new Color(0f, 0f, 0f, 1f);

        public static void Erase(Texture2D paint, Texture2D groundMask, Texture2D paintMask,
            Vector2 pixelPosition, float radius, PixelCommand command)
        {
            if (paint == null || groundMask == null || paintMask == null || radius <= 0f) return;
            if (paint.width != groundMask.width || paint.height != groundMask.height
                || paint.width != paintMask.width || paint.height != paintMask.height) return;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(pixelPosition.x - radius));
            int x1 = Mathf.Min(paint.width - 1, Mathf.CeilToInt(pixelPosition.x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(pixelPosition.y - radius));
            int y1 = Mathf.Min(paint.height - 1, Mathf.CeilToInt(pixelPosition.y + radius));
            float radiusSqr = radius * radius;

            Color[] paintPixels = paint.GetPixels();
            Color[] groundPixels = groundMask.GetPixels();
            Color[] paintMaskPixels = paintMask.GetPixels();
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
                        command?.Record(paint, index, paintPixels[index], Clear);
                        paintPixels[index] = Clear;
                        changed = true;
                    }
                    if (groundPixels[index] != Erased)
                    {
                        command?.Record(groundMask, index, groundPixels[index], Erased);
                        groundPixels[index] = Erased;
                        changed = true;
                    }
                    if (paintMaskPixels[index] != Erased)
                    {
                        command?.Record(paintMask, index, paintMaskPixels[index], Erased);
                        paintMaskPixels[index] = Erased;
                        changed = true;
                    }
                }
            }
            if (!changed) return;
            paint.SetPixels(paintPixels);
            paint.Apply(false, false);
            groundMask.SetPixels(groundPixels);
            groundMask.Apply(false, false);
            paintMask.SetPixels(paintMaskPixels);
            paintMask.Apply(false, false);
        }

        public static void EraseLine(Texture2D paint, Texture2D groundMask, Texture2D paintMask,
            Vector2 from, Vector2 to, float radius, PixelCommand command)
        {
            float distance = Vector2.Distance(from, to);
            float step = Mathf.Max(1f, radius * 0.5f);
            int count = Mathf.Max(1, Mathf.CeilToInt(distance / step));
            for (int i = 1; i <= count; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)count);
                Erase(paint, groundMask, paintMask, point, radius, command);
            }
        }
    }
}
