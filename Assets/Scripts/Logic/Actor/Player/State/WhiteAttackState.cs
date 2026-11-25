using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>开火那一帧：按朝向与上下输入发射，出膛点取两个开火点插值，后坐力沿弹道反向叠加</summary>
    /// <remarks>IsDone 恒 true，Enter 副作用每周期只一次；速度与伤害只读 PlayerConfig</remarks>
    public sealed class WhiteAttackState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.WhiteAttack;

        private readonly PlayerConfig _config;
        private readonly IStateMove _motor;
        private readonly PostureGroup _posture;

        private IBulletLauncher _launcher;
        private Vector2 _muzzleStand;
        private Vector2 _muzzleCrouch;

        public WhiteAttackState(PlayerConfig config, IStateMove motor, IBulletLauncher launcher, PostureGroup posture)
        {
            _config = config;
            _motor = motor;
            _launcher = launcher;
            _posture = posture;
        }

        public void Bind(IBulletLauncher launcher) => _launcher = launcher;

        public void BindMuzzle(Vector2 stand, Vector2 crouch)
        {
            _muzzleStand = stand;
            _muzzleCrouch = crouch;
        }

        public override void Enter(in LogicContext ctx)
        {
            Vector2 direction = AttackAim.Direction(ctx.WorldInfo.Face, ctx.InputSnapshot.Move.y);

            _launcher?.Launch(new BulletSpawn(
                Muzzle(ctx.WorldInfo.Pos, ctx.WorldInfo.Face), direction, _config.bulletSpeed, _config.bulletDamage,
                _config.bulletKnockback, _config.bulletTransferSpeed));
            _motor.AddExternalVelocity(AttackAim.RecoilSpeed(direction, _config, _posture.CrouchProgress));
        }

        private Vector2 Muzzle(Vector2 pos, int face)
        {
            Vector2 offset = Vector2.Lerp(_muzzleStand, _muzzleCrouch, _posture.CrouchProgress);

            return pos + new Vector2(Mathf.Abs(offset.x) * (face < 0 ? -1f : 1f), offset.y);
        }

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx) => true;

        public override void Exit() { }
    }
}
