using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>黑突进：平劈走普攻、斜上 / 斜下才突进；前摇锁死速度、本体整段无敌、后摇只挡动作</summary>
    public sealed class 黑突进Test
    {
        private const float Dt = 0.02f;
        private const float Eps = 1e-3f;
        private const float Diagonal = 0.70710678f;

        private PlayerConfig _config;
        private 记速度的电机 _motor;
        private 记表现的黑攻击 _view;
        private ConditionGroup _conditions;
        private AttackGroup _attack;

        [SetUp]
        public void 建立攻击组()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.useBlackDash = true;
            _config.blackDashPrepareTime = 0.1f;
            _config.blackDashRecoverTime = 0.1f;
            _config.blackDashCooldown = 0.5f;
            _config.blackDashSpeedUp = 10f;
            _config.blackDashSpeedDown = 30f;
            _config.blackDashTimeUp = 0.1f;
            _config.blackDashTimeDown = 0.1f;
            _config.blackAttackPrepareTime = 0.1f;
            _config.blackAttackTime = 0.1f;
            _config.blackAttackRecoverTime = 0.1f;
            _config.flatAttackPrepareTime = 0.1f;
            _config.flatAttackTime = 0.1f;
            _config.flatAttackRecoverTime = 0.1f;
            _config.attackCooldown = 0f;
            _config.flatAttackCooldown = 0f;
            _config.blackDashBounceSpeed = 9f;

            _motor = new();
            _view = new();
            _conditions = new(_motor, _config);
            _attack = new(_config, _motor, null, null, _view, _conditions);

            // 极性必须在订阅之后发：攻击组构造里才订阅，先发它收不到
            EventBus<WorldPolarityChanged>.Publish(new(WorldPolarity.Black));
        }

        [TearDown]
        public void 释放攻击组()
        {
            _attack.Dispose();
            EventBus<WorldPolarityChanged>.Clear();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 平劈_走普攻不进突进()
        {
            走到(0f, 0f, true);

            Assert.AreEqual(AttackStateTag.FlatAttackPrepare, _attack.Current, "只按攻击键是普攻，不是突进");
            Assert.IsFalse(_attack.BlocksMovement, "普攻打的时候照常能走");
            Assert.IsFalse(_attack.BlocksActions, "普攻也不挡跳跃与冲刺");
        }

        [Test]
        public void 平劈_本体点亮正前方那只判定盒()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, false);

            Assert.AreEqual(AttackStateTag.FlatAttack, _attack.Current, "普攻前摇走完进本体");
            Assert.IsTrue(_view.Began, "普攻要点亮黑突进那套判定盒");
            Assert.AreEqual(BlackAttackKind.Ahead, _view.FlatKind, "判定固定正前方");
        }

        [Test]
        public void 斜上_按攻击键进突进前摇()
        {
            走到(0f, 1f, true);

            Assert.AreEqual(AttackStateTag.BlackDashPrepare, _attack.Current, "按住上走斜上突进");
            Assert.IsTrue(_attack.BlocksMovement, "前摇也算突进的一部分，移动组已被压住");
        }

        [Test]
        public void 斜下_按攻击键进突进前摇()
        {
            走到(0f, -1f, true, grounded: false);

            Assert.AreEqual(AttackStateTag.BlackDashPrepare, _attack.Current, "按住下走斜下突进");
        }

        [Test]
        public void 开关关闭_三个方向都还是黑劈砍()
        {
            _config.useBlackDash = false;

            走到(0f, 1f, true);

            Assert.AreEqual(AttackStateTag.BlackAttackPrepare, _attack.Current, "开关关掉就走原来那条劈砍链");
            Assert.IsFalse(_attack.BlocksMovement, "劈砍不压移动");
        }

        [Test]
        public void 前摇_速度锁成零并关掉重力()
        {
            _motor.VelocityX = 12f;
            _motor.VelocityY = -6f;

            走到(0f, 1f, true);

            Assert.AreEqual(0f, _motor.VelocityX, Eps, "前摇里横向速度被清成 0");
            Assert.AreEqual(0f, _motor.VelocityY, Eps, "前摇里竖直速度也被清成 0");
            Assert.IsFalse(_motor.GravityEnabled, "前摇连重力加速度都关掉");

            走到(0.05f, 1f, false);

            Assert.AreEqual(0f, _motor.VelocityX, Eps, "前摇每帧都在锁速度");
            Assert.AreEqual(0f, _motor.VelocityY, Eps, "不会因为重力攒出竖直速度");
        }

        [Test]
        public void 斜上_前摇走完按斜上速度冲()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);

            Assert.AreEqual(AttackStateTag.BlackDash, _attack.Current, "前摇走完进本体");
            Assert.AreEqual(BlackAttackKind.Up, _view.DashKind, "斜上点亮斜上那一路突进盒");
            Assert.IsFalse(_motor.GravityEnabled, "突进期间关重力");
            Assert.AreEqual(Diagonal * 10f, _motor.VelocityX, Eps, "斜上横向分量");
            Assert.AreEqual(Diagonal * 10f, _motor.VelocityY, Eps, "斜上竖直分量");
            Assert.IsTrue(_attack.BlocksMovement, "本体期间移动组继续被压住");
        }

        [Test]
        public void 站在地面上_按斜下不进突进()
        {
            走到(0f, -1f, true, grounded: true);

            Assert.AreEqual(AttackStateTag.None, _attack.Current, "下突进只能从空中起手，站在地上按斜下不成立");
        }

        [Test]
        public void 离地时_斜下照常进突进前摇()
        {
            走到(0f, -1f, true, grounded: false);

            Assert.AreEqual(AttackStateTag.BlackDashPrepare, _attack.Current, "空中按斜下走突进");
        }

        [Test]
        public void 斜下_竖直分量为负()
        {
            走到(0f, -1f, true, grounded: false);
            走到(0.11f, -1f, false, grounded: false);

            Assert.AreEqual(BlackAttackKind.Down, _view.DashKind, "按住下就走斜下那一路");
            Assert.AreEqual(Diagonal * 30f, _motor.VelocityX, Eps, "斜下横向分量");
            Assert.AreEqual(-Diagonal * 30f, _motor.VelocityY, Eps, "斜下竖直分量为负");
        }

        [Test]
        public void 方向_在前摇最后一刻固定_本体期间改输入无效()
        {
            走到(0f, 1f, true);
            走到(0.05f, 1f, false);
            走到(0.11f, -1f, false);

            Assert.AreEqual(BlackAttackKind.Up, _view.DashKind, "本体里按着下也改不回方向");
            Assert.Greater(_motor.VelocityY, 0f, "冲出去还是前摇最后定下的斜上");
        }

        [Test]
        public void 本体_整段无敌_后摇不无敌()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);

            Assert.IsTrue(_conditions.IsDashInvulnerable, "冲出去那一刻进无敌");

            走到(0.19f, 1f, false);

            Assert.IsTrue(_conditions.IsDashInvulnerable, "本体最后一段仍然无敌");

            走到(0.22f, 1f, false);

            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "本体走完进后摇");
            Assert.IsFalse(_conditions.IsDashInvulnerable, "后摇不再无敌");
        }

        [Test]
        public void 命中第一个目标_突进提前结束并向上弹起()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            Assert.AreEqual(AttackStateTag.BlackDash, _attack.Current, "先在突进本体里");

            _view.OnHit?.Invoke();
            走到(0.13f, 1f, false);

            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "命中就结束本体，不等时间到");
            Assert.AreEqual(9f, _motor.VelocityY, Eps, "竖直速度换成配置的弹起值");
            Assert.AreEqual(Diagonal * 10f, _motor.VelocityX, Eps, "横向保留冲出去的那一份");
            Assert.IsTrue(_motor.GravityEnabled, "重力已经还回来，弹上去才会落回地面");
        }

        [Test]
        public void 撞到墙_突进提前结束且速度清零()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            Assert.AreEqual(AttackStateTag.BlackDash, _attack.Current, "先在突进本体里");

            走到(0.13f, 1f, false, wall: true);
            走到(0.15f, 1f, false, wall: true);

            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "撞墙直接结束本体，不等时间到");
            Assert.AreEqual(0f, _motor.VelocityX, Eps, "撞墙后横向速度清零");
            Assert.AreEqual(0f, _motor.VelocityY, Eps, "竖直速度也清零");
            Assert.IsTrue(_motor.GravityEnabled, "重力恢复");
        }

        [Test]
        public void 前摇受击_整段作废且重力还回来()
        {
            走到(0f, 1f, true);
            Assert.IsFalse(_motor.GravityEnabled, "前摇里关着重力");

            _conditions.RegisterHit(new Damage(1f, DamageSource.Contact, Vector2.down, 5f), true);
            走到(0.04f, 1f, false);

            Assert.IsTrue(_conditions.IsHurt, "这一下真掉血，条件组进受伤");
            Assert.AreEqual(AttackStateTag.None, _attack.Current, "前摇挨打整段作废，不会继续冲出去");
            Assert.IsTrue(_motor.GravityEnabled, "作废之后没人接手，重力必须还回来");
        }

        [Test]
        public void 后摇_重力恢复且能移动_但跳跃冲刺被挡()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            走到(0.22f, 1f, false);

            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "本体走完进后摇");
            Assert.IsTrue(_motor.GravityEnabled, "后摇一开始重力就恢复了");
            Assert.IsFalse(_attack.BlocksMovement, "后摇不再压移动组，能照常走");
            Assert.IsTrue(_attack.BlocksActions, "但跳跃与冲刺的抢占被挡掉");
        }

        [Test]
        public void 后摇_再按攻击键不会取消后摇()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            走到(0.22f, 1f, false);
            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "先站在后摇里");

            走到(0.24f, 1f, true);

            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "后摇不再支持按突进取消");
        }

        [Test]
        public void 走完整条链_回到空闲且松开移动组()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            走到(0.22f, 1f, false);
            走到(0.35f, 1f, false);

            Assert.AreEqual(AttackStateTag.None, _attack.Current, "后摇走完回空闲");
            Assert.IsFalse(_attack.BlocksMovement, "回到空闲就把移动组松开");
            Assert.IsFalse(_attack.BlocksActions, "跳跃与冲刺也一并放开");
            Assert.IsTrue(_view.Ended, "结束时突进盒与弹反盒一并关掉");
        }

        private void 走到(float now, float moveY, bool attack, bool grounded = true, bool wall = false)
        {
            InputSnapshot input = new(new Vector2(0f, moveY), false, false, false, false, attack, false);
            WorldInfo world = new(grounded, wall, Vector2.zero, 1, Vector2.zero);
            LogicContext ctx = new(input, world, new GameTime(now, Dt));

            _conditions.Tick(in ctx);
            _attack.Tick(in ctx);
        }

        private sealed class 记表现的黑攻击 : IBlackAttackView
        {
            public BlackAttackKind FlatKind;

            public BlackAttackKind DashKind;

            public bool Began;

            public bool Ended;

            public void Begin(BlackAttackKind kind)
            {
                Began = true;
                FlatKind = kind;
            }

            public void BeginFlat()
            {
                Began = true;
                FlatKind = BlackAttackKind.Ahead;
            }

            public void BeginDash(BlackAttackKind kind) => DashKind = kind;

            public void End() => Ended = true;

            public System.Action OnHit;

            public void SetHitHandler(System.Action onHit) => OnHit = onHit;
        }

        private sealed class 记速度的电机 : IMoveMotor
        {
            public float VelocityX;

            public float VelocityY;

            public bool GravityEnabled = true;

            public float BodyHeight => 2f;

            public float FeetOffsetY => 0f;

            public int FaceSign => 1;

            public void MoveTowardsX(Vector2 direction, float speed) { }

            public void BrakeTowardsX() { }

            public void AddImpulse(Vector2 deltaVelocity) { }

            public void AddExternalVelocity(Vector2 velocity) { }

            public void SetVelocityX(float speed) => VelocityX = speed;

            public void SetVelocityY(float speed) => VelocityY = speed;

            public void SnapVelocity(Vector2 velocity)
            {
                VelocityX = velocity.x;
                VelocityY = velocity.y;
            }

            public void SetSpeedScaleX(float scale) { }

            public void ScaleVelocityY(float multiplier) { }

            public void SetGravityEnable(bool enable) => GravityEnabled = enable;

            public void SetGravity(float gravity) { }

            public void SetBodyScale(float scale) { }

            public void BeginStep(float now, float deltaTime) { }

            public void Commit() { }

            public void FaceTowards(Vector2 dir) { }

            public void ApplyGravity(in LogicContext ctx) { }

            public void SpawnAt(Vector2 position, Vector2 velocity) { }

            public bool CanStandUp(float standHeight, float crouchHeight) => true;

            public void CommitGates(in MoveGates gates) { }

            public void ApplyGates() { }
        }
    }
}
