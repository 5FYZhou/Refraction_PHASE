using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Refraction.MapEditor
{
    public static class GroundHashCalculator
    {
        public static string CalculateSpriteHash(MapSpriteData sprite)
        {
            if (sprite == null) return string.Empty;
            var builder = new StringBuilder();
            builder.Append(sprite.spriteGuid).Append('|');
            builder.Append(sprite.localFileId.ToString(CultureInfo.InvariantCulture)).Append('|');
            AppendVector2(builder, sprite.localPosition);
            AppendVector3(builder, sprite.localRotation);
            AppendVector3(builder, sprite.localScale);
            builder.Append(sprite.color.r.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(sprite.color.g.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(sprite.color.b.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(sprite.color.a.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            builder.Append(sprite.flipX ? 1 : 0).Append(sprite.flipY ? 1 : 0).Append('|');
            if (sprite.corners != null)
            {
                foreach (Vector2 corner in sprite.corners) AppendVector2(builder, corner);
            }
            return Digest(builder.ToString());
        }

        public static string CalculateGroundHash(List<MapSpriteData> sprites)
        {
            if (sprites == null || sprites.Count == 0) return string.Empty;
            var sorted = new List<MapSpriteData>(sprites);
            sorted.Sort((a, b) => string.CompareOrdinal(a.hierarchyPath, b.hierarchyPath));
            var builder = new StringBuilder();
            foreach (MapSpriteData sprite in sorted) builder.Append(CalculateSpriteHash(sprite)).Append(';');
            return Digest(builder.ToString());
        }

        private static void AppendVector2(StringBuilder builder, Vector2 value)
        {
            builder.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static void AppendVector3(StringBuilder builder, Vector3 value)
        {
            builder.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static string Digest(string source)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(source));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte item in hash) builder.Append(item.ToString("x2"));
            return builder.ToString();
        }
    }
}
