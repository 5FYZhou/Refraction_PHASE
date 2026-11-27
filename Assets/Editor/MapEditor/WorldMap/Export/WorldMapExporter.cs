using UnityEngine;

namespace Refraction.MapEditor
{
    public static class WorldMapExporter
    {
        public static Texture2D Export(WorldMapData data, WorldMapExportSettings settings)
        {
            if (data == null || settings == null) return null;
            Rect bounds = WorldMapRenderer.Bounds(data);
            float ppu = settings.pixelsPerUnit > 0f ? settings.pixelsPerUnit : 32f;
            float scale = Mathf.Clamp(settings.scale, 0.25f, 8f);
            float density = ppu * scale;
            int width = Mathf.Clamp(Mathf.RoundToInt(bounds.width * density), 1, 8192);
            int height = Mathf.Clamp(Mathf.RoundToInt(bounds.height * density), 1, 8192);

            var pixels = new Color[width * height];
            Color fill = settings.transparent ? Color.clear : settings.backgroundColor;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;

            if (settings.showGrid) DrawGrid(pixels, width, height, bounds, density);
            DrawLevels(pixels, width, height, data, bounds, density, scale);
            if (settings.showConnections) DrawConnections(pixels, width, height, data, bounds, density);

            var result = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = data.mapName + "_WorldMap",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            result.SetPixels(pixels);
            result.Apply(false, false);
            return result;
        }

        private static void DrawLevels(Color[] pixels, int width, int height, WorldMapData data,
            Rect bounds, float density, float scale)
        {
            foreach (LevelMapPlacement placement in data.levels)
            {
                if (placement == null) continue;
                LevelMapData level = MapDataManager.Load(placement.sceneGuid);
                if (level == null || level.groundTexture == null) continue;
                Texture2D composite = MapCompositor.CreateComposite(level);
                if (composite == null) continue;
                try
                {
                    Vector2 origin = WorldToPixel(placement.position, bounds, density);
                    int x0 = Mathf.RoundToInt(origin.x);
                    int y0 = Mathf.RoundToInt(origin.y);
                    int destWidth = Mathf.Max(1, Mathf.RoundToInt(composite.width * scale));
                    int destHeight = Mathf.Max(1, Mathf.RoundToInt(composite.height * scale));
                    Color[] source = composite.GetPixels();
                    for (int y = 0; y < destHeight; y++)
                    {
                        int dy = y0 + y;
                        if (dy < 0 || dy >= height) continue;
                        int sy = Mathf.Min(composite.height - 1, Mathf.FloorToInt(y / scale));
                        for (int x = 0; x < destWidth; x++)
                        {
                            int dx = x0 + x;
                            if (dx < 0 || dx >= width) continue;
                            int sx = Mathf.Min(composite.width - 1, Mathf.FloorToInt(x / scale));
                            Blend(pixels, width, dx, dy, source[sy * composite.width + sx]);
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(composite);
                }
            }
        }

        private static void DrawConnections(Color[] pixels, int width, int height, WorldMapData data,
            Rect bounds, float density)
        {
            Color color = new Color(0.2f, 0.8f, 1f, 0.9f);
            foreach (MapConnection connection in data.connections)
            {
                if (connection == null) continue;
                LevelMapPlacement from = WorldMapRenderer.FindPlacement(data, connection.fromSceneGuid);
                LevelMapPlacement to = WorldMapRenderer.FindPlacement(data, connection.toSceneGuid);
                if (from == null || to == null) continue;
                Vector2 a = WorldToPixel(from.position + connection.fromPosition, bounds, density);
                Vector2 b = WorldToPixel(to.position + connection.toPosition, bounds, density);
                DrawLine(pixels, width, height, a, b, color);
            }
        }

        private static void DrawGrid(Color[] pixels, int width, int height, Rect bounds, float density)
        {
            Color color = new Color(1f, 1f, 1f, 0.12f);
            for (float x = Mathf.Ceil(bounds.xMin); x <= bounds.xMax; x += 1f)
            {
                int px = Mathf.RoundToInt((x - bounds.xMin) * density);
                DrawLine(pixels, width, height, new Vector2(px, 0f), new Vector2(px, height - 1), color);
            }
            for (float y = Mathf.Ceil(bounds.yMin); y <= bounds.yMax; y += 1f)
            {
                int py = Mathf.RoundToInt((y - bounds.yMin) * density);
                DrawLine(pixels, width, height, new Vector2(0f, py), new Vector2(width - 1, py), color);
            }
        }

        private static void DrawLine(Color[] pixels, int width, int height, Vector2 a, Vector2 b, Color color)
        {
            int x0 = Mathf.RoundToInt(a.x);
            int y0 = Mathf.RoundToInt(a.y);
            int x1 = Mathf.RoundToInt(b.x);
            int y1 = Mathf.RoundToInt(b.y);
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int stepX = x0 < x1 ? 1 : -1;
            int stepY = y0 < y1 ? 1 : -1;
            int error = dx - dy;
            while (true)
            {
                if (x0 >= 0 && y0 >= 0 && x0 < width && y0 < height) Blend(pixels, width, x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int doubled = error * 2;
                if (doubled > -dy) { error -= dy; x0 += stepX; }
                if (doubled < dx) { error += dx; y0 += stepY; }
            }
        }

        private static void Blend(Color[] pixels, int width, int x, int y, Color source)
        {
            int index = y * width + x;
            Color destination = pixels[index];
            float alpha = source.a + destination.a * (1f - source.a);
            if (alpha <= 0f)
            {
                pixels[index] = Color.clear;
                return;
            }
            Color result = (source * source.a + destination * destination.a * (1f - source.a)) / alpha;
            result.a = alpha;
            pixels[index] = result;
        }

        private static Vector2 WorldToPixel(Vector2 world, Rect bounds, float density)
        {
            return new Vector2((world.x - bounds.xMin) * density, (world.y - bounds.yMin) * density);
        }
    }
}
