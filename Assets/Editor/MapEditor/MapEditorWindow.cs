using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.MapEditor
{
    public sealed class MapEditorWindow : EditorWindow
    {
        private const float Padding = 1f;
        private const float DefaultPpu = 32f;

        private readonly MapCanvas _canvas = new();
        private readonly MapHistory _history = new();
        private List<SceneAssetInfo> _levels = new();
        private LevelMapData _current;
        private MapTool _tool = MapTool.Brush;
        private float _radius = 10f;
        private Color _brushColor = Color.white;
        private Vector2 _listScroll;
        private string _message;

        [MenuItem("Tools/Refraction/Map Editor")]
        public static void Open()
        {
            GetWindow<MapEditorWindow>("Map Editor").Show();
        }

        private void OnEnable()
        {
            _levels = SceneScanner.ScanLevels();
            if (_levels.Count > 0) SelectLevel(_levels[0]);
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawLevelList();
            DrawCanvas();
            DrawInspector();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            HandleShortcuts(Event.current);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("扫描 / 重建", EditorStyles.toolbarButton)) ScanAllLevels();
            if (GUILayout.Button("保存", EditorStyles.toolbarButton)) SaveCurrentLevel();
            EditorGUI.BeginDisabledGroup(!_history.CanUndo);
            if (GUILayout.Button("撤销", EditorStyles.toolbarButton) && _current != null)
            {
                if (_history.Undo(_current)) _canvas.Refresh(_current);
                Repaint();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!_history.CanRedo);
            if (GUILayout.Button("重做", EditorStyles.toolbarButton) && _current != null)
            {
                if (_history.Redo(_current)) _canvas.Refresh(_current);
                Repaint();
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_current == null ? "未选择关卡" : _current.sceneName);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLevelList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(120f));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (SceneAssetInfo info in _levels)
            {
                bool selected = _current != null && _current.sceneGuid == info.guid;
                if (GUILayout.Toggle(selected, info.sceneName, EditorStyles.toolbarButton, GUILayout.Height(22f))
                    && !selected) SelectLevel(info);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawCanvas()
        {
            Rect rect = GUILayoutUtility.GetRect(320f, 320f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Event current = Event.current;
            if (_current != null)
            {
                _canvas.HandleInput(current, rect, _current, _tool, out bool paint, out bool erase,
                    out Vector2 pixel, out bool beginStroke);
                if (paint || erase)
                {
                    if (beginStroke) _history.Record(_current);
                    if (paint) BrushTool.Draw(_current.paintTexture, pixel, _radius, _brushColor);
                    else EraserTool.Erase(_current.paintTexture, _current.eraseMask, pixel, _radius);
                    _canvas.Refresh(_current);
                    MapDataManager.MarkDirty(_current);
                    Repaint();
                }
            }
            _canvas.Draw(rect, _current);
        }

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(180f));
            EditorGUILayout.LabelField("画笔", EditorStyles.boldLabel);
            _tool = (MapTool)EditorGUILayout.EnumPopup("工具", _tool);
            _radius = EditorGUILayout.FloatField("半径(px)", _radius);
            if (_tool == MapTool.Brush) _brushColor = EditorGUILayout.ColorField("颜色", _brushColor);
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("地图", EditorStyles.boldLabel);
            if (_current != null)
            {
                EditorGUILayout.LabelField("关卡", _current.sceneName);
                EditorGUILayout.LabelField("GUID", _current.sceneGuid);
                EditorGUILayout.LabelField("原点", _current.mapBounds.min.ToString());
                EditorGUILayout.LabelField("尺寸", _current.mapBounds.size.ToString());
                EditorGUILayout.LabelField("PPU", _current.pixelsPerUnit.ToString("0"));
            }
            EditorGUILayout.EndVertical();
        }

        private void HandleShortcuts(Event current)
        {
            if (current.type != EventType.KeyDown) return;
            bool command = current.control || current.command;
            if (command && current.keyCode == KeyCode.S)
            {
                SaveCurrentLevel();
                current.Use();
            }
            else if (command && current.keyCode == KeyCode.Z && _current != null)
            {
                bool success = current.shift ? _history.Redo(_current) : _history.Undo(_current);
                if (success) _canvas.Refresh(_current);
                current.Use();
            }
            else if (command && current.keyCode == KeyCode.Y && _current != null)
            {
                if (_history.Redo(_current)) _canvas.Refresh(_current);
                current.Use();
            }
        }

        private void SelectLevel(SceneAssetInfo info)
        {
            _current = MapDataManager.Load(info.guid);
            _history.Clear();
            if (_current == null)
            {
                _message = info.sceneName + " 尚无地图数据，点「扫描 / 重建」生成。";
                return;
            }
            _message = null;
            _canvas.SetData(_current, new Rect(0f, 0f, position.width, position.height));
        }

        private void SaveCurrentLevel()
        {
            if (_current == null) return;
            MapDataManager.Save(_current);
            _message = _current.sceneName + " 已保存。";
            Repaint();
        }

        private void ScanAllLevels()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string restorePath = SceneManager.GetActiveScene().path;
            List<SceneAssetInfo> levels = SceneScanner.ScanLevels();
            int created = 0;
            int rebuilt = 0;
            foreach (SceneAssetInfo info in levels)
            {
                Scene scene = EditorSceneManager.OpenScene(info.path, OpenSceneMode.Single);
                Transform ground = GroundScanner.FindGroundRoot(scene);
                if (ground == null)
                {
                    Debug.LogWarning("Map Editor: " + info.sceneName + " 没有 Ground 根节点，跳过。");
                    continue;
                }
                List<MapSpriteData> sprites = GroundScanner.Scan(ground);
                Rect bounds = MapGeometry.CalculateBounds(sprites, Padding);
                LevelMapData data = MapDataManager.Load(info.guid);
                if (data == null)
                {
                    Vector2Int size = MapGeometry.TextureSize(bounds, DefaultPpu);
                    data = CreateInstance<LevelMapData>();
                    data.sceneGuid = info.guid;
                    data.sceneName = info.sceneName;
                    data.mapBounds = bounds;
                    data.pixelsPerUnit = DefaultPpu;
                    data.sprites = sprites;
                    data.groundTexture = MapGenerator.GenerateGround(sprites, bounds, DefaultPpu);
                    data.paintTexture = MapGenerator.CreateBlank("Paint", size.x, size.y);
                    data.eraseMask = MapGenerator.CreateBlank("EraseMask", size.x, size.y);
                    MapDataManager.Create(data);
                    created++;
                }
                else if (MapDataManager.RebuildGround(data, sprites, bounds, DefaultPpu))
                {
                    MapDataManager.Save(data);
                    rebuilt++;
                }
                else
                {
                    Debug.LogWarning("Map Editor: " + info.sceneName + " 边界变化且无法可靠映射手绘层，未覆盖。");
                }
            }
            if (!string.IsNullOrEmpty(restorePath)) EditorSceneManager.OpenScene(restorePath, OpenSceneMode.Single);
            _levels = levels;
            _message = "重建完成：新增 " + created + " 关，更新 " + rebuilt + " 关。";
            if (_levels.Count > 0) SelectLevel(_levels[0]);
            Repaint();
        }
    }
}
