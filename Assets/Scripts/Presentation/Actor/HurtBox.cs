using Refraction.Logic.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    public sealed class HurtBox : MonoBehaviour
    {
        public IAttackable Attackable { get; private set; }

        public void SetIAttackable(in IAttackable attackable)
        {
            Attackable = attackable;
        }
    }
}
