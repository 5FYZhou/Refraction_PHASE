using System;
using NUnit.Framework;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>关卡推进独立于战斗根：同关传送只靠 LevelManager 就够，门数据不对时绝不发落点</summary>
    public sealed class 关卡推进Test
    {
        private const int CurLevel = 3;
        private const int TargetDoor = 7;

        private readonly 假门索引 _doors = new();
        private readonly 记交棒 _transfer = new();

        private LevelManager _mgr;
        private RequestPlayerPosChange _got;
        private int _gotCount;

        [SetUp]
        public void 建立关卡()
        {
            _doors.LevelId = CurLevel;
            _mgr = new LevelManager(CurLevel, _doors, WorldPolarity.White, _transfer);
            _mgr.Init();

            _gotCount = 0;
            EventBus<RequestPlayerPosChange>.Subscribe(OnPosChange);
        }

        [TearDown]
        public void 清理关卡()
        {
            EventBus<RequestPlayerPosChange>.Unsubscribe(OnPosChange);
            EventBus<RequestNextDoor>.Clear();

            _mgr.Dispose();
            _mgr = null;
        }

        [Test]
        public void 同关传送_没有战斗根也把落点发给玩家()
        {
            EventBus<RequestNextDoor>.Publish(new(CurLevel, TargetDoor, MovementStateTag.Jump, -1));

            Assert.AreEqual(1, _gotCount, "门指向本关时应该发一次落点");
            Assert.AreEqual(_doors.Point, _got.Entry.Point, "落点取的是目标门的位置");
            Assert.AreEqual(MovementStateTag.Jump, _got.EnterTag, "进门时的移动状态要原样带过去");
            Assert.AreEqual(-1, _got.EnterFace, "进门朝向要原样带过去");
            Assert.AreEqual(0, _transfer.Calls, "同关传送不重载场景，不该走跨场景交棒");
        }

        [Test]
        public void 门没指定目标门_玩家原地不动()
        {
            EventBus<RequestNextDoor>.Publish(new(CurLevel, 0, MovementStateTag.Idle, 1));

            Assert.AreEqual(0, _gotCount, "nextDoor 非法时宁可不动，也不能把玩家丢到默认位置");
            Assert.AreEqual(0, _doors.Asked, "没给门 id 就不该去查门");
        }

        [Test]
        public void 门索引绑在别的关卡_拒绝按门编号传送()
        {
            _doors.LevelId = CurLevel + 1;

            EventBus<RequestNextDoor>.Publish(new(CurLevel, TargetDoor, MovementStateTag.Idle, 1));

            Assert.AreEqual(0, _gotCount, "同号门在别的关卡里是另一扇，按 id 落过去就是错的位置");
        }

        [Test]
        public void 本关没这扇门_玩家原地不动()
        {
            EventBus<RequestNextDoor>.Publish(new(CurLevel, TargetDoor + 1, MovementStateTag.Idle, 1));

            Assert.AreEqual(0, _gotCount, "查不到门时不发落点");
            Assert.AreEqual(TargetDoor + 1, _doors.Asked, "确实按请求的门 id 查过");
        }

        [Test]
        public void 退场后不再接管门()
        {
            _mgr.Dispose();

            EventBus<RequestNextDoor>.Publish(new(CurLevel, TargetDoor, MovementStateTag.Idle, 1));

            Assert.AreEqual(0, _gotCount, "Dispose 之后旧实例不许再响应门");
        }

        private void OnPosChange(RequestPlayerPosChange evt)
        {
            _got = evt;
            _gotCount++;
        }

        private sealed class 假门索引 : IDoorReader
        {
            public readonly Vector2 Point = new(5f, 2f);

            public int Asked;

            public int LevelId { get; set; }

            public bool TryGetEntry(int doorId, out DoorEntry entry)
            {
                Asked = doorId;

                if (doorId != TargetDoor)
                {
                    entry = default;
                    return false;
                }

                entry = new DoorEntry(Point, Vector2.right, default, false, default, false);
                return true;
            }
        }

        private sealed class 记交棒 : ISceneTransfer
        {
            public int Calls;

            public void RequestLevelLoad(int doorId, MovementStateTag enterTag, int enterFace) => Calls++;
        }
    }
}
