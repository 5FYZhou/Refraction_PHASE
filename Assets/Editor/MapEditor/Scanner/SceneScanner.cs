using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Refraction.MapEditor
{
    public sealed class SceneAssetInfo
    {
        public string guid;
        public string path;
        public string sceneName;
        public int levelNumber;
    }

    public static class SceneScanner
    {
        private static readonly Regex LevelName = new(@"^Level_([1-9]\d*)$", RegexOptions.Compiled);

        // 只查 AssetDatabase，不切场景：切场景会丢用户未保存的修改
        public static List<SceneAssetInfo> ScanLevels()
        {
            var result = new List<SceneAssetInfo>();
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!IsValidLevelName(name)) continue;
                result.Add(new SceneAssetInfo
                {
                    guid = guid,
                    path = path,
                    sceneName = name,
                    levelNumber = GetLevelNumber(name)
                });
            }
            result.Sort(CompareByLevelNumber);
            return result;
        }

        public static bool IsValidLevelName(string name)
        {
            return !string.IsNullOrEmpty(name) && LevelName.IsMatch(name);
        }

        public static int GetLevelNumber(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            Match match = LevelName.Match(name);
            return match.Success && int.TryParse(match.Groups[1].Value, out int number) ? number : 0;
        }

        private static int CompareByLevelNumber(SceneAssetInfo a, SceneAssetInfo b)
        {
            return a.levelNumber.CompareTo(b.levelNumber);
        }
    }
}
