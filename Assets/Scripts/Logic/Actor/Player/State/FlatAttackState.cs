using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>普攻本体：黑形态的平劈，判定固定 Player/BlackDash/AheadCollider，时长 flatAttackTime</summary>
    /// <remarks>数值与黑突进各成一套（flatAttack*）；命中反冲要留到下一帧写——物理回调早过了 Commit，当场写会被 BeginStep 清掉</remarks>
    public sealed class FlatAttackState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.FlatAttack;

        private readonly PlayerConfig _config;
        private readonly IBlackAttackView _view;
        private readonly IStateMove _motor;
        private readonly PostureGroup _posture;
        private float _enterTime;
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

        public override void Enter(in LogicContext ctx)
        {
            _enterTime = ctx.GameTime.now;
            _face = ctx.WorldInfo.Face >= 0 ? 1f : -1f;

            _view?.BeginFlat();

            _pending = _config.flatAttackHitRecoil != _config.flatAttackMissRecoil;

            Apply(new Vector2(-_face * RecoilOf(_config.flatAttackMissRecoil), 0f));
        }

        /// <summary>命中回报：只排队，速度留到下一帧 Tick 里写</summary>
        public void NotifyHit()
        {
            if (!_pending) return;

            _pending = false;
            _hitQueued = true;
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

        public override void Tick(in LogicContext ctx) => FlushHit();

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.flatAttackTime;

        public override void Exit()
        {
            // 卡在最后一帧的命中不能丢：Exit 同样在 tick 窗口内
            FlushHit();
            _pending = false;
            _view?.End();
        }
    }
}
