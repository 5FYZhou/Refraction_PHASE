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
    /// <summary>空中黑突进按次数走：落地补满、劈到敌人与弹反子弹各返还一份，返还钳在上限内</summary>
    public sealed class 空中突进次数Test
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
            _config.blackDashPrepareTime = 0.1f;
            _config.blackDashTimeUp = 0.1f;
            _config.blackDashTimeDown = 0.1f;
            _config.blackDashRecoverTime = 0.1f;
            _config.blackDashUpCooldown = 0f;
            _config.blackDashDownCooldown = 0f;
            _config.flatAttackCooldown = 0f;
            _config.blackDashAirCount = 1;
            _config.blackDashHitRefund = 1;
            _config.blackDashReboundRefund = 1;

            FakeMotor motor = new();
            PostureGroup posture = new(_config, motor);
            _conditions = new ConditionGroup(motor, _config);
            _view = new();
            _group = new AttackGroup(_config, motor, null, posture, _view, _conditions);

            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));
        }

        [TearDown]
        public void 释放攻击组()
        {
            _group.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 空中只够突一次_第二次按不出来()
        {
            float now = 0f;

            _group.Tick(Ctx(now, false, false));
            Assert.AreEqual(1, _group.AirDash, "刚离地时次数是满的");

            _group.Tick(Ctx(now + 0.01f, true, false));
            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "空中第一次斜上突进照常起手");
            Assert.AreEqual(0, _group.AirDash, "起手当场扣掉一次");

            now = 推进到空闲(now + 0.01f, false);

            _group.Tick(Ctx(now, true, false));
            Assert.AreEqual(AttackStateTag.None, _group.Current, "次数用完，空中再按也起不来");
        }

        [Test]
        public void 地面起手不扣次数()
        {
            _group.Tick(Ctx(0f, true, false, grounded: true));

            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "踩地时斜上突进照常起手");
            Assert.AreEqual(1, _group.AirDash, "地面起手不占空中次数");

            float now = 推进到空闲(0f, true, true);

            now += 0.2f;
            _group.Tick(Ctx(now, true, false, grounded: false));
            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "落地补满后空中那一份还在");
            Assert.AreEqual(0, _group.AirDash, "空中起手才扣");
        }

        [Test]
        public void 离地宽限内起手不扣次数()
        {
            _config.blackDashCoyoteTime = 0.12f;

            _group.Tick(Ctx(0f, false, false, grounded: true));
            _group.Tick(Ctx(0.06f, true, false, grounded: false));

            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "刚离地还在宽限内，斜上突进照常起手");
            Assert.AreEqual(1, _group.AirDash, "宽限内按地面起手算，不吃空中次数");
        }

        [Test]
        public void 离地宽限过期后起手要扣次数()
        {
            _config.blackDashCoyoteTime = 0.12f;

            _group.Tick(Ctx(0f, false, false, grounded: true));
            _group.Tick(Ctx(0.2f, true, false, grounded: false));

            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "过了宽限就按空中起手");
            Assert.AreEqual(0, _group.AirDash, "过期之后起手要扣空中次数");
        }

        [Test]
        public void 离地宽限内不给斜下突进()
        {
            _group.Tick(Ctx(0f, false, false, grounded: true));
            _group.Tick(Ctx(0.06f, true, false, grounded: false, moveY: -1f));

            Assert.AreEqual(AttackStateTag.None, _group.Current, "宽限内仍算踩地，斜下突进跟站着一样不给");
        }

        [Test]
        public void 一次离地只借一次宽限()
        {
            _group.Tick(Ctx(0f, false, false, grounded: true));
            _group.Tick(Ctx(0.02f, true, false, grounded: false));
            Assert.AreEqual(1, _group.AirDash, "第一次借宽限起手不扣次数");

            float now = 推进到空闲(0.02f, false);

            _group.Tick(Ctx(now + 0.01f, true, false, grounded: false));
            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "同一次离地的第二次起手仍然给");
            Assert.AreEqual(0, _group.AirDash, "第二次已经按空中算，要扣次数");
        }

        [Test]
        public void 落地当帧补满()
        {
            _group.Tick(Ctx(0f, false, false));
            _group.Tick(Ctx(0.01f, true, false));
            Assert.AreEqual(0, _group.AirDash, "空中突完是 0");

            _group.Tick(Ctx(0.02f, false, false));
            Assert.AreEqual(0, _group.AirDash, "还没落地就一直是 0");

            _group.Tick(Ctx(0.03f, false, false, grounded: true));
            Assert.AreEqual(1, _group.AirDash, "踩地那一帧直接补满");
        }

        [Test]
        public void 劈到敌人返还一次()
        {
            _group.Tick(Ctx(0f, false, false));
            _group.Tick(Ctx(0.01f, true, false));
            Assert.AreEqual(0, _group.AirDash, "突进已把次数用光");

            _view.Hit?.Invoke();

            Assert.AreEqual(1, _group.AirDash, "本体劈到人返还一次");
        }

        [Test]
        public void 弹反返还一次()
        {
            _group.Tick(Ctx(0f, false, false));
            _group.Tick(Ctx(0.01f, true, false));
            Assert.AreEqual(0, _group.AirDash, "突进已把次数用光");

            _group.RefundAirDash(_config.blackDashReboundRefund);

            Assert.AreEqual(1, _group.AirDash, "弹反打回一颗敌弹返还一次");

            float now = 推进到空闲(0.02f, false);

            _group.Tick(Ctx(now, true, false));
            Assert.AreEqual(AttackStateTag.BlackDashUp, _group.Current, "返还后空中还能再突一次");
        }

        [Test]
        public void 返还不会超过上限()
        {
            _group.Tick(Ctx(0f, false, false));
            _group.Tick(Ctx(0.01f, true, false));

            _group.RefundAirDash(5);

            Assert.AreEqual(1, _group.AirDash, "返还钳在 blackDashAirCount 上，不累积");
        }

        [Test]
        public void 命中返还设0_劈到人也不加()
        {
            _config.blackDashHitRefund = 0;

            _group.Tick(Ctx(0f, false, false));
            _group.Tick(Ctx(0.01f, true, false));
            _view.Hit?.Invoke();

            Assert.AreEqual(0, _group.AirDash, "返还配成 0 就不该加次数");
        }

        private float 推进到空闲(float now, bool atkHeld, bool grounded = false)
        {
            for (int i = 0; i < 60 && _group.Current != AttackStateTag.None; i++)
            {
                now += 0.02f;
                _group.Tick(Ctx(now, false, atkHeld, grounded: grounded));
            }

            return now;
        }

        private static LogicContext Ctx(float now, bool attack, bool attackHeld, bool grounded = false, float moveY = 1f)
        {
            InputSnapshot input = new(new Vector2(0f, moveY), false, false, false, false, attack, false, false, false, attackHeld);
            WorldInfo world = new(grounded, false, Vector2.zero, 1, Vector2.zero);

            return new LogicContext(input, world, new GameTime(now, Dt));
        }

        private sealed class 记盒子 : IBlackAttackView
        {
            public System.Action Hit { get; private set; }

            public void BeginFlat() { }

            public void BeginDash(BlackAttackKind kind) { }

            public void End() { }

            public void SetHitHandler(System.Action onHit) => Hit = onHit;
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
