using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 外部速度Test
    {
        private const float Dt = 0.02f;
        private const float Eps = 1e-4f;

        private PlayerConfig _config;
        private MoveLedger _ledger;
        private Vector2 _body;

        [SetUp]
        public void 建立账本()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.moveAcceleration = 0f;
            _config.moveDeceleration = 0f;
            _config.turnAcceleration = 0f;
            _config.externalDeceleration = 100f;

            _body = Vector2.zero;
            _ledger = new MoveLedger(() => _body, v => _body = v);
            _ledger.Configure(_config);
        }

        [TearDown]
        public void 释放配置()
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void 后坐力_只进外部速度_不改常规速度()
        {
            _ledger.BeginStep(0f, Dt);
            _ledger.SetVelocityX(0f);
            _ledger.AddExternalVelocity(new Vector2(8f, 0f));
            _ledger.Commit();

            Assert.AreEqual(8f, _body.x, Eps, "刚体速度 = 常规 0 + 外部 8");

            _ledger.BeginStep(Dt, Dt);
            Assert.AreEqual(0f, _ledger.FrameStartVelocity.x, Eps, "帧首常规速度不该被外部速度污染");

            _ledger.SetVelocityX(0f);
            _ledger.Commit();
            Assert.AreEqual(6f, _body.x, Eps, "外部速度按 100/s 衰减一帧后为 6");
        }

        [Test]
        public void 反向抵消_常规负向_外部正向()
        {
            _ledger.BeginStep(0f, Dt);
            _ledger.SetVelocityX(-8f);
            _ledger.AddExternalVelocity(new Vector2(8f, 0f));
            _ledger.Commit();
            Assert.AreEqual(0f, _body.x, Eps, "常规 -8 与外部 +8 抵消，刚体应为 0");

            _ledger.BeginStep(Dt, Dt);
            Assert.AreEqual(-8f, _ledger.FrameStartVelocity.x, Eps, "抵消不是撞墙：常规速度应原样保留 -8");

            _ledger.SetVelocityX(-8f);
            _ledger.Commit();
            Assert.AreEqual(-2f, _body.x, Eps, "刚体 = 常规 -8 + 衰减后的外部 +6");
        }

        [Test]
        public void 撞墙_刚体被物理清零后丢弃冲量()
        {
            _ledger.BeginStep(0f, Dt);
            _ledger.SetVelocityX(0f);
            _ledger.AddExternalVelocity(new Vector2(8f, 0f));
            _ledger.Commit();
            Assert.AreEqual(8f, _body.x, Eps);

            _body = Vector2.zero;

            _ledger.BeginStep(Dt, Dt);
            Assert.AreEqual(0f, _ledger.FrameStartVelocity.x, Eps, "被物理吃掉的冲量不该倒扣成反向常规速度");

            _ledger.SetVelocityX(0f);
            _ledger.Commit();
            Assert.AreEqual(0f, _body.x, Eps, "冲量作废后不该贴着墙往回弹");
        }

        [Test]
        public void 连续攻击_外部速度累加()
        {
            _ledger.BeginStep(0f, Dt);
            _ledger.AddExternalVelocity(new Vector2(8f, 0f));
            _ledger.AddExternalVelocity(new Vector2(8f, 0f));
            _ledger.Commit();

            Assert.AreEqual(16f, _body.x, Eps, "两次后坐力叠加为 16");
        }

        [Test]
        public void 冲刺覆盖常规速度_外部速度仍叠加()
        {
            _ledger.BeginStep(0f, Dt);
            _ledger.AddExternalVelocity(new Vector2(4f, 0f));
            _ledger.SetVelocityX(30f);
            _ledger.Commit();

            Assert.AreEqual(34f, _body.x, Eps, "冲刺只写常规部分，残留冲量照旧叠加");
        }

        [Test]
        public void 瞬移_清空外部速度与记账()
        {
            _ledger.BeginStep(0f, Dt);
            _ledger.AddExternalVelocity(new Vector2(8f, 0f));
            _ledger.Commit();
            Assert.AreEqual(8f, _body.x, Eps);

            _body = new Vector2(3f, 0f);
            _ledger.ResetExternal();

            _ledger.BeginStep(Dt, Dt);
            Assert.AreEqual(3f, _ledger.FrameStartVelocity.x, Eps, "瞬移后帧首就是给定初速度，不该再剥旧记账");

            _ledger.SetVelocityX(3f);
            _ledger.Commit();
            Assert.AreEqual(3f, _body.x, Eps, "外部速度已清空，提交时不再叠加");
        }

        [Test]
        public void 衰减_线性归零_不越过零反向()
        {
            _ledger.BeginStep(0f, Dt);
            _ledger.AddExternalVelocity(new Vector2(1f, 0f));
            _ledger.Commit();
            Assert.AreEqual(1f, _body.x, Eps);

            _ledger.BeginStep(Dt, Dt);
            _ledger.SetVelocityX(0f);
            _ledger.Commit();
            Assert.AreEqual(0f, _body.x, Eps, "单帧衰减量大于冲量时停在 0，不反向");
        }
    }
}
