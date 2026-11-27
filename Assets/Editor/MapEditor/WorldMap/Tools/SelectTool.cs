using UnityEngine;

namespace Refraction.MapEditor
{
    public enum WorldTool
    {
        Select,
        Connection,
        Hand
    }

    public interface IWorldMapTool
    {
        void OnDown(WorldToolContext context);
        void OnDrag(WorldToolContext context);
        void OnUp(WorldToolContext context);
    }

    public sealed class WorldToolContext
    {
        public Event Current;
        public Rect Rect;
        public Vector2 LocalMouse;
        public Vector2 World;
        public WorldMapData Data;
        public bool Snap;
        public float Zoom = 1f;
        public string SelectedGuid;
        public int SelectedConnection = -1;
        public bool Changed;
        public string Message;
    }

    public sealed class SelectTool : IWorldMapTool
    {
        private string _dragGuid;
        private Vector2 _grabOffset;

        public void OnDown(WorldToolContext context)
        {
            LevelMapPlacement placement = WorldMapRenderer.HitPlacement(context.Data, context.World);
            if (placement != null)
            {
                context.SelectedGuid = placement.sceneGuid;
                context.SelectedConnection = -1;
                if (!placement.locked)
                {
                    _dragGuid = placement.sceneGuid;
                    _grabOffset = context.World - placement.position;
                }
                return;
            }
            int connection = WorldMapRenderer.HitConnection(context.Data, context.World, 8f / Mathf.Max(0.01f, context.Zoom));
            context.SelectedConnection = connection;
            if (connection >= 0) context.SelectedGuid = null;
        }

        public void OnDrag(WorldToolContext context)
        {
            if (_dragGuid == null || context.Data == null) return;
            LevelMapPlacement placement = WorldMapRenderer.FindPlacement(context.Data, _dragGuid);
            if (placement == null || placement.locked) return;
            Vector2 target = context.World - _grabOffset;
            if (context.Snap) target = new Vector2(Mathf.Round(target.x), Mathf.Round(target.y));
            if (target == placement.position) return;
            placement.position = target;
            context.Changed = true;
        }

        public void OnUp(WorldToolContext context)
        {
            _dragGuid = null;
        }
    }
}
