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
    /// <summary>黑形态三只自带前后摇的状态：平劈 / 斜上 / 斜下各有自己的冷却，前摇与本体不许抢，后摇可以顶掉</summary>
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
            _config.blackDashPrepareTime = 0.1f;
            _config.blackDashRecoverTime = 0.1f;
            _config.blackDashUpCooldown = 0f;
            _config.blackDashDownCooldown = 0f;
            _config.blackDashSpeedUp = 10f;
            _config.blackDashSpeedDown = 30f;
            _config.blackDashTimeUp = 0.1f;
            _config.blackDashTimeDown = 0.1f;
            _config.blackDashBounceSpeed = 9f;
            _config.flatAttackPrepareTime = 0.1f;
            _config.flatAttackTime = 0.1f;
            _config.flatAttackRecoverTime = 0.1f;
            _config.attackCooldown = 0f;
            _config.flatAttackCooldown = 0f;

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

            Assert.AreEqual(AttackStateTag.FlatAttack, _attack.Current, "只按攻击键走普攻，不碰突进");
            Assert.IsFalse(_view.Began, "这一刻还在前摇，判定盒没点亮");
            Assert.IsFalse(_attack.BlocksMovement, "普攻打的时候照常能走");
            Assert.IsFalse(_attack.BlocksActions, "普攻也不挡跳跃与冲刺");
        }

        [Test]
        public void 平劈_本体点亮正前方那只判定盒()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, false);

            Assert.AreEqual(AttackStateTag.FlatAttack, _attack.Current, "普攻前摇走完仍在同一只状态里");
            Assert.IsTrue(_view.Began, "普攻要点亮 BlackDash 下正前方那套判定盒");
            Assert.AreEqual(BlackAttackKind.Ahead, _view.FlatKind, "判定固定正前方");
        }

        [Test]
        public void 斜上_按攻击键进斜上突进()
        {
            走到(0f, 1f, true);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "按住上走斜上突进");
            Assert.IsTrue(_attack.BlocksMovement, "前摇也算突进的一部分，移动组已被压住");
            Assert.IsFalse(_attack.Cancelable, "前摇不许被别的攻击顶掉");
        }

        [Test]
        public void 斜下_按攻击键进斜下突进()
        {
            走到(0f, -1f, true, grounded: false);

            Assert.AreEqual(AttackStateTag.BlackDashDown, _attack.Current, "按住下走斜下突进，是另一只状态");
        }

        [Test]
        public void 站在地面上_按斜下不进突进()
        {
            走到(0f, -1f, true, grounded: true);

            Assert.AreEqual(AttackStateTag.None, _attack.Current, "下突进只能从空中起手，站在地上按斜下不成立");
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

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "前摇走完进本体");
            Assert.AreEqual(BlackAttackKind.Up, _view.DashKind, "斜上点亮斜上那一路突进盒");
            Assert.IsFalse(_motor.GravityEnabled, "突进期间关重力");
            Assert.AreEqual(Diagonal * 10f, _motor.VelocityX, Eps, "斜上横向分量");
            Assert.AreEqual(Diagonal * 10f, _motor.VelocityY, Eps, "斜上竖直分量");
            Assert.IsTrue(_attack.BlocksMovement, "本体期间移动组继续被压住");
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
        public void 方向_按下那一刻定死_前摇里改输入不换()
        {
            走到(0f, 1f, true);
            走到(0.05f, -1f, false, grounded: false);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "状态本身就是方向，前摇里改输入也不换");

            走到(0.11f, -1f, false, grounded: false);

            Assert.Greater(_motor.VelocityY, 0f, "冲出去还是按下那一刻的斜上");
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

            Assert.IsTrue(_attack.Cancelable, "本体走完进后摇");
            Assert.IsFalse(_conditions.IsDashInvulnerable, "后摇不再无敌");
        }

        [Test]
        public void 本体_只开无敌不虚化()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);

            Assert.IsTrue(_conditions.IsDashInvulnerable, "本体照旧无敌");
            Assert.IsFalse(_conditions.IsPhasing, "虚化只归充能残影，黑突进不穿子弹");
        }

        [Test]
        public void 虚化窗_与冲刺无敌各记各的()
        {
            走到(0f, 0f, false);

            _conditions.BeginDashInvulnerable(0.1f);
            _conditions.BeginPhase(0.2f);

            走到(0.05f, 0f, false);

            Assert.IsTrue(_conditions.IsDashInvulnerable, "冲刺无敌窗先开");
            Assert.IsTrue(_conditions.IsPhasing, "虚化窗同时开着，两者互不顶替");

            走到(0.15f, 0f, false);

            Assert.IsFalse(_conditions.IsDashInvulnerable, "0.1 秒后冲刺无敌先关");
            Assert.IsTrue(_conditions.IsPhasing, "虚化窗还没到期，说明是两个独立计时器");

            _conditions.ClearHurt();

            Assert.IsFalse(_conditions.IsPhasing, "清伤一并清掉虚化，窗口不会带进下一条命");
        }

        [Test]
        public void 命中第一个目标_提前进后摇并向上弹起()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);

            Assert.IsFalse(_attack.Cancelable, "先在突进本体里");

            _view.OnHit?.Invoke();
            走到(0.13f, 1f, false);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "命中不换状态，只把段跳到后摇");
            Assert.IsTrue(_attack.Cancelable, "命中直接进后摇，不等本体时间到");
            Assert.AreEqual(9f, _motor.VelocityY, Eps, "竖直速度换成配置的弹起值");
            Assert.AreEqual(Diagonal * 10f, _motor.VelocityX, Eps, "横向保留冲出去的那一份");
            Assert.IsTrue(_motor.GravityEnabled, "重力已经还回来，弹上去才会落回地面");
            Assert.IsTrue(_view.Ended, "命中收尾，突进盒与弹反盒一并关掉");
        }

        [Test]
        public void 撞到墙_提前进后摇且速度清零()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);

            Assert.IsFalse(_attack.Cancelable, "先在突进本体里");

            走到(0.13f, 1f, false, wall: true);

            Assert.IsTrue(_attack.Cancelable, "撞墙直接进后摇，不等本体时间到");
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

            Assert.IsTrue(_attack.Cancelable, "本体走完进后摇");
            Assert.IsTrue(_motor.GravityEnabled, "后摇一开始重力就恢复了");
            Assert.IsFalse(_attack.BlocksMovement, "后摇不再压移动组，能照常走");
            Assert.IsTrue(_attack.BlocksActions, "但跳跃与冲刺的抢占被挡掉");
        }

        [Test]
        public void 前摇与本体_不接受别的攻击()
        {
            走到(0f, 1f, true);
            走到(0.05f, -1f, true, grounded: false);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "前摇里按别的方向不换手");

            走到(0.11f, -1f, false, grounded: false);
            走到(0.13f, 0f, true);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "本体也不许被平劈顶掉");
        }

        [Test]
        public void 后摇_换一种攻击可以直接顶掉()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, false);
            走到(0.22f, 0f, false);

            Assert.IsTrue(_attack.Cancelable, "平劈后摇允许被顶掉");

            走到(0.24f, 1f, true);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "后摇里按斜上，直接换到斜上突进前摇");
        }

        [Test]
        public void 后摇_同方向再按不会重开()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            走到(0.22f, 1f, false);

            走到(0.24f, 1f, true);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "仍是同一只状态");
            Assert.IsTrue(_attack.Cancelable, "没有重开前摇，还停在后摇里");
        }

        [Test]
        public void 斜上冷却没到_斜上按不出来但平劈可以()
        {
            _config.blackDashUpCooldown = 0.5f;

            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            走到(0.31f, 1f, false);
            Assert.AreEqual(AttackStateTag.None, _attack.Current, "斜上整条链已经收尾");

            走到(0.35f, 1f, true);
            Assert.AreEqual(AttackStateTag.None, _attack.Current, "斜上自己的冷却还没过，再按不成立");

            走到(0.37f, 0f, true);
            Assert.AreEqual(AttackStateTag.FlatAttack, _attack.Current, "平劈读自己那份冷却，不受斜上牵连");
        }

        [Test]
        public void 斜上冷却中_斜下照样可以起手()
        {
            _config.blackDashUpCooldown = 0.5f;

            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            走到(0.31f, 1f, false);

            走到(0.35f, -1f, true, grounded: false);

            Assert.AreEqual(AttackStateTag.BlackDashDown, _attack.Current, "斜下有自己的冷却，斜上在冷却也不挡它");
        }

        [Test]
        public void 走完整条链_回到空闲且松开移动组()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);
            走到(0.22f, 1f, false);

            Assert.AreEqual(AttackStateTag.BlackDashUp, _attack.Current, "后摇里仍是这只状态");

            走到(0.35f, 1f, false);

            Assert.AreEqual(AttackStateTag.None, _attack.Current, "后摇走完回空闲");
            Assert.IsFalse(_attack.BlocksMovement, "回到空闲就把移动组松开");
            Assert.IsFalse(_attack.BlocksActions, "跳跃与冲刺也一并放开");
            Assert.IsTrue(_view.Ended, "结束后突进盒与弹反盒都已关掉");
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
