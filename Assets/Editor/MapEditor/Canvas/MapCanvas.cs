using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class MapCanvas
    {
        private readonly CanvasTransform _transform = new();
        private readonly MapToolContext _context = new();
        private Texture2D _composite;
        private IMapCommand _activeCommand;
        private IMapCommand _completedCommand;
        private bool _spaceHeld;
        private bool _strokeActive;
        private Vector2 _lastMouse;

        public float Zoom => _transform.Zoom;

        public void SetData(LevelMapData data, Rect rect)
        {
            _composite = MapCompositor.Compose(data);
            Fit(rect, data);
        }

        public void Refresh(LevelMapData data)
        {
            _composite = MapCompositor.Compose(data);
        }

        public void Fit(Rect rect, LevelMapData data)
        {
            if (data == null || data.groundTexture == null) return;
            _transform.FitToView(rect, new Vector2(data.groundTexture.width, data.groundTexture.height));
        }

        public IMapCommand TakeCommand()
        {
            IMapCommand result = _completedCommand;
            _completedCommand = null;
            return result;
        }

        public void HandleInput(Event current, Rect rect, LevelMapData data, ToolManager tools,
            float radius, Color color, out bool changed)
        {
            changed = false;
            if (data == null || data.groundTexture == null) return;

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

            if (current.type == EventType.MouseUp && current.button == 0 && _strokeActive)
            {
                tools.Active.OnMouseUp(_context);
                _completedCommand = _activeCommand;
                _activeCommand = null;
                _strokeActive = false;
                changed = true;
                current.Use();
                return;
            }
            if (!rect.Contains(current.mousePosition)) return;

            Vector2 localMouse = current.mousePosition - rect.position;
            PrepareContext(data, rect, localMouse, radius, color);

            if (current.type == EventType.ScrollWheel)
            {
                _transform.ZoomAt(localMouse, current.delta.y);
                current.Use();
                return;
            }

            bool pan = CanvasInput.IsDragging(current, 1)
                       || (_spaceHeld && CanvasInput.IsDragging(current, 0))
                       || (tools.Current == MapTool.Hand && CanvasInput.IsDragging(current, 0));
            if (pan)
            {
                if (current.type == EventType.MouseDown) _lastMouse = localMouse;
                _transform.Pan(localMouse - _lastMouse);
                _lastMouse = localMouse;
                current.Use();
                return;
            }

            if (tools.Current == MapTool.Hand) return;
            if (!TryScreenToPixel(localMouse, data, out Vector2 pixel)) return;
            _context.Pixel = pixel;

            bool begin = (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                         && current.button == 0 && !_strokeActive;
            if (begin)
            {
                _context.PreviousPixel = pixel;
                tools.Active.OnMouseDown(_context);
                _activeCommand = _context.Command;
                _strokeActive = true;
                changed = true;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && current.button == 0 && _strokeActive)
            {
                _context.Command = _activeCommand;
                tools.Active.OnMouseDrag(_context);
                changed = true;
                current.Use();
            }
        }

        public void DrawPreview(Rect rect, ToolManager tools, LevelMapData data, float radius, Color color)
        {
            if (data == null || tools.Current == MapTool.Hand) return;
            if (!TryScreenToPixel(Event.current.mousePosition - rect.position, data, out Vector2 pixel)) return;
            PrepareContext(data, rect, Event.current.mousePosition - rect.position, radius, color);
            _context.Pixel = pixel;
            tools.Active.DrawPreview(_context);
        }

        public void Draw(Rect rect, LevelMapData data)
        {
            MapRenderer.Draw(rect, _transform, _composite, data);
        }

        private void PrepareContext(LevelMapData data, Rect rect, Vector2 localMouse, float radius, Color color)
        {
            _context.Data = data;
            _context.Transform = _transform;
            _context.CanvasRect = rect;
            _context.MouseLocal = localMouse;
            _context.Radius = radius;
            _context.Color = color;
            _context.Command = _activeCommand;
        }

        private bool TryScreenToPixel(Vector2 localMouse, LevelMapData data, out Vector2 pixel)
        {
            pixel = _transform.ScreenToMap(localMouse);
            float width = data.groundTexture.width;
            float height = data.groundTexture.height;
            pixel.y = height - pixel.y;
            if (pixel.x < 0f || pixel.y < 0f || pixel.x >= width || pixel.y >= height) return false;
            return true;
        }
    }
}
