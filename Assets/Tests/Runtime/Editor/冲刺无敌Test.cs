using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 冲刺无敌Test
    {
        private const float Dt = 0.02f;

        private PlayerConfig _config;
        private ConditionGroup _conditions;

        [SetUp]
        public void 建立条件组()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.invulnerableTime = 0.5f;

            _conditions = new ConditionGroup(new FakeMotor(), _config);
        }

        [TearDown]
        public void 释放条件组()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 开窗后_窗口内无敌()
        {
            _conditions.Tick(Ctx(1f));
            _conditions.BeginDashInvulnerable(0.15f);

            Assert.IsTrue(_conditions.IsDashInvulnerable, "落影当帧开的窗，本帧就该在无敌里");
        }

        [Test]
        public void 窗口走完_恢复可受击()
        {
            _conditions.Tick(Ctx(1f));
            _conditions.BeginDashInvulnerable(0.15f);

            _conditions.Tick(Ctx(1.2f));

            Assert.IsFalse(_conditions.IsDashInvulnerable, "过了 0.15 秒就不再无敌");
        }

        [Test]
        public void 时长配成零_不开窗()
        {
            _conditions.Tick(Ctx(1f));
            _conditions.BeginDashInvulnerable(0f);

            Assert.IsFalse(_conditions.IsDashInvulnerable, "时长为 0 不产生窗口，表现层也就不会闪一下");
        }

        [Test]
        public void 冲刺无敌_不影响受击无敌窗()
        {
            _conditions.Tick(Ctx(1f));
            _conditions.BeginDashInvulnerable(0.15f);

            Assert.IsTrue(_conditions.IsDashInvulnerable, "冲刺窗自己开着");
            Assert.IsFalse(_conditions.IsInvulnerable, "受击无敌窗保持关闭，两种表现才能分开");
        }

        [Test]
        public void 复活清窗_出生点不白送无敌()
        {
            _conditions.Tick(Ctx(1f));
            _conditions.BeginDashInvulnerable(1f);

            _conditions.ClearHurt();

            Assert.IsFalse(_conditions.IsDashInvulnerable, "重生要连冲刺无敌一起清掉");
        }

        private static LogicContext Ctx(float now)
        {
            InputSnapshot input = new(Vector2.zero, false, false, false, false, false, false);
            WorldInfo world = new(true, false, 1, Vector2.zero, 1, Vector2.zero);

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
