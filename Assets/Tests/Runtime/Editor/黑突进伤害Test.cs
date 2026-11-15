using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Combat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Refraction.Tests
{
    public sealed class 黑突进伤害Test
    {
        private const float Diagonal = 0.70710678f;

        private PlayerConfig _config;
        private 突进场景 _scene;
        private BlackAttackView _view;
        private int _face = 1;
        private int _swingHits;

        [SetUp]
        public void 建立表现()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.blackAttackDamage = 7f;
            _config.blackAttackKnockback = 3f;
            _config.blackDashDamage = 11f;
            _config.blackDashKnockback = 5f;

            _face = 1;
            _swingHits = 0;

            _scene = new();
            _view = new(_scene.Root.transform, _config, () => _face);
            _view.SetHitHandler(() => _swingHits++);
        }

        [TearDown]
        public void 清理表现()
        {
            if (_view != null) _view.End();

            _scene.Dispose();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 突进盒下画出紫色实时范围()
        {
            _view.BeginDash(BlackAttackKind.Ahead);

            Transform range = _scene.DashAhead.transform.Find("Range");

            Assert.IsNotNull(range, "突进盒下要有紫色实时范围");
            MeshRenderer mr = range.GetComponent<MeshRenderer>();

            Assert.IsNotNull(mr, "要有渲染器才画得出来");
            Assert.Greater(VertexCount(range.gameObject), 2, "顶点够画一个面");
        }

        [Test]
        public void 突进画出绿色扫掠面_结束就清掉()
        {
            _view.BeginDash(BlackAttackKind.Ahead);

            GameObject sweep = GameObject.Find("DashSweep");
            Assert.IsNotNull(sweep, "突进开始要把扫过的范围画出来");

            MeshRenderer mr = sweep.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mr, "扫掠面要有渲染器");
            Assert.Greater(VertexCount(sweep), 2, "凸包至少能拼出三角面");

            float startX = _scene.DashAhead.transform.position.x;

            Assert.Greater(mr.bounds.max.x, startX + 1f, "扫掠面要沿突进方向延伸出去");
            Assert.Less(mr.sortingOrder, 30000, "绿色压在紫色实时范围下面");

            _view.End();
            Assert.IsNull(GameObject.Find("DashSweep"), "突进结束清掉绿色范围");
        }

        [Test]
        public void 扫掠面画在起手位置_玩家跑开也不跟着动()
        {
            _view.BeginDash(BlackAttackKind.Ahead);

            GameObject sweep = GameObject.Find("DashSweep");
            Bounds before = sweep.GetComponent<MeshRenderer>().bounds;

            _scene.Root.transform.position = new Vector3(0f, 6f, 0f);

            Bounds after = sweep.GetComponent<MeshRenderer>().bounds;

            Assert.AreEqual(before.center.y, after.center.y, 1e-3f, "扫过范围是已经发生过的区域，不跟着玩家挪");
            Assert.AreEqual(before.center.x, after.center.x, 1e-3f, "横向也不动");
        }

        [Test]
        public void 点亮突进_盒留在玩家层级里随人跑()
        {
            _view.BeginDash(BlackAttackKind.Ahead);

            Assert.IsTrue(_scene.DashAhead.activeSelf, "平推点亮 BlackDash/AheadCollider");
            Assert.IsFalse(_scene.DashUp.activeSelf, "另外两路不开");
            Assert.IsFalse(_scene.DashDown.activeSelf, "另外两路不开");

            _scene.Root.transform.position = new Vector3(5f, 3f, 0f);

            Assert.AreEqual(5f, _scene.DashAhead.transform.position.x, 1e-3f, "突进盒随玩家一起挪");
            Assert.AreEqual(3f, _scene.DashAhead.transform.position.y, 1e-3f, "纵向也一样，不冻在世界里");
        }

        [Test]
        public void 结束_三只突进盒全关()
        {
            _view.BeginDash(BlackAttackKind.Up);
            _view.End();

            Assert.IsFalse(_scene.DashAhead.activeSelf, "收尾要关平推");
            Assert.IsFalse(_scene.DashUp.activeSelf, "收尾要关斜上");
            Assert.IsFalse(_scene.DashDown.activeSelf, "收尾要关斜下");
            Assert.AreEqual(_scene.Root.transform, _scene.DashUp.transform.root, "关掉之后仍挂在玩家下面");
        }

        [Test]
        public void 突进伤害读自己那份配置()
        {
            _view.BeginDash(BlackAttackKind.Ahead);

            Damage dmg = _view.ProduceDamage();

            Assert.AreEqual(11f, dmg.Amount, 1e-4f, "伤害取 blackDashDamage，不是黑劈砍那份");
            Assert.AreEqual(5f, dmg.KnockbackSpeed, 1e-4f, "击退取 blackDashKnockback");
            Assert.AreEqual(DamageSource.Melee, dmg.Source, "突进是近战，玩家与敌人都按 Melee 过滤");
        }

        [Test]
        public void 突进命中不触发黑劈砍反冲()
        {
            _view.BeginDash(BlackAttackKind.Ahead);
            _view.ProduceDamage();

            Assert.AreEqual(0, _swingHits, "反冲回调只属于黑劈砍，突进撞到人不推自己");
        }

        [Test]
        public void 击退方向沿突进方向且按朝向镜像()
        {
            _view.BeginDash(BlackAttackKind.Up);
            Damage up = _view.ProduceDamage();

            Assert.AreEqual(Diagonal, up.Direction.x, 1e-3f, "斜上朝右：横向 0.707");
            Assert.AreEqual(Diagonal, up.Direction.y, 1e-3f, "斜上朝右：纵向 0.707");

            _face = -1;
            _view.BeginDash(BlackAttackKind.Down);
            Damage down = _view.ProduceDamage();

            Assert.AreEqual(-Diagonal, down.Direction.x, 1e-3f, "朝左斜下要往左");
            Assert.AreEqual(-Diagonal, down.Direction.y, 1e-3f, "朝左斜下要往下");

            _view.BeginDash(BlackAttackKind.Ahead);
            Damage ahead = _view.ProduceDamage();

            Assert.AreEqual(-1f, ahead.Direction.x, 1e-3f, "平推纯横向");
            Assert.AreEqual(0f, ahead.Direction.y, 1e-3f, "平推不带竖直分量");
        }

        [Test]
        public void 一次突进_同一个受击盒只结算一次()
        {
            HitLedger ledger = _scene.DashAhead.GetComponent<AttackBox>().OnceLedger;

            Assert.AreSame(ledger, _scene.DashUp.GetComponent<AttackBox>().OnceLedger, "三只突进盒共用同一份账本");
            Assert.AreSame(ledger, _scene.DashDown.GetComponent<AttackBox>().OnceLedger, "三只突进盒共用同一份账本");

            _view.BeginDash(BlackAttackKind.Ahead);

            Assert.IsTrue(ledger.TryMark(_scene.Target), "第一次碰到受击盒放行");
            Assert.IsFalse(ledger.TryMark(_scene.Target), "同一段突进里再碰一次不放行");
            Assert.IsFalse(_scene.DashUp.GetComponent<AttackBox>().OnceLedger.TryMark(_scene.Target), "换一只盒碰到同一个目标也不再结算");
            Assert.IsTrue(ledger.TryMark(_scene.Other), "换个受击盒照常结算，穿一排怪各吃一次");
        }

        [Test]
        public void 下一段突进重新记帐()
        {
            HitLedger ledger = _scene.DashAhead.GetComponent<AttackBox>().OnceLedger;

            _view.BeginDash(BlackAttackKind.Ahead);
            Assert.IsTrue(ledger.TryMark(_scene.Target), "第一段先记一次");
            Assert.IsFalse(ledger.TryMark(_scene.Target), "第一段里不重复");

            _view.End();
            _view.BeginDash(BlackAttackKind.Ahead);

            Assert.IsTrue(ledger.TryMark(_scene.Target), "第二段突进重新记帐，同一个敌人还能再吃一次");
        }

        [Test]
        public void 黑劈砍点亮时_突进盒一只不开()
        {
            _view.Begin(BlackAttackKind.Up);

            Assert.IsTrue(_scene.Up.activeSelf, "点亮黑劈砍的斜上判定");
            Assert.IsFalse(_scene.DashAhead.activeSelf, "突进盒不跟着开");
            Assert.IsFalse(_scene.DashUp.activeSelf, "突进盒不跟着开");
            Assert.IsFalse(_scene.DashDown.activeSelf, "突进盒不跟着开");
        }

        private static int VertexCount(GameObject go)
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;

            return mesh != null ? mesh.vertexCount : 0;
        }

        private sealed class 突进场景 : System.IDisposable
        {
            public readonly GameObject Root;
            public readonly GameObject Black;
            public readonly GameObject Ahead;
            public readonly GameObject Up;
            public readonly GameObject Down;
            public readonly GameObject Dash;
            public readonly GameObject DashAhead;
            public readonly GameObject DashUp;
            public readonly GameObject DashDown;
            public readonly GameObject Enemy;
            public readonly HurtBox Target;
            public readonly HurtBox Other;

            public 突进场景()
            {
                Root = new GameObject("Player");

                Black = Child(Root, "BlackAttack");
                Ahead = Box(Black, "AheadCollider");
                Up = Box(Black, "UpCollider");
                Down = Box(Black, "DownCollider");

                Dash = Child(Root, "BlackDash");
                DashAhead = Box(Dash, "AheadCollider");
                DashUp = Box(Dash, "UpCollider");
                DashDown = Box(Dash, "DownCollider");

                Enemy = new GameObject("Enemy");
                Target = Enemy.AddComponent<HurtBox>();
                Other = new GameObject("Enemy2").AddComponent<HurtBox>();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Ahead);
                Object.DestroyImmediate(Up);
                Object.DestroyImmediate(Down);
                Object.DestroyImmediate(Enemy);
                Object.DestroyImmediate(Other.gameObject);
                Object.DestroyImmediate(Root);
            }

            private static GameObject Child(GameObject parent, string name)
            {
                GameObject go = new(name);
                go.transform.SetParent(parent.transform, false);
                return go;
            }

            private static GameObject Box(GameObject parent, string name)
            {
                GameObject go = Child(parent, name);
                go.AddComponent<BoxCollider2D>();
                go.AddComponent<AttackBox>();
                return go;
            }
        }
    }
}
