using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>接触伤害按 contactInterval 反复结算，且敌人之间互不吃接触伤害</summary>
    public sealed class 敌人接触伤害Test
    {
        private E_SecondConfig _config;
        private FakeMotor _motor;
        private E_SecondLogic _logic;

        [SetUp]
        public void 建立敌人()
        {
            _config = ScriptableObject.CreateInstance<E_SecondConfig>();
            _config.maxHealth = 3;
            _config.contactInterval = 1f;

            _motor = new FakeMotor();
            _logic = new E_SecondLogic(_motor, _config, default, default, false);
        }

        [TearDown]
        public void 释放敌人()
        {
            _logic.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 接触伤害_按间隔反复结算()
        {
            IIntervalAttacker interval = _logic;

            Assert.IsTrue(interval.ReadyToHit(0f), "开局就在接触盒里，第一口立刻结算");

            interval.MarkHit(0f);

            Assert.IsFalse(interval.ReadyToHit(0.5f), "间隔没到，贴着也不结算");
            Assert.IsTrue(interval.ReadyToHit(1f), "到点再咬一口");
        }

        [Test]
        public void 接触伤害_不作用于敌人自己()
        {
            Damage contact = new(1f, DamageSource.Contact, Vector2.zero, 0f);

            Assert.IsFalse(_logic.TakeDamage(in contact), "接触伤害只对玩家生效，敌人之间挤在一起不掉血");
            Assert.AreEqual(3, _logic.CurHealth, "血没掉");
        }

        private sealed class FakeMotor : IMoveMotor
        {
            public Vector2 LastSnap;

            public float BodyHeight => 0.9f;

            public float FeetOffsetY => 0f;

            public int FaceSign => 1;

            public void MoveTowardsX(Vector2 direction, float speed) { }
            public void BrakeTowardsX() { }
            public void AddImpulse(Vector2 deltaVelocity) { }
            public void AddExternalVelocity(Vector2 velocity) { }
            public void SetVelocityX(float speed) { }
            public void SetVelocityY(float speed) { }
            public void SnapVelocity(Vector2 velocity) => LastSnap = velocity;
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
