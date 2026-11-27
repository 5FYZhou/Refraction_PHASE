using System;
using UnityEngine;

namespace Refraction.MapEditor
{
    [Serializable]
    public sealed class MapConnection
    {
        public string fromSceneGuid;
        public string toSceneGuid;
        public Vector2 fromPosition;
        public Vector2 toPosition;
    }
}
