using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public static class MapDataManager
    {
        public const string LevelsFolder = "Assets/MapEditorData/Levels";
        public const string TexturesFolder = "Assets/MapEditorData/Textures";

        private static readonly Dictionary<string, LevelMapData> Cache = new();

        public static LevelMapData Load(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid)) return null;
            if (Cache.TryGetValue(sceneGuid, out LevelMapData cached) && cached != null) return cached;

            string[] guids = AssetDatabase.FindAssets("t:LevelMapData", new[] { LevelsFolder });
            foreach (string guid in guids)
            {
                LevelMapData data = AssetDatabase.LoadAssetAtPath<LevelMapData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null || data.sceneGuid != sceneGuid) continue;
                if (MapDataMigrator.Migrate(data)) EditorUtility.SetDirty(data);
                Cache[sceneGuid] = data;
                return data;
            }
            return null;
        }

        public static bool Exists(string sceneGuid) => Load(sceneGuid) != null;

        public static LevelMapData Create(SceneAssetInfo info)
        {
            if (info == null) return null;
            LevelMapData data = ScriptableObject.CreateInstance<LevelMapData>();
            data.sceneGuid = info.guid;
            data.sceneName = info.sceneName;
            return data;
        }

        public static void Create(LevelMapData data)
        {
            if (data == null) return;
            EnsureFolder(LevelsFolder);
            string textureFolder = TextureFolder(data.sceneName);
            EnsureFolder(textureFolder);
            SaveTextures(data, textureFolder);
            AssetDatabase.CreateAsset(data, LevelAssetPath(data.sceneName));
            AssetDatabase.SaveAssets();
            Cache[data.sceneGuid] = data;
        }

        public static void Save(LevelMapData data)
        {
            if (data == null) return;
            string textureFolder = TextureFolder(data.sceneName);
            EnsureFolder(textureFolder);
            SaveTextures(data, textureFolder);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Cache[data.sceneGuid] = data;
        }

        public static void SaveAll()
        {
            var snapshot = new List<LevelMapData>(Cache.Values);
            foreach (LevelMapData data in snapshot) Save(data);
        }

        public static void Delete(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid)) return;
            LevelMapData data = Load(sceneGuid);
            Cache.Remove(sceneGuid);
            if (data == null) return;
            string assetPath = LevelAssetPath(data.sceneName);
            if (AssetDatabase.LoadAssetAtPath<LevelMapData>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            string textureFolder = TextureFolder(data.sceneName);
            if (AssetDatabase.IsValidFolder(textureFolder)) AssetDatabase.DeleteAsset(textureFolder);
            AssetDatabase.SaveAssets();
        }

        public static void MarkDirty(LevelMapData data)
        {
            if (data == null) return;
            data.isDirty = true;
            EditorUtility.SetDirty(data);
        }

        // 扫描：只换底图；手绘层按局部坐标重映射，源数据缺失时原地保留并告警
        public static bool Scan(LevelMapData data, List<MapSpriteData> sprites, Rect bounds, float ppu, string groundHash)
        {
            if (data == null) return false;
            bool canRemap = data.groundTexture != null && data.paintTexture != null
                            && data.groundEraseMask != null && data.paintEraseMask != null
                            && data.sourceBounds.width > 0f && data.sourceBounds.height > 0f && data.pixelsPerUnit > 0f;
            Texture2D newPaint = canRemap
                ? RemapTexture(data.paintTexture, data.sourceBounds, data.pixelsPerUnit, bounds, ppu)
                : null;
            Texture2D newGroundMask = canRemap
                ? RemapTexture(data.groundEraseMask, data.sourceBounds, data.pixelsPerUnit, bounds, ppu)
                : null;
            Texture2D newPaintMask = canRemap
                ? RemapTexture(data.paintEraseMask, data.sourceBounds, data.pixelsPerUnit, bounds, ppu)
                : null;
            bool remapped = canRemap && newPaint != null && newGroundMask != null && newPaintMask != null;
            data.sourceSprites = sprites;
            data.sourceBounds = bounds;
            data.pixelsPerUnit = ppu;
            data.groundHash = groundHash;
            data.groundTexture = MapGenerator.GenerateGround(sprites, bounds, ppu);
            if (remapped)
            {
                data.paintTexture = newPaint;
                data.groundEraseMask = newGroundMask;
                data.paintEraseMask = newPaintMask;
            }
            data.isDirty = true;
            return remapped;
        }

        // 重建：底图重新扫描，手绘层与遮罩一并清空
        public static void Rebuild(LevelMapData data, List<MapSpriteData> sprites, Rect bounds, float ppu, string groundHash)
        {
            if (data == null) return;
            Vector2Int size = MapGeometry.TextureSize(bounds, ppu);
            data.sourceSprites = sprites;
            data.sourceBounds = bounds;
            data.pixelsPerUnit = ppu;
            data.groundHash = groundHash;
            data.groundTexture = MapGenerator.GenerateGround(sprites, bounds, ppu);
            data.groundEraseMask = MapGenerator.CreateBlank("GroundEraseMask", size.x, size.y);
            data.paintTexture = MapGenerator.CreateBlank("Paint", size.x, size.y);
            data.paintEraseMask = MapGenerator.CreateBlank("PaintEraseMask", size.x, size.y);
            data.isDirty = true;
        }

        public static Texture2D Compose(LevelMapData data)
        {
            return MapCompositor.Compose(data);
        }

        private static void SaveTextures(LevelMapData data, string folder)
        {
            data.groundTexture = TextureStorage.Save(data.groundTexture, Path.Combine(folder, "Ground.png"));
            data.groundEraseMask = TextureStorage.Save(data.groundEraseMask, Path.Combine(folder, "GroundEraseMask.png"));
            data.paintTexture = TextureStorage.Save(data.paintTexture, Path.Combine(folder, "Paint.png"));
            data.paintEraseMask = TextureStorage.Save(data.paintEraseMask, Path.Combine(folder, "PaintEraseMask.png"));
            data.isDirty = false;
        }

        private static Texture2D RemapTexture(Texture2D source, Rect sourceBounds, float sourcePpu, Rect targetBounds, float targetPpu)
        {
            Vector2Int size = MapGeometry.TextureSize(targetBounds, targetPpu);
            Texture2D target = MapGenerator.CreateBlank(source.name, size.x, size.y);
            var pixels = new Color[size.x * size.y];
            int sourceWidth = source.width;
            int sourceHeight = source.height;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Vector2 local = MapGeometry.PixelToMap(new Vector2(x + 0.5f, y + 0.5f), targetBounds, targetPpu);
                    Vector2 sourcePixel = MapGeometry.MapToPixel(local, sourceBounds, sourcePpu);
                    int sx = Mathf.FloorToInt(sourcePixel.x);
                    int sy = Mathf.FloorToInt(sourcePixel.y);
                    if (sx < 0 || sy < 0 || sx >= sourceWidth || sy >= sourceHeight) continue;
                    pixels[y * size.x + x] = source.GetPixel(sx, sy);
                }
            }
            target.SetPixels(pixels);
            target.Apply(false, false);
            return target;
        }

        private static string LevelAssetPath(string sceneName)
        {
            return LevelsFolder + "/" + sceneName + ".asset";
        }

        private static string TextureFolder(string sceneName)
        {
            return TexturesFolder + "/" + sceneName;
        }

        private static void EnsureFolder(string path)
        {
            TextureStorage.EnsureFolder(path);
        }
    }
}
