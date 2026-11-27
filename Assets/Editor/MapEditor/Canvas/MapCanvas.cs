using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class MapCanvas
    {
        private float _zoom = 1f;
        private Vector2 _panOffset;
        private Rect _canvasRect;
        private Texture2D _composite;
        private bool _spaceHeld;
        private bool _strokeRecorded;
        private Vector2 _lastMouse;

        public float Zoom => _zoom;

        public void SetData(LevelMapData data, Rect rect)
        {
            _composite = MapDataManager.Compose(data);
            Fit(rect, data);
        }

        public void Refresh(LevelMapData data)
        {
            _composite = MapDataManager.Compose(data);
        }

        public void Fit(Rect rect, LevelMapData data)
        {
            _canvasRect = rect;
            if (data == null || data.groundTexture == null) return;
            float width = Mathf.Max(1, data.groundTexture.width);
            float height = Mathf.Max(1, data.groundTexture.height);
            _zoom = Mathf.Clamp(Mathf.Min(rect.width / width, rect.height / height) * 0.9f,
                CanvasInput.MinZoom, CanvasInput.MaxZoom);
            _panOffset = new Vector2(
                (rect.width - width * _zoom) * 0.5f,
                (rect.height - height * _zoom) * 0.5f);
        }

        public void Draw(Rect rect, LevelMapData data)
        {
            _canvasRect = rect;
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
            if (data == null || data.groundTexture == null) return;
            if (_composite == null) _composite = MapDataManager.Compose(data);

            Vector2 origin = rect.position + _panOffset;
            var drawRect = new Rect(origin.x, origin.y,
                data.groundTexture.width * _zoom, data.groundTexture.height * _zoom);
            DrawGrid(rect, drawRect, data.pixelsPerUnit);
            if (_composite != null) GUI.DrawTexture(drawRect, _composite, ScaleMode.StretchToFill, true);
            EditorGUI.DrawRect(drawRect, new Color(1f, 1f, 1f, 0.18f));
        }

        public void HandleInput(Event current, Rect rect, LevelMapData data, MapTool tool,
            out bool paint, out bool erase, out Vector2 pixel, out bool beginStroke)
        {
            paint = false;
            erase = false;
            pixel = Vector2.zero;
            beginStroke = false;
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
            if (!rect.Contains(current.mousePosition)) return;

            if (current.type == EventType.MouseDown && current.button == 0) _strokeRecorded = false;
            if (current.type == EventType.MouseUp && current.button == 0) _strokeRecorded = false;

            Vector2 localMouse = current.mousePosition - rect.position;
            if (current.type == EventType.ScrollWheel)
            {
                float previous = _zoom;
                _zoom = CanvasInput.ZoomBy(current, _zoom);
                Vector2 origin = _panOffset;
                Vector2 anchored = (localMouse - origin) / previous;
                _panOffset = localMouse - anchored * _zoom;
                current.Use();
                return;
            }

            bool middleDrag = CanvasInput.IsDragging(current, 1);
            bool spaceDrag = _spaceHeld && CanvasInput.IsDragging(current, 0);
            if (middleDrag || spaceDrag)
            {
                if (current.type == EventType.MouseDown) _lastMouse = localMouse;
                _panOffset += localMouse - _lastMouse;
                _lastMouse = localMouse;
                current.Use();
                return;
            }

            if (current.type != EventType.MouseDown && current.type != EventType.MouseDrag) return;
            // 画笔与擦除统一左键：IMGUI 下右键会被编辑器菜单截走
            if (current.button != 0) return;
            if (!TryScreenToPixel(localMouse, data, out pixel)) return;

            if (!_strokeRecorded)
            {
                beginStroke = true;
                _strokeRecorded = true;
            }
            if (tool == MapTool.Brush) paint = true;
            else erase = true;
            current.Use();
        }

        private static void DrawGrid(Rect rect, Rect drawRect, float ppu)
        {
            float step = Mathf.Max(4f, ppu * 1f);
            Handles.color = new Color(1f, 1f, 1f, 0.06f);
            for (float x = drawRect.x; x <= drawRect.xMax; x += step)
            {
                if (x < rect.x || x > rect.xMax) continue;
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
            }
            for (float y = drawRect.y; y <= drawRect.yMax; y += step)
            {
                if (y < rect.y || y > rect.yMax) continue;
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }
        }

        private bool TryScreenToPixel(Vector2 localMouse, LevelMapData data, out Vector2 pixel)
        {
            pixel = Vector2.zero;
            Vector2 origin = _panOffset;
            float width = data.groundTexture.width;
            float height = data.groundTexture.height;
            float x = (localMouse.x - origin.x) / _zoom;
            float y = height - (localMouse.y - origin.y) / _zoom;
            if (x < 0f || y < 0f || x >= width || y >= height) return false;
            pixel = new Vector2(x, y);
            return true;
        }
    }
}
