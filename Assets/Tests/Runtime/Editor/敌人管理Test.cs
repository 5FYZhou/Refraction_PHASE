using NUnit.Framework;
using Refraction.Logic.Actor;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 敌人管理Test
    {
        private sealed class 假敌人 : IManagedEnemy
        {
            public 假敌人(EnemyKind kind = EnemyKind.Ground)
            {
                SpawnState = new EnemySpawnState(kind, new Vector2(3f, -1f), -1, true, 4);
            }

            public EnemySpawnState SpawnState { get; }

            public bool IsDead { get; set; }

            public int TickCount { get; private set; }

            public int RecycleCount { get; private set; }

            public int ResetCount { get; private set; }

            public void ManagedTick(float deltaTime) => TickCount++;

            public void Recycle() => RecycleCount++;

            public void ResetToSpawn()
            {
                ResetCount++;
                IsDead = false;
            }
        }

        private EnemyMgr _mgr;

        [SetUp]
        public void 建立管理器()
        {
            _mgr = new EnemyMgr();
        }

        [TearDown]
        public void 清理()
        {
            _mgr.Dispose();
        }

        [Test]
        public void 注册_重复注册只算一只()
        {
            假敌人 enemy = new();

            _mgr.Register(enemy);
            _mgr.Register(enemy);

            Assert.AreEqual(1, _mgr.ActiveCount, "同一只敌人注册两次也只占一个位置");
            Assert.AreEqual(0, _mgr.DeadCount, "还没死，回收表是空的");
        }

        [Test]
        public void 驱动_死亡当场迁进回收表并停用()
        {
            假敌人 enemy = new();

            _mgr.Register(enemy);
            _mgr.FixedTick(0.02f);

            enemy.IsDead = true;

            _mgr.FixedTick(0.02f);
            _mgr.FixedTick(0.02f);

            Assert.AreEqual(2, enemy.TickCount, "死掉那一帧之后再驱动就该跳过它");
            Assert.AreEqual(1, enemy.RecycleCount, "回收只做一次");
            Assert.AreEqual(0, _mgr.ActiveCount, "死后不再留在活动表");
            Assert.AreEqual(1, _mgr.DeadCount, "进回收表等刷新");
        }

        [Test]
        public void 刷新_存活与已死都回到出生状态()
        {
            假敌人 alive = new();
            假敌人 dead = new(EnemyKind.Warrior);

            _mgr.Register(alive);
            _mgr.Register(dead);

            dead.IsDead = true;
            _mgr.FixedTick(0.02f);

            _mgr.ResetAll();

            Assert.AreEqual(1, alive.ResetCount, "活着的敌人也要复位：追击、受伤、冷却一并清掉");
            Assert.AreEqual(1, dead.ResetCount, "死掉的敌人靠同一条复位路径复活");
            Assert.AreEqual(2, _mgr.ActiveCount, "两只都回到活动表");
            Assert.AreEqual(0, _mgr.DeadCount, "回收表清空");
            Assert.AreEqual(1, _mgr.ResetCount, "刷新次数记一次");
        }

        [Test]
        public void 刷新后_复活敌人照旧被驱动()
        {
            假敌人 enemy = new();

            _mgr.Register(enemy);
            enemy.IsDead = true;
            _mgr.FixedTick(0.02f);

            _mgr.ResetAll();
            _mgr.FixedTick(0.02f);

            Assert.AreEqual(2, enemy.TickCount, "复位后的下一帧要重新跑起来");
            Assert.IsFalse(enemy.IsDead, "复位把死亡标记清掉");
        }

        [Test]
        public void 退订_离开后不再被驱动()
        {
            假敌人 enemy = new();

            _mgr.Register(enemy);
            _mgr.Unregister(enemy);
            _mgr.FixedTick(0.02f);

            Assert.AreEqual(0, enemy.TickCount, "已经退订的敌人不该再被推进");
            Assert.AreEqual(0, _mgr.ActiveCount, "活动表里也没有它");
        }

        [Test]
        public void 顺序_按注册顺序驱动()
        {
            假敌人 first = new();
            假敌人 second = new();

            _mgr.Register(first);
            _mgr.Register(second);
            _mgr.FixedTick(0.02f);

            Assert.AreEqual(1, first.TickCount, "先注册先驱动");
            Assert.AreEqual(1, second.TickCount, "后注册的跟在后面");
            CollectionAssert.AreEqual(
                new[] { first, second },
                new[] { _mgr.Active[0], _mgr.Active[1] },
                "驱动顺序与本帧的列表顺序一致");
        }
    }
}
