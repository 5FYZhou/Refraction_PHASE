using NUnit.Framework;
using Refraction.Logic.Actor;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 场景上下文Test
    {
        [Test]
        public void 门落点_取一次就清空()
        {
            SceneContext scene = new();
            scene.SetNextDoor(7, MovementStateTag.Dash, 1);

            Assert.AreEqual(7, scene.TakeNextDoor(out MovementStateTag tag, out int face), "第一次取到门 7");
            Assert.AreEqual(MovementStateTag.Dash, tag, "进门时的移动状态原样带出");
            Assert.AreEqual(1, face, "进门朝向原样带出");

            Assert.AreEqual(0, scene.TakeNextDoor(out tag, out face), "取过一次就清空，重复取拿到 0");
            Assert.AreEqual(MovementStateTag.None, tag, "清空后移动状态回到 None");
            Assert.AreEqual(0, face, "清空后朝向回到 0");
        }

        [Test]
        public void 进门口_取一次就清空()
        {
            SceneContext scene = new();
            DoorEntry entry = new(new Vector2(3f, 4f), Vector2.right, default, false, default, false);

            scene.SetEntry(in entry, MovementStateTag.Dash, -1);

            Assert.IsTrue(scene.TryConsumeEntry(out DoorEntry first, out MovementStateTag tag, out int face), "有落点时返回 true");
            Assert.AreEqual(new Vector2(3f, 4f), first.Point, "落点坐标原样带出");
            Assert.AreEqual(MovementStateTag.Dash, tag, "进门时的移动状态原样带出");
            Assert.AreEqual(-1, face, "进门朝向原样带出");

            Assert.IsFalse(scene.TryConsumeEntry(out _, out _, out _), "第二次取不到：残留落点会把之后的重载丢到错的门口");
        }

        [Test]
        public void 待复活点_取一次就清空()
        {
            SceneContext scene = new();
            scene.BeginRespawn(new Vector2(9f, 2f));

            Assert.IsTrue(scene.TryConsumeRespawn(out Vector2 pivot), "有复活点时返回 true");
            Assert.AreEqual(new Vector2(9f, 2f), pivot, "复活坐标原样带出");
            Assert.IsFalse(scene.TryConsumeRespawn(out _), "复活点只生效一次");
        }

        [Test]
        public void 加载失败_取消复活点不影响已有存档()
        {
            SceneContext scene = new();
            scene.SaveCheckpoint(2, new Vector2(5f, 5f));
            scene.BeginRespawn(new Vector2(9f, 2f));

            scene.CancelRespawn();

            Assert.IsFalse(scene.TryConsumeRespawn(out _), "取消后没有待复活点");
            Assert.IsTrue(scene.TryGetCheckpoint(out Vector2 pivot, out int level), "取消复活不该抹掉存档点");
            Assert.AreEqual(2, level, "存档关卡不变");
            Assert.AreEqual(new Vector2(5f, 5f), pivot, "存档坐标不变");
        }

        [Test]
        public void 反转_黑白互换()
        {
            SceneContext scene = new();

            Assert.AreEqual(WorldPolarity.White, scene.World, "世界初始为白");
            Assert.IsFalse(scene.IsBlackWorld, "初始不是黑世界");

            scene.ReverseWorld();
            Assert.AreEqual(WorldPolarity.Black, scene.World, "白反转一次是黑");

            scene.ReverseWorld();
            Assert.AreEqual(WorldPolarity.White, scene.World, "再反转一次回到白");
        }

        [Test]
        public void 重置_清掉跨关落点但保留存档点()
        {
            SceneContext scene = new();
            scene.ReverseWorld();
            scene.SetNextDoor(3, MovementStateTag.Dash, 1);
            scene.BeginRespawn(new Vector2(9f, 2f));
            scene.SaveCheckpoint(1, new Vector2(1f, 1f));

            scene.Reset();

            Assert.AreEqual(WorldPolarity.White, scene.World, "退出播放后世界回到白");
            Assert.AreEqual(0, scene.TakeNextDoor(out _, out _), "门落点被清掉");
            Assert.IsFalse(scene.TryConsumeRespawn(out _), "待复活点被清掉");
            Assert.IsTrue(scene.TryGetCheckpoint(out _, out _), "存档点保留，重进仍从上次存档继续");
        }
    }
}
