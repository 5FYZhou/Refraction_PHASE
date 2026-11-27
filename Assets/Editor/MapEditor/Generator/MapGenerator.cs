using System.Collections.Generic;
using UnityEngine;

namespace Refraction.MapEditor
{
    public static class MapGenerator
    {
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        private static readonly Color Solid = new Color(1f, 1f, 1f, 1f);

        public static Texture2D GenerateGround(List<MapSpriteData> sprites, Rect bounds, float ppu)
        {
            Vector2Int size = MapGeometry.TextureSize(bounds, ppu);
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false)
            {
                name = "Ground",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size.x * size.y];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;

            if (sprites != null)
            {
                foreach (MapSpriteData sprite in sprites)
                {
                    if (sprite.corners == null || sprite.corners.Length < 4) continue;
                    Vector2[] quad = MapGeometry.CornersToPixels(sprite, bounds, ppu);
                    FillConvexQuad(pixels, size.x, size.y, quad, Solid);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static Texture2D CreateBlank(string name, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // 逐像素填凸四边形而非外接矩形：Sprite 旋转时不能画成轴对齐方块
        private static void FillConvexQuad(Color[] pixels, int width, int height, Vector2[] quad, Color color)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                if (quad[i].x < minX) minX = quad[i].x;
                if (quad[i].y < minY) minY = quad[i].y;
                if (quad[i].x > maxX) maxX = quad[i].x;
                if (quad[i].y > maxY) maxY = quad[i].y;
            }

            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(maxX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(maxY));

            float orientation = Mathf.Sign(Cross(quad[0], quad[1], quad[2]));
            if (orientation == 0f) orientation = 1f;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = true;
                    for (int edge = 0; edge < 4; edge++)
                    {
                        Vector2 a = quad[edge];
                        Vector2 b = quad[(edge + 1) % 4];
                        if (Cross(a, b, point) * orientation < 0f)
                        {
                            inside = false;
                            break;
                        }
                    }
                    if (inside) pixels[y * width + x] = color;
                }
            }
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }
    }
}
