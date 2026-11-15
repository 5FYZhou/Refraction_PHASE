using System.Collections.Generic;
using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 子弹出射方向Test
    {
        private sealed class 假视图 : IBulletView
        {
            public BulletPose Pose;

            public Vector2 Position => Pose.Pos;

            public void Apply(in BulletPose pose) => Pose = pose;

            public void Kill() { }
        }

        private sealed class 假工厂 : IBulletViewFactory
        {
            public readonly List<假视图> Made = new();

            public IBulletView Create(BulletLogic system, int id)
            {
                var view = new 假视图();
                Made.Add(view);

                return view;
            }
        }

        private PlayerConfig _config;
        private AfterimageLogic _afterimages;
        private 假工厂 _factory;
        private BulletLogic _bullets;

        [SetUp]
        public void 建立系统()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.maxAfterimages = 4;
            _config.afterimageDuration = 8f;
            _config.afterimageLinkSenseRadius = 6f;

            _afterimages = new AfterimageLogic(_config);
            _factory = new 假工厂();
            _bullets = new BulletLogic(_factory, false);
        }

        [TearDown]
        public void 释放配置()
        {
            Object.DestroyImmediate(_config);
        }

        private int 落影(float x, int facing)
        {
            _afterimages.Arm();
            _afterimages.TrySpawn(new Vector2(x, 0f), 0f, facing);

            AfterimageLedger ledger = _afterimages.Ledger;
            return ledger[ledger.Count - 1].Index;
        }

        // 打一发进链、把整条路径走完，返回出射那一帧的姿势
        private BulletPose 走完传输(Vector2 direction, int entry)
        {
            _bullets.Launch(new BulletSpawn(Vector2.zero, direction, 10f, 1f, 0f, 1000f));

            Assert.IsTrue(_bullets.TryEnterTransport(0, _afterimages, entry), "两枚残影在感应范围内，应该能接管");

            _bullets.Tick(0.1f);
            _bullets.Tick(0.1f);

            Assert.AreEqual(BulletState.Flying, _factory.Made[0].Pose.State, "路径走完应该出射");

            return _factory.Made[0].Pose;
        }

        [Test]
        public void 顺着残影飞进来_沿残影朝向出射()
        {
            落影(0f, 1);
            int entry = 落影(1f, 1);

            BulletPose pose = 走完传输(Vector2.right, entry);

            Assert.AreEqual(1f, pose.Dir.x, 1e-3f, "飞行方向与残影同向，出射也同向");
            Assert.AreEqual(0f, pose.Dir.y, 1e-3f);
        }

        [Test]
        public void 顶着残影飞进来_反向出射()
        {
            落影(0f, 1);
            int entry = 落影(1f, 1);

            BulletPose pose = 走完传输(Vector2.left, entry);

            Assert.AreEqual(-1f, pose.Dir.x, 1e-3f, "迎着头飞进来的，出射也要迎着头，不能被弹回原路");
        }

        [Test]
        public void 出射朝向_取飞出那一枚()
        {
            落影(0f, -1);
            int entry = 落影(1f, 1);

            BulletPose pose = 走完传输(Vector2.right, entry);

            Assert.AreEqual(-1f, pose.Dir.x, 1e-3f, "飞出的是朝左那枚，就从左边出去");
        }

        [Test]
        public void 垂直飞进来_判不出逆向_沿用残影朝向()
        {
            落影(0f, 1);
            int entry = 落影(1f, 1);

            BulletPose pose = 走完传输(new Vector2(0f, -1f), entry);

            Assert.Greater(pose.Dir.x, 0f, "水平分量为零时判不出逆向，按残影朝向出");
            Assert.Less(pose.Dir.y, 0f, "垂直分量沿用进入方向");
        }
    }
}
