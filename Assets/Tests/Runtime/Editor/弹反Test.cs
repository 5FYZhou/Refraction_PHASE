using System.Collections.Generic;
using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>黑形态弹反：范围随黑攻击开关、留在玩家层级里跟随转身；敌弹反向飞出并换成玩家子弹的伤害</summary>
    public sealed class 弹反Test
    {
        private const float Eps = 1e-4f;

        private PlayerConfig _config;

        [SetUp]
        public void 建立参数()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.bulletDamage = 1f;
            _config.bulletKnockback = 3f;
            _config.bulletTransferSpeed = 26f;
        }

        [TearDown]
        public void 清理参数() => Object.DestroyImmediate(_config);

        [Test]
        public void 弹反范围_按逻辑指令开关_且不脱开玩家层级()
        {
            using 玩家层级 scene = new();

            BlackAttackView view = new(scene.Root.transform, _config, () => 1);

            AssertAllReboundOff(scene, "构造完弹反范围必须全关，不能自带来判定");

            view.BeginFlat();

            AssertAllReboundOff(scene, "普攻不再自己开弹反，开关归逻辑层");

            view.ShowRebound(BlackAttackKind.Ahead, 1);

            Assert.IsTrue(scene.ReboundAhead.activeSelf, "指令正前只开 BlackRebound/AheadCollider");
            AssertAllReboundOff(scene, "正前时另外两只弹反范围必须关着", scene.ReboundAhead);
            Assert.AreEqual(scene.Rebound.transform, scene.ReboundAhead.transform.parent, "弹反范围留在 BlackRebound 下，不跟着攻击盒摘出去");

            view.ShowRebound(BlackAttackKind.Up, 1);

            Assert.IsTrue(scene.ReboundUp.activeSelf, "斜上开 BlackRebound/UpCollider");
            AssertAllReboundOff(scene, "斜上时另外两只弹反范围必须关着", scene.ReboundUp);

            view.ShowRebound(BlackAttackKind.Down, -1);

            Assert.IsTrue(scene.ReboundDown.activeSelf, "斜下开 BlackRebound/DownCollider");
            AssertAllReboundOff(scene, "斜下时另外两只弹反范围必须关着", scene.ReboundDown);

            view.End();

            Assert.IsTrue(scene.ReboundDown.activeSelf, "近战结束不该顺手关掉逻辑层还开着的弹反");

            view.HideRebound();

            AssertAllReboundOff(scene, "逻辑层收手，弹反范围要跟着关");
            Assert.AreEqual(scene.Rebound.transform, scene.ReboundAhead.transform.parent, "关掉后仍留在玩家层级里");
        }

        [Test]
        public void 弹反左右_与朝向相反时镜像到另一侧()
        {
            using 玩家层级 scene = new();

            BlackAttackView view = new(scene.Root.transform, _config, () => 1);

            view.ShowRebound(BlackAttackKind.Ahead, 1);

            Vector3 local = scene.ReboundAhead.transform.localPosition;

            Assert.AreEqual(1f, scene.ReboundAhead.transform.position.x, Eps, "朝右、指令也朝右时弹反范围在身体右侧");

            view.ShowRebound(BlackAttackKind.Ahead, -1);

            Assert.AreEqual(-1f, scene.ReboundAhead.transform.position.x, Eps, "指令朝左要镜像到左侧");
            Assert.AreEqual(local.x, scene.ReboundAhead.transform.localPosition.x, Eps, "镜像靠 BlackRebound 翻 scale，局部坐标不许被改写");
            Assert.AreEqual(scene.Rebound.transform, scene.ReboundAhead.transform.parent, "翻身不改层级");
        }

        [Test]
        public void 玩家转身_同一侧指令仍然落在世界同一侧()
        {
            using 玩家层级 scene = new();

            int face = 1;
            BlackAttackView view = new(scene.Root.transform, _config, () => face);

            view.ShowRebound(BlackAttackKind.Ahead, 1);

            Assert.AreEqual(1f, scene.ReboundAhead.transform.position.x, Eps, "朝右时正前范围偏右");

            face = -1;
            scene.Root.transform.localScale = new Vector3(-1f, 1f, 1f);
            view.ShowRebound(BlackAttackKind.Ahead, 1);

            Assert.AreEqual(1f, scene.ReboundAhead.transform.position.x, Eps, "转身后同一个右侧指令要翻回来，不能跟着人跑到另一侧");
        }

        [Test]
        public void 弹反_反向同速_伤害换成玩家子弹()
        {
            GameObject host = new("BulletField");
            BulletField field = host.AddComponent<BulletField>();
            真子弹工厂 factory = new(field);
            BulletLogic enemy = new(factory, false);
            记开火 launcher = new();

            enemy.Launch(new BulletSpawn(new Vector2(3f, 1f), new Vector2(-1f, 0f), 12f, 9f, 4f, 26f));

            Bullet bullet = factory.Last;

            Assert.IsFalse(bullet.PlayerOwned, "敌人打出的子弹不属于玩家这一侧");

            Assert.IsTrue(bullet.TryReflect(launcher, 1f, 3f, 26f), "飞行中的敌弹应该能被弹反");
            Assert.AreEqual(1, launcher.Spawns.Count, "弹反要由玩家那一侧的发射口重发一发");

            BulletSpawn spawn = launcher.Spawns[0];

            Assert.AreEqual(1f, spawn.Direction.x, Eps, "子弹向相反方向飞出");
            Assert.AreEqual(0f, spawn.Direction.y, Eps, "反向不引入额外竖直分量");
            Assert.AreEqual(12f, spawn.Speed, Eps, "飞行速度沿用打进弹反范围的那一发");
            Assert.AreEqual(1f, spawn.Damage, Eps, "弹反后重新附上玩家的子弹伤害");
            Assert.AreEqual(3f, spawn.KnockbackSpeed, Eps, "击退也换成玩家配置那一份");
            Assert.AreEqual(26f, spawn.TransferSpeed, Eps, "弹反出来的子弹照样能进残影");
            Assert.AreEqual(0, enemy.Count, "原弹在弹反当帧就从敌方系统里收走");

            Object.DestroyImmediate(host);
            Object.DestroyImmediate(factory.Last.gameObject);
        }

        [Test]
        public void 身体里的子弹_按受击优先_不算弹反()
        {
            using 玩家层级 scene = new();

            BoxCollider2D hurt = scene.Root.AddComponent<BoxCollider2D>();
            hurt.size = new Vector2(0.9f, 1.8f);

            BlackAttackView view = new(scene.Root.transform, _config, () => 1);
            view.BindRebound(() => null, hurt);

            Assert.IsTrue(view.InHurtRange(Vector2.zero), "身体中心属于受击范围");
            Assert.IsTrue(view.InHurtRange(new Vector2(0.4f, 0.8f)), "受击盒右上角仍在受击范围内");
            Assert.IsFalse(view.InHurtRange(new Vector2(1.2f, 0f)), "身体外的弹反范围不算受击");

            view.BindRebound(() => null, null);

            Assert.IsFalse(view.InHurtRange(Vector2.zero), "没有受击盒时不挡弹反");
        }

        private static void AssertAllReboundOff(玩家层级 scene, string message, GameObject except = null)
        {
            if (scene.ReboundAhead != except) Assert.IsFalse(scene.ReboundAhead.activeSelf, $"{message}（AheadCollider 仍开着）");
            if (scene.ReboundUp != except) Assert.IsFalse(scene.ReboundUp.activeSelf, $"{message}（UpCollider 仍开着）");
            if (scene.ReboundDown != except) Assert.IsFalse(scene.ReboundDown.activeSelf, $"{message}（DownCollider 仍开着）");
        }

        /// <summary>搭 Player/BlackDash 与 Player/BlackRebound 两套名字，正是 BlackAttackView 按路径找的那套</summary>
        private sealed class 玩家层级 : System.IDisposable
        {
            public readonly GameObject Root;
            public readonly GameObject Dash;
            public readonly GameObject Rebound;
            public readonly GameObject DashAhead;
            public readonly GameObject DashUp;
            public readonly GameObject DashDown;
            public readonly GameObject ReboundAhead;
            public readonly GameObject ReboundUp;
            public readonly GameObject ReboundDown;

            public 玩家层级()
            {
                Root = new GameObject("Player");
                Dash = Child(Root, "BlackDash");
                DashAhead = Box(Dash, "AheadCollider", new Vector3(1f, 0f, 0f));
                DashUp = Box(Dash, "UpCollider", new Vector3(0f, 1f, 0f));
                DashDown = Box(Dash, "DownCollider", new Vector3(0f, -1f, 0f));

                Rebound = Child(Root, "BlackRebound");
                ReboundAhead = Child(Rebound, "AheadCollider");
                ReboundUp = Child(Rebound, "UpCollider");
                ReboundDown = Child(Rebound, "DownCollider");

                ReboundAhead.transform.localPosition = new Vector3(1f, 0f, 0f);
                ReboundUp.transform.localPosition = new Vector3(0f, 1f, 0f);
                ReboundDown.transform.localPosition = new Vector3(0f, -1f, 0f);

                ReboundAhead.AddComponent<BoxCollider2D>().isTrigger = true;
                ReboundUp.AddComponent<BoxCollider2D>().isTrigger = true;
                ReboundDown.AddComponent<BoxCollider2D>().isTrigger = true;
            }

            public void Dispose()
            {
                // 攻击盒点亮时会摘成根物体，销毁玩家带不走它们
                Object.DestroyImmediate(DashAhead);
                Object.DestroyImmediate(DashUp);
                Object.DestroyImmediate(DashDown);
                Object.DestroyImmediate(Root);
            }

            private static GameObject Child(GameObject parent, string name)
            {
                GameObject go = new(name);
                go.transform.SetParent(parent.transform, false);
                return go;
            }

            private static GameObject Box(GameObject parent, string name, Vector3 local)
            {
                GameObject go = Child(parent, name);
                go.transform.localPosition = local;
                go.AddComponent<BoxCollider2D>();
                go.AddComponent<AttackBox>();
                return go;
            }
        }

        private sealed class 记开火 : IBulletLauncher
        {
            public readonly List<BulletSpawn> Spawns = new();

            public void Launch(in BulletSpawn spawn) => Spawns.Add(spawn);
        }

        private sealed class 真子弹工厂 : IBulletViewFactory
        {
            private readonly BulletField _home;

            public Bullet Last;

            public 真子弹工厂(BulletField home) => _home = home;

            public IBulletView Create(BulletLogic system, int id)
            {
                GameObject go = new("Bullet");
                go.AddComponent<BoxCollider2D>().isTrigger = true;

                Bullet bullet = go.AddComponent<Bullet>();
                bullet.Bind(_home, system, id, null, null);

                Last = bullet;

                return bullet;
            }
        }
    }
}
