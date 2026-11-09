using Refraction.Foundation;
using Refraction.Presentation.Actor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    public class CombatRoot : MonoBehaviour, ISceneRoot
    {
        public int Order => SceneOrder.World;

        [SerializeField]
        private PlayerController playerController;

        public void Attach()
        {
            Require.Assigned(playerController, nameof(GameRoot), nameof(playerController));
        }
    }
}
