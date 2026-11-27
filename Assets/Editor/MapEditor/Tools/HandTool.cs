using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class HandTool : IMapTool
    {
        public MapTool Kind => MapTool.Hand;

        public void OnMouseDown(MapToolContext context)
        {
            context.PreviousMouse = context.MouseLocal;
        }

        public void OnMouseDrag(MapToolContext context)
        {
            context.Transform.Pan(context.MouseLocal - context.PreviousMouse);
            context.PreviousMouse = context.MouseLocal;
        }

        public void OnMouseUp(MapToolContext context)
        {
        }

        public void DrawPreview(MapToolContext context)
        {
        }
    }
}
