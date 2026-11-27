using UnityEngine;

namespace Refraction.MapEditor
{
    public interface IMapTool
    {
        MapTool Kind { get; }
        void OnMouseDown(MapToolContext context);
        void OnMouseDrag(MapToolContext context);
        void OnMouseUp(MapToolContext context);
        void DrawPreview(MapToolContext context);
    }

    public sealed class MapToolContext
    {
        public LevelMapData Data;
        public CanvasTransform Transform;
        public Rect CanvasRect;
        public Vector2 MouseLocal;
        public Vector2 Pixel;
        public Vector2 PreviousPixel;
        public Vector2 PreviousMouse;
        public RectInt Dirty;
        public float Radius;
        public Color Color;
        public IMapCommand Command;

        public void MarkDirty(RectInt rect)
        {
            Dirty = Union(Dirty, rect);
        }

        public static RectInt Union(RectInt a, RectInt b)
        {
            if (a.width <= 0 || a.height <= 0) return b;
            if (b.width <= 0 || b.height <= 0) return a;
            int x0 = Mathf.Min(a.xMin, b.xMin);
            int y0 = Mathf.Min(a.yMin, b.yMin);
            int x1 = Mathf.Max(a.xMax, b.xMax);
            int y1 = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
    }
}
