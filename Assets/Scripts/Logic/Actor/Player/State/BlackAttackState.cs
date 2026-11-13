using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>黑攻击本体：按 blackAttackTime 持续，到时交给后摇</summary>
    /// <remarks>有效时长是 blackAttackTime 而非一帧：进状态那一帧按竖直输入选定判定并点亮对应攻击盒，出状态时全部关掉</remarks>
    public sealed class BlackAttackState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.BlackAttack;

        private readonly PlayerConfig _config;
        private readonly IBlackAttackView _view;
        private readonly IStateMove _motor;
        private readonly PostureGroup _posture;
        private float _enterTime;
        private Vector2 _missRecoil;
        private Vector2 _hitRecoil;
        private bool _pending;
        private bool _hitQueued;

        public BlackAttackState(PlayerConfig config, IBlackAttackView view = null, IStateMove motor = null, PostureGroup posture = null)
        {
            _config = config;
            _view = view;
            _motor = motor;
            _posture = posture;
        }

        /// <summary>本次挥击选定的判定方向，供表现层与测试读取</summary>
        public BlackAttackKind Kind { get; private set; }

        public override void Enter(in LogicContext ctx)
        {
            _enterTime = ctx.GameTime.now;
            Kind = AttackAim.Kind(ctx.InputSnapshot.Move.y);
            _view?.Begin(Kind);

            _missRecoil = RecoilOf(ctx, false);
            _hitRecoil = RecoilOf(ctx, true);
            _pending = _hitRecoil != _missRecoil;

            if (_missRecoil != Vector2.zero) Apply(_missRecoil);
        }

        /// <summary>命中回报：只排队，速度留到下一帧 Tick 里写——物理回调早过了 Commit，当场写会被 BeginStep 清掉</summary>
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
            Apply(_hitRecoil);
        }

        private void Apply(Vector2 recoil)
        {
            if (_motor == null) return;

            if (recoil.x != 0f) _motor.SetVelocityX(recoil.x);
            if (recoil.y != 0f) _motor.SetVelocityY(recoil.y);
        }

        /// <summary>沿劈击反向的后坐力，乘下蹲倍率；空挥那份开打就写上，命中那份等回调再盖</summary>
        private Vector2 RecoilOf(in LogicContext ctx, bool hit)
        {
            if (_motor == null) return Vector2.zero;

            float speed = Kind switch
            {
                BlackAttackKind.Up => hit ? _config.blackAttackHitRecoilUp : _config.blackAttackMissRecoilUp,
                BlackAttackKind.Down => hit ? _config.blackAttackHitRecoilDown : _config.blackAttackMissRecoilDown,
                _ => hit ? _config.blackAttackHitRecoilAhead : _config.blackAttackMissRecoilAhead,
            };

            float scale = _posture != null ? Mathf.Lerp(1f, _config.crouchRecoilScale, Mathf.Clamp01(_posture.CrouchProgress)) : 1f;

            Vector2 push = Kind switch
            {
                BlackAttackKind.Up => new Vector2(0f, 1f),
                BlackAttackKind.Down => new Vector2(0f, -1f),
                _ => new Vector2(ctx.WorldInfo.Face >= 0 ? 1f : -1f, 0f),
            };

            return -push * (speed * scale);
        }

        public override void Tick(in LogicContext ctx) => FlushHit();

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.blackAttackTime;

        public override void Exit()
        {
            // 卡在最后一帧的命中不能丢：Exit 同样在 tick 窗口内
            FlushHit();
            _pending = false;
            _view?.End();
        }
    }
}
