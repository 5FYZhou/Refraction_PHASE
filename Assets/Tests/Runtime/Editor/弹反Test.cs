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
        public void 弹反_就地改写归属_反向同速且不换实例()
        {
            BulletLogic system = new();
            GameObject fieldHost = new("BulletField");
            BulletField field = fieldHost.AddComponent<BulletField>();
            GameObject bulletHost = new("Bullet");
            bulletHost.AddComponent<BoxCollider2D>().isTrigger = true;

            Bullet bullet = bulletHost.AddComponent<Bullet>();

            system.Launch(
                new BulletShooter(1, 0, BulletFaction.Enemy),
                new BulletSpawn(new Vector2(3f, 1f), Vector2.left, 12f, 9f, 4f, 8f));

            Assert.IsTrue(system.TryTakeRequest(out BulletRequest request), "发射先记成请求，等 CombatRoot 取走");

            bullet.Initialize(null, field, system, in request, null, null);
            system.AttachView(request.Id, bullet);

            Assert.AreEqual(BulletFaction.Enemy, bullet.Faction, "敌人打出的子弹属于敌方");
            Assert.AreEqual(1, field.LiveCount, "实例要登记进活跃子弹场");
            Assert.AreEqual(1, system.Count, "逻辑层账上就这一发");

            Vector2 inbound = bullet.ReflectDirection;

            Assert.AreEqual(1f, inbound.x, Eps, "弹反方向取当前弹道的反向");

            BulletRebind rebind = new(BulletFaction.Player, 1f, 3f, 26f);

            Assert.IsTrue(bullet.TryReflect(in rebind, null, null), "飞行中的敌弹应该能被弹反");
            Assert.AreEqual(BulletFaction.Player, bullet.Faction, "弹反后这一发归玩家，同籍判定自然换边");
            Assert.AreEqual(1f, bullet.ProduceDamage().Amount, Eps, "伤害换成弹反者那一份");
            Assert.AreEqual(3f, bullet.ProduceDamage().KnockbackSpeed, Eps, "击退也换成弹反者那一份");

            Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();

            Assert.AreEqual(12f, body.velocity.x, Eps, "弹反后按原速朝反方向飞出");
            Assert.AreEqual(0f, body.velocity.y, Eps, "反向不引入额外竖直分量");
            Assert.AreEqual(1, system.Count, "原子弹继续在飞，没有销毁重发");
            Assert.AreEqual(1, field.LiveCount, "也没有换实例");

            Object.DestroyImmediate(bulletHost);
            Object.DestroyImmediate(fieldHost);
        }

        [Test]
        public void 弹反_换敌我两位_穿墙不变()
        {
            const int GroundLayer = 6;
            const int PlayerLayer = 7;
            const int EnemyLayer = 8;

            BulletLogic system = new();
            GameObject fieldHost = new("BulletField");
            BulletField field = fieldHost.AddComponent<BulletField>();
            GameObject bulletHost = new("Bullet");
            BoxCollider2D box = bulletHost.AddComponent<BoxCollider2D>();

            box.isTrigger = true;

            Bullet bullet = bulletHost.AddComponent<Bullet>();

            system.Launch(
                new BulletShooter(1, 0, BulletFaction.Enemy),
                new BulletSpawn(new Vector2(3f, 1f), Vector2.left, 12f, 9f, 4f, 8f));

            Assert.IsTrue(system.TryTakeRequest(out BulletRequest request), "发射先记成请求");

            bullet.Initialize(null, field, system, in request, null, null);

            Assert.IsTrue(排掉了(box, EnemyLayer), "敌弹初发要排敌人层，别打到自己人");
            Assert.IsTrue(排掉了(box, GroundLayer), "敌弹初发就穿墙：地面层排掉");
            Assert.IsFalse(排掉了(box, PlayerLayer), "敌弹初发要留着玩家层，否则打不到人");

            BulletRebind rebind = new(BulletFaction.Player, 1f, 3f, 26f);

            Assert.IsTrue(bullet.TryReflect(in rebind, null, null), "飞行中的敌弹应该能被弹反");

            Assert.IsFalse(排掉了(box, EnemyLayer), "弹反后归玩家，敌人层不能再排，否则打不到敌人");
            Assert.IsTrue(排掉了(box, PlayerLayer), "玩家自己的受击盒要排掉");
            Assert.IsTrue(排掉了(box, GroundLayer), "穿墙跟着子弹出处，弹反不动这一位");

            Object.DestroyImmediate(bulletHost);
            Object.DestroyImmediate(fieldHost);
        }

        private static bool 排掉了(Collider2D collider, int layer) => (collider.excludeLayers & (1 << layer)) != 0;

        [Test]
        public void 身体里的子弹_按受击优先_不算弹反()
        {
            using 玩家层级 scene = new();

            BoxCollider2D hurt = scene.Root.AddComponent<BoxCollider2D>();
            hurt.size = new Vector2(0.9f, 1.8f);

            BlackAttackView view = new(scene.Root.transform, _config, () => 1);
            view.BindRebound(null, hurt);

            Assert.IsTrue(view.InHurtRange(Vector2.zero), "身体中心属于受击范围");
            Assert.IsTrue(view.InHurtRange(new Vector2(0.4f, 0.8f)), "受击盒右上角仍在受击范围内");
            Assert.IsFalse(view.InHurtRange(new Vector2(1.2f, 0f)), "身体外的弹反范围不算受击");

            view.BindRebound(null, null);

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
    }
}
