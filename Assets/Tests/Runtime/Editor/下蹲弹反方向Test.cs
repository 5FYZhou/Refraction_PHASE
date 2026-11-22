using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Input;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>蹲着弹反仍按 W / S 分上下，只有没按竖直方向时才落到水平那一份</summary>
    public sealed class 下蹲弹反方向Test
    {
        private const float Dt = 0.02f;

        private PlayerConfig _config;
        private ReboundGroup _group;

        [SetUp]
        public void 建立弹反组()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.reboundMaxTime = 0.4f;
            _config.reboundCooldown = 0.2f;

            _group = new ReboundGroup(_config, null);
        }

        [TearDown]
        public void 释放弹反组()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 蹲着按上_开斜上弹反()
        {
            _group.Tick(Ctx(0f, 1f, true), true, true);

            Assert.IsTrue(_group.IsOpen, "蹲着按住攻击键照样能开弹反");
            Assert.AreEqual(BlackAttackKind.Up, _group.Kind, "蹲着按 W 开斜上那一份，不被水平压掉");
        }

        [Test]
        public void 蹲着按下_开斜下弹反()
        {
            _group.Tick(Ctx(0f, -1f, true), true, true);

            Assert.IsTrue(_group.IsOpen);
            Assert.AreEqual(BlackAttackKind.Down, _group.Kind, "蹲着按 S 开斜下那一份");
        }

        [Test]
        public void 蹲着不按方向_开水平弹反()
        {
            _group.Tick(Ctx(0f, 0f, true), true, true);

            Assert.IsTrue(_group.IsOpen);
            Assert.AreEqual(BlackAttackKind.Level, _group.Kind, "蹲着没按竖直方向才是水平");
        }

        [Test]
        public void 站着不按方向_开正前弹反()
        {
            _group.Tick(Ctx(0f, 0f, false), true, false);

            Assert.IsTrue(_group.IsOpen);
            Assert.AreEqual(BlackAttackKind.Ahead, _group.Kind, "站着仍是正前，不因为没蹲而变水平");
        }

        private static LogicContext Ctx(float now, float vertical, bool crouchHeld)
        {
            InputSnapshot input = new(new Vector2(0f, vertical), false, false, false, crouchHeld, true, false, attackHeld: true);
            WorldInfo world = new(true, false, Vector2.zero, 1, Vector2.zero);

            return new LogicContext(input, world, new GameTime(now, Dt));
        }
    }
}
