using System.Collections.Generic;
using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 残影传输Test
    {
        private PlayerConfig _config;
        private AfterimageLogic _afterimages;
        private readonly List<TransferNode> _path = new();

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

        private int 落影(float x, float y = 0f, int facing = 1)
        {
            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(x, y), 0f, facing);

            AfterimageLedger ledger = _afterimages.Ledger;
            return ledger[ledger.Count - 1].Index;
        }

        [Test]
        public void 单枚残影_路径只有自己()
        {
            int entry = 落影(0f);

            Assert.AreEqual(1, _afterimages.BuildTransfer(entry, _path), "只有一枚时路径就是它自己");
            Assert.AreEqual(0f, _path[0].Pos.x, 1e-4f);
        }

        [Test]
        public void 从中间进入_按生成顺序绕环()
        {
            int first = 落影(0f);
            int second = 落影(1f);
            int third = 落影(2f);
            int fourth = 落影(3f);

            Assert.AreEqual(4, _afterimages.BuildTransfer(third, _path), "四枚都在感应范围内，整环都能走");

            Assert.AreEqual(third, _path[0].Index, "第一站是进入的那一枚");
            Assert.AreEqual(fourth, _path[1].Index, "先往后走");
            Assert.AreEqual(first, _path[2].Index, "走到末位接回第一枚");
            Assert.AreEqual(second, _path[3].Index, "再接着往下走");

            Assert.AreEqual(2f, _path[0].Pos.x, 1e-4f);
            Assert.AreEqual(3f, _path[1].Pos.x, 1e-4f);
            Assert.AreEqual(0f, _path[2].Pos.x, 1e-4f);
            Assert.AreEqual(1f, _path[3].Pos.x, 1e-4f, "位置也要跟着顺序排好");
        }

        [Test]
        public void 断链处即路径末尾()
        {
            int first = 落影(0f);
            int second = 落影(1f);
            int third = 落影(2f);
            落影(30f);

            Assert.AreEqual(3, _afterimages.BuildTransfer(first, _path), "2 号到 30 号那一跳超范围，路径到此为止");
            Assert.AreEqual(third, _path[2].Index, "最后落脚的是链上最后一枚活着的");
            Assert.AreEqual(first, _path[0].Index);
            Assert.AreEqual(second, _path[1].Index);
        }

        [Test]
        public void 感应范围为零_不限距离()
        {
            _config.afterimageLinkSenseRadius = 0f;

            int first = 落影(0f);
            落影(100f);

            Assert.AreEqual(2, _afterimages.BuildTransfer(first, _path), "0 表示不限距离，再远也算一跳");
        }

        [Test]
        public void 两端重合_不算一跳()
        {
            int entry = 落影(2f, 2f);
            落影(2f, 2f);

            Assert.AreEqual(1, _afterimages.BuildTransfer(entry, _path), "位置重合的残影之间没有可走的距离");
        }

        [Test]
        public void 入口序号不在了_路径为空()
        {
            int entry = 落影(0f);

            _afterimages.Tick(100f);

            Assert.AreEqual(0, _afterimages.BuildTransfer(entry, _path), "残影已经过期，规划不出路径");
        }

        [Test]
        public void 验活_过期的与顶掉的都不算()
        {
            _config.maxAfterimages = 2;

            int first = 落影(0f);
            int second = 落影(1f);

            Assert.IsTrue(_afterimages.IsAlive(first));
            Assert.IsTrue(_afterimages.IsAlive(second));

            int third = 落影(2f);

            Assert.IsFalse(_afterimages.IsAlive(first), "被上限顶掉的那一枚已经不在场上");
            Assert.IsTrue(_afterimages.IsAlive(third), "新落的那一枚在");

            _afterimages.Tick(100f);

            Assert.IsFalse(_afterimages.IsAlive(third), "过期后同样验不过");
        }

        [Test]
        public void 出射朝向_取落影那一帧()
        {
            int entry = 落影(0f, 0f, -1);

            _afterimages.BuildTransfer(entry, _path);

            Assert.AreEqual(-1, _path[0].Facing, "残影朝左，子弹就从左边出来");
        }
    }
}
