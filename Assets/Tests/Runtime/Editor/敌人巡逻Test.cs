using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>毛毛虫巡逻：越过框边那几帧不能把巡逻框丢掉</summary>
    public sealed class 敌人巡逻Test
    {
        private const float Dt = 0.02f;

        private E_FirstConfig _config;
        private FakeMotor _motor;
        private E_FirstLogic _logic;
        private EnemyArea _area;

        private float _now;

        [SetUp]
        public void 建立毛毛虫()
        {
            _config = ScriptableObject.CreateInstance<E_FirstConfig>();
            _config.patrolSpeed = 2f;

            _motor = new FakeMotor();
            _area = EnemyArea.SpanX(-2f, 2f);

            _logic = new E_FirstLogic(_motor, _config, true);
        }

        [TearDown]
        public void 清理()
        {
            _logic?.Dispose();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 越过框边一帧_巡逻框仍然有效()
        {
            推进(new Vector2(1.95f, 0f));
            推进(new Vector2(2.05f, 0f));

            Assert.IsTrue(_logic.PatrolArea.HasBounds, "惯性把身体带出框一帧，框不能就此失效");
        }

        [Test]
        public void 越过框边之后_朝框内掉头()
        {
            推进(new Vector2(1.95f, 0f));
            推进(new Vector2(2.05f, 0f));
            推进(new Vector2(2.07f, 0f));

            Assert.AreEqual(-1f, _motor.LastDir.x, 0.001f, "贴在框外就该往框里走，不能顺着惯性一直爬出去");
        }

        [Test]
        public void 出生在框外_自己走回框里()
        {
            推进(new Vector2(10f, 0f));

            Assert.AreEqual(-1f, _motor.LastDir.x, 0.001f, "框画在身侧掉头走回来，不能当成无界");
        }

        [Test]
        public void 没画巡逻框_一直朝同一侧爬()
        {
            _area = default;

            推进(Vector2.zero);
            推进(new Vector2(0.5f, 0f));

            Assert.IsFalse(_logic.PatrolArea.HasBounds, "没画框就是无界");
            Assert.AreEqual(1f, _motor.LastDir.x, 0.001f, "无界时没有边界可掉头");
        }

        private void 推进(Vector2 pos, bool wall = false, bool ledge = false, int face = 1)
        {
            WorldInfo world = new(true, wall, Vector2.zero, face, pos, ledge);
            LogicContext ctx = new(default, world, new GameTime(_now, Dt));

            _logic.FixedTick(ctx, default, _area);

            _now += Dt;
        }

        private sealed class FakeMotor : IMoveMotor
        {
            public Vector2 LastDir { get; private set; }

            public float BodyHeight => 0.9f;

            public float FeetOffsetY => 0f;

            public int FaceSign => 1;

            public void MoveTowardsX(Vector2 direction, float speed) => LastDir = direction;

            public void BrakeTowardsX() => LastDir = Vector2.zero;

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
            public void ApplyExternal() { }
            public void FaceTowards(Vector2 dir) { }
            public void ApplyGravity(in LogicContext ctx) { }
            public void SpawnAt(Vector2 position, Vector2 velocity) { }
            public bool CanStandUp(float standHeight, float crouchHeight) => true;
            public void CommitGates(in MoveGates gates) { }
            public void ApplyGates() { }
        }
    }
}
