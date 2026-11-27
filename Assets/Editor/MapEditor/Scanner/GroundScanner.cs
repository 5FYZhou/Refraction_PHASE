using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.MapEditor
{
    [Serializable]
    public sealed class MapSpriteData
    {
        public string hierarchyPath;
        public Vector2[] corners = new Vector2[4];

        public MapSpriteData()
        {
        }

        public MapSpriteData(string path, Vector2[] points)
        {
            hierarchyPath = path;
            corners = points;
        }
    }

    public static class GroundScanner
    {
        public static Transform FindGroundRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root.name == "Ground") return root.transform;
            }
            return null;
        }

        public static List<SpriteRenderer> CollectSprites(Transform groundRoot)
        {
            var result = new List<SpriteRenderer>();
            if (groundRoot == null) return result;
            SpriteRenderer[] found = groundRoot.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in found)
            {
                if (renderer.sprite == null) continue;
                result.Add(renderer);
            }
            return result;
        }

        public static List<MapSpriteData> Scan(Transform groundRoot)
        {
            var result = new List<MapSpriteData>();
            if (groundRoot == null) return result;

            Matrix4x4 toGround = groundRoot.worldToLocalMatrix;
            List<SpriteRenderer> sprites = CollectSprites(groundRoot);
            foreach (SpriteRenderer renderer in sprites)
            {
                Bounds bounds = renderer.localBounds;
                Vector3[] localCorners =
                {
                    new Vector3(bounds.min.x, bounds.min.y, 0f),
                    new Vector3(bounds.min.x, bounds.max.y, 0f),
                    new Vector3(bounds.max.x, bounds.max.y, 0f),
                    new Vector3(bounds.max.x, bounds.min.y, 0f)
                };
                Matrix4x4 matrix = toGround * renderer.transform.localToWorldMatrix;
                var corners = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(localCorners[i]);
                    corners[i] = new Vector2(point.x, point.y);
                }
                result.Add(new MapSpriteData(RelativePath(renderer.transform, groundRoot), corners));
            }
            return result;
        }

        private static string RelativePath(Transform target, Transform root)
        {
            var segments = new List<string>();
            Transform cursor = target;
            while (cursor != null && cursor != root)
            {
                segments.Add(cursor.name);
                cursor = cursor.parent;
            }
            segments.Reverse();
            var builder = new StringBuilder();
            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0) builder.Append('/');
                builder.Append(segments[i]);
            }
            return builder.ToString();
        }
    }
}
