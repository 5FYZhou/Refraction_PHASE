using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 敌人追击Test
    {
        private const float Dt = 0.02f;

        private E_SecondConfig _config;
        private FakeMotor _motor;
        private E_SecondLogic _logic;
        private float _now;

        [SetUp]
        public void 建立敌人()
        {
            _config = ScriptableObject.CreateInstance<E_SecondConfig>();
            _config.maxHealth = 3;
            _config.whiteWindupTime = 0f;
            _config.whiteRecoverTime = 0f;

            _motor = new FakeMotor();
            _logic = new E_SecondLogic(
                _motor,
                _config,
                new E_SecondArea(true, Vector2.zero, new Vector2(2f, 2f)),
                new E_SecondArea(true, Vector2.zero, new Vector2(10f, 3f)),
                false);
        }

        [TearDown]
        public void 释放敌人()
        {
            _logic.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        private void 推进(Vector2 enemyPos, Vector2 playerPos, bool visible, bool wall = false)
        {
            _now += Dt;

            WorldInfo world = new(false, wall, Vector2.zero, 1, enemyPos);
            LogicContext ctx = new(default, world, new GameTime(_now, Dt));

            _logic.FixedTick(ctx, new E_SecondTargetInfo(true, visible, playerPos, playerPos, new Vector2(0.45f, 0.9f)));
        }

        [Test]
        public void 追击中触地撞墙_不结束()
        {
            Vector2 self = new(5f, 0f);
            Vector2 player = new(7f, -2f);

            推进(self, player, false);

            Assert.AreEqual(E_SecondStateTag.Chase, _logic.CurrentTag, "玩家在追击框里就该一直追");

            for (int i = 0; i < 5; i++) 推进(self, player, false, true);

            Assert.AreEqual(E_SecondStateTag.Chase, _logic.CurrentTag, "贴着地面 / 墙面飞不算脱战");
        }

        [Test]
        public void 玩家出追击框_追击才结束并回家()
        {
            Vector2 self = new(5f, 0f);

            推进(self, new Vector2(7f, 0f), false);
            Assert.AreEqual(E_SecondStateTag.Chase, _logic.CurrentTag, "先进追击");

            推进(self, new Vector2(12f, 0f), false);

            Assert.AreEqual(E_SecondStateTag.Return, _logic.CurrentTag, "玩家出框即脱战，身体在巡逻框外就飞回去");
        }

        [Test]
        public void 玩家纵向出追击框_追击也结束()
        {
            Vector2 self = new(5f, 0f);

            推进(self, new Vector2(7f, 2f), false);
            Assert.AreEqual(E_SecondStateTag.Chase, _logic.CurrentTag, "x、y 都还在框里，先追起来");

            推进(self, new Vector2(7f, 5f), false);

            Assert.AreEqual(E_SecondStateTag.Return, _logic.CurrentTag, "玩家往上飞出框同样算脱战");
        }

        [Test]
        public void 攻击结束_玩家还在追击框里_接着追()
        {
            Vector2 self = new(5f, 0f);
            Vector2 player = new(7f, 0f);

            推进(self, player, true);
            Assert.AreEqual(E_SecondStateTag.Chase, _logic.CurrentTag, "看见玩家先进追击");

            推进(self, player, true);
            Assert.AreEqual(E_SecondStateTag.Attack, _logic.CurrentTag, "追击中再看一眼就起手");

            推进(self, player, true);
            推进(self, player, false);
            Assert.AreEqual(E_SecondStateTag.Attack, _logic.CurrentTag, "后摇走完才判要不要继续追");

            推进(self, player, false);

            Assert.AreEqual(E_SecondStateTag.Chase, _logic.CurrentTag, "看不见但没出框，回到追击而不是放弃");
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
