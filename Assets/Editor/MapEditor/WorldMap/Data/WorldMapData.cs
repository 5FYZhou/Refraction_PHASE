using System.Collections.Generic;
using UnityEngine;

namespace Refraction.MapEditor
{
    [CreateAssetMenu(menuName = "Refraction/World Map", fileName = "WorldMap")]
    public sealed class WorldMapData : ScriptableObject
    {
        public string mapName = "WorldMap";
        public List<LevelMapPlacement> levels = new();
        public List<MapConnection> connections = new();
    }
}
