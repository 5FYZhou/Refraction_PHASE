using NUnit.Framework;
using Refraction.Logic.Actor;

namespace Refraction.Tests
{
    public sealed class 极性值Test
    {
        [Test]
        public void 初始_为零()
        {
            PolarityGauge gauge = new(100);

            Assert.AreEqual(0, gauge.Value, "开局没有极性");
            Assert.AreEqual(100, gauge.Max, "上限取自配置");
        }

        [Test]
        public void 累加_按每次有效攻击的值()
        {
            PolarityGauge gauge = new(100);

            gauge.Add(5);
            gauge.Add(5);

            Assert.AreEqual(10, gauge.Value, "两次有效攻击 = 2 × 5");
        }

        [Test]
        public void 加满_停在最大值不溢出()
        {
            PolarityGauge gauge = new(10);

            gauge.Add(7);
            gauge.Add(7);

            Assert.AreEqual(10, gauge.Value, "7 + 7 超过上限 10，应停在 10");
        }

        [Test]
        public void 单次加值超过上限_仍停在最大值()
        {
            PolarityGauge gauge = new(10);

            gauge.Add(int.MaxValue);

            Assert.AreEqual(10, gauge.Value, "一次加爆也只会到上限，不会溢出成负数");
        }

        [Test]
        public void 加负值_不改变也不为负()
        {
            PolarityGauge gauge = new(10);

            gauge.Add(-5);

            Assert.AreEqual(0, gauge.Value, "负值被忽略，极性不会掉到 0 以下");
        }

        [Test]
        public void 已满再攻击_保持不变()
        {
            PolarityGauge gauge = new(10);
            gauge.Add(10);

            gauge.Add(5);

            Assert.AreEqual(10, gauge.Value, "满值后再命中仍是满值");
        }

        [Test]
        public void 清空_归零()
        {
            PolarityGauge gauge = new(10);
            gauge.Add(10);

            gauge.Clear();

            Assert.AreEqual(0, gauge.Value, "清空后从 0 重新累计");
        }

        [Test]
        public void 上限配成零_按一处理()
        {
            PolarityGauge gauge = new(0);

            gauge.Add(5);

            Assert.AreEqual(1, gauge.Max, "上限至少为 1，避免整套系统静默失效");
            Assert.AreEqual(1, gauge.Value, "上限为 1 时加值也停在 1");
        }
    }
}
