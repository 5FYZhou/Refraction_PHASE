using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Data
{
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Refraction/Settings/Player")]
    public class PlayerConfig : ScriptableObject
    {
        public float speed;
    }
}
