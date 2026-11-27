using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public static class WorldMapRenderer
    {
        public const float GridStep = 1f;

        public static Vector2 LevelSize(LevelMapData level)
        {
            if (level == null || level.groundTexture == null) return Vector2.zero;
            float ppu = level.pixelsPerUnit > 0f ? level.pixelsPerUnit : 32f;
            return new Vector2(level.groundTexture.width / ppu, level.groundTexture.height / ppu);
        }

        public static Rect LevelRect(LevelMapData level, LevelMapPlacement placement)
        {
            Vector2 size = LevelSize(level);
            return new Rect(placement.position.x, placement.position.y, size.x, size.y);
        }

        public static LevelMapPlacement FindPlacement(WorldMapData data, string guid)
        {
            if (data == null || string.IsNullOrEmpty(guid)) return null;
            foreach (LevelMapPlacement placement in data.levels)
            {
                if (placement != null && placement.sceneGuid == guid) return placement;
            }
            return null;
        }

        public static LevelMapPlacement HitPlacement(WorldMapData data, Vector2 world)
        {
            if (data == null) return null;
            for (int i = data.levels.Count - 1; i >= 0; i--)
            {
                LevelMapPlacement placement = data.levels[i];
                if (placement == null) continue;
                LevelMapData level = MapDataManager.Load(placement.sceneGuid);
                if (level != null && LevelRect(level, placement).Contains(world)) return placement;
            }
            return null;
        }

        public static int HitConnection(WorldMapData data, Vector2 world, float radius)
        {
            if (data == null) return -1;
            for (int i = data.connections.Count - 1; i >= 0; i--)
            {
                MapConnection connection = data.connections[i];
                if (connection == null) continue;
                LevelMapPlacement from = FindPlacement(data, connection.fromSceneGuid);
                LevelMapPlacement to = FindPlacement(data, connection.toSceneGuid);
                if (from == null || to == null) continue;
                if (DistanceToSegment(world, from.position + connection.fromPosition,
                        to.position + connection.toPosition) <= radius) return i;
            }
            return -1;
        }

        public static Rect Bounds(WorldMapData data)
        {
            if (data == null || data.levels.Count == 0) return new Rect(-4f, -4f, 8f, 8f);
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (LevelMapPlacement placement in data.levels)
            {
                if (placement == null) continue;
                LevelMapData level = MapDataManager.Load(placement.sceneGuid);
                if (level == null) continue;
                Rect rect = LevelRect(level, placement);
                minX = Mathf.Min(minX, rect.xMin);
                minY = Mathf.Min(minY, rect.yMin);
                maxX = Mathf.Max(maxX, rect.xMax);
                maxY = Mathf.Max(maxY, rect.yMax);
            }
            if (minX > maxX) return new Rect(-4f, -4f, 8f, 8f);
            const float padding = 1f;
            return Rect.MinMaxRect(minX - padding, minY - padding, maxX + padding, maxY + padding);
        }

        public static void Draw(Rect rect, CanvasTransform transform, WorldMapData data,
            Dictionary<string, Texture2D> composites, string selectedGuid, int selectedConnection, bool showGrid)
        {
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
            if (data == null) return;
            if (showGrid) DrawGrid(rect, transform);

            foreach (LevelMapPlacement placement in data.levels)
            {
                if (placement == null) continue;
                LevelMapData level = MapDataManager.Load(placement.sceneGuid);
                if (level == null || level.groundTexture == null) continue;
                Rect world = LevelRect(level, placement);
                Rect screen = WorldToScreen(world, transform, rect);
                if (composites != null && composites.TryGetValue(placement.sceneGuid, out Texture2D composite)
                    && composite != null) GUI.DrawTexture(screen, composite, ScaleMode.StretchToFill, true);
                EditorGUI.DrawRect(screen, new Color(1f, 1f, 1f, 0.12f));
                if (placement.sceneGuid == selectedGuid) DrawOutline(screen, new Color(1f, 0.9f, 0.2f, 1f), 2f);
            }

            Handles.color = new Color(0.3f, 0.8f, 1f, 1f);
            for (int i = 0; i < data.connections.Count; i++)
            {
                MapConnection connection = data.connections[i];
                if (connection == null) continue;
                LevelMapPlacement from = FindPlacement(data, connection.fromSceneGuid);
                LevelMapPlacement to = FindPlacement(data, connection.toSceneGuid);
                if (from == null || to == null) continue;
                Vector2 a = WorldToScreen(from.position + connection.fromPosition, transform, rect);
                Vector2 b = WorldToScreen(to.position + connection.toPosition, transform, rect);
                Handles.DrawLine(a, b, i == selectedConnection ? 3f : 2f);
            }
        }

        public static Vector2 WorldToScreen(Vector2 world, CanvasTransform transform, Rect rect)
        {
            Vector2 canvas = transform.MapToScreen(new Vector2(world.x, -world.y));
            return canvas + rect.position;
        }

        public static Rect WorldToScreen(Rect world, CanvasTransform transform, Rect rect)
        {
            Vector2 topLeft = WorldToScreen(new Vector2(world.xMin, world.yMax), transform, rect);
            return new Rect(topLeft.x, topLeft.y, world.width * transform.Zoom, world.height * transform.Zoom);
        }

        public static Vector2 ScreenToWorld(Vector2 local, CanvasTransform transform)
        {
            Vector2 map = transform.ScreenToMap(local);
            return new Vector2(map.x, -map.y);
        }

        private static void DrawGrid(Rect rect, CanvasTransform transform)
        {
            Vector2 min = ScreenToWorld(Vector2.zero, transform);
            Vector2 max = ScreenToWorld(new Vector2(rect.width, rect.height), transform);
            Handles.color = new Color(1f, 1f, 1f, 0.08f);
            int xStart = Mathf.FloorToInt(min.x / GridStep);
            int xEnd = Mathf.CeilToInt(max.x / GridStep);
            int yStart = Mathf.FloorToInt(min.y / GridStep);
            int yEnd = Mathf.CeilToInt(max.y / GridStep);
            if ((xEnd - xStart) * (yEnd - yStart) > 40000) return;
            for (int x = xStart; x <= xEnd; x++)
            {
                Vector2 a = WorldToScreen(new Vector2(x * GridStep, min.y), transform, rect);
                Vector2 b = WorldToScreen(new Vector2(x * GridStep, max.y), transform, rect);
                Handles.DrawLine(a, b);
            }
            for (int y = yStart; y <= yEnd; y++)
            {
                Vector2 a = WorldToScreen(new Vector2(min.x, y * GridStep), transform, rect);
                Vector2 b = WorldToScreen(new Vector2(max.x, y * GridStep), transform, rect);
                Handles.DrawLine(a, b);
            }
        }

        private static void DrawOutline(Rect screen, Color color, float width)
        {
            Handles.color = color;
            Handles.DrawLine(new Vector2(screen.xMin, screen.yMin), new Vector2(screen.xMax, screen.yMin), width);
            Handles.DrawLine(new Vector2(screen.xMax, screen.yMin), new Vector2(screen.xMax, screen.yMax), width);
            Handles.DrawLine(new Vector2(screen.xMax, screen.yMax), new Vector2(screen.xMin, screen.yMax), width);
            Handles.DrawLine(new Vector2(screen.xMin, screen.yMax), new Vector2(screen.xMin, screen.yMin), width);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 0.0001f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSqr);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
