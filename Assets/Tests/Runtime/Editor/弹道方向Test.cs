using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 弹道方向Test
    {
        private const float Eps = 1e-3f;
        private const float Diagonal = 0.70710678f;

        private PlayerConfig _config;

        [SetUp]
        public void 建立配置()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.recoilSpeedX = 8f;
            _config.recoilSpeedUp = 6f;
            _config.recoilSpeedDown = 4f;
        }

        [TearDown]
        public void 释放配置()
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void 平射_朝右()
        {
            Vector2 dir = AttackAim.Direction(1, 0f);
            Assert.AreEqual(1f, dir.x, Eps);
            Assert.AreEqual(0f, dir.y, Eps);
        }

        [Test]
        public void 平射_朝左()
        {
            Vector2 dir = AttackAim.Direction(-1, 0f);
            Assert.AreEqual(-1f, dir.x, Eps);
            Assert.AreEqual(0f, dir.y, Eps);
        }

        [Test]
        public void 斜上_跟随朝向()
        {
            Vector2 right = AttackAim.Direction(1, 1f);
            Assert.AreEqual(Diagonal, right.x, Eps, "朝右按上应为右上 45°");
            Assert.AreEqual(Diagonal, right.y, Eps);
            Assert.AreEqual(1f, right.magnitude, Eps, "斜向仍是单位向量");

            Vector2 left = AttackAim.Direction(-1, 1f);
            Assert.AreEqual(-Diagonal, left.x, Eps, "朝左按上应为左上 45°");
            Assert.AreEqual(Diagonal, left.y, Eps);
        }

        [Test]
        public void 斜下_跟随朝向()
        {
            Vector2 right = AttackAim.Direction(1, -1f);
            Assert.AreEqual(Diagonal, right.x, Eps);
            Assert.AreEqual(-Diagonal, right.y, Eps);

            Vector2 left = AttackAim.Direction(-1, -1f);
            Assert.AreEqual(-Diagonal, left.x, Eps);
            Assert.AreEqual(-Diagonal, left.y, Eps);
        }

        [Test]
        public void 竖直输入很小时不算斜射()
        {
            Vector2 dir = AttackAim.Direction(1, 0.4f);
            Assert.AreEqual(0f, dir.y, Eps, "摇杆漂移不该把平射变成斜射");
        }

        [Test]
        public void 后坐力_平射沿反向()
        {
            Vector2 recoil = AttackAim.RecoilSpeed(new Vector2(1f, 0f), _config);
            Assert.AreEqual(-8f, recoil.x, Eps);
            Assert.AreEqual(0f, recoil.y, Eps);
        }

        [Test]
        public void 后坐力_斜上读斜上参数且方向相反()
        {
            Vector2 recoil = AttackAim.RecoilSpeed(AttackAim.Direction(1, 1f), _config);
            Assert.AreEqual(-Diagonal * 6f, recoil.x, Eps, "弹道右上，后坐力应朝左下");
            Assert.AreEqual(-Diagonal * 6f, recoil.y, Eps);
        }

        [Test]
        public void 后坐力_斜下读斜下参数()
        {
            Vector2 recoil = AttackAim.RecoilSpeed(AttackAim.Direction(-1, -1f), _config);
            Assert.AreEqual(Diagonal * 4f, recoil.x, Eps, "弹道左下，后坐力应朝右上");
            Assert.AreEqual(Diagonal * 4f, recoil.y, Eps);
        }
    }
}
