using System;
using UnityEngine;

namespace Refraction.MapEditor
{
    [Serializable]
    public sealed class MapSpriteData
    {
        public string hierarchyPath;
        public Vector2[] corners = new Vector2[4];
        public string spriteGuid;
        public long localFileId;
        public Vector2 localPosition;
        public Vector3 localRotation;
        public Vector3 localScale;
        public Color color = Color.white;
        public bool flipX;
        public bool flipY;

        public MapSpriteData()
        {
        }

        public MapSpriteData(string path, Vector2[] points)
        {
            hierarchyPath = path;
            corners = points;
        }
    }
}
