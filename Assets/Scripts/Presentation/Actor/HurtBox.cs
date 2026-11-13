using Refraction.Logic.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>受击盒：只负责"这个碰撞体属于谁"，扣血与击退由挂上来的 IHurtable 决定</summary>
    public sealed class HurtBox : MonoBehaviour
    {
        public IHurtable Attackable { get; private set; }

        public void SetIAttackable(in IHurtable attackable)
        {
            Attackable = attackable;
        }
    }
}
