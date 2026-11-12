using NUnit.Framework;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 存档点Test
    {
        [Test]
        public void 没存过档_取不到落点()
        {
            CheckpointBook book = new();

            Assert.IsFalse(book.TryGetRespawn(out _, out int level), "没存过档时死亡只能退回最近落脚点");
            Assert.AreEqual(0, level, "关卡 id 保持 0，表示还没有任何存档");
        }

        [Test]
        public void 存档后_取回同一关与落点()
        {
            CheckpointBook book = new();

            Assert.IsTrue(book.Save(3, new Vector2(4f, -2f)), "关卡 id 合法，存档应当成功");
            Assert.IsTrue(book.TryGetRespawn(out Vector2 pivot, out int level));

            Assert.AreEqual(3, level, "回到存档时所在的那一关");
            Assert.AreEqual(4f, pivot.x, 1e-4f);
            Assert.AreEqual(-2f, pivot.y, 1e-4f);
        }

        [Test]
        public void 再次存档_覆盖前一关()
        {
            CheckpointBook book = new();
            book.Save(1, new Vector2(1f, 0f));
            book.Save(2, new Vector2(9f, 5f));

            book.TryGetRespawn(out Vector2 pivot, out int level);

            Assert.AreEqual(2, level, "只保留最后一次存档的关卡");
            Assert.AreEqual(9f, pivot.x, 1e-4f);
            Assert.AreEqual(5f, pivot.y, 1e-4f);
        }

        [Test]
        public void 关卡非法_拒绝记录且不抹掉旧存档()
        {
            CheckpointBook book = new();
            book.Save(2, new Vector2(1f, 1f));

            Assert.IsFalse(book.Save(0, new Vector2(5f, 5f)), "场景不符合 Level_正整数，存下来只会把玩家送去死路");

            book.TryGetRespawn(out Vector2 pivot, out int level);

            Assert.AreEqual(2, level, "被拒绝的那次不能动到旧存档");
            Assert.AreEqual(1f, pivot.x, 1e-4f);
        }
    }
}
