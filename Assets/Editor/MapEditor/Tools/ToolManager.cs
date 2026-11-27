using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class BrushMapTool : IMapTool
    {
        public MapTool Kind => MapTool.Brush;

        public void OnMouseDown(MapToolContext context)
        {
            context.Command = new PaintCommand();
            context.MarkDirty(BrushTool.Draw(context.Data.paintTexture, context.Data.paintEraseMask,
                context.Pixel, context.Radius, context.Color, (PixelCommand)context.Command));
            context.PreviousPixel = context.Pixel;
        }

        public void OnMouseDrag(MapToolContext context)
        {
            context.MarkDirty(BrushTool.DrawLine(context.Data.paintTexture, context.Data.paintEraseMask,
                context.PreviousPixel, context.Pixel, context.Radius, context.Color,
                (PixelCommand)context.Command));
            context.PreviousPixel = context.Pixel;
        }

        public void OnMouseUp(MapToolContext context)
        {
        }

        public void DrawPreview(MapToolContext context)
        {
            Vector2 center = context.MouseLocal + context.CanvasRect.position;
            Handles.color = new Color(context.Color.r, context.Color.g, context.Color.b, 0.7f);
            Handles.DrawWireDisc(center, Vector3.forward, context.Radius * context.Transform.Zoom);
        }
    }

    public sealed class EraserMapTool : IMapTool
    {
        public MapTool Kind => MapTool.Eraser;

        public void OnMouseDown(MapToolContext context)
        {
            context.Command = new EraseCommand();
            context.MarkDirty(EraserTool.Erase(context.Data.paintTexture, context.Data.groundEraseMask,
                context.Data.paintEraseMask, context.Pixel, context.Radius, (PixelCommand)context.Command));
            context.PreviousPixel = context.Pixel;
        }

        public void OnMouseDrag(MapToolContext context)
        {
            context.MarkDirty(EraserTool.EraseLine(context.Data.paintTexture, context.Data.groundEraseMask,
                context.Data.paintEraseMask, context.PreviousPixel, context.Pixel, context.Radius,
                (PixelCommand)context.Command));
            context.PreviousPixel = context.Pixel;
        }

        public void OnMouseUp(MapToolContext context)
        {
        }

        public void DrawPreview(MapToolContext context)
        {
            Vector2 center = context.MouseLocal + context.CanvasRect.position;
            Handles.color = new Color(1f, 1f, 1f, 0.7f);
            Handles.DrawWireDisc(center, Vector3.forward, context.Radius * context.Transform.Zoom);
        }
    }

    public sealed class ToolManager
    {
        private readonly IMapTool[] _tools =
        {
            new BrushMapTool(),
            new EraserMapTool(),
            new HandTool()
        };

        public MapTool Current { get; private set; } = MapTool.Brush;

        public IMapTool Active => _tools[(int)Current];

        public void Select(MapTool tool)
        {
            Current = tool;
        }
    }
}
