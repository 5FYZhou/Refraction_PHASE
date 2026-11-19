using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>玩家状态到动画的翻译器：状态怎么变成动画参数只在这里落笔，改规则不必动 PlayerController</summary>
    /// <remarks>动画器认四个 bool（Move / Jump / Fall / Dash）与一个触发（Land）；名字与 Animator 里必须一致，改一边等于静默失效</remarks>
    public sealed class PlayerAniView : MonoBehaviour
    {
        private const string MoveParam = "Move";
        private const string JumpParam = "Jump";
        private const string FallParam = "Fall";
        private const string DashParam = "Dash";
        private const string LandParam = "Land";

        private static readonly int MoveId = Animator.StringToHash(MoveParam);
        private static readonly int JumpId = Animator.StringToHash(JumpParam);
        private static readonly int FallId = Animator.StringToHash(FallParam);
        private static readonly int DashId = Animator.StringToHash(DashParam);
        private static readonly int LandId = Animator.StringToHash(LandParam);

        [Tooltip("播放玩家动画的动画器。留空则从自身或子物体上找（预制体里挂在 View 上）。")]
        [SerializeField]
        private Animator animator;

        private bool _move;
        private bool _moveApplied;
        private bool _jump;
        private bool _jumpApplied;
        private bool _fall;
        private bool _fallApplied;
        private bool _dash;
        private bool _dashApplied;
        private bool _wasAirborne;
        private bool _warnedNoAnimator;

        private void Awake()
        {
            if (animator != null) return;

            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);

            if (animator == null) WarnNoAnimator();
        }

        /// <summary>按玩家状态一次写完四个参数，并在空中转地面那一帧补一次 Land 触发</summary>
        /// <remarks>四个 bool 必须同一帧一起写：过渡条件按互斥写（idle→jump 要 Move 与 Fall 都为 false），分开调会互相打架</remarks>
        public void PlayLocomotion(MovementStateTag tag, float inputX, bool hurt)
        {
            bool dash = tag == MovementStateTag.Dash;
            bool jump = tag == MovementStateTag.Jump || tag == MovementStateTag.AirJump;
            bool fall = tag == MovementStateTag.Fall;
            bool move = tag == MovementStateTag.Move && !hurt && Mathf.Abs(inputX) > 0f;

            PlayMove(move);
            PlayJump(jump);
            PlayFall(fall);
            PlayDash(dash);

            // 冲刺期间不判落地：空中转地面会置上 Land，而 dash 没有接它的边，触发会挂到下次进 jump 误播落地
            if (dash) return;

            // 落地靠标签的空中→地面翻转判定：出生与传送是直接站在地上，不会误触发
            if (_wasAirborne && !jump && !fall) Trigger(LandId);

            _wasAirborne = jump || fall;
        }

        /// <summary>直接开关移动动画：参数只在真的变了才写一次，免得每帧 SetBool</summary>
        public void PlayMove(bool moving) => Write(MoveId, moving, ref _move, ref _moveApplied);

        /// <summary>直接开关跳跃动画：起跳与二段跳整段都是跳跃，不区分上升下落</summary>
        public void PlayJump(bool jumping) => Write(JumpId, jumping, ref _jump, ref _jumpApplied);

        /// <summary>直接开关下落动画</summary>
        public void PlayFall(bool falling) => Write(FallId, falling, ref _fall, ref _fallApplied);

        /// <summary>直接开关冲刺动画：单帧 clip 停在冲刺姿势，靠 Dash 保持整段冲刺</summary>
        public void PlayDash(bool dashing) => Write(DashId, dashing, ref _dash, ref _dashApplied);

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

        private void Trigger(int id)
        {
            if (animator == null)
            {
                WarnNoAnimator();
                return;
            }

            animator.SetTrigger(id);
        }

        private void WarnNoAnimator()
        {
            if (_warnedNoAnimator) return;

            _warnedNoAnimator = true;
            Debug.LogWarning($"[PlayerAniView] 找不到 Animator：不会播任何动作（需要参数 {MoveParam} / {JumpParam} / {FallParam} / {DashParam} / {LandParam}）。");
        }
    }
}
