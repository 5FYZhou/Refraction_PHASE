using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>开火那一帧：以玩家中心为起点按朝向生成子弹，并沿朝向反向叠加一份外部速度作为后坐力</summary>
    /// <remarks>IsDone 恒 true，Enter 里的副作用整个攻击周期只发生一次；速度与伤害都只读 PlayerConfig</remarks>
    public sealed class WhiteAttackState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.WhiteAttack;

        private readonly PlayerConfig _config;
        private readonly IStateMove _motor;
        private readonly IBulletLauncher _launcher;

        public WhiteAttackState(PlayerConfig config, IStateMove motor, IBulletLauncher launcher)
        {
            _config = config;
            _motor = motor;
            _launcher = launcher;
        }

        public override void Enter(in LogicContext ctx)
        {
            int face = ctx.WorldInfo.Face;

            _launcher.Launch(new BulletSpawn(
                ctx.WorldInfo.Pos, face, _config.bulletSpeed, _config.bulletDamage));
            _motor.AddExternalVelocity(new Vector2(-face * _config.recoilSpeedX, 0f));
        }

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx) => true;

        public override void Exit() { }
    }
}
