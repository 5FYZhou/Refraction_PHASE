using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Refraction.MapEditor
{
    [System.Flags]
    public enum MapDirtyFlags
    {
        None = 0,
        Ground = 1 << 0,
        GroundEraseMask = 1 << 1,
        Paint = 1 << 2,
        PaintEraseMask = 1 << 3,
        All = Ground | GroundEraseMask | Paint | PaintEraseMask
    }

    [CreateAssetMenu(menuName = "Map Editor/Level Map")]
    public sealed class LevelMapData : ScriptableObject
    {
        public string sceneGuid;
        public string sceneName;

        [FormerlySerializedAs("mapBounds")]
        public Rect sourceBounds;

        public float pixelsPerUnit = 32f;

        [FormerlySerializedAs("sprites")]
        public List<MapSpriteData> sourceSprites = new List<MapSpriteData>();

        public string groundHash;

        public Texture2D groundTexture;
        [FormerlySerializedAs("eraseMask")]
        public Texture2D groundEraseMask;

        public Texture2D paintTexture;
        public Texture2D paintEraseMask;

        public Color groundColor = Color.white;
        public Color backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);

        public bool isDirty;
        public MapDirtyFlags dirtyFlags;
        public int dataVersion = 2;
    }
}
