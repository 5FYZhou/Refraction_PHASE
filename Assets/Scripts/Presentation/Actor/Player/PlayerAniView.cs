using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>玩家状态到动画的翻译器：状态怎么变成动画参数只在这里落笔，改规则不必动 PlayerController</summary>
    /// <remarks>动画器只认一个 bool 参数 Move（true = 移动动画，false = idle）；参数名与 Animator 里必须一致，改一边等于静默失效</remarks>
    public sealed class PlayerAniView : MonoBehaviour
    {
        private const string MoveParam = "Move";

        private static readonly int MoveId = Animator.StringToHash(MoveParam);

        [Tooltip("播放玩家动画的动画器。留空则从自身或子物体上找（预制体里挂在 View 上）。")]
        [SerializeField]
        private Animator animator;

        private bool _move;
        private bool _applied;
        private bool _warnedNoAnimator;

        private void Awake()
        {
            if (animator != null) return;

            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);

            if (animator == null) WarnNoAnimator();
        }

        /// <summary>按玩家状态播移动动画：移动、跳跃、下落且有水平输入才播；受击当帧打断，落回 idle</summary>
        /// <remarks>冲刺不播（它有自己的位移，播跑动等于打滑）；输入阈值为 0，与移动状态机判"有输入"的口径一致</remarks>
        public void PlayMove(MovementStateTag tag, float inputX, bool hurt)
        {
            bool moving = !hurt && Mathf.Abs(inputX) > 0f && IsLateralTag(tag);

            PlayMove(moving);
        }

        private static bool IsLateralTag(MovementStateTag tag) =>
            tag == MovementStateTag.Move
            || tag == MovementStateTag.Jump
            || tag == MovementStateTag.AirJump
            || tag == MovementStateTag.Fall;

        /// <summary>直接开关移动动画：参数只在真的变了才写一次，免得每帧 SetBool</summary>
        public void PlayMove(bool moving)
        {
            if (_applied && _move == moving) return;

            _applied = true;
            _move = moving;

            if (animator == null)
            {
                WarnNoAnimator();
                return;
            }

            animator.SetBool(MoveId, moving);
        }

        private void WarnNoAnimator()
        {
            if (_warnedNoAnimator) return;

            _warnedNoAnimator = true;
            Debug.LogWarning($"[PlayerAniView] 找不到 Animator：只播 idle，移动动画不会出现（参数名应为 {MoveParam}）。");
        }
    }
}
