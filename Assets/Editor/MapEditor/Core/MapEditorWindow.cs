using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.MapEditor
{
    public sealed class MapEditorWindow : EditorWindow
    {
        private enum ScanScope
        {
            Current,
            Unimported,
            All
        }

        private const float Padding = 1f;
        private const float DefaultPpu = 32f;

        private readonly MapCanvas _canvas = new();
        private readonly MapHistory _history = new();
        private readonly ToolManager _tools = new();
        private readonly MapAutoSave _autoSave = new();
        private List<SceneAssetInfo> _levels = new();
        private LevelMapData _current;
        private ScanScope _scanScope = ScanScope.Current;
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
            _autoSave.LoadEnabled();
            EditorApplication.update += OnUpdate;
            EditorApplication.quitting += OnQuitting;
            _levels = SceneScanner.ScanLevels();
            if (_levels.Count > 0) SelectLevel(_levels[0]);
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.quitting -= OnQuitting;
            _autoSave.Flush();
            if (_autoSave.Enabled) MapDataManager.SaveAll();
            _canvas.Dispose();
        }

        private void OnUpdate()
        {
            _autoSave.SavePending();
        }

        private void OnQuitting()
        {
            _autoSave.Flush();
            if (_autoSave.Enabled) MapDataManager.SaveAll();
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
            _scanScope = (ScanScope)EditorGUILayout.EnumPopup(_scanScope, EditorStyles.toolbarPopup, GUILayout.Width(100f));
            if (GUILayout.Button("扫描", EditorStyles.toolbarButton)) ScanAllLevels(false);
            if (GUILayout.Button("重建", EditorStyles.toolbarButton)) ScanAllLevels(true);
            if (GUILayout.Button("保存", EditorStyles.toolbarButton)) SaveCurrentLevel();
            if (GUILayout.Button("保存全部", EditorStyles.toolbarButton)) SaveAllLevels();
            bool autoSave = EditorGUILayout.ToggleLeft("自动保存", _autoSave.Enabled, GUILayout.Width(82f));
            if (autoSave != _autoSave.Enabled) _autoSave.SetEnabled(autoSave);
            EditorGUI.BeginDisabledGroup(!_history.CanUndo);
            if (GUILayout.Button("撤销", EditorStyles.toolbarButton)) Undo();
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!_history.CanRedo);
            if (GUILayout.Button("重做", EditorStyles.toolbarButton)) Redo();
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
                _canvas.HandleInput(current, rect, _current, _tools, _radius, _brushColor, out bool changed);
                IMapCommand command = _canvas.TakeCommand();
                if (command != null)
                {
                    _history.Push(command);
                    _autoSave.MarkDirty(_current, GetDirtyFlags(command));
                }
                if (changed)
                {
                    _canvas.Refresh(_current);
                    Repaint();
                }
            }
            _canvas.Draw(rect, _current);
            _canvas.DrawPreview(rect, _tools, _current, _radius, _brushColor);
        }

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(180f));
            EditorGUILayout.LabelField("画笔", EditorStyles.boldLabel);
            _tools.Select((MapTool)EditorGUILayout.EnumPopup("工具", _tools.Current));
            _radius = EditorGUILayout.FloatField("半径(px)", _radius);
            if (_tools.Current == MapTool.Brush) _brushColor = EditorGUILayout.ColorField("颜色", _brushColor);
            if (GUILayout.Button("适应画布 (F)")) _canvas.Fit(CurrentCanvasRect(), _current);
            EditorGUILayout.LabelField("缩放", (_canvas.Zoom * 100f).ToString("0") + "%");
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("地图", EditorStyles.boldLabel);
            if (_current != null)
            {
                _current.groundColor = EditorGUILayout.ColorField("地形颜色", _current.groundColor);
                _current.backgroundColor = EditorGUILayout.ColorField("背景颜色", _current.backgroundColor);
                EditorGUILayout.LabelField("关卡", _current.sceneName);
                EditorGUILayout.LabelField("原点", _current.sourceBounds.min.ToString());
                EditorGUILayout.LabelField("尺寸", _current.sourceBounds.size.ToString());
                EditorGUILayout.LabelField("PPU", _current.pixelsPerUnit.ToString("0"));
            }
            EditorGUILayout.EndVertical();
        }

        private Rect CurrentCanvasRect()
        {
            return new Rect(120f, 20f, Mathf.Max(320f, position.width - 320f), Mathf.Max(320f, position.height - 60f));
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
            else if (command && current.keyCode == KeyCode.Z)
            {
                if (current.shift) Redo(); else Undo();
                current.Use();
            }
            else if (command && current.keyCode == KeyCode.Y)
            {
                Redo();
                current.Use();
            }
        }

        private MapDirtyFlags GetDirtyFlags(IMapCommand command)
        {
            if (_current == null) return MapDirtyFlags.None;
            if (!(command is PixelCommand pixel)) return MapDirtyFlags.All;
            MapDirtyFlags flags = MapDirtyFlags.None;
            foreach (Texture2D texture in pixel.EditedTextures)
            {
                if (texture == _current.groundTexture) flags |= MapDirtyFlags.Ground;
                else if (texture == _current.groundEraseMask) flags |= MapDirtyFlags.GroundEraseMask;
                else if (texture == _current.paintTexture) flags |= MapDirtyFlags.Paint;
                else if (texture == _current.paintEraseMask) flags |= MapDirtyFlags.PaintEraseMask;
            }
            return flags == MapDirtyFlags.None ? MapDirtyFlags.All : flags;
        }

        private void Undo()
        {
            if (_current == null) return;
            IMapCommand command = _history.Undo();
            if (command == null) return;
            _canvas.Refresh(_current);
            _autoSave.MarkDirty(_current, GetDirtyFlags(command));
            Repaint();
        }

        private void Redo()
        {
            if (_current == null) return;
            IMapCommand command = _history.Redo();
            if (command == null) return;
            _canvas.Refresh(_current);
            _autoSave.MarkDirty(_current, GetDirtyFlags(command));
            Repaint();
        }

        private void SelectLevel(SceneAssetInfo info)
        {
            if (_autoSave.Enabled && _current != null && _current.isDirty) _autoSave.RequestSave(_current);
            _current = MapDataManager.Load(info.guid);
            _history.Clear();
            if (_current == null)
            {
                _message = info.sceneName + " 尚无地图数据，点「扫描」或「重建」生成。";
                return;
            }
            _message = null;
            _canvas.SetData(_current, CurrentCanvasRect());
        }

        private void SaveCurrentLevel()
        {
            if (_current == null) return;
            _autoSave.RequestSave(_current);
            _message = _current.sceneName + " 已保存。";
            Repaint();
        }

        private void SaveAllLevels()
        {
            _autoSave.Flush();
            MapDataManager.SaveAll();
            _message = "全部关卡已保存。";
            Repaint();
        }

        private void ScanAllLevels(bool clearEdits)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string restorePath = SceneManager.GetActiveScene().path;
            List<SceneAssetInfo> levels = SceneScanner.ScanLevels();
            List<SceneAssetInfo> targets = FilterTargets(levels);
            if (targets.Count == 0)
            {
                _message = "没有需要处理的关卡。";
                Repaint();
                return;
            }

            int created = 0;
            int updated = 0;
            int skipped = 0;
            foreach (SceneAssetInfo info in targets)
            {
                Scene scene = EditorSceneManager.OpenScene(info.path, OpenSceneMode.Single);
                Transform ground = GroundScanner.FindGroundRoot(scene);
                if (ground == null)
                {
                    Debug.LogWarning("Map Editor: " + info.sceneName + " 没有 Ground 根节点，跳过。");
                    skipped++;
                    continue;
                }
                List<MapSpriteData> sprites = GroundScanner.Scan(ground);
                Rect bounds = MapGeometry.CalculateBounds(sprites, Padding);
                string hash = GroundHashCalculator.CalculateGroundHash(sprites);
                LevelMapData data = MapDataManager.Load(info.guid);
                bool isNew = data == null;
                if (isNew)
                {
                    data = MapDataManager.Create(info);
                    MapDataManager.Rebuild(data, sprites, bounds, DefaultPpu, hash);
                    MapDataManager.Create(data);
                    created++;
                }
                else
                {
                    if (!clearEdits && MapDataSynchronizer.HasChanged(data, sprites))
                    {
                        GroundChangeAction action = MapDataSynchronizer.Ask(info.sceneName);
                        if (action == GroundChangeAction.Keep)
                        {
                            skipped++;
                            continue;
                        }
                        if (action == GroundChangeAction.RebuildAll)
                        {
                            MapDataManager.Rebuild(data, sprites, bounds, DefaultPpu, hash);
                        }
                        else if (!MapDataManager.Scan(data, sprites, bounds, DefaultPpu, hash))
                        {
                            Debug.LogWarning("Map Editor: " + info.sceneName + " 边界变化且无法可靠映射手绘层，未更新。");
                            skipped++;
                            continue;
                        }
                    }
                    else if (clearEdits)
                    {
                        MapDataManager.Rebuild(data, sprites, bounds, DefaultPpu, hash);
                    }
                    else if (!MapDataManager.Scan(data, sprites, bounds, DefaultPpu, hash))
                    {
                        Debug.LogWarning("Map Editor: " + info.sceneName + " 边界变化且无法可靠映射手绘层，未更新。");
                        skipped++;
                        continue;
                    }
                    MapDataManager.Save(data);
                    updated++;
                    if (_current != null && _current.sceneGuid == info.guid)
                    {
                        _history.Clear();
                        _canvas.SetData(_current, CurrentCanvasRect());
                    }
                }
            }
            if (!string.IsNullOrEmpty(restorePath)) EditorSceneManager.OpenScene(restorePath, OpenSceneMode.Single);
            _levels = levels;
            string actionName = clearEdits ? "重建" : "扫描";
            string extra = clearEdits ? "（画笔与橡皮已清空）" : "（绘制内容保留）";
            _message = actionName + "完成：新增 " + created + " 关，更新 " + updated + " 关" + extra
                       + (skipped > 0 ? "，跳过 " + skipped + " 关。" : "。");
            if (_current == null && _levels.Count > 0) SelectLevel(_levels[0]);
            Repaint();
        }

        private List<SceneAssetInfo> FilterTargets(List<SceneAssetInfo> levels)
        {
            var result = new List<SceneAssetInfo>();
            if (_scanScope == ScanScope.All)
            {
                result.AddRange(levels);
                return result;
            }
            if (_scanScope == ScanScope.Unimported)
            {
                foreach (SceneAssetInfo info in levels)
                {
                    if (!MapDataManager.Exists(info.guid)) result.Add(info);
                }
                return result;
            }

            string activeScene = SceneManager.GetActiveScene().name;
            foreach (SceneAssetInfo info in levels)
            {
                bool selected = _current != null
                    ? _current.sceneGuid == info.guid
                    : info.sceneName == activeScene;
                if (selected) result.Add(info);
            }
            return result;
        }
    }
}
