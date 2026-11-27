using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class WorldMapCanvas
    {
        private readonly CanvasTransform _transform = new();
        private readonly Dictionary<string, Texture2D> _composites = new();
        private readonly Dictionary<string, Texture2D> _sources = new();
        private readonly SelectTool _select = new();
        private readonly ConnectionTool _connect = new();
        private readonly WorldToolContext _context = new();
        private bool _spaceHeld;
        private bool _panning;
        private bool _interacting;
        private Vector2 _lastMouse;

        public float Zoom => _transform.Zoom;
        public string SelectedGuid { get; private set; }
        public int SelectedConnection { get; private set; } = -1;

        public void Draw(Rect rect, WorldMapData data, bool showGrid)
        {
            SyncComposites(data);
            WorldMapRenderer.Draw(rect, _transform, data, _composites, SelectedGuid, SelectedConnection, showGrid);
        }

        public void Fit(Rect rect, WorldMapData data)
        {
            if (data == null) return;
            Rect bounds = WorldMapRenderer.Bounds(data);
            _transform.FitToView(rect, bounds.size);
            Vector2 center = new Vector2(bounds.center.x, -bounds.center.y);
            Vector2 current = _transform.MapToScreen(center);
            _transform.Pan(rect.size * 0.5f - current);
        }

        public void Dispose()
        {
            foreach (Texture2D texture in _composites.Values) MapCompositor.Release(texture);
            _composites.Clear();
            _sources.Clear();
        }

        public void HandleInput(Event current, Rect rect, WorldMapData data, WorldTool tool, bool snap,
            out bool changed, out string message)
        {
            changed = false;
            message = null;
            if (data == null) return;

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Space)
            {
                _spaceHeld = true;
                current.Use();
                return;
            }
            if (current.type == EventType.KeyUp && current.keyCode == KeyCode.Space)
            {
                _spaceHeld = false;
                current.Use();
                return;
            }
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.F)
            {
                Fit(rect, data);
                current.Use();
                return;
            }
            if (current.type == EventType.KeyDown
                && (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace))
            {
                changed = DeleteSelection(data);
                current.Use();
                return;
            }
            if (current.type == EventType.MouseUp)
            {
                if (current.button == 0 && _interacting)
                {
                    PrepareContext(current, rect, data, tool, snap);
                    ActiveTool(tool).OnUp(_context);
                    _interacting = false;
                    if (rect.Contains(current.mousePosition)) current.Use();
                }
                _panning = false;
                return;
            }
            if (!rect.Contains(current.mousePosition)) return;

            Vector2 localMouse = current.mousePosition - rect.position;
            if (current.type == EventType.ScrollWheel)
            {
                _transform.ZoomAt(localMouse, current.delta.y);
                current.Use();
                return;
            }
            bool panGesture = current.button == 1 || (current.button == 0 && _spaceHeld) || tool == WorldTool.Hand;
            if (current.type == EventType.MouseDown && panGesture)
            {
                _panning = true;
                _interacting = true;
                _lastMouse = localMouse;
                current.Use();
                return;
            }
            if (_panning && current.type == EventType.MouseDrag)
            {
                _transform.Pan(localMouse - _lastMouse);
                _lastMouse = localMouse;
                current.Use();
                return;
            }
            if (tool == WorldTool.Hand) return;

            PrepareContext(current, rect, data, tool, snap);
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                _interacting = true;
                ActiveTool(tool).OnDown(_context);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && current.button == 0)
            {
                ActiveTool(tool).OnDrag(_context);
                current.Use();
            }
            CommitContext();
            changed = _context.Changed;
            message = _context.Message;
        }

        private IWorldMapTool ActiveTool(WorldTool tool)
        {
            return tool == WorldTool.Connection ? (IWorldMapTool)_connect : _select;
        }

        private void PrepareContext(Event current, Rect rect, WorldMapData data, WorldTool tool, bool snap)
        {
            Vector2 localMouse = current.mousePosition - rect.position;
            _context.Current = current;
            _context.Rect = rect;
            _context.LocalMouse = localMouse;
            _context.World = WorldMapRenderer.ScreenToWorld(localMouse, _transform);
            _context.Data = data;
            _context.Snap = snap;
            _context.Zoom = _transform.Zoom;
            _context.SelectedGuid = SelectedGuid;
            _context.SelectedConnection = SelectedConnection;
            _context.Changed = false;
            _context.Message = null;
        }

        private void CommitContext()
        {
            SelectedGuid = _context.SelectedGuid;
            SelectedConnection = _context.SelectedConnection;
        }

        private bool DeleteSelection(WorldMapData data)
        {
            if (SelectedConnection >= 0 && SelectedConnection < data.connections.Count)
            {
                data.connections.RemoveAt(SelectedConnection);
                SelectedConnection = -1;
                return true;
            }
            if (string.IsNullOrEmpty(SelectedGuid)) return false;
            data.levels.RemoveAll(placement => placement != null && placement.sceneGuid == SelectedGuid);
            data.connections.RemoveAll(connection => connection != null
                && (connection.fromSceneGuid == SelectedGuid || connection.toSceneGuid == SelectedGuid));
            SelectedGuid = null;
            return true;
        }

        private void SyncComposites(WorldMapData data)
        {
            if (data == null) return;
            var valid = new HashSet<string>();
            foreach (LevelMapPlacement placement in data.levels)
            {
                if (placement == null) continue;
                LevelMapData level = MapDataManager.Load(placement.sceneGuid);
                if (level == null || level.groundTexture == null) continue;
                valid.Add(placement.sceneGuid);
                _sources.TryGetValue(placement.sceneGuid, out Texture2D previous);
                if (previous == level.groundTexture && _composites.ContainsKey(placement.sceneGuid) && !level.isDirty) continue;
                if (_composites.TryGetValue(placement.sceneGuid, out Texture2D old)) MapCompositor.Release(old);
                _composites[placement.sceneGuid] = MapCompositor.CreateComposite(level);
                _sources[placement.sceneGuid] = level.groundTexture;
            }
            var stale = new List<string>();
            foreach (string guid in _composites.Keys)
            {
                if (valid.Contains(guid)) continue;
                stale.Add(guid);
            }
            foreach (string guid in stale)
            {
                MapCompositor.Release(_composites[guid]);
                _composites.Remove(guid);
                _sources.Remove(guid);
            }
        }
    }
}
