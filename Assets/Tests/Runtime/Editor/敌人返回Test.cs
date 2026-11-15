using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 敌人返回Test
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

            _motor = new FakeMotor();
            _logic = new E_SecondLogic(
                _motor,
                _config,
                new EnemyArea(true, Vector2.zero, new Vector2(2f, 2f)),
                new EnemyArea(true, Vector2.zero, new Vector2(10f, 3f)),
                false);
        }

        [TearDown]
        public void 释放敌人()
        {
            _logic.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        /// <summary>场上没有玩家：帧首落到返回态，同一帧就开始飞回框心（玩家还在追击框里会一直追，进不了返回）</summary>
        private void 推进(Vector2 pos, bool wall)
        {
            _now += Dt;

            WorldInfo world = new(false, wall, Vector2.zero, 1, pos);
            LogicContext ctx = new(default, world, new GameTime(_now, Dt));

            _logic.FixedTick(ctx, default);
        }

        [Test]
        public void 返回途中撞墙_巡逻框与追击框一起平移()
        {
            推进(new Vector2(5f, 0f), true);

            Assert.AreEqual(EnemyStateTag.Return, _logic.CurrentTag, "没玩家可追，身体又在巡逻框外，就该飞回去");
            Assert.AreEqual(5f, _logic.PatrolArea.Center.x, 0.001f, "撞墙那一刻就地立成新的巡逻中心");
            Assert.AreEqual(5f, _logic.ChaseArea.Center.x, 0.001f, "追击框按同一段位移跟着平移");
            Assert.AreEqual(2f, _logic.PatrolArea.HalfSize.x, 0.001f, "巡逻框只挪中心，宽度不变");
            Assert.AreEqual(10f, _logic.ChaseArea.HalfSize.x, 0.001f, "追击框宽度同样不变");
        }

        [Test]
        public void 返回途中不撞墙_两个框都留在原地()
        {
            推进(new Vector2(5f, 0f), false);

            Assert.AreEqual(EnemyStateTag.Return, _logic.CurrentTag, "还没飞到框心，仍在返回态");
            Assert.AreEqual(0f, _logic.PatrolArea.Center.x, 0.001f, "没撞墙不重定位");
            Assert.AreEqual(0f, _logic.ChaseArea.Center.x, 0.001f, "追击框也不动");
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
