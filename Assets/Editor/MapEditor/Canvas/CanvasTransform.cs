using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class CanvasTransform
    {
        public const float MinZoom = 0.05f;
        public const float MaxZoom = 20f;

        public float Zoom { get; private set; } = 1f;
        public Vector2 PanOffset { get; private set; }

        public Vector2 MapToScreen(Vector2 mapPosition)
        {
            return mapPosition * Zoom + PanOffset;
        }

        public Vector2 ScreenToMap(Vector2 screenPosition)
        {
            return (screenPosition - PanOffset) / Zoom;
        }

        public void ZoomAt(Vector2 screenPosition, float delta)
        {
            float previous = Zoom;
            float next = Mathf.Clamp(previous * Mathf.Exp(-delta * 0.15f), MinZoom, MaxZoom);
            if (Mathf.Approximately(previous, next)) return;
            Vector2 anchored = (screenPosition - PanOffset) / previous;
            Zoom = next;
            PanOffset = screenPosition - anchored * Zoom;
        }

        public void Pan(Vector2 delta)
        {
            PanOffset += delta;
        }

        public void FitToView(Rect canvasRect, Vector2 mapSize)
        {
            if (mapSize.x <= 0f || mapSize.y <= 0f) return;
            Zoom = Mathf.Clamp(Mathf.Min(canvasRect.width / mapSize.x, canvasRect.height / mapSize.y) * 0.9f,
                MinZoom, MaxZoom);
            PanOffset = new Vector2(
                (canvasRect.width - mapSize.x * Zoom) * 0.5f,
                (canvasRect.height - mapSize.y * Zoom) * 0.5f);
        }
    }
}
