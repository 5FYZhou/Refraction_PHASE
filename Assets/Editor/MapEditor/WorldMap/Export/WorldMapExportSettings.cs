using System;
using UnityEngine;

namespace Refraction.MapEditor
{
    [Serializable]
    public sealed class WorldMapExportSettings
    {
        public bool transparent = true;
        public Color backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        public float pixelsPerUnit = 32f;
        public float scale = 1f;
        public bool showGrid = false;
        public bool showConnections = true;
    }
}
