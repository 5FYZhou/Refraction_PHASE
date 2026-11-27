using System.Collections.Generic;
using UnityEngine;

namespace Refraction.MapEditor
{
    public static class MapGeometry
    {
        public static Rect CalculateBounds(List<MapSpriteData> sprites, float padding)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool any = false;
            if (sprites != null)
            {
                foreach (MapSpriteData sprite in sprites)
                {
                    if (sprite.corners == null) continue;
                    foreach (Vector2 corner in sprite.corners)
                    {
                        any = true;
                        if (corner.x < minX) minX = corner.x;
                        if (corner.y < minY) minY = corner.y;
                        if (corner.x > maxX) maxX = corner.x;
                        if (corner.y > maxY) maxY = corner.y;
                    }
                }
            }
            if (!any) return new Rect(-padding, -padding, padding * 2f, padding * 2f);
            return Rect.MinMaxRect(minX - padding, minY - padding, maxX + padding, maxY + padding);
        }

        public static Vector2 MapToPixel(Vector2 localPosition, Rect bounds, float ppu)
        {
            return new Vector2(
                (localPosition.x - bounds.xMin) * ppu,
                (localPosition.y - bounds.yMin) * ppu);
        }

        public static Vector2 PixelToMap(Vector2 pixel, Rect bounds, float ppu)
        {
            return new Vector2(
                pixel.x / ppu + bounds.xMin,
                pixel.y / ppu + bounds.yMin);
        }

        public static Vector2[] CornersToPixels(MapSpriteData sprite, Rect bounds, float ppu)
        {
            var result = new Vector2[4];
            for (int i = 0; i < 4 && i < sprite.corners.Length; i++)
            {
                result[i] = MapToPixel(sprite.corners[i], bounds, ppu);
            }
            return result;
        }

        public static Vector2Int TextureSize(Rect bounds, float ppu)
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(bounds.width * ppu));
            int height = Mathf.Max(1, Mathf.RoundToInt(bounds.height * ppu));
            return new Vector2Int(width, height);
        }
    }
}
