using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Combat;
using UnityEngine;

namespace Refraction.Tests
{
    public sealed class 黑攻击判定Test
    {
        private const float Dt = 0.02f;

        private PlayerConfig _config;

        [SetUp]
        public void 建立参数()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.blackAttackDamage = 7f;
            _config.blackAttackKnockback = 3f;
            _config.blackAttackTime = 0.2f;
            _config.blackAttackMissRecoilAhead = 1f;
            _config.blackAttackMissRecoilUp = 3f;
            _config.blackAttackMissRecoilDown = 5f;
            _config.blackAttackHitRecoilAhead = 2f;
            _config.blackAttackHitRecoilUp = 10f;
            _config.blackAttackHitRecoilDown = 16f;
        }

        [TearDown]
        public void 清理参数() => Object.DestroyImmediate(_config);

        private static LogicContext Ctx(float vertical, float now = 0f, int face = 1)
        {
            InputSnapshot input = new(new Vector2(0f, vertical), false, false, false, false, true, false);
            WorldInfo world = new(true, false, 0, Vector2.zero, face, Vector2.zero);

            return new LogicContext(input, world, new GameTime(now, Dt));
        }

        [Test]
        public void 竖直输入_决定三种判定()
        {
            Assert.AreEqual(BlackAttackKind.Up, AttackAim.Kind(1f), "按住上：斜上");
            Assert.AreEqual(BlackAttackKind.Down, AttackAim.Kind(-1f), "按住下：斜下");
            Assert.AreEqual(BlackAttackKind.Ahead, AttackAim.Kind(0f), "只有攻击键：平推");
            Assert.AreEqual(BlackAttackKind.Ahead, AttackAim.Kind(0.5f), "阈值以内仍算平推，吸收摇杆漂移");
        }

        [Test]
        public void 进本体_点亮对应判定_出本体_全部关掉()
        {
            记判定的表现记录 view = new();
            BlackAttackState state = new(_config, view);

            state.Enter(Ctx(1f));
            Assert.AreEqual(BlackAttackKind.Up, view.BeginKind, "按住上时点亮斜上判定");
            Assert.AreEqual(BlackAttackKind.Up, state.Kind, "状态自己也要记下本次判定");

            state.Exit();
            Assert.IsTrue(view.Ended, "离开本体要关掉攻击盒，否则站着不动也会一直打人");
        }

        [Test]
        public void 三个攻击盒_只点亮命中的那一个()
        {
            using 层级场景 scene = new();

            BlackAttackView view = new(scene.Root.transform, _config, () => 1);

            AssertAllOff(scene, "构造完只留空判，不能自带来伤害");

            view.Begin(BlackAttackKind.Ahead);
            Assert.IsTrue(scene.Ahead.activeSelf, "平推只开 AheadCollider");
            AssertAllOff(scene, "平推时另外两只必须关着", scene.Ahead);

            view.Begin(BlackAttackKind.Up);
            Assert.IsTrue(scene.Up.activeSelf, "斜上只开 UpCollider");
            AssertAllOff(scene, "斜上时另外两只必须关着", scene.Up);

            view.Begin(BlackAttackKind.Down);
            Assert.IsTrue(scene.Down.activeSelf, "斜下只开 DownCollider");
            AssertAllOff(scene, "斜下时另外两只必须关着", scene.Down);

            view.End();
            AssertAllOff(scene, "结束后三只都要关");
        }

        [Test]
        public void 伤害_走配置_方向随判定与朝向()
        {
            using 层级场景 scene = new();
            BlackAttackView view = new(scene.Root.transform, _config, () => 1);

            view.Begin(BlackAttackKind.Ahead);
            Damage ahead = view.ProduceDamage();
            Assert.AreEqual(7f, ahead.Amount, "伤害取 blackAttackDamage");
            Assert.AreEqual(3f, ahead.KnockbackSpeed, "击退取 blackAttackKnockback");
            Assert.AreEqual(1f, ahead.Direction.x, 1e-4f, "平推沿朝向");
            Assert.AreEqual(0f, ahead.Direction.y, 1e-4f, "平推不带竖直分量");

            view.Begin(BlackAttackKind.Up);
            Damage up = view.ProduceDamage();
            Assert.Greater(up.Direction.y, 0f, "斜上判定要往上打");
            Assert.AreEqual(0.70710678f, up.Direction.x, 1e-4f, "斜上横向分量为 cos45");

            view.Begin(BlackAttackKind.Down);
            Damage down = view.ProduceDamage();
            Assert.Less(down.Direction.y, 0f, "斜下判定要往下打");
        }

        [Test]
        public void 朝左时_方向镜像()
        {
            using 层级场景 scene = new();
            BlackAttackView view = new(scene.Root.transform, _config, () => -1);

            view.Begin(BlackAttackKind.Ahead);
            Assert.AreEqual(-1f, view.ProduceDamage().Direction.x, 1e-4f, "朝左时平推要朝左");

            view.Begin(BlackAttackKind.Up);
            Assert.Less(view.ProduceDamage().Direction.x, 0f, "朝左时斜上也要朝左");
        }

        [Test]
        public void 点亮判定时_盒下画出范围色块()
        {
            using 层级场景 scene = new();
            BlackAttackView view = new(scene.Root.transform, _config, () => 1);

            AssertRange(scene.Ahead, "平推盒要能看见范围");
            AssertRange(scene.Up, "斜上盒要能看见范围");
            AssertRange(scene.Down, "斜下盒要能看见范围");

            view.Begin(BlackAttackKind.Up);

            Transform range = scene.Up.transform.Find("Range");
            Assert.IsTrue(range.gameObject.activeInHierarchy, "盒点亮时色块跟着出现");
            Assert.IsFalse(scene.Ahead.transform.Find("Range").gameObject.activeInHierarchy, "没点亮的盒不该留着色块");
        }

        private static void AssertRange(GameObject box, string message)
        {
            Transform range = box.transform.Find("Range");

            Assert.IsNotNull(range, message);

            MeshFilter filter = range.GetComponent<MeshFilter>();
            MeshRenderer mr = range.GetComponent<MeshRenderer>();

            Assert.IsNotNull(filter, $"{message}（缺少 MeshFilter）");
            Assert.IsNotNull(mr, $"{message}（缺少 MeshRenderer）");
            Assert.IsNotNull(mr.sharedMaterial, $"{message}（没有材质，画出来也是透明的）");
            Assert.IsNotNull(filter.sharedMesh, $"{message}（没有网格就画不出来）");
            Assert.Greater(filter.sharedMesh.vertexCount, 2, $"{message}（顶点太少，画不出面）");

            // 盒平时是关着的：关着读 bounds 拿不到真实体积，先点亮再量
            bool active = box.activeSelf;
            box.SetActive(true);

            Bounds col = box.GetComponent<Collider2D>().bounds;
            Bounds drawn = mr.bounds;

            box.SetActive(active);

            Assert.AreEqual(col.size.x, drawn.size.x, 0.01f, $"{message}（宽度没对上碰撞体）");
            Assert.AreEqual(col.size.y, drawn.size.y, 0.01f, $"{message}（高度没对上碰撞体）");
            Assert.AreEqual(col.center.x, drawn.center.x, 0.01f, $"{message}（横向没对齐碰撞体中心）");
            Assert.AreEqual(col.center.y, drawn.center.y, 0.01f, $"{message}（纵向没对齐碰撞体中心）");
        }

        [Test]
        public void 判定盒层_不被敌人受击盒排除()
        {
            GameObject player = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Player.prefab");
            GameObject enemy = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Enemy.prefab");

            Assert.IsNotNull(player, "找不到玩家预制体，判定盒的层就没人守了");
            Assert.IsNotNull(enemy, "找不到敌人预制体");

            HurtBox hurt = enemy.GetComponentInChildren<HurtBox>(true);
            Assert.IsNotNull(hurt, "敌人身上必须有受击盒");

            Collider2D body = hurt.GetComponent<Collider2D>();
            Assert.IsNotNull(body, "受击盒所在物体要挂碰撞体，否则永远碰不到");

            AttackBox[] boxes = player.GetComponentsInChildren<AttackBox>(true);
            Assert.GreaterOrEqual(boxes.Length, 3, "玩家身上应有 AheadCollider / UpCollider / DownCollider 三只判定盒");

            int excluded = body.excludeLayers.value;

            for (int i = 0; i < boxes.Length; i++)
            {
                int layer = boxes[i].gameObject.layer;

                Assert.AreEqual(0, excluded & (1 << layer),
                    $"{boxes[i].name} 在第 {layer} 层，而敌人受击盒把这一层排除了：物理引擎不会产生接触，砍上去一点伤害都没有");
            }
        }

        [Test]
        public void 空挥_按判定取空挥那份并反向()
        {
            记后坐力的电机 motor = new();
            BlackAttackState state = new(_config, null, motor, null);

            state.Enter(Ctx(1f));
            Assert.AreEqual(0f, motor.Total.x, 1e-3f, "上劈只推竖直，横向必须为 0");
            Assert.AreEqual(-3f, motor.Total.y, 1e-3f, "上劈空挥把玩家往下推");

            motor.Reset();
            state.Enter(Ctx(-1f));
            Assert.AreEqual(0f, motor.Total.x, 1e-3f, "下劈只推竖直，横向必须为 0");
            Assert.AreEqual(5f, motor.Total.y, 1e-3f, "下劈空挥把玩家往上推");

            motor.Reset();
            state.Enter(Ctx(0f));
            Assert.AreEqual(-1f, motor.Total.x, 1e-3f, "平劈空挥沿朝向反向");
            Assert.AreEqual(0f, motor.Total.y, 1e-3f, "平劈空挥不带竖直分量");
            Assert.AreEqual(0, motor.ExternalCalls, "反冲力直接写速度，不走会衰减的外部速度");
        }

        [Test]
        public void 命中_把空挥那份换成命中那份()
        {
            记后坐力的电机 motor = new();
            BlackAttackState state = new(_config, null, motor, null);

            state.Enter(Ctx(0f));
            Assert.AreEqual(-1f, motor.Total.x, 1e-3f, "开打先给空挥那份");

            state.NotifyHit();
            Assert.AreEqual(-1f, motor.Total.x, 1e-3f, "命中只排队，不当场写速度");

            state.Tick(Ctx(Dt));
            Assert.AreEqual(-2f, motor.Total.x, 1e-3f, "下一帧 tick 里重写一次，速度等于命中那份，不是空挥 + 命中");

            motor.Reset();
            state.Enter(Ctx(1f));
            state.NotifyHit();
            state.Tick(Ctx(Dt));
            Assert.AreEqual(0f, motor.Total.x, 1e-3f, "上劈命中换成命中那份，横向仍为 0");
            Assert.AreEqual(-10f, motor.Total.y, 1e-3f, "上劈命中仍往下推");
        }

        [Test]
        public void 反冲力_朝左时镜像()
        {
            记后坐力的电机 motor = new();
            BlackAttackState state = new(_config, null, motor, null);

            state.Enter(Ctx(0f, 0f, -1));
            Assert.AreEqual(1f, motor.Total.x, 1e-3f, "朝左平劈空挥要往右后推");

            state.NotifyHit();
            state.Tick(Ctx(Dt));
            Assert.AreEqual(2f, motor.Total.x, 1e-3f, "命中那份同样按朝向镜像");

            motor.Reset();
            state.Enter(Ctx(1f, 0f, -1));
            Assert.AreEqual(0f, motor.Total.x, 1e-3f, "上劈朝左也不带横向分量");
            Assert.AreEqual(-3f, motor.Total.y, 1e-3f, "上劈朝左仍是竖直往下推");
        }

        [Test]
        public void 反冲力_一次挥击只换一次()
        {
            记后坐力的电机 motor = new();
            BlackAttackState state = new(_config, null, motor, null);

            state.Enter(Ctx(0f));
            state.NotifyHit();
            state.Tick(Ctx(Dt));
            Assert.AreEqual(-2f, motor.Total.x, 1e-3f, "第一次命中换成命中那份");

            state.NotifyHit();
            state.Tick(Ctx(Dt * 2f));
            Assert.AreEqual(-2f, motor.Total.x, 1e-3f, "同一挥击砍到第二个目标不再补一份");
        }

        [Test]
        public void 反冲力_出本体后迟到的命中不再换()
        {
            记后坐力的电机 motor = new();
            BlackAttackState state = new(_config, null, motor, null);

            state.Enter(Ctx(0f));
            state.Exit();
            state.NotifyHit();

            Assert.AreEqual(-1f, motor.Total.x, 1e-3f, "只留空挥那份，迟到的命中不再换成命中值");
        }

        [Test]
        public void 盒子碰到受击盒_换成命中那份()
        {
            using 层级场景 scene = new();

            记后坐力的电机 motor = new();
            BlackAttackView view = new(scene.Root.transform, _config, () => 1);
            BlackAttackState state = new(_config, view, motor, null);
            view.SetHitHandler(state.NotifyHit);

            state.Enter(Ctx(0f));
            Assert.AreEqual(-1f, motor.Total.x, 1e-3f, "盒刚点亮还没接触，先给空挥那份");

            view.ProduceDamage();
            Assert.AreEqual(-1f, motor.Total.x, 1e-3f, "物理回调里只排队，当场写不进速度");

            state.Tick(Ctx(Dt));
            Assert.AreEqual(-2f, motor.Total.x, 1e-3f, "AttackBox 接触后先取伤害，下一帧换成命中那份");
        }

        [Test]
        public void 无敌窗里没扣血_反冲仍按命中算()
        {
            using 层级场景 scene = new();

            记后坐力的电机 motor = new();
            BlackAttackView view = new(scene.Root.transform, _config, () => 1);
            BlackAttackState state = new(_config, view, motor, null);
            view.SetHitHandler(state.NotifyHit);

            state.Enter(Ctx(0f));

            // 模拟 AttackBox：取伤害 → TakeDamage 返回 false → 不回调 IHitLanded
            view.ProduceDamage();
            state.Tick(Ctx(Dt));

            Assert.AreEqual(-2f, motor.Total.x, 1e-3f, "没扣到血也是砍在实体上，反冲不该缩回空挥那份");
        }

        private static void AssertAllOff(层级场景 scene, string message, GameObject except = null)
        {
            if (scene.Ahead != except) Assert.IsFalse(scene.Ahead.activeSelf, $"{message}（AheadCollider 仍开着）");
            if (scene.Up != except) Assert.IsFalse(scene.Up.activeSelf, $"{message}（UpCollider 仍开着）");
            if (scene.Down != except) Assert.IsFalse(scene.Down.activeSelf, $"{message}（DownCollider 仍开着）");
        }

        /// <summary>只搭 Player/BlackAttack/xxxCollider 这三层名字，正是 BlackAttackView 按路径找的那套</summary>
        private sealed class 层级场景 : System.IDisposable
        {
            public readonly GameObject Root;
            public readonly GameObject Black;
            public readonly GameObject Ahead;
            public readonly GameObject Up;
            public readonly GameObject Down;

            public 层级场景()
            {
                Root = new GameObject("Player");
                Black = Child(Root, "BlackAttack");
                Ahead = Box(Black, "AheadCollider");
                Up = Box(Black, "UpCollider");
                Down = Box(Black, "DownCollider");
            }

            public void Dispose() => Object.DestroyImmediate(Root);

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

        private sealed class 记后坐力的电机 : IStateMove
        {
            public Vector2 Total;

            public Vector2 Last;

            public int ExternalCalls;

            public void Reset()
            {
                Total = Vector2.zero;
                Last = Vector2.zero;
            }

            public void AddExternalVelocity(Vector2 velocity)
            {
                ExternalCalls++;
                Last = velocity;
                Total += velocity;
            }
            public void MoveTowardsX(Vector2 direction, float speed) { }
            public void BrakeTowardsX() { }
            public void AddImpulse(Vector2 deltaVelocity) { }
            public void SetVelocityX(float speed)
            {
                Total.x = speed;
                Last = new Vector2(speed, Last.y);
            }
            public void SetVelocityY(float speed)
            {
                Total.y = speed;
                Last = new Vector2(Last.x, speed);
            }
            public void SnapVelocity(Vector2 velocity) { }
            public void SetSpeedScaleX(float scale) { }
            public void ScaleVelocityY(float multiplier) { }
            public void SetGravityEnable(bool enable) { }
            public void SetGravity(float gravity) { }
            public void SetBodyScale(float scale) { }
        }

        private sealed class 记判定的表现记录 : IBlackAttackView
        {
            public BlackAttackKind BeginKind;

            public bool Ended;

            public System.Action OnHit;

            public void Begin(BlackAttackKind kind) => BeginKind = kind;

            public void End() => Ended = true;

            public void SetHitHandler(System.Action onHit) => OnHit = onHit;
        }
    }
}
