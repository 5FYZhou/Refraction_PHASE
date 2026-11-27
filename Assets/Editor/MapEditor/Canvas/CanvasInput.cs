using UnityEngine;

namespace Refraction.MapEditor
{
    public enum MapTool
    {
        Brush,
        Eraser
    }

    public static class CanvasInput
    {
        public const float MinZoom = 0.1f;
        public const float MaxZoom = 8f;

        public static float ZoomBy(Event current, float zoom)
        {
            return Mathf.Clamp(zoom * Mathf.Exp(-current.delta.y * 0.15f), MinZoom, MaxZoom);
        }

        public static bool IsDragging(Event current, int button)
        {
            return current.button == button
                   && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag);
        }

        public static Vector2 WheelDelta(Event current)
        {
            return current.type == EventType.ScrollWheel ? current.delta : Vector2.zero;
        }
    }
}
