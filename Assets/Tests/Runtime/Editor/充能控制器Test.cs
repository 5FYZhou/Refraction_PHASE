using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 充能控制器Test
    {
        private const float Prepare = 0.5f;
        private const float Cooldown = 1f;
        private const float Eps = 1e-4f;

        private PlayerConfig _config;
        private ChargeController _charge;

        [SetUp]
        public void 建立充能器()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.chargePrepareTime = Prepare;
            _config.chargeCooldown = Cooldown;
            _config.maxAfterimages = 4;
            _config.afterimageDuration = 8f;

            _charge = new ChargeController(_config);
        }

        [TearDown]
        public void 释放配置()
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        private static LogicContext 造帧(float now, bool 按下充能 = false, float 位置x = 0f)
        {
            InputSnapshot input = new(Vector2.zero, false, false, false, false, false, 按下充能);
            WorldInfo world = new(true, false, 0, Vector2.zero, 1, new Vector2(位置x, 0f));
            return new LogicContext(input, world, new GameTime(now, 0.02f));
        }

        [Test]
        public void 按下沿即开始蓄力_不必按住()
        {
            LogicContext 按下 = 造帧(0f, 按下充能: true);
            _charge.Tick(in 按下, false);

            Assert.AreEqual(ChargePhase.Charging, _charge.Phase, "按一下就该进充能");

            LogicContext 松手 = 造帧(0.25f);
            _charge.Tick(in 松手, false);

            Assert.AreEqual(ChargePhase.Charging, _charge.Phase, "松手不该取消，充能照常进行");
            Assert.AreEqual(0.5f, _charge.Progress, Eps, "进度 = 0.25 / 0.5");
        }

        [Test]
        public void 前摇走满_转蓄满不再自己回落()
        {
            _charge.Tick(造帧(0f, 按下充能: true), false);
            _charge.Tick(造帧(Prepare), false);

            Assert.AreEqual(ChargePhase.Charged, _charge.Phase, "前摇走满应停在蓄满");

            _charge.Tick(造帧(50f), false);

            Assert.AreEqual(ChargePhase.Charged, _charge.Phase, "蓄满不会自己松开，只有冲刺才释放");
            Assert.AreEqual(1f, _charge.Progress, Eps, "蓄满后进度报 1，表现层据此变橙");
        }

        [Test]
        public void 蓄满后冲刺_在冲刺起手位置落影()
        {
            _charge.Tick(造帧(0f, 按下充能: true), false);
            _charge.Tick(造帧(Prepare), false);

            LogicContext 冲刺 = 造帧(Prepare + 1f, 位置x: 7f);
            _charge.Tick(in 冲刺, true);

            Assert.AreEqual(1, _charge.Afterimages.Count, "蓄满后冲刺应落一枚残影");
            Assert.AreEqual(7f, _charge.Afterimages[0].Pos.x, Eps, "落点取冲刺起手那一帧的位置");
            Assert.AreEqual(ChargePhase.None, _charge.Phase, "释放后回到空闲");
        }

        [Test]
        public void 未蓄满就冲刺_不落影也不进冷却()
        {
            _charge.Tick(造帧(0f, 按下充能: true), false);

            LogicContext 冲刺 = 造帧(0.1f, 位置x: 3f);
            _charge.Tick(in 冲刺, true);

            Assert.AreEqual(0, _charge.Afterimages.Count, "还在前摇里，这次冲刺不落影");
            Assert.AreEqual(ChargePhase.Charging, _charge.Phase, "充能继续，不会被冲刺打断");

            _charge.Tick(造帧(Prepare), false);
            _charge.Tick(造帧(Prepare + 1f, 位置x: 4f), true);

            Assert.AreEqual(1, _charge.Afterimages.Count, "蓄满之后再冲才落影");
            Assert.AreEqual(4f, _charge.Afterimages[0].Pos.x, Eps, "落点跟着这一次冲刺走");
        }

        [Test]
        public void 冷却内再按_不响应()
        {
            _charge.Tick(造帧(0f, 按下充能: true), false);
            _charge.Tick(造帧(Prepare), false);
            _charge.Tick(造帧(Prepare + 1f, 位置x: 1f), true);

            _charge.Tick(造帧(Prepare + 1.1f, 按下充能: true), false);

            Assert.AreEqual(ChargePhase.None, _charge.Phase, "冷却没到，按键不该重新蓄力");

            _charge.Tick(造帧(Prepare + 1f + Cooldown, 按下充能: true), false);

            Assert.AreEqual(ChargePhase.Charging, _charge.Phase, "冷却一到就能再蓄");
        }

        [Test]
        public void 蓄满时再按_不重开前摇()
        {
            _charge.Tick(造帧(0f, 按下充能: true), false);
            _charge.Tick(造帧(Prepare), false);

            _charge.Tick(造帧(Prepare + 0.1f, 按下充能: true), false);

            Assert.AreEqual(ChargePhase.Charged, _charge.Phase, "蓄满期间按键不该把它打回前摇");
        }

        [Test]
        public void 前摇配零_按下即蓄满()
        {
            _config.chargePrepareTime = 0f;

            _charge.Tick(造帧(0f, 按下充能: true), false);

            Assert.AreEqual(ChargePhase.Charged, _charge.Phase, "前摇为 0 时按下当帧就蓄满");
        }

        [Test]
        public void 清空残影_账本归零()
        {
            _charge.Tick(造帧(0f, 按下充能: true), false);
            _charge.Tick(造帧(Prepare), false);
            _charge.Tick(造帧(Prepare + 1f), true);

            Assert.AreEqual(1, _charge.Afterimages.Count);

            _charge.ClearAfterimages();

            Assert.AreEqual(0, _charge.Afterimages.Count, "跨场景传送后不该留着上一关的残影");
        }
    }
}
