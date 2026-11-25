using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>下蹲时按攻击键走水平突进：接管这一路的分流，且因为踩地不占空中次数</summary>
    public sealed class 下蹲突进Test
    {
        private const float Dt = 0.02f;

        private PlayerConfig _config;
        private PostureGroup _posture;
        private AttackGroup _group;
        private bool _black;

        [SetUp]
        public void 建立攻击组()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.crouchPrepareTime = 0f;
            _config.blackDashPrepareTime = 0.1f;
            _config.blackDashTimeLevel = 0.1f;
            _config.blackDashRecoverTime = 0.1f;
            _config.blackDashLevelCooldown = 0f;
            _config.flatAttackCooldown = 0f;
            _config.blackDashAirCount = 1;

            FakeMotor motor = new();
            _posture = new PostureGroup(_config, motor);
            _group = new AttackGroup(_config, motor, null, _posture, null, null, () => _black);

            _black = true;
        }

        [TearDown]
        public void 释放攻击组()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 蹲着按攻击_走水平突进()
        {
            _posture.Tick(Ctx(0f, false, true));
            Assert.AreEqual(PostureStateTag.Crouch, _posture.Current, "前摇为 0，按住蹲键当帧就蹲下");

            _group.Tick(Ctx(0.02f, true, true));

            Assert.AreEqual(AttackStateTag.BlackDashLevel, _group.Current, "蹲着按攻击走水平突进，不是平劈");
        }

        [Test]
        public void 站着按攻击_仍是平劈()
        {
            _group.Tick(Ctx(0f, true, false));

            Assert.AreEqual(AttackStateTag.FlatAttack, _group.Current, "没蹲下就不该被水平突进抢走");
        }

        [Test]
        public void 蹲着突进不扣空中次数()
        {
            _posture.Tick(Ctx(0f, false, true));
            _group.Tick(Ctx(0.02f, true, true));

            Assert.AreEqual(1, _group.AirDash, "蹲在地方上起手，不占空中次数");
        }

        private static LogicContext Ctx(float now, bool attack, bool crouchHeld)
        {
            InputSnapshot input = new(new Vector2(0f, crouchHeld ? -1f : 0f), false, false, false, crouchHeld, attack, false);
            WorldInfo world = new(true, false, Vector2.zero, 1, Vector2.zero);

            return new LogicContext(input, world, new GameTime(now, Dt));
        }

        private sealed class FakeMotor : IMoveMotor
        {
            public float BodyHeight => 2f;

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
