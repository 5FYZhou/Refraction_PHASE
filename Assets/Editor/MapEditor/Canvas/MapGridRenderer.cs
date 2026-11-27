using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public static class MapGridRenderer
    {
        public static void Draw(Rect canvasRect, Rect drawRect, float zoom, float pixelsPerUnit, Color color)
        {
            float step = Mathf.Max(4f, pixelsPerUnit * zoom);
            Handles.color = color;
            for (float x = drawRect.x; x <= drawRect.xMax; x += step)
            {
                if (x < canvasRect.x || x > canvasRect.xMax) continue;
                Handles.DrawLine(new Vector3(x, canvasRect.y), new Vector3(x, canvasRect.yMax));
            }
            for (float y = drawRect.y; y <= drawRect.yMax; y += step)
            {
                if (y < canvasRect.y || y > canvasRect.yMax) continue;
                Handles.DrawLine(new Vector3(canvasRect.x, y), new Vector3(canvasRect.xMax, y));
            }
        }
    }
}
