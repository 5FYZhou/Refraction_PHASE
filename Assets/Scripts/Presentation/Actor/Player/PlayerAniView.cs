using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>玩家状态到动画参数的翻译器，改规则只在这里落笔</summary>
    /// <remarks>动画器认七个 bool（Move / Jump / Fall / Dash / Rising / Crouch / CrouchPrepare）与两个触发（Land / AirJump）；名字必须一致，改一边等于静默失效</remarks>
    public sealed class PlayerAniView : MonoBehaviour
    {
        private const string MoveParam = "Move";
        private const string JumpParam = "Jump";
        private const string FallParam = "Fall";
        private const string DashParam = "Dash";
        private const string RisingParam = "Rising";
        private const string CrouchParam = "Crouch";
        private const string CrouchPrepareParam = "CrouchPrepare";
        private const string LandParam = "Land";
        private const string AirJumpParam = "AirJump";

        private static readonly int MoveId = Animator.StringToHash(MoveParam);
        private static readonly int JumpId = Animator.StringToHash(JumpParam);
        private static readonly int FallId = Animator.StringToHash(FallParam);
        private static readonly int DashId = Animator.StringToHash(DashParam);
        private static readonly int RisingId = Animator.StringToHash(RisingParam);
        private static readonly int CrouchId = Animator.StringToHash(CrouchParam);
        private static readonly int CrouchPrepareId = Animator.StringToHash(CrouchPrepareParam);
        private static readonly int LandId = Animator.StringToHash(LandParam);
        private static readonly int AirJumpId = Animator.StringToHash(AirJumpParam);

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
        private bool _rising;
        private bool _risingApplied;
        private bool _crouch;
        private bool _crouchApplied;
        private bool _crouchPrepare;
        private bool _crouchPrepareApplied;
        private MovementStateTag _lastTag;
        private bool _wasAirborne;
        private bool _warnedNoAnimator;

        private void Awake()
        {
            if (animator != null) return;

            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);

            if (animator == null) WarnNoAnimator();
        }

        /// <summary>按状态一次写完七个参数，并在空中转地面那一帧补一次 Land 触发</summary>
        /// <remarks>七个 bool 必须同一帧一起写：过渡条件按互斥写，分开调会互相打架</remarks>
        public void PlayLocomotion(MovementStateTag tag, PostureStateTag posture, float inputX, bool hurt, float velocityY)
        {
            bool crouch = posture == PostureStateTag.Crouch;
            bool crouchPrepare = posture == PostureStateTag.CrouchingTransition;

            // 蹲姿压过跳跃与移动：与动画器里蹲姿出边排最前的口径一致
            bool crouched = crouch || crouchPrepare;
            bool dash = tag == MovementStateTag.Dash;
            bool jump = !crouched && (tag == MovementStateTag.Jump || tag == MovementStateTag.AirJump);
            bool fall = tag == MovementStateTag.Fall;
            bool move = !crouched && tag == MovementStateTag.Move && !hurt && Mathf.Abs(inputX) > 0f;
            bool rising = velocityY > 0f;

            PlayMove(move);
            PlayJump(jump);
            PlayFall(fall);
            PlayDash(dash);
            PlayRising(rising);
            PlayCrouch(crouch);
            PlayCrouchPrepare(crouchPrepare);

            // 二段跳是事件不是状态：整段都为真，只能靠标签翻转那一帧发一次
            if (tag == MovementStateTag.AirJump && _lastTag != MovementStateTag.AirJump) Trigger(AirJumpId);

            _lastTag = tag;

            // 冲刺不判落地：dash 没接 Land 的边，触发会挂到下次进 jump 误播
            if (dash) return;

            // 蹲姿同理：起身当帧不补落地，但离地记账照常推进
            if (crouched)
            {
                _wasAirborne = jump || fall;
                return;
            }

            // 落地靠空中→地面翻转判定：出生与传送直接站在地上，不会误触发
            if (_wasAirborne && !jump && !fall) Trigger(LandId);

            _wasAirborne = jump || fall;
        }

        /// <summary>直接开关移动动画：只在真的变了才写一次</summary>
        public void PlayMove(bool moving) => Write(MoveId, moving, ref _move, ref _moveApplied);

        /// <summary>直接开关跳跃动画：起跳与二段跳整段都是跳跃</summary>
        public void PlayJump(bool jumping) => Write(JumpId, jumping, ref _jump, ref _jumpApplied);

        public void PlayFall(bool falling) => Write(FallId, falling, ref _fall, ref _fallApplied);

        /// <summary>直接开关冲刺动画：单帧 clip 停在冲刺姿势</summary>
        public void PlayDash(bool dashing) => Write(DashId, dashing, ref _dash, ref _dashApplied);

        /// <summary>速度向上：上突进把 y 推成正数时也走上升姿势</summary>
        public void PlayRising(bool rising) => Write(RisingId, rising, ref _rising, ref _risingApplied);

        /// <summary>直接开关蹲姿循环：蹲着走与蹲着不动共用</summary>
        public void PlayCrouch(bool crouching) => Write(CrouchId, crouching, ref _crouch, ref _crouchApplied);

        /// <summary>直接开关下蹲前摇：单帧 clip，靠参数保持姿势直到蹲满</summary>
        public void PlayCrouchPrepare(bool preparing) => Write(CrouchPrepareId, preparing, ref _crouchPrepare, ref _crouchPrepareApplied);

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
            Debug.LogWarning($"[PlayerAniView] 找不到 Animator：不会播任何动作（需要参数 {MoveParam} / {JumpParam} / {FallParam} / {DashParam} / {RisingParam} / {CrouchParam} / {CrouchPrepareParam} / {LandParam} / {AirJumpParam}）。");
        }
    }
}
