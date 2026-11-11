using NUnit.Framework;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 残影账本Test
    {
        private const float Eps = 1e-4f;

        private AfterimageLedger _ledger;

        [SetUp]
        public void 建立账本()
        {
            _ledger = new AfterimageLedger();
        }

        [Test]
        public void 落影_按生成顺序记账_序号递增()
        {
            _ledger.Place(new Vector2(0f, 0f), 0f, 4, 8f);
            _ledger.Place(new Vector2(1f, 0f), 1f, 4, 8f);

            Assert.AreEqual(2, _ledger.Count, "落两枚就该有两枚");
            Assert.AreEqual(0, _ledger[0].Index, "第一枚序号为 0");
            Assert.AreEqual(1, _ledger[1].Index, "第二枚序号为 1");
            Assert.AreEqual(1f, _ledger[1].Pos.x, Eps, "位置原样记下");
            Assert.AreEqual(1f, _ledger[1].SpawnTime, Eps, "落地时刻原样记下");
        }

        [Test]
        public void 超过上限_顶掉最早的一枚()
        {
            _ledger.Place(new Vector2(0f, 0f), 0f, 2, 0f);
            _ledger.Place(new Vector2(1f, 0f), 1f, 2, 0f);
            _ledger.Place(new Vector2(2f, 0f), 2f, 2, 0f);

            Assert.AreEqual(2, _ledger.Count, "上限为 2 时不该涨到 3");
            Assert.AreEqual(1f, _ledger[0].Pos.x, Eps, "最早的（x=0）被顶掉");
            Assert.AreEqual(2f, _ledger[1].Pos.x, Eps, "新残影排在末尾");
            Assert.AreEqual(2, _ledger[1].Index, "序号不因顶掉而回收，表现层靠它认出换新");
        }

        [Test]
        public void 上限为零_不落影()
        {
            _ledger.Place(Vector2.zero, 0f, 0, 0f);

            Assert.AreEqual(0, _ledger.Count, "上限为 0 时残影无处可放");
        }

        [Test]
        public void 到点过期_从头部清到第一枚活着的()
        {
            _ledger.Place(new Vector2(0f, 0f), 0f, 8, 1f);
            _ledger.Place(new Vector2(1f, 0f), 0.5f, 8, 1f);

            _ledger.Prune(1.2f);
            Assert.AreEqual(1, _ledger.Count, "第一枚满 1 秒已过期，第二枚还没到点");

            _ledger.Prune(1.6f);
            Assert.AreEqual(0, _ledger.Count, "两枚都过期后账本清空");
        }

        [Test]
        public void 持续时间为零_不按时间消失()
        {
            _ledger.Place(Vector2.zero, 0f, 4, 0f);

            _ledger.Prune(999f);

            Assert.AreEqual(1, _ledger.Count, "0 表示只等被上限顶掉");
        }

        [Test]
        public void 连线段数_不足两枚为零_两枚一条_三枚三条()
        {
            Assert.AreEqual(0, _ledger.LinkCapacity, "空账本没有连线");

            _ledger.Place(Vector2.zero, 0f, 8, 0f);
            Assert.AreEqual(0, _ledger.LinkCapacity, "只有一枚连不起来");

            _ledger.Place(new Vector2(1f, 0f), 0f, 8, 0f);
            Assert.AreEqual(1, _ledger.LinkCapacity, "两枚只有一条，首尾是同一对");

            _ledger.Place(new Vector2(2f, 0f), 0f, 8, 0f);
            Assert.AreEqual(3, _ledger.LinkCapacity, "三枚两两相邻，末枚接回首枚");
        }

        [Test]
        public void 连线_末段接回第一枚()
        {
            _ledger.Place(new Vector2(0f, 0f), 0f, 8, 0f);
            _ledger.Place(new Vector2(3f, 0f), 0f, 8, 0f);
            _ledger.Place(new Vector2(0f, 4f), 0f, 8, 0f);

            Assert.IsTrue(_ledger.TryGetLink(2, 0f, out Vector2 from, out Vector2 to), "第三段应成立");
            Assert.AreEqual(0f, from.x, Eps);
            Assert.AreEqual(4f, from.y, Eps);
            Assert.AreEqual(0f, to.x, Eps);
            Assert.AreEqual(0f, to.y, Eps);
        }

        [Test]
        public void 超出感应范围_不连()
        {
            _ledger.Place(Vector2.zero, 0f, 8, 0f);
            _ledger.Place(new Vector2(10f, 0f), 0f, 8, 0f);

            Assert.IsFalse(_ledger.TryGetLink(0, 6f, out _, out _), "跨图的两个残影不该拉出超长连线");
            Assert.IsTrue(_ledger.TryGetLink(0, 12f, out _, out _), "把感应范围放大就成立");
        }

        [Test]
        public void 感应范围为零_不限距离()
        {
            _ledger.Place(Vector2.zero, 0f, 8, 0f);
            _ledger.Place(new Vector2(500f, 0f), 0f, 8, 0f);

            Assert.IsTrue(_ledger.TryGetLink(0, 0f, out _, out _), "0 表示不限距离");
        }

        [Test]
        public void 两端重合_不连()
        {
            _ledger.Place(new Vector2(2f, 2f), 0f, 8, 0f);
            _ledger.Place(new Vector2(2f, 2f), 0f, 8, 0f);

            Assert.IsFalse(_ledger.TryGetLink(0, 0f, out _, out _), "长度为 0 的线段画不出也没法判定");
        }

        [Test]
        public void 越界段号_不连()
        {
            _ledger.Place(Vector2.zero, 0f, 8, 0f);
            _ledger.Place(Vector2.right, 0f, 8, 0f);

            Assert.IsFalse(_ledger.TryGetLink(-1, 0f, out _, out _), "负段号直接不成立");
            Assert.IsFalse(_ledger.TryGetLink(_ledger.LinkCapacity, 0f, out _, out _), "段号到上界之外不成立");
        }

        [Test]
        public void 清空_连线一起没了()
        {
            _ledger.Place(Vector2.zero, 0f, 8, 0f);
            _ledger.Place(Vector2.right, 0f, 8, 0f);

            _ledger.Clear();

            Assert.AreEqual(0, _ledger.Count);
            Assert.AreEqual(0, _ledger.LinkCapacity, "跨场景传送后不该留着上一关的连线");
        }
    }
}
