using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>玩家状态到动画的翻译器：状态怎么变成动画参数只在这里落笔，改规则不必动 PlayerController</summary>
    /// <remarks>动画器认两个 bool：Move（跑动）与 Jump（起跳/二段跳），参数名与 Animator 里必须一致，改一边等于静默失效</remarks>
    public sealed class PlayerAniView : MonoBehaviour
    {
        private const string MoveParam = "Move";
        private const string JumpParam = "Jump";

        private static readonly int MoveId = Animator.StringToHash(MoveParam);
        private static readonly int JumpId = Animator.StringToHash(JumpParam);

        [Tooltip("播放玩家动画的动画器。留空则从自身或子物体上找（预制体里挂在 View 上）。")]
        [SerializeField]
        private Animator animator;

        private bool _move;
        private bool _moveApplied;
        private bool _jump;
        private bool _jumpApplied;
        private bool _warnedNoAnimator;

        private void Awake()
        {
            if (animator != null) return;

            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);

            if (animator == null) WarnNoAnimator();
        }

        /// <summary>按玩家状态播移动动画：地面移动与下落且有水平输入才播；受击当帧打断，落回 idle</summary>
        /// <remarks>跳跃有独立动画，跳跃期间 Move 恒为 false（标签互斥，不靠调用顺序）；冲刺不播跑动，否则等于打滑</remarks>
        public void PlayMove(MovementStateTag tag, float inputX, bool hurt)
        {
            bool moving = !hurt && Mathf.Abs(inputX) > 0f && IsLateralTag(tag);

            PlayMove(moving);
        }

        /// <summary>直接开关移动动画：参数只在真的变了才写一次，免得每帧 SetBool</summary>
        public void PlayMove(bool moving) => Write(MoveId, moving, ref _move, ref _moveApplied);

        /// <summary>按玩家状态播跳跃动画：起跳与二段跳整段都是跳跃，不区分上升下落</summary>
        public void PlayJump(MovementStateTag tag) =>
            PlayJump(tag == MovementStateTag.Jump || tag == MovementStateTag.AirJump);

        /// <summary>直接开关跳跃动画：跳跃期间由它接管画面，移动那条要另行关掉</summary>
        public void PlayJump(bool jumping) => Write(JumpId, jumping, ref _jump, ref _jumpApplied);

        private static bool IsLateralTag(MovementStateTag tag) =>
            tag == MovementStateTag.Move
            || tag == MovementStateTag.Fall;

        private void Write(int id, bool value, ref bool cache, ref bool applied)
        {
            if (applied && cache == value) return;

            applied = true;
            cache = value;

            if (animator == null)
            {
                WarnNoAnimator();
                return;
            }

            animator.SetBool(id, value);
        }

        private void WarnNoAnimator()
        {
            if (_warnedNoAnimator) return;

            _warnedNoAnimator = true;
            Debug.LogWarning($"[PlayerAniView] 找不到 Animator：只会停在 idle（需要参数 {MoveParam} / {JumpParam}）。");
        }
    }
}
