using NUnit.Framework;
using Refraction.Logic.Events;
using Refraction.Logic.Level;

namespace Refraction.Tests
{
    public sealed class 世界反转Test
    {
        private sealed class 记色地面 : IReverse
        {
            public WorldPolarity Applied;

            public int Count;

            public void Apply(WorldPolarity world)
            {
                Applied = world;
                Count++;
            }
        }

        private LevelManager _level;
        private 记色地面 _ground;

        [SetUp]
        public void 建立关卡()
        {
            _level = new LevelManager(1);
            _level.Init();

            _ground = new 记色地面();
            _level.RegisterReverse(_ground);
        }

        [TearDown]
        public void 拆掉关卡()
        {
            _level.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
        }

        [Test]
        public void 登记_立刻套一次当前极性()
        {
            Assert.AreEqual(WorldPolarity.White, _ground.Applied, "登记时就把当前世界推给地面，晚登记的不会停在错误颜色");
            Assert.AreEqual(1, _ground.Count, "登记只套一次，不重复");
        }

        [Test]
        public void 反转_黑白互换_并把新极性推给地面()
        {
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            Assert.AreEqual(WorldPolarity.Black, _level.World, "白世界反转一次是黑");
            Assert.AreEqual(WorldPolarity.Black, _ground.Applied, "地面跟着换到黑");

            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.White));

            Assert.AreEqual(WorldPolarity.White, _level.World, "再反转一次回到白");
            Assert.AreEqual(WorldPolarity.White, _ground.Applied, "地面跟着回白");
        }

        [Test]
        public void 跨关保留_新关卡从常驻极性起步()
        {
            LevelManager next = new(2, null, WorldPolarity.Black);
            next.Init();

            记色地面 ground = new();
            next.RegisterReverse(ground);

            Assert.AreEqual(WorldPolarity.Black, next.World, "新关卡的世界极性要接住上一关留下的黑");
            Assert.AreEqual(WorldPolarity.Black, ground.Applied, "新场景的地面装配时就该是黑的");

            next.Dispose();
        }

        [Test]
        public void 释放后_不再响应反转()
        {
            _level.Dispose();
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));

            Assert.AreEqual(WorldPolarity.White, _ground.Applied, "Dispose 之后不该再收到世界极性");
            Assert.AreEqual(1, _ground.Count, "已注销的关卡不再推极性");
        }
    }
}
