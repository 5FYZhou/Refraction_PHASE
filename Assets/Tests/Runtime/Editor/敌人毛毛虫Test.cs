using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 敌人毛毛虫Test
    {
        private const float Dt = 0.02f;
        private const float SelfX = 0f;
        private const float Speed = 2f;
        private const float Radius = 1.5f;

        private E_FirstConfig _config;
        private FakeMotor _motor;
        private E_FirstLogic _logic;
        private float _now;

        [SetUp]
        public void 建立敌人()
        {
            _config = ScriptableObject.CreateInstance<E_FirstConfig>();
            _config.maxHealth = 3;
            _config.windupTime = 0f;
            _config.strikeTime = 0f;
            _config.recoverTime = 10f;
            _config.recoverChaseSpeed = Speed;
            _config.recoverChaseRadius = Radius;

            _motor = new FakeMotor();
            _logic = new E_FirstLogic(_motor, _config, true, WorldPolarity.White);
        }

        [TearDown]
        public void 释放敌人()
        {
            _logic.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        /// <summary>前摇与攻击时长都是 0：第 3 帧已经站在后摇里，速度由 CrawlTowards 写</summary>
        private void 推进(int frames, float playerX)
        {
            for (int i = 0; i < frames; i++)
            {
                _now += Dt;

                WorldInfo world = new(true, false, Vector2.zero, 1, new Vector2(SelfX, 0f));
                LogicContext ctx = new(default, world, new GameTime(_now, Dt));

                _logic.FixedTick(ctx, new EnemyTargetInfo(true, true, new Vector2(playerX, 0f)), default);
            }
        }

        [Test]
        public void 后摇_玩家在半径外_恒速爬过去()
        {
            推进(3, 5f);

            Assert.AreEqual(EnemyStateTag.Attack, _logic.CurrentTag, "看得见玩家就一直待在攻击态");
            Assert.AreEqual(Speed, _motor.LastSetX, 0.001f, "距离大于半径，按固定速度朝玩家爬");
        }

        [Test]
        public void 后摇_玩家在半径内_原地不动()
        {
            推进(3, SelfX + 1f);

            Assert.AreEqual(0f, _motor.LastSetX, 0.001f, "已经进了半径，再爬会把玩家顶走");
        }

        [Test]
        public void 后摇_玩家在背后_反向恒速()
        {
            推进(3, -5f);

            Assert.AreEqual(-Speed, _motor.LastSetX, 0.001f, "玩家在左边就朝左爬，速度大小不变");
        }

        private sealed class FakeMotor : IMoveMotor
        {
            public float LastSetX;

            public float BodyHeight => 1f;

            public float FeetOffsetY => 0f;

            public int FaceSign => 1;

            public void MoveTowardsX(Vector2 direction, float speed) => LastSetX = speed;
            public void BrakeTowardsX() { }
            public void AddImpulse(Vector2 deltaVelocity) { }
            public void AddExternalVelocity(Vector2 velocity) { }
            public void SetVelocityX(float speed) => LastSetX = speed;
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
