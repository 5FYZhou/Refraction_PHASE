using System.IO;
using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public static class TextureStorage
    {
        public static Texture2D Load(string path)
        {
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Texture2D Save(Texture2D texture, string assetPath)
        {
            if (texture == null) return null;
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            string full = Path.GetFullPath(assetPath);
            string temp = Path.Combine(Path.GetTempPath(), "MapEditor_" + System.Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(temp, texture.EncodeToPNG());
            if (File.Exists(full)) File.Delete(full);
            File.Move(temp, full);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && (importer.isReadable || importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !importer.alphaIsTransparency))
            {
                importer.isReadable = true;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        public static void Delete(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (File.Exists(assetPath)) File.Delete(assetPath);
            if (File.Exists(assetPath + ".meta")) File.Delete(assetPath + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
