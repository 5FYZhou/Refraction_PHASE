using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 敌人冲刺Test
    {
        private const float Dt = 0.02f;
        private static readonly Vector2 SelfPos = Vector2.zero;

        private E_SecondConfig _config;
        private FakeMotor _motor;
        private E_SecondLogic _logic;
        private float _now;

        [SetUp]
        public void 建立敌人()
        {
            _config = ScriptableObject.CreateInstance<E_SecondConfig>();
            _config.maxHealth = 3;
            _config.blackWindupTime = 0f;
            _config.blackDashSpeed = 16f;
            _config.blackDashMaxTime = 1f;
            _config.riseSpeed = 5f;
            _config.riseMin = 1f;
            _config.riseMax = 1f;

            _motor = new FakeMotor();
            _logic = new E_SecondLogic(_motor, _config, new E_SecondArea(true, SelfPos, 6f), default, true);
        }

        [TearDown]
        public void 释放敌人()
        {
            _logic.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        private void 推进(int frames, bool wall = false, Vector2? targetPos = null,
            Vector2? bodyCenter = null, Vector2? halfSize = null)
        {
            Vector2 pos = targetPos ?? new Vector2(5f, 0f);
            Vector2 center = bodyCenter ?? (pos + new Vector2(0f, 0.9f));
            Vector2 half = halfSize ?? new Vector2(0.45f, 0.9f);

            for (int i = 0; i < frames; i++)
            {
                _now += Dt;

                WorldInfo world = new(false, wall, Vector2.zero, 1, SelfPos);
                LogicContext ctx = new(default, world, new GameTime(_now, Dt));

                _logic.FixedTick(ctx, new E_SecondTargetInfo(true, true, pos, center, half));
            }
        }

        /// <summary>巡逻 → 追击 → 攻击 → 冲刺，四帧走完，前摇时长配成 0</summary>
        private void 冲起来()
        {
            推进(4);

            Assert.AreEqual(E_SecondStateTag.Attack, _logic.CurrentTag, "看得见玩家就该进攻击");
            Assert.AreEqual(E_SecondVisual.Strike, _logic.Visual, "前摇为零，第四帧已经在冲刺");
            Assert.AreEqual(new Vector2(_config.blackDashSpeed, 0f), _motor.LastSnap, "冲刺速度朝玩家那一侧");
        }

        [Test]
        public void 撞墙_当帧进后摇()
        {
            冲起来();

            推进(1, wall: true);

            Assert.AreEqual(E_SecondVisual.Recover, _logic.Visual, "撞到墙那一帧就进后摇");
            Assert.AreEqual(E_SecondStateTag.Attack, _logic.CurrentTag, "后摇仍然算在攻击状态里");
        }

        [Test]
        public void 撞到玩家_当帧进后摇()
        {
            冲起来();

            推进(1, targetPos: new Vector2(0.3f, 0f), bodyCenter: new Vector2(0.3f, 0f));

            Assert.AreEqual(E_SecondVisual.Recover, _logic.Visual, "身体与玩家受击盒相交就进后摇");
        }

        [Test]
        public void 没撞到任何东西_继续冲()
        {
            冲起来();

            推进(3);

            Assert.AreEqual(E_SecondVisual.Strike, _logic.Visual, "既没撞墙也没撞人，冲刺不结束");
        }

        [Test]
        public void 一直撞不到_冲满上限才进后摇()
        {
            冲起来();

            推进(40);
            Assert.AreEqual(E_SecondVisual.Strike, _logic.Visual, "不到 blackDashMaxTime 仍在冲");

            // 逐帧推：后摇一结束就会重进前摇，写死帧数会整段跨过 Recover
            int extra = 0;
            while (_logic.Visual == E_SecondVisual.Strike && extra < 20)
            {
                推进(1);
                extra++;
            }

            Assert.AreEqual(E_SecondVisual.Recover, _logic.Visual, "越过上限后必须结束冲刺，不能一直飞");
        }

        [Test]
        public void 前摇结束那帧锁方向_之后玩家换边也不改()
        {
            冲起来();

            推进(1, targetPos: new Vector2(-5f, 0f));

            Assert.AreEqual(new Vector2(_config.blackDashSpeed, 0f), _motor.LastSnap,
                "方向在前摇结束那一刻就锁死，冲刺途中玩家跑到背后也不回头");
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
