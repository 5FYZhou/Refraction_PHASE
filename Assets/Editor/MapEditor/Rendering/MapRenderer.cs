using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public static class MapRenderer
    {
        public static void Draw(Rect rect, CanvasTransform transform, Texture2D composite, LevelMapData data)
        {
            EditorGUI.DrawRect(rect, data == null ? new Color(0.16f, 0.16f, 0.16f, 1f) : data.backgroundColor);
            if (data == null || data.groundTexture == null) return;

            Vector2 origin = rect.position + transform.PanOffset;
            var drawRect = new Rect(origin.x, origin.y,
                data.groundTexture.width * transform.Zoom, data.groundTexture.height * transform.Zoom);
            MapGridRenderer.Draw(rect, drawRect, transform.Zoom, data.pixelsPerUnit, new Color(1f, 1f, 1f, 0.06f));
            if (composite != null) GUI.DrawTexture(drawRect, composite, ScaleMode.StretchToFill, true);
            EditorGUI.DrawRect(drawRect, new Color(1f, 1f, 1f, 0.18f));
        }
    }
}
