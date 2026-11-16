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
        private 记盒子 _view;

        [SetUp]
        public void 建立攻击组()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.attackCooldown = 0f;
            _config.attackPrepareTime = 0.1f;
            _config.attackRecoverTime = 0.1f;
            _config.flatAttackPrepareTime = 0.1f;
            _config.flatAttackTime = 0.2f;
            _config.flatAttackRecoverTime = 0.15f;
            _config.flatAttackCooldown = 0f;

            FakeMotor motor = new();
            PostureGroup posture = new(_config, motor);
            _conditions = new ConditionGroup(motor, _config);
            _view = new();
            _group = new AttackGroup(_config, motor, null, posture, _view, _conditions);
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
            WorldInfo world = new(true, false, Vector2.zero, 1, Vector2.zero);

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
        public void 反转后攻击进普攻前摇()
        {
            _group.Tick(Ctx(0f, false));
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            Assert.IsTrue(_group.IsBlack, "世界转黑后形态应跟着变黑");

            _group.Tick(Ctx(Dt, true));
            Assert.AreEqual(AttackStateTag.FlatAttack, _group.Current, "黑形态按攻击键进普攻那只状态");
            Assert.IsFalse(_group.Cancelable, "开局还在前摇，不能被别的攻击顶掉");
            Assert.IsFalse(_view.Began, "前摇期间判定盒不点亮");
        }

        [Test]
        public void 黑链_前摇到本体到后摇到空闲()
        {
            _group.Tick(Ctx(0f, false));
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            _group.Tick(Ctx(Dt, true));
            Assert.AreEqual(AttackStateTag.FlatAttack, _group.Current, "普攻先走前摇");
            Assert.IsFalse(_view.Began, "前摇期间不出判定");

            _group.Tick(Ctx(0.13f, false));
            Assert.AreEqual(AttackStateTag.FlatAttack, _group.Current, "前摇走满仍停在同一只状态里");
            Assert.IsTrue(_view.Began, "走满前摇才点亮判定盒");

            _group.Tick(Ctx(0.16f, false));
            Assert.AreEqual(AttackStateTag.FlatAttack, _group.Current, "本体未到 flatAttackTime 不结束");
            Assert.IsFalse(_group.Cancelable, "本体不能被顶掉");

            _group.Tick(Ctx(0.35f, false));
            Assert.AreEqual(AttackStateTag.FlatAttack, _group.Current, "本体走满仍在同一只状态里");
            Assert.IsTrue(_group.Cancelable, "本体走满进后摇，这一段才能被顶掉");
            Assert.IsFalse(_view.Began, "进后摇就收判定盒");

            _group.Tick(Ctx(0.51f, false));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "后摇走满回到空闲");
        }

        [Test]
        public void 三个方向各占一份冷却_互不影响()
        {
            _config.flatAttackCooldown = 10f;
            _config.blackDashUpCooldown = 10f;
            _config.blackDashDownCooldown = 10f;
            _config.flatAttackRecoverTime = 0f;
            _config.blackDashPrepareTime = 0f;
            _config.blackDashRecoverTime = 0f;

            _group.Tick(Ctx(0f, false));
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            _group.Tick(Ctx(0.01f, true));
            Assert.AreEqual(AttackStateTag.FlatAttack, _group.Current, "平劈走普攻");

            for (int i = 0; i < 40 && _group.Current != AttackStateTag.None; i++) _group.Tick(Ctx(0.01f * (i + 2), false));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "普攻收尾后回到空闲");

            InputSnapshot up = new(new Vector2(0f, 1f), false, false, false, false, true, false);
            _group.Tick(new LogicContext(up, new WorldInfo(true, false, Vector2.zero, 1, Vector2.zero), new GameTime(1f, Dt)));
            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "普攻的冷却不挡斜上");

            _group.Tick(Ctx(2f, false));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "斜上收尾后回到空闲");

            _group.Tick(Ctx(2.01f, true));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "平劈仍在自己的冷却里，按键不生效");

            InputSnapshot upAgain = new(new Vector2(0f, 1f), false, false, false, false, true, false);
            _group.Tick(new LogicContext(upAgain, new WorldInfo(true, false, Vector2.zero, 1, Vector2.zero), new GameTime(2.02f, Dt)));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "斜上也还在自己的冷却里");
        }

        [Test]
        public void 后摇能被别的方向的攻击顶掉()
        {
            _config.flatAttackCooldown = 0f;
            _config.blackDashPrepareTime = 0f;
            _config.blackDashRecoverTime = 0f;

            _group.Tick(Ctx(0f, false));
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            _group.Tick(Ctx(Dt, true));
            _group.Tick(Ctx(0.13f, false));
            _group.Tick(Ctx(0.35f, false));
            Assert.AreEqual(AttackStateTag.FlatAttack, _group.Current, "先走到普攻后摇");
            Assert.IsTrue(_group.Cancelable, "后摇可被取消");

            InputSnapshot up = new(new Vector2(0f, 1f), false, false, false, false, true, false);
            _group.Tick(new LogicContext(up, new WorldInfo(true, false, Vector2.zero, 1, Vector2.zero), new GameTime(0.36f, Dt)));
            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "后摇被斜上突进顶掉");
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

        /// <summary>只记开关状态的假表现端：判定盒到底点没点亮，就是它说了算</summary>
        private sealed class 记盒子 : IBlackAttackView
        {
            public bool Began { get; private set; }

            public BlackAttackKind Kind { get; private set; }

            public void BeginFlat()
            {
                Began = true;
                Kind = BlackAttackKind.Ahead;
            }

            public void BeginDash(BlackAttackKind kind)
            {
                Began = true;
                Kind = kind;
            }

            public void End() => Began = false;

            public void SetHitHandler(System.Action onHit) { }
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
