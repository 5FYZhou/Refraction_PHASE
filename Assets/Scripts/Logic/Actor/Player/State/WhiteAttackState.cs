using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>开火那一帧：以玩家中心为起点按朝向与上下输入生成子弹，后坐力沿弹道反向叠加外部速度</summary>
    /// <remarks>IsDone 恒 true，Enter 里的副作用整个攻击周期只发生一次；速度与伤害都只读 PlayerConfig</remarks>
    public sealed class WhiteAttackState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.WhiteAttack;

        private readonly PlayerConfig _config;
        private readonly IStateMove _motor;
        private readonly IBulletLauncher _launcher;
        private readonly PostureGroup _posture;

        public WhiteAttackState(PlayerConfig config, IStateMove motor, IBulletLauncher launcher, PostureGroup posture)
        {
            _config = config;
            _motor = motor;
            _launcher = launcher;
            _posture = posture;
        }

        public override void Enter(in LogicContext ctx)
        {
            Vector2 direction = AttackAim.Direction(ctx.WorldInfo.Face, ctx.InputSnapshot.Move.y);

            _launcher.Launch(new BulletSpawn(
                ctx.WorldInfo.Pos, direction, _config.bulletSpeed, _config.bulletDamage, _config.bulletKnockback,
                _config.bulletTransferSpeed));
            _motor.AddExternalVelocity(AttackAim.RecoilSpeed(direction, _config, _posture.CrouchProgress));
        }

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx) => true;

        public override void Exit() { }
    }
}
