using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>黑突进起手那一帧移动组就被压成 Fall，后摇放开且踩地才回站姿</summary>
    public sealed class 黑突进移动态Test
    {
        private const float Dt = 0.02f;

        private PlayerConfig _config;
        private 空电机 _motor;
        private PlayerLogic _player;

        [SetUp]
        public void 建立玩家()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.blackDashPrepareTime = 0.1f;
            _config.blackDashTimeUp = 0.1f;
            _config.blackDashRecoverTime = 0.1f;
            _config.blackDashUpCooldown = 0f;
            _config.blackDashSpeedUp = 10f;
            _config.blackDashBounceSpeed = 9f;
            _config.attackCooldown = 0f;
            _config.flatAttackCooldown = 0f;

            _motor = new();
            _player = new(_motor, _config, new InputBuffer(0.1f, 60), null, null);

            // 极性必须在构造之后发：攻击组在构造里才订阅
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));
        }

        [TearDown]
        public void 释放玩家()
        {
            _player.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            EventBus<PlayerPolarityChanged>.Clear();
            EventBus<PlayerHealthChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 起手那一帧_移动组就压成Fall()
        {
            走到(0f, 1f, true);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _player.AttackTag, "按住上 + 攻击进斜上突进前摇");
            Assert.AreEqual(MovementStateTag.Fall, _player.CurrentTag, "起手当帧移动组就被压成 Fall，不留一帧站姿");
        }

        [Test]
        public void 前摇与本体_一直保持Fall()
        {
            走到(0f, 1f, true);
            走到(0.04f, 0f, false);

            Assert.AreEqual(MovementStateTag.Fall, _player.CurrentTag, "前摇里仍是 Fall");

            走到(0.12f, 0f, false);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _player.AttackTag, "前摇走完进本体");
            Assert.AreEqual(MovementStateTag.Fall, _player.CurrentTag, "本体里仍是 Fall，移动组被压住不推进");
        }

        [Test]
        public void 后摇放开且踩地_回到站姿()
        {
            走到(0f, 1f, true);
            走到(0.12f, 0f, false);
            走到(0.25f, 0f, false);

            Assert.AreEqual(MovementStateTag.Fall, _player.CurrentTag, "后摇这一帧移动组还没放开，仍是 Fall");

            走到(0.27f, 0f, false);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _player.AttackTag, "仍在后摇里");
            Assert.AreEqual(MovementStateTag.Idle, _player.CurrentTag, "后摇放开 + 踩地，Fall 结束回到站姿");
        }

        [Test]
        public void 后摇放开但还在空中_继续Fall()
        {
            走到(0f, 1f, true);
            走到(0.12f, 0f, false);
            走到(0.25f, 0f, false, grounded: false);
            走到(0.27f, 0f, false, grounded: false);

            Assert.AreEqual(MovementStateTag.Fall, _player.CurrentTag, "没踩地就还是 Fall，靠重力落回地面");
        }

        [Test]
        public void 不按攻击_移动组不进Fall()
        {
            走到(0f, 0f, false);
            走到(0.04f, 0f, false);

            Assert.AreEqual(AttackStateTag.None, _player.AttackTag, "没按攻击就没有突进");
            Assert.AreEqual(MovementStateTag.Idle, _player.CurrentTag, "踩地不动就是站姿，别被压成 Fall");
        }

        private void 走到(float now, float moveY, bool attack, bool grounded = true)
        {
            InputSnapshot input = new(new Vector2(0f, moveY), false, false, false, false, attack, false);
            WorldInfo world = new(grounded, false, Vector2.zero, 1, Vector2.zero);

            _player.FixedTick(new LogicContext(input, world, new GameTime(now, Dt)));
        }

        private sealed class 空电机 : IMoveMotor
        {
            public float BodyHeight => 0.9f;

            public float FeetOffsetY => 0f;

            public int FaceSign => 1;

            public void MoveTowardsX(Vector2 direction, float speed) { }
            public void BrakeTowardsX() { }
            public void AddImpulse(Vector2 deltaVelocity) { }
            public void AddExternalVelocity(Vector2 velocity) { }
            public void SetVelocityX(float speed) { }
            public void SetVelocityY(float speed) { }
            public void SnapVelocity(Vector2 velocity) { }
            public void SetSpeedScaleX(float scale) { }
            public void ScaleVelocityY(float multiplier) { }
            public void SetGravityEnable(bool enable) { }
            public void SetGravity(float gravity) { }
            public void SetBodyScale(float scale) { }
            public void BeginStep(float now, float deltaTime) { }
            public void Commit() { }
            public void FaceTowards(Vector2 dir) { }
            public void ApplyGravity(in LogicContext ctx) { }
            public void SpawnAt(Vector2 position, Vector2 velocity) { }
            public bool CanStandUp(float standHeight, float crouchHeight) => true;
            public void CommitGates(in MoveGates gates) { }
            public void ApplyGates() { }
        }
    }
}
