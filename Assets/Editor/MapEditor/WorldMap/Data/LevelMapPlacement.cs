using System;
using UnityEngine;

namespace Refraction.MapEditor
{
    [Serializable]
    public sealed class LevelMapPlacement
    {
        public string sceneGuid;
        public Vector2 position;
        public bool locked;
    }
}
