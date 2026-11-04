using System;

namespace Refraction.Logic.Level
{
    /// <summary>
    /// 关卡场景命名约定：Level_1、Level_2……
    /// 拼接、校验、取 id 全部走这里，其它地方不要自己拼字符串。
    /// </summary>
    public static class SceneName
    {
        public const string Begin = "Begin";
        public const string Game = "Level_1";

        private const string LevelPrefix = "Level_";

        public static string GetLevelName(int id)
        {
            return LevelPrefix + id.ToString();
        }

        /// <summary>场景名是否符合 Level_正整数。</summary>
        public static bool CheckLevelName(string levelName)
        {
            return TryGetLevelId(levelName, out _);
        }

        /// <summary>
        /// 从场景名解析关卡 id。合法返回 true 并写回 id，非法返回 false、id 置 0。
        /// </summary>
        public static bool TryGetLevelId(string levelName, out int id)
        {
            id = 0;

            if (string.IsNullOrEmpty(levelName)) return false;
            if (!levelName.StartsWith(LevelPrefix, StringComparison.Ordinal)) return false;

            string digits = levelName.Substring(LevelPrefix.Length);

            // 只认 ASCII 数字：int.TryParse 会放过 "+1"、" 1"、全角数字这类写法，
            // 而它们和 GetLevelName 生成的规范名不是同一个场景
            for (int i = 0; i < digits.Length; i++)
            {
                char c = digits[i];
                if (c < '0' || c > '9') return false;
            }

            // 关卡从 1 开始；digits 为空时 TryParse 自然失败
            return int.TryParse(digits, out id) && id > 0;
        }
    }
}
