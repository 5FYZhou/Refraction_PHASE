using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 攻击形态Test
    {
        private const float Dt = 0.02f;

        private PlayerConfig _config;
        private ConditionGroup _conditions;
        private AttackGroup _group;

        [SetUp]
        public void 建立攻击组()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.attackCooldown = 0f;
            _config.attackPrepareTime = 0.1f;
            _config.attackRecoverTime = 0.1f;
            _config.blackAttackPrepareTime = 0.1f;
            _config.blackAttackTime = 0.2f;
            _config.blackAttackRecoverTime = 0.15f;

            FakeMotor motor = new();
            PostureGroup posture = new(_config, motor);
            _conditions = new ConditionGroup(motor, _config);
            _group = new AttackGroup(_config, motor, null, posture, null, _conditions);
        }

        [TearDown]
        public void 释放攻击组()
        {
            _group.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        private static LogicContext Ctx(float now, bool attack)
        {
            InputSnapshot input = new(Vector2.zero, false, false, false, false, attack, false);
            WorldInfo world = new(true, false, 0, Vector2.zero, 1, Vector2.zero);

            return new LogicContext(input, world, new GameTime(now, Dt));
        }

        [Test]
        public void 开局为白_攻击进白前摇()
        {
            _group.Tick(Ctx(0f, false));
            Assert.IsFalse(_group.IsBlack, "开局形态应为白");

            _group.Tick(Ctx(Dt, true));
            Assert.AreEqual(AttackStateTag.WhiteAttackPrepare, _group.Current, "白形态攻击先走白前摇");
        }

        [Test]
        public void 受击期间_攻击键不生效()
        {
            _group.Tick(Ctx(0f, false));

            _conditions.RegisterHit(new Damage(1, DamageSource.Contact, Vector2.up, 5f), true);
            _conditions.Tick(Ctx(0f, false));
            Assert.IsTrue(_conditions.IsHurt, "有击退的伤害登记后条件组应进入受伤");

            _group.Tick(Ctx(Dt, true));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "受伤期间按攻击键也不该起手");

            // 击退靠 KnockbackDecay 逐帧衰减，一帧掉不完，得连推几帧才脱伤
            for (int i = 0; i < 15 && _conditions.IsHurt; i++) _conditions.Tick(Ctx(Dt * (i + 2), false));
            Assert.IsFalse(_conditions.IsHurt, "击退衰减完就脱离受伤");

            _group.Tick(Ctx(0.5f, true));
            Assert.AreEqual(AttackStateTag.WhiteAttackPrepare, _group.Current, "伤好之后攻击键照常生效");
        }

        [Test]
        public void 白链_前摇到本体到后摇到空闲()
        {
            _group.Tick(Ctx(0f, false));
            _group.Tick(Ctx(Dt, true));
            Assert.AreEqual(AttackStateTag.WhiteAttackPrepare, _group.Current, "按键当帧进白前摇");

            _group.Tick(Ctx(0.13f, false));
            Assert.AreEqual(AttackStateTag.WhiteAttack, _group.Current, "前摇走满进白攻击");

            _group.Tick(Ctx(0.16f, false));
            Assert.AreEqual(AttackStateTag.WhiteAttackRecover, _group.Current, "白攻击一帧后进白后摇");

            _group.Tick(Ctx(0.27f, false));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "白后摇走满回到空闲");
        }

        [Test]
        public void 反转后攻击进黑前摇()
        {
            _group.Tick(Ctx(0f, false));
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            Assert.IsTrue(_group.IsBlack, "世界转黑后形态应跟着变黑");

            _group.Tick(Ctx(Dt, true));
            Assert.AreEqual(AttackStateTag.BlackAttackPrepare, _group.Current, "黑形态攻击先走黑前摇");
        }

        [Test]
        public void 黑链_前摇到本体到后摇到空闲()
        {
            _group.Tick(Ctx(0f, false));
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            _group.Tick(Ctx(Dt, true));
            Assert.AreEqual(AttackStateTag.BlackAttackPrepare, _group.Current, "黑攻击先走前摇");

            _group.Tick(Ctx(0.13f, false));
            Assert.AreEqual(AttackStateTag.BlackAttack, _group.Current, "黑前摇走满进黑本体");

            _group.Tick(Ctx(0.16f, false));
            Assert.AreEqual(AttackStateTag.BlackAttack, _group.Current, "黑本体未到 blackAttackTime 不结束");

            _group.Tick(Ctx(0.35f, false));
            Assert.AreEqual(AttackStateTag.BlackAttackRecover, _group.Current, "黑本体走满进黑后摇");

            _group.Tick(Ctx(0.51f, false));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "黑后摇走满回到空闲");
        }

        [Test]
        public void 白前摇中反转_打完白链再换黑()
        {
            _group.Tick(Ctx(0f, false));
            _group.Tick(Ctx(Dt, true));
            Assert.AreEqual(AttackStateTag.WhiteAttackPrepare, _group.Current, "先进入白前摇");

            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));
            Assert.IsTrue(_group.IsBlack, "反转已经把形态翻成黑");

            _group.Tick(Ctx(0.13f, false));
            Assert.AreEqual(AttackStateTag.WhiteAttack, _group.Current, "进行中的白链不因反转改走黑链");

            _group.Tick(Ctx(0.16f, false));
            Assert.AreEqual(AttackStateTag.WhiteAttackRecover, _group.Current, "白链的后摇同样不受影响");
        }

        [Test]
        public void 世界转黑转白_形态跟着同步()
        {
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));
            Assert.IsTrue(_group.IsBlack, "世界转黑进入黑形态");

            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.White));
            Assert.IsFalse(_group.IsBlack, "世界转白回到白形态");
        }

        [Test]
        public void 退订后不再跟随世界极性()
        {
            _group.Dispose();
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            Assert.IsFalse(_group.IsBlack, "Dispose 之后不该再收到世界极性");
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
