using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>弹反范围的时长与冷却：只有黑形态能开，按住维持、松键即关、到最长时限自动关，关掉才开始算冷却</summary>
    public sealed class 弹反控制Test
    {
        private const float Dt = 0.02f;
        private const float MaxTime = 0.4f;
        private const float Cooldown = 0.6f;

        private PlayerConfig _config;
        private 记范围 _view;
        private ReboundGroup _group;

        [SetUp]
        public void 建立弹反()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.reboundMaxTime = MaxTime;
            _config.reboundCooldown = Cooldown;

            _view = new();
            _group = new(_config, _view);
        }

        [TearDown]
        public void 清理参数() => Object.DestroyImmediate(_config);

        [Test]
        public void 黑形态_只按攻击键_没按方向_按朝向开()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, -1, Vector2.zero), true);

            Assert.IsTrue(_group.IsOpen, "有方向就按方向开，没有方向也得按朝向开");
            Assert.AreEqual(BlackAttackKind.Ahead, _group.Kind, "没有方向输入走正前");
            Assert.AreEqual(-1, _group.Side, "正前不带方向时取当前朝向");
            Assert.IsTrue(_view.Open, "方向要下发给表现层");
        }

        [Test]
        public void 按住W_斜上_左右取当前速度()
        {
            _group.Tick(Ctx(0f, new Vector2(0f, 1f), true, 1, new Vector2(-4f, 3f)), true);

            Assert.AreEqual(BlackAttackKind.Up, _group.Kind, "按 W 走斜上弹反");
            Assert.AreEqual(-1, _group.Side, "斜上的左右跟着速度走，不看朝向");
        }

        [Test]
        public void 按住S_斜下()
        {
            _group.Tick(Ctx(0f, new Vector2(0f, -1f), true, -1, new Vector2(5f, -2f)), true);

            Assert.AreEqual(BlackAttackKind.Down, _group.Kind, "按 S 走斜下弹反");
            Assert.AreEqual(1, _group.Side, "斜下的左右同样跟着速度");
        }

        [Test]
        public void 斜向静止时_左右回落朝向()
        {
            _group.Tick(Ctx(0f, new Vector2(0f, 1f), true, -1, Vector2.zero), true);

            Assert.AreEqual(BlackAttackKind.Up, _group.Kind, "静止也照样走斜上");
            Assert.AreEqual(-1, _group.Side, "没有速度就用朝向定左右");
        }

        [Test]
        public void 按A走左侧_按D走右侧()
        {
            _group.Tick(Ctx(0f, new Vector2(-1f, 0f), true, 1, Vector2.zero), true);

            Assert.AreEqual(BlackAttackKind.Ahead, _group.Kind, "左右键仍然是正前范围");
            Assert.AreEqual(-1, _group.Side, "按 A 翻到左侧");

            _group.Tick(Ctx(0.02f, new Vector2(1f, 0f), true, 1, Vector2.zero), true);

            Assert.AreEqual(1, _group.Side, "按 D 回到右侧");
        }

        [Test]
        public void 白形态_按住攻击键_不开()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, 1, Vector2.zero), false);

            Assert.IsFalse(_group.IsOpen, "白形态没有弹反");
            Assert.IsFalse(_view.Open, "表现层也不许点亮");
        }

        [Test]
        public void 转白那一帧_已经开着的弹反跟着关()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsTrue(_group.IsOpen, "黑形态先开着");

            _group.Tick(Ctx(0.02f, Vector2.zero, true, 1, Vector2.zero), false);

            Assert.IsFalse(_group.IsOpen, "世界转白就收手，不用等松键");
            Assert.IsFalse(_view.Open, "表现层同样要收到关闭");
        }

        [Test]
        public void 按住不动_每帧都下发一次()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, 1, Vector2.zero), true);
            _group.Tick(Ctx(0.02f, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.AreEqual(2, _view.Shows, "每帧都要下发，表现层才能跟着转身改镜像");
        }

        [Test]
        public void 松开攻击键_当帧就关()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, 1, Vector2.zero), true);

            _group.Tick(Ctx(0.1f, Vector2.zero, false, 1, Vector2.zero), true);

            Assert.IsFalse(_group.IsOpen, "持续时间跟按键走，松开当帧即关");
            Assert.IsFalse(_view.Open, "表现层也要收到关闭");
        }

        [Test]
        public void 按住超过最长时长_自动关()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, 1, Vector2.zero), true);

            _group.Tick(Ctx(MaxTime - 0.02f, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsTrue(_group.IsOpen, "没到最长时限就一直在");

            _group.Tick(Ctx(MaxTime, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsFalse(_group.IsOpen, "到最长时限自动关，不用等松键");
            Assert.IsFalse(_view.Open, "表现层也要收到关闭");
        }

        [Test]
        public void 到点关了_按住不放也不自动重开()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, 1, Vector2.zero), true);
            _group.Tick(Ctx(MaxTime, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsFalse(_group.IsOpen, "到点先关");

            _group.Tick(Ctx(MaxTime + Cooldown + 0.2f, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsFalse(_group.IsOpen, "冷却早过了但手还没松，必须松开重按才能再开");
        }

        [Test]
        public void 松开重按_冷却没走完不开_走完才开()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, 1, Vector2.zero), true);
            _group.Tick(Ctx(0.1f, Vector2.zero, false, 1, Vector2.zero), true);

            Assert.IsFalse(_group.IsOpen, "松开先关，冷却从这一刻起算");

            _group.Tick(Ctx(0.2f, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsFalse(_group.IsOpen, "冷却没走完，重按也不开");

            _group.Tick(Ctx(0.3f, Vector2.zero, false, 1, Vector2.zero), true);
            _group.Tick(Ctx(0.1f + Cooldown, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsTrue(_group.IsOpen, "冷却走完再重按就重新打开");
        }

        [Test]
        public void 冷却按关掉那一刻起算_不是按下那一刻()
        {
            _group.Tick(Ctx(0f, Vector2.zero, true, 1, Vector2.zero), true);
            _group.Tick(Ctx(MaxTime, Vector2.zero, true, 1, Vector2.zero), true);

            _group.Tick(Ctx(MaxTime + Cooldown - 0.02f, Vector2.zero, false, 1, Vector2.zero), true);
            _group.Tick(Ctx(MaxTime + Cooldown - 0.02f, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsFalse(_group.IsOpen, "差一帧也不给开");

            _group.Tick(Ctx(MaxTime + Cooldown, Vector2.zero, false, 1, Vector2.zero), true);
            _group.Tick(Ctx(MaxTime + Cooldown, Vector2.zero, true, 1, Vector2.zero), true);

            Assert.IsTrue(_group.IsOpen, "从关掉那一刻起算满冷却才重新打开");
        }

        private static LogicContext Ctx(float now, Vector2 move, bool attackHeld, int face, Vector2 velocity)
            => new(new InputSnapshot(move, false, false, false, false, false, false, false, false, attackHeld),
                   new WorldInfo(true, false, velocity, face, Vector2.zero),
                   new GameTime(now, Dt));

        private sealed class 记范围 : IReboundView
        {
            public bool Open;

            public int Shows;

            public void ShowRebound(BlackAttackKind kind, int side)
            {
                Open = true;
                Shows++;
            }

            public void HideRebound() => Open = false;
        }
    }
}
