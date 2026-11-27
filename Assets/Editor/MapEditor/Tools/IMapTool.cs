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
        public float Radius;
        public Color Color;
        public IMapCommand Command;
    }
}
