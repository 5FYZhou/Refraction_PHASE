using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public enum MovementStateTag
    {
        None = 0,
        Idle,
        Move,
        Jump,
        AirJump,
        Fall,
        Dash
    }

    /// <summary>姿态：与移动正交的一组状态，站着还是蹲着。</summary>
    public enum PostureStateTag
    {
        None = 0,
        Stand,

        /// <summary>已完全蹲下，限制生效</summary>
        Crouch,

        /// <summary>下蹲前摇：身体与移速线性过渡到蹲姿，可被打断</summary>
        CrouchingTransition
    }
}
