using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class WorldMapEditorWindow : EditorWindow
    {
        private const string PrefKey = "Refraction.WorldMap.Path";
        private const string DefaultFolder = "Assets/MapEditorData/WorldMaps";

        private readonly WorldMapCanvas _canvas = new();
        private readonly WorldMapExportSettings _exportSettings = new();
        private List<SceneAssetInfo> _levels = new();
        private WorldMapData _world;
        private WorldTool _tool = WorldTool.Select;
        private bool _snap = true;
        private bool _showGrid = true;
        private Vector2 _listScroll;
        private string _message;

        [MenuItem("Tools/Refraction/World Map Editor")]
        public static void Open()
        {
            GetWindow<WorldMapEditorWindow>("World Map Editor").Show();
        }

        private void OnEnable()
        {
            string path = EditorPrefs.GetString(PrefKey, string.Empty);
            if (!string.IsNullOrEmpty(path)) LoadWorld(path);
            if (_world == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:WorldMapData", new[] { DefaultFolder });
                if (guids.Length > 0) LoadWorld(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            _levels = SceneScanner.ScanLevels();
        }

        private void OnDisable()
        {
            _canvas.Dispose();
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
            WorldMapData selected = (WorldMapData)EditorGUILayout.ObjectField(_world, typeof(WorldMapData), false,
                GUILayout.Width(160f));
            if (selected != _world) LoadWorld(selected == null ? null : AssetDatabase.GetAssetPath(selected));
            if (GUILayout.Button("新建", EditorStyles.toolbarButton)) CreateWorld();
            if (GUILayout.Button("保存", EditorStyles.toolbarButton)) SaveWorld();
            _tool = (WorldTool)EditorGUILayout.EnumPopup(_tool, EditorStyles.toolbarPopup, GUILayout.Width(90f));
            _snap = GUILayout.Toggle(_snap, "吸附", EditorStyles.toolbarButton, GUILayout.Width(44f));
            _showGrid = GUILayout.Toggle(_showGrid, "网格", EditorStyles.toolbarButton, GUILayout.Width(44f));
            if (GUILayout.Button("适应画布", EditorStyles.toolbarButton)) _canvas.Fit(CurrentCanvasRect(), _world);
            if (GUILayout.Button("导出 PNG", EditorStyles.toolbarButton)) ExportPng();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_world == null ? "未创建世界地图" : _world.mapName);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLevelList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(140f));
            EditorGUILayout.LabelField("关卡", EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (SceneAssetInfo info in _levels)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(info.sceneName, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("+", GUILayout.Width(22f))) AddLevel(info);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawCanvas()
        {
            Rect rect = GUILayoutUtility.GetRect(320f, 320f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_world != null)
            {
                _canvas.HandleInput(Event.current, rect, _world, _tool, _snap, out bool changed, out string message);
                if (changed)
                {
                    EditorUtility.SetDirty(_world);
                    Repaint();
                }
                if (!string.IsNullOrEmpty(message)) _message = message;
            }
            _canvas.Draw(rect, _world, _showGrid);
        }

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210f));
            EditorGUILayout.LabelField("导出设置", EditorStyles.boldLabel);
            _exportSettings.transparent = EditorGUILayout.Toggle("透明背景", _exportSettings.transparent);
            if (!_exportSettings.transparent)
                _exportSettings.backgroundColor = EditorGUILayout.ColorField("背景颜色", _exportSettings.backgroundColor);
            _exportSettings.pixelsPerUnit = EditorGUILayout.FloatField("PPU", _exportSettings.pixelsPerUnit);
            _exportSettings.scale = EditorGUILayout.FloatField("倍率", _exportSettings.scale);
            _exportSettings.showGrid = EditorGUILayout.Toggle("导出网格", _exportSettings.showGrid);
            _exportSettings.showConnections = EditorGUILayout.Toggle("导出连接线", _exportSettings.showConnections);
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("世界地图", EditorStyles.boldLabel);
            if (_world == null)
            {
                EditorGUILayout.HelpBox("先新建或选择一张世界地图。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            _world.mapName = EditorGUILayout.TextField("名称", _world.mapName);
            EditorGUILayout.LabelField("关卡数", _world.levels.Count.ToString());
            EditorGUILayout.LabelField("连接数", _world.connections.Count.ToString());
            DrawSelection();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelection()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("选中", EditorStyles.boldLabel);
            if (_canvas.SelectedConnection >= 0 && _canvas.SelectedConnection < _world.connections.Count)
            {
                MapConnection connection = _world.connections[_canvas.SelectedConnection];
                EditorGUILayout.LabelField("连接", LevelName(connection.fromSceneGuid) + " → " + LevelName(connection.toSceneGuid));
                if (GUILayout.Button("删除连接")) RemoveConnection(_canvas.SelectedConnection);
                return;
            }
            if (string.IsNullOrEmpty(_canvas.SelectedGuid)) return;
            LevelMapPlacement placement = WorldMapRenderer.FindPlacement(_world, _canvas.SelectedGuid);
            if (placement == null) return;
            Vector2 position = EditorGUILayout.Vector2Field("位置", placement.position);
            if (position != placement.position)
            {
                placement.position = position;
                EditorUtility.SetDirty(_world);
                Repaint();
            }
            bool locked = EditorGUILayout.Toggle("锁定位置", placement.locked);
            if (locked != placement.locked)
            {
                placement.locked = locked;
                EditorUtility.SetDirty(_world);
            }
            if (GUILayout.Button("移出世界地图")) RemovePlacement(placement);
        }

        private Rect CurrentCanvasRect()
        {
            return new Rect(140f, 20f, Mathf.Max(320f, position.width - 370f), Mathf.Max(320f, position.height - 60f));
        }

        private void HandleShortcuts(Event current)
        {
            if (current.type != EventType.KeyDown) return;
            if ((current.control || current.command) && current.keyCode == KeyCode.S)
            {
                SaveWorld();
                current.Use();
            }
        }

        private void LoadWorld(string path)
        {
            _world = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<WorldMapData>(path);
            if (_world != null) EditorPrefs.SetString(PrefKey, path);
            else EditorPrefs.DeleteKey(PrefKey);
            _message = null;
            Repaint();
        }

        private void CreateWorld()
        {
            TextureStorage.EnsureFolder(DefaultFolder);
            string path = EditorUtility.SaveFilePanelInProject("新建世界地图", "WorldMap", "asset", "选择保存位置", DefaultFolder);
            if (string.IsNullOrEmpty(path)) return;
            WorldMapData data = CreateInstance<WorldMapData>();
            data.mapName = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            LoadWorld(path);
            _message = "已创建世界地图：" + path;
        }

        private void SaveWorld()
        {
            if (_world == null) return;
            EditorUtility.SetDirty(_world);
            AssetDatabase.SaveAssets();
            _message = _world.mapName + " 已保存。";
            Repaint();
        }

        private void AddLevel(SceneAssetInfo info)
        {
            if (_world == null)
            {
                _message = "先新建或选择一张世界地图。";
                return;
            }
            if (WorldMapRenderer.FindPlacement(_world, info.guid) != null)
            {
                _message = info.sceneName + " 已在世界地图中。";
                return;
            }
            var placement = new LevelMapPlacement
            {
                sceneGuid = info.guid,
                position = new Vector2(_world.levels.Count * 8f, 0f)
            };
            _world.levels.Add(placement);
            EditorUtility.SetDirty(_world);
            _message = info.sceneName + " 已添加，拖动调整位置。";
            Repaint();
        }

        private void RemovePlacement(LevelMapPlacement placement)
        {
            string guid = placement.sceneGuid;
            _world.levels.Remove(placement);
            _world.connections.RemoveAll(connection => connection != null
                && (connection.fromSceneGuid == guid || connection.toSceneGuid == guid));
            EditorUtility.SetDirty(_world);
            Repaint();
        }

        private void RemoveConnection(int index)
        {
            if (index < 0 || index >= _world.connections.Count) return;
            _world.connections.RemoveAt(index);
            EditorUtility.SetDirty(_world);
            Repaint();
        }

        private string LevelName(string guid)
        {
            foreach (SceneAssetInfo info in _levels)
            {
                if (info.guid == guid) return info.sceneName;
            }
            return guid;
        }

        private void ExportPng()
        {
            if (_world == null)
            {
                _message = "先新建或选择一张世界地图。";
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("导出世界地图", _world.mapName + ".png", "png", "选择导出位置", DefaultFolder);
            if (string.IsNullOrEmpty(path)) return;
            Texture2D texture = WorldMapExporter.Export(_world, _exportSettings);
            if (texture == null)
            {
                _message = "导出失败：世界地图为空。";
                return;
            }
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            _message = "已导出：" + path;
            Repaint();
        }
    }
}
