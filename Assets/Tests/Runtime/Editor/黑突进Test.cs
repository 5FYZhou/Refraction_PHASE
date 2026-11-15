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
    /// <summary>黑突进三段：前摇定方向、本体关重力定速冲、后摇重力恢复不清速度；与黑劈砍由 useBlackDash 二选一</summary>
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
            _config.blackDashInvulnerableTime = 0.1f;
            _config.blackDashSpeedAhead = 20f;
            _config.blackDashSpeedUp = 10f;
            _config.blackDashSpeedDown = 30f;
            _config.blackDashTimeAhead = 0.1f;
            _config.blackDashTimeUp = 0.1f;
            _config.blackDashTimeDown = 0.1f;
            _config.blackAttackPrepareTime = 0.1f;
            _config.attackCooldown = 0f;

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
        public void 开关打开_按攻击键进黑突进前摇()
        {
            走到(0f, 0f, true);

            Assert.AreEqual(AttackStateTag.BlackDashPrepare, _attack.Current, "useBlackDash 打开时黑形态按攻击键走突进");
            Assert.IsTrue(_attack.BlocksMovement, "前摇就算突进的一部分，移动组已被压住");
        }

        [Test]
        public void 开关关闭_按攻击键仍走黑劈砍()
        {
            _config.useBlackDash = false;

            走到(0f, 0f, true);

            Assert.AreEqual(AttackStateTag.BlackAttackPrepare, _attack.Current, "开关关掉就还是原来那条劈砍链");
            Assert.IsFalse(_attack.BlocksMovement, "劈砍不压移动");
        }

        [Test]
        public void 平推_关重力按平推速度冲且不带走竖直速度()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, false);

            Assert.AreEqual(AttackStateTag.BlackDash, _attack.Current, "前摇走完进本体");
            Assert.AreEqual(BlackAttackKind.Ahead, _view.DashKind, "平推点亮正前方那一路突进盒");
            Assert.IsFalse(_motor.GravityEnabled, "突进期间关重力");
            Assert.AreEqual(20f, _motor.VelocityX, Eps, "平推按 blackDashSpeedAhead 冲");
            Assert.AreEqual(0f, _motor.VelocityY, Eps, "平推不带竖直分量");
            Assert.IsTrue(_attack.BlocksMovement, "本体期间移动组继续被压住");
        }

        [Test]
        public void 斜上_两个分量各取百分之七十()
        {
            走到(0f, 1f, true);
            走到(0.11f, 1f, false);

            Assert.AreEqual(BlackAttackKind.Up, _view.DashKind, "按住上就走斜上那一路");
            Assert.AreEqual(Diagonal * 10f, _motor.VelocityX, Eps, "斜上横向分量");
            Assert.AreEqual(Diagonal * 10f, _motor.VelocityY, Eps, "斜上竖直分量");
        }

        [Test]
        public void 斜下_竖直分量为负()
        {
            走到(0f, -1f, true);
            走到(0.11f, -1f, false);

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
        public void 突进开始_给无敌帧()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, false);

            Assert.IsTrue(_conditions.IsDashInvulnerable, "冲出去那一刻进入无敌窗口");
        }

        [Test]
        public void 前摇受击_整段作废回空闲()
        {
            走到(0f, 0f, true);
            Assert.AreEqual(AttackStateTag.BlackDashPrepare, _attack.Current, "先进前摇");

            _conditions.RegisterHit(new Damage(1f, DamageSource.Contact, Vector2.down, 5f), true);
            走到(0.04f, 0f, false);

            Assert.IsTrue(_conditions.IsHurt, "这一下真掉血，条件组进受伤");
            Assert.AreEqual(AttackStateTag.None, _attack.Current, "前摇挨打整段作废，不会继续冲出去");
        }

        [Test]
        public void 后摇_重力恢复且不清空冲出去的速度()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, false);
            走到(0.22f, 0f, false);

            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "本体走完进后摇");
            Assert.IsTrue(_motor.GravityEnabled, "后摇一开始重力就恢复了");
            Assert.AreEqual(20f, _motor.VelocityX, Eps, "后摇不清空冲出去的速度");
            Assert.Greater(_motor.BrakeCalls, 0, "后摇靠移动减速让速度自然掉下去");
            Assert.IsTrue(_attack.BlocksMovement, "后摇里仍然不能移动 / 跳跃 / 冲刺");
        }

        [Test]
        public void 后摇再按突进_取消后摇接下一段前摇()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, false);
            走到(0.22f, 0f, false);
            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "先站在后摇里");

            走到(0.24f, 0f, true);

            Assert.AreEqual(AttackStateTag.BlackDashPrepare, _attack.Current, "再按一次直接接下一段前摇，不吃完后摇");
        }

        [Test]
        public void 后摇一直按着攻击键_不算再按一次()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, true);
            走到(0.22f, 0f, true);

            Assert.AreEqual(AttackStateTag.BlackDashRecover, _attack.Current, "一路按着不算「再次按下」，后摇照常走完");
        }

        [Test]
        public void 走完整条链_回到空闲且松开移动组()
        {
            走到(0f, 0f, true);
            走到(0.11f, 0f, false);
            走到(0.22f, 0f, false);
            走到(0.35f, 0f, false);

            Assert.AreEqual(AttackStateTag.None, _attack.Current, "后摇走完回空闲");
            Assert.IsFalse(_attack.BlocksMovement, "回到空闲就把移动组松开");
            Assert.IsTrue(_view.Ended, "结束时弹反盒一并关掉");
        }

        private void 走到(float now, float moveY, bool attack)
        {
            InputSnapshot input = new(new Vector2(0f, moveY), false, false, false, false, attack, false);
            WorldInfo world = new(true, false, Vector2.zero, 1, Vector2.zero);
            LogicContext ctx = new(input, world, new GameTime(now, Dt));

            _conditions.Tick(in ctx);
            _attack.Tick(in ctx);
        }

        private sealed class 记表现的黑攻击 : IBlackAttackView
        {
            public BlackAttackKind DashKind;

            public bool Ended;

            public void Begin(BlackAttackKind kind) { }

            public void BeginDash(BlackAttackKind kind) => DashKind = kind;

            public void End() => Ended = true;

            public void SetHitHandler(System.Action onHit) { }
        }

        private sealed class 记速度的电机 : IMoveMotor
        {
            public float VelocityX;

            public float VelocityY;

            public int BrakeCalls;

            public bool GravityEnabled = true;

            public float BodyHeight => 2f;

            public float FeetOffsetY => 0f;

            public int FaceSign => 1;

            public void MoveTowardsX(Vector2 direction, float speed) { }

            public void BrakeTowardsX() => BrakeCalls++;

            public void AddImpulse(Vector2 deltaVelocity) { }

            public void AddExternalVelocity(Vector2 velocity) { }

            public void SetVelocityX(float speed) => VelocityX = speed;

            public void SetVelocityY(float speed) => VelocityY = speed;

            public void SnapVelocity(Vector2 velocity) { }

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
