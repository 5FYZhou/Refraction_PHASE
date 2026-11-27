using UnityEngine;

namespace Refraction.MapEditor
{
    public enum MapTool
    {
        Brush,
        Eraser,
        Hand
    }

    public static class CanvasInput
    {
        public static bool IsDragging(Event current, int button)
        {
            return current.button == button
                   && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag);
        }
    }
}
