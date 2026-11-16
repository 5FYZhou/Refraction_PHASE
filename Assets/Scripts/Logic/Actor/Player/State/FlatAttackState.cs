using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>普攻：前摇 → 平劈本体 → 后摇，三段全在自己身上，判定固定 Player/BlackDash/AheadCollider</summary>
    /// <remarks>数值自成一套 flatAttack*；普攻不压移动组、不挡跳跃冲刺，只有后摇能被别的攻击顶掉；
    /// 命中反冲要留到下一帧写——物理回调早过了 Commit，当场写会被 BeginStep 清掉</remarks>
    public sealed class FlatAttackState : AttackPhaseState
    {
        public override AttackStateTag Tag => AttackStateTag.FlatAttack;

        private readonly PlayerConfig _config;
        private readonly IBlackAttackView _view;
        private readonly IStateMove _motor;
        private readonly PostureGroup _posture;
        private float _face = 1f;
        private bool _pending;
        private bool _hitQueued;

        public FlatAttackState(PlayerConfig config, IBlackAttackView view = null, IStateMove motor = null, PostureGroup posture = null)
        {
            _config = config;
            _view = view;
            _motor = motor;
            _posture = posture;
        }

        protected override float PrepareTime => _config.flatAttackPrepareTime;

        protected override float ActiveTime => _config.flatAttackTime;

        protected override float RecoverTime => _config.flatAttackRecoverTime;

        public override bool BlocksMovement => false;

        public override bool BlocksActions => false;

        /// <summary>命中回报：只排队，速度留到下一帧 Tick 里写</summary>
        public void NotifyHit()
        {
            if (!_pending) return;

            _pending = false;
            _hitQueued = true;
        }

        protected override void OnPrepareEnter(in LogicContext ctx)
            => _face = ctx.WorldInfo.Face >= 0 ? 1f : -1f;

        protected override void OnActiveEnter(in LogicContext ctx)
        {
            _view?.BeginFlat();

            _pending = _config.flatAttackHitRecoil != _config.flatAttackMissRecoil;

            Apply(new Vector2(-_face * RecoilOf(_config.flatAttackMissRecoil), 0f));
        }

        /// <summary>进后摇就不再吃命中：判定盒关掉，迟到的接触不该再换反冲</summary>
        protected override void OnRecoverEnter(in LogicContext ctx)
        {
            FlushHit();
            _pending = false;
            _view?.End();
        }

        protected override void OnTick(in LogicContext ctx) => FlushHit();

        protected override void OnExit()
        {
            FlushHit();
            _pending = false;
            _view?.End();
        }

        private void FlushHit()
        {
            if (!_hitQueued) return;

            _hitQueued = false;
            Apply(new Vector2(-_face * RecoilOf(_config.flatAttackHitRecoil), 0f));
        }

        private void Apply(Vector2 recoil)
        {
            if (_motor == null || recoil.x == 0f) return;

            _motor.SetVelocityX(recoil.x);
        }

        private float RecoilOf(float speed)
            => speed * (_posture != null ? Mathf.Lerp(1f, _config.crouchRecoilScale, Mathf.Clamp01(_posture.CrouchProgress)) : 1f);
    }
}
