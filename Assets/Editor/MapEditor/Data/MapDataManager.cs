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
                Cache[sceneGuid] = data;
                return data;
            }
            return null;
        }

        public static void Create(LevelMapData data)
        {
            if (data == null) return;
            EnsureFolder(LevelsFolder);
            string textureFolder = TextureFolder(data.sceneName);
            EnsureFolder(textureFolder);

            data.groundTexture = SaveTexture(data.groundTexture, Path.Combine(textureFolder, "Ground.png"));
            data.paintTexture = SaveTexture(data.paintTexture, Path.Combine(textureFolder, "Paint.png"));
            data.eraseMask = SaveTexture(data.eraseMask, Path.Combine(textureFolder, "EraseMask.png"));

            AssetDatabase.CreateAsset(data, LevelAssetPath(data.sceneName));
            AssetDatabase.SaveAssets();
            Cache[data.sceneGuid] = data;
        }

        public static void Save(LevelMapData data)
        {
            if (data == null) return;
            string textureFolder = TextureFolder(data.sceneName);
            EnsureFolder(textureFolder);
            data.groundTexture = SaveTexture(data.groundTexture, Path.Combine(textureFolder, "Ground.png"));
            data.paintTexture = SaveTexture(data.paintTexture, Path.Combine(textureFolder, "Paint.png"));
            data.eraseMask = SaveTexture(data.eraseMask, Path.Combine(textureFolder, "EraseMask.png"));

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Cache[data.sceneGuid] = data;
        }

        public static void MarkDirty(LevelMapData data)
        {
            if (data != null) EditorUtility.SetDirty(data);
        }

        // 重扫只换底图：手绘层与遮罩按地图局部坐标重映射，源数据缺失时拒绝覆盖
        public static bool RebuildGround(LevelMapData data, List<MapSpriteData> sprites, Rect bounds, float ppu)
        {
            if (data == null) return false;
            bool canRemap = data.groundTexture != null && data.paintTexture != null && data.eraseMask != null
                            && data.mapBounds.width > 0f && data.mapBounds.height > 0f && data.pixelsPerUnit > 0f;
            Texture2D newPaint = canRemap
                ? RemapTexture(data.paintTexture, data.mapBounds, data.pixelsPerUnit, bounds, ppu)
                : null;
            Texture2D newMask = canRemap
                ? RemapTexture(data.eraseMask, data.mapBounds, data.pixelsPerUnit, bounds, ppu)
                : null;
            if (canRemap && (newPaint == null || newMask == null)) return false;

            Texture2D ground = MapGenerator.GenerateGround(sprites, bounds, ppu);
            data.sprites = sprites;
            data.mapBounds = bounds;
            data.pixelsPerUnit = ppu;
            data.groundTexture = ground;
            if (newPaint != null) data.paintTexture = newPaint;
            if (newMask != null) data.eraseMask = newMask;
            return true;
        }

        public static Texture2D Compose(LevelMapData data)
        {
            if (data == null || data.groundTexture == null) return null;
            int width = data.groundTexture.width;
            int height = data.groundTexture.height;
            Texture2D result = MapGenerator.CreateBlank("Composite", width, height);
            Color[] ground = data.groundTexture.GetPixels();
            Color[] paint = data.paintTexture != null && data.paintTexture.width == width && data.paintTexture.height == height
                ? data.paintTexture.GetPixels() : null;
            Color[] mask = data.eraseMask != null && data.eraseMask.width == width && data.eraseMask.height == height
                ? data.eraseMask.GetPixels() : null;
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                float maskAlpha = mask != null ? mask[i].a : 0f;
                Color baseColor = ground[i];
                baseColor.a *= 1f - maskAlpha;
                Color paintColor = paint != null ? paint[i] : new Color(0f, 0f, 0f, 0f);
                pixels[i] = new Color(
                    baseColor.r * (1f - paintColor.a) + paintColor.r * paintColor.a,
                    baseColor.g * (1f - paintColor.a) + paintColor.g * paintColor.a,
                    baseColor.b * (1f - paintColor.a) + paintColor.b * paintColor.a,
                    Mathf.Max(baseColor.a, paintColor.a));
            }
            result.SetPixels(pixels);
            result.Apply(false, false);
            return result;
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

        private static Texture2D SaveTexture(Texture2D texture, string assetPath)
        {
            if (texture == null) return null;
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && (importer.isReadable || importer.filterMode != FilterMode.Point || importer.mipmapEnabled))
            {
                importer.isReadable = true;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
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
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
