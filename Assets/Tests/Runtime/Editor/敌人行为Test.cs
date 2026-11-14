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
    public sealed class 敌人行为Test
    {
        private const float Dt = 0.02f;
        private const float Eps = 1e-3f;

        private static readonly Vector2 EnemyPos = Vector2.zero;
        private static readonly Vector2 TargetRight = new(5f, 0f);

        private EnemyConfig _config;
        private FakeMotor _motor;
        private FakeLauncher _launcher;
        private EnemyLogic _logic;

        [SetUp]
        public void 建立敌人()
        {
            _config = ScriptableObject.CreateInstance<EnemyConfig>();
            _config.maxHealth = 3;
            _config.loseTargetTime = 1.5f;
            _config.bulletSpeed = 12f;
            _config.bulletDamage = 2f;
            _config.bulletKnockback = 3f;
            _config.fireInterval = 1f;
            _config.bulletMuzzle = new Vector2(1.6f, 0f);

            _motor = new FakeMotor();
            _launcher = new FakeLauncher();
            _logic = new EnemyLogic(_motor, _config, true);
            _logic.BindLauncher(_launcher);
        }

        [TearDown]
        public void 释放敌人()
        {
            _logic.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        private static LogicContext Ctx(float now, int face = 1)
        {
            WorldInfo world = new(true, false, 0, Vector2.zero, face, EnemyPos);

            return new LogicContext(default, world, new GameTime(now, Dt));
        }

        private BulletSpawn Tick(float now, bool visible, Vector2 targetPos, int face = 1)
        {
            _logic.FixedTick(Ctx(now, face), new EnemyTargetInfo(visible, targetPos), default);

            return _launcher.Last;
        }

        [Test]
        public void 看见玩家_朝玩家平射一发()
        {
            Tick(0f, true, TargetRight);

            Assert.AreEqual(1, _launcher.Count, "看见玩家的当帧应开一枪");
            Assert.AreEqual(1f, _launcher.Last.Direction.x, Eps, "玩家在右侧，弹道朝右");
            Assert.AreEqual(0f, _launcher.Last.Direction.y, Eps, "平射：弹道不带上下的分量");
            Assert.AreEqual(_config.bulletSpeed, _launcher.Last.Speed, Eps, "飞行速度取配置");
            Assert.AreEqual(_config.bulletDamage, _launcher.Last.Damage, Eps, "伤害值取配置");
            Assert.AreEqual(_config.bulletKnockback, _launcher.Last.KnockbackSpeed, Eps, "击退值取配置");
        }

        [Test]
        public void 玩家在左侧_弹道朝左_枪口也翻到左侧()
        {
            BulletSpawn spawn = Tick(0f, true, new Vector2(-5f, 0f));

            Assert.AreEqual(-1f, spawn.Direction.x, Eps, "玩家在左侧，弹道朝左");
            Assert.AreEqual(EnemyPos.x - 1.6f, spawn.Origin.x, Eps, "枪口跟着朝左，不能留在右边");
        }

        [Test]
        public void 玩家在斜上方_仍是平射()
        {
            BulletSpawn spawn = Tick(0f, true, new Vector2(5f, 6f));

            Assert.AreEqual(0f, spawn.Direction.y, Eps, "平射不看高度差，竖直分量恒为 0");
            Assert.AreEqual(EnemyPos.y, spawn.Origin.y, Eps, "出膛点与敌人中心齐平");
        }

        [Test]
        public void 一直看得见_只开一枪()
        {
            for (int i = 0; i < 8; i++) Tick(Dt * i, true, TargetRight);

            Assert.AreEqual(1, _launcher.Count, "一次进入视野只开一枪，不是每帧一枪");
        }

        [Test]
        public void 看不见玩家_不开枪()
        {
            for (int i = 0; i < 8; i++) Tick(Dt * i, false, Vector2.zero);

            Assert.AreEqual(0, _launcher.Count, "没进视野范围就不该有子弹");
        }

        [Test]
        public void 离开再进来_间隔内不连发()
        {
            Tick(0f, true, TargetRight);
            Tick(Dt, false, TargetRight);
            Tick(Dt * 2f, true, TargetRight);

            Assert.AreEqual(1, _launcher.Count, "出视野立刻再进来，冷却没走完不该补一发");

            Tick(2f, false, TargetRight);
            Tick(2f + Dt, true, TargetRight);

            Assert.AreEqual(2, _launcher.Count, "冷却走完后再进来照常开火");
        }

        [Test]
        public void 子弹速度配零_不开火()
        {
            _config.bulletSpeed = 0f;

            Tick(0f, true, TargetRight);

            Assert.AreEqual(0, _launcher.Count, "bulletSpeed 为 0 等于关掉开火");
        }

        [Test]
        public void 站桩敌人_看见玩家也不开火()
        {
            EnemyLogic standstill = new(_motor, _config, false);
            standstill.BindLauncher(_launcher);

            standstill.FixedTick(Ctx(0f), new EnemyTargetInfo(true, TargetRight), default);

            Assert.AreEqual(0, _launcher.Count, "AI 关掉的站桩靶子不该开火");

            standstill.Dispose();
        }

        [Test]
        public void 看见玩家_进追击()
        {
            Tick(0f, true, TargetRight);
            Tick(Dt, true, TargetRight);

            Assert.AreEqual(EnemyStateTag.Chase, _logic.CurrentTag, "白世界看见玩家就该追");
        }

        [Test]
        public void 黑世界_看见玩家也不追击()
        {
            EnemyLogic black = new(_motor, _config, true, WorldPolarity.Black);
            black.BindLauncher(_launcher);

            black.FixedTick(Ctx(0f), new EnemyTargetInfo(true, TargetRight), default);
            black.FixedTick(Ctx(Dt), new EnemyTargetInfo(true, TargetRight), default);

            Assert.IsFalse(black.ChaseAllowed, "开局就是黑世界");
            Assert.AreEqual(EnemyStateTag.Patrol, black.CurrentTag, "黑世界看见玩家也只能继续巡逻");

            black.Dispose();
        }

        [Test]
        public void 追击中转黑_回巡逻()
        {
            Tick(0f, true, TargetRight);
            Tick(Dt, true, TargetRight);
            Assert.AreEqual(EnemyStateTag.Chase, _logic.CurrentTag, "先追起来");

            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));
            Tick(Dt * 2f, true, TargetRight);

            Assert.AreEqual(EnemyStateTag.Patrol, _logic.CurrentTag, "世界一转黑，追击当场结束回巡逻");
        }

        [Test]
        public void 转回白世界_重新追击()
        {
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));
            Tick(0f, true, TargetRight);
            Tick(Dt, true, TargetRight);
            Assert.AreEqual(EnemyStateTag.Patrol, _logic.CurrentTag, "黑世界不追");

            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.White));
            Tick(Dt * 2f, true, TargetRight);

            Assert.AreEqual(EnemyStateTag.Chase, _logic.CurrentTag, "转回白世界后照常追击");
        }

        [Test]
        public void 退订后不再跟随世界极性()
        {
            _logic.Dispose();
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            Assert.IsTrue(_logic.ChaseAllowed, "Dispose 之后不该再收到世界极性");
        }

        private sealed class FakeLauncher : IBulletLauncher
        {
            public int Count;
            public BulletSpawn Last;

            public void Launch(in BulletSpawn spawn)
            {
                Count++;
                Last = spawn;
            }
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
