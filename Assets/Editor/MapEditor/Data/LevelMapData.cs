using System.Collections.Generic;
using UnityEngine;

namespace Refraction.MapEditor
{
    [CreateAssetMenu(menuName = "Map Editor/Level Map")]
    public sealed class LevelMapData : ScriptableObject
    {
        public string sceneGuid;
        public string sceneName;
        public Rect mapBounds;
        public float pixelsPerUnit = 32f;
        public List<MapSpriteData> sprites = new List<MapSpriteData>();
        public Texture2D groundTexture;
        public Texture2D paintTexture;
        public Texture2D eraseMask;
        public int dataVersion = 1;
    }
}
