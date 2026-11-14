using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>白色攻击的走位：前摇与后摇都要朝玩家靠到 whiteChaseRadius 以内，黑色前摇仍然原地蓄力</summary>
    public sealed class 敌人白攻击Test
    {
        private const float Dt = 0.02f;
        private const float Speed = 4f;
        private const float Radius = 5f;
        private static readonly Vector2 SelfPos = Vector2.zero;

        private E_SecondConfig _config;
        private FakeMotor _motor;
        private E_SecondLogic _logic;
        private float _now;

        [SetUp]
        public void 建立敌人()
        {
            _config = ScriptableObject.CreateInstance<E_SecondConfig>();
            _config.whiteWindupTime = 0.5f;
            _config.whiteRecoverTime = 0.5f;
            _config.whiteChaseSpeed = Speed;
            _config.whiteChaseRadius = Radius;

            _motor = new FakeMotor();
            _logic = new E_SecondLogic(_motor, _config, new E_SecondArea(true, SelfPos, new Vector2(6f, 3f)), default, false);
        }

        [TearDown]
        public void 释放敌人()
        {
            _logic.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        private void 推进(int frames, Vector2 targetPos)
        {
            for (int i = 0; i < frames; i++)
            {
                _now += Dt;

                WorldInfo world = new(false, false, Vector2.zero, 1, SelfPos);
                LogicContext ctx = new(default, world, new GameTime(_now, Dt));

                _logic.FixedTick(ctx, new E_SecondTargetInfo(true, true, targetPos, targetPos, Vector2.zero));
            }
        }

        /// <summary>一帧一帧推到指定展示态，停在那一帧上，最后写进去的速度才作数</summary>
        private void 到(E_SecondVisual visual, Vector2 targetPos)
        {
            for (int i = 0; i < 40 && _logic.Visual != visual; i++) 推进(1, targetPos);

            Assert.AreEqual(visual, _logic.Visual, $"推了 40 帧还没到 {visual}");
        }

        [Test]
        public void 前摇_玩家在半径外_朝玩家飞()
        {
            到(E_SecondVisual.Windup, new Vector2(10f, 0f));

            Assert.AreEqual(new Vector2(Speed, 0f), _motor.LastSnap, "距离 10 远大于半径 5，前摇就一边瞄一边靠过去");
        }

        [Test]
        public void 前摇_玩家在半径内_原地悬停()
        {
            到(E_SecondVisual.Windup, new Vector2(2f, 0f));

            Assert.AreEqual(Vector2.zero, _motor.LastSnap, "已经进了 whiteChaseRadius，不再往里挤");
        }

        [Test]
        public void 前摇_玩家在正上方_竖直靠近()
        {
            到(E_SecondVisual.Windup, new Vector2(0f, 10f));

            Assert.AreEqual(new Vector2(0f, Speed), _motor.LastSnap, "只按直线飞，不因为是正上方就只动 x");
        }

        [Test]
        public void 后摇_玩家跑远_继续追上去()
        {
            _config.whiteWindupTime = 0f;

            Vector2 target = new(10f, 0f);

            到(E_SecondVisual.Recover, target);

            // 到() 停在"刚变成 Recover"那一帧，那一帧只切了阶段，速度要等 Recover 自己的 Tick 才写
            推进(1, target);

            Assert.AreEqual(new Vector2(Speed, 0f), _motor.LastSnap, "开火之后的后摇也在朝玩家飞");
        }

        [Test]
        public void 后摇_玩家在半径内_原地悬停()
        {
            _config.whiteWindupTime = 0f;

            Vector2 target = new(2f, 0f);

            到(E_SecondVisual.Recover, target);
            推进(1, target);

            Assert.AreEqual(Vector2.zero, _motor.LastSnap, "半径内不再移动，只等后摇计时走完");
        }

        [Test]
        public void 黑前摇_不跟着走()
        {
            _logic.Dispose();
            _logic = new E_SecondLogic(_motor, _config, new E_SecondArea(true, SelfPos, new Vector2(6f, 3f)), default, true);

            到(E_SecondVisual.Windup, new Vector2(10f, 0f));

            Assert.AreEqual(Vector2.zero, _motor.LastSnap, "黑前摇是蓄力，走位只在白色形态生效");
        }

        [Test]
        public void 白速度为零_原地悬停()
        {
            _config.whiteChaseSpeed = 0f;

            到(E_SecondVisual.Windup, new Vector2(10f, 0f));

            Assert.AreEqual(Vector2.zero, _motor.LastSnap, "whiteChaseSpeed = 0 等于关掉白走位");
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
