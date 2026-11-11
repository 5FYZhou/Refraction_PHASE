using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 残影逻辑Test
    {
        private const float Eps = 1e-4f;

        private PlayerConfig _config;
        private AfterimageLogic _afterimages;

        [SetUp]
        public void 建立残影系统()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.maxAfterimages = 4;
            _config.afterimageDuration = 8f;
            _config.afterimageLinkSenseRadius = 6f;

            _afterimages = new AfterimageLogic(_config);
        }

        [TearDown]
        public void 释放配置()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 没蓄满_冲刺不落影()
        {
            Assert.IsFalse(_afterimages.TrySpawn(Vector2.zero, 0f, 1), "没蓄满时残影系统不该生成");
            Assert.AreEqual(0, _afterimages.Ledger.Count, "账本保持为空");
        }

        [Test]
        public void 蓄满后落影_一枚只落一次()
        {
            _afterimages.Arm();

            Assert.IsTrue(_afterimages.TrySpawn(new Vector2(3f, 0f), 1f, 1), "蓄满后冲刺应当落影");
            Assert.IsFalse(_afterimages.TrySpawn(new Vector2(4f, 0f), 2f, 1), "同一份蓄力只换一枚残影");

            Assert.AreEqual(1, _afterimages.Ledger.Count, "第二次请求不该再加一枚");
            Assert.AreEqual(3f, _afterimages.Ledger[0].Pos.x, Eps, "落点取请求时给的位置");
            Assert.AreEqual(1f, _afterimages.Ledger[0].SpawnTime, Eps, "落地时刻取请求时给的时间");
        }

        [Test]
        public void 超过上限_顶掉最早的一枚()
        {
            _config.maxAfterimages = 2;

            for (int i = 0; i < 3; i++)
            {
                _afterimages.Arm();
                _afterimages.TrySpawn(new Vector2(i, 0f), i, 1);
            }

            Assert.AreEqual(2, _afterimages.Ledger.Count, "上限为 2 时不该涨到 3");
            Assert.AreEqual(1f, _afterimages.Ledger[0].Pos.x, Eps, "最早的（x=0）被顶掉");
            Assert.AreEqual(2f, _afterimages.Ledger[1].Pos.x, Eps, "新残影排在末尾");
            Assert.AreEqual(2, _afterimages.Ledger[1].Index, "序号不因顶掉而回收，表现层靠它认出换新");
        }

        [Test]
        public void 上限为零_不落影()
        {
            _config.maxAfterimages = 0;
            _afterimages.Arm();

            Assert.IsFalse(_afterimages.TrySpawn(Vector2.zero, 0f, 1), "上限为 0 时残影无处可放");
            Assert.AreEqual(0, _afterimages.Ledger.Count);
        }

        [Test]
        public void 到点过期_从头部清到第一枚活着的()
        {
            // 持续时间在落影那刻就写进残影里，改配置必须赶在 TrySpawn 之前
            _config.afterimageDuration = 1f;

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(0f, 0f), 0f, 1);

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(1f, 0f), 0.5f, 1);

            _afterimages.Tick(1.2f);
            Assert.AreEqual(1, _afterimages.Ledger.Count, "第一枚满 1 秒已过期，第二枚还没到点");

            _afterimages.Tick(1.6f);
            Assert.AreEqual(0, _afterimages.Ledger.Count, "两枚都过期后账本清空");
        }

        [Test]
        public void 持续时间为零_不按时间消失()
        {
            _config.afterimageDuration = 0f;
            _afterimages.Arm();
            _afterimages.TrySpawn(Vector2.zero, 0f, 1);

            _afterimages.Tick(999f);

            Assert.AreEqual(1, _afterimages.Ledger.Count, "0 表示只等被上限顶掉");
        }

        [Test]
        public void 连线段数_不足两枚为零_两枚一条_三枚三条()
        {
            Assert.AreEqual(0, _afterimages.LinkCapacity, "空账本没有连线");

            _afterimages.Arm();
            _afterimages.TrySpawn(Vector2.zero, 0f, 1);
            Assert.AreEqual(0, _afterimages.LinkCapacity, "只有一枚连不起来");

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(1f, 0f), 0f, 1);
            Assert.AreEqual(1, _afterimages.LinkCapacity, "两枚只有一条，首尾是同一对");

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(2f, 0f), 0f, 1);
            Assert.AreEqual(3, _afterimages.LinkCapacity, "三枚两两相邻，末枚接回首枚");
        }

        [Test]
        public void 连线_末段接回第一枚()
        {
            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(0f, 0f), 0f, 1);

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(3f, 0f), 0f, 1);

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(0f, 4f), 0f, 1);

            Assert.IsTrue(_afterimages.TryGetLink(2, out Vector2 from, out Vector2 to), "第三段应成立");
            Assert.AreEqual(0f, from.x, Eps);
            Assert.AreEqual(4f, from.y, Eps);
            Assert.AreEqual(0f, to.x, Eps);
            Assert.AreEqual(0f, to.y, Eps);
        }

        [Test]
        public void 超出感应范围_不连()
        {
            _afterimages.Arm();
            _afterimages.TrySpawn(Vector2.zero, 0f, 1);

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(10f, 0f), 0f, 1);

            Assert.IsFalse(_afterimages.TryGetLink(0, out _, out _), "跨图的两个残影不该拉出超长连线");

            _config.afterimageLinkSenseRadius = 12f;
            Assert.IsTrue(_afterimages.TryGetLink(0, out _, out _), "把感应范围放大就成立");
        }

        [Test]
        public void 感应范围为零_不限距离()
        {
            _config.afterimageLinkSenseRadius = 0f;

            _afterimages.Arm();
            _afterimages.TrySpawn(Vector2.zero, 0f, 1);

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(500f, 0f), 0f, 1);

            Assert.IsTrue(_afterimages.TryGetLink(0, out _, out _), "0 表示不限距离");
        }

        [Test]
        public void 两端重合_不连()
        {
            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(2f, 2f), 0f, 1);

            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(2f, 2f), 0f, 1);

            Assert.IsFalse(_afterimages.TryGetLink(0, out _, out _), "长度为 0 的线段画不出也没法判定");
        }

        [Test]
        public void 越界段号_不连()
        {
            _afterimages.Arm();
            _afterimages.TrySpawn(Vector2.zero, 0f, 1);

            _afterimages.Arm();
            _afterimages.TrySpawn(Vector2.right, 0f, 1);

            Assert.IsFalse(_afterimages.TryGetLink(-1, out _, out _), "负段号直接不成立");
            Assert.IsFalse(_afterimages.TryGetLink(_afterimages.LinkCapacity, out _, out _), "段号到上界之外不成立");
        }

        [Test]
        public void 清空_连线与待释放一起复位()
        {
            _afterimages.Arm();
            _afterimages.TrySpawn(Vector2.zero, 0f, 1);

            _afterimages.Arm();
            _afterimages.TrySpawn(Vector2.right, 0f, 1);

            _afterimages.Arm();
            _afterimages.Clear();

            Assert.AreEqual(0, _afterimages.Ledger.Count, "重生后不该留着上一处的残影");
            Assert.AreEqual(0, _afterimages.LinkCapacity, "连线一起没了");
            Assert.IsFalse(_afterimages.TrySpawn(Vector2.zero, 5f, 1), "清空也要把待释放状态清掉，不能拿旧的蓄力落影");
        }
    }
}
