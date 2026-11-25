using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>伤害源只有两支：玩家子弹只伤敌人、敌人子弹只伤玩家；敌弹的伤害数值由玩家自己的「破坏力」给</summary>
    public sealed class 伤害源Test
    {
        private const int Health = 3;

        private PlayerConfig _playerConfig;
        private EnemyConfig _enemyConfig;
        private FakeMotor _motor;

        [SetUp]
        public void 建立参数()
        {
            _motor = new FakeMotor();

            _playerConfig = ScriptableObject.CreateInstance<PlayerConfig>();
            _playerConfig.maxHealth = Health;
            _playerConfig.invulnerableTime = 0f;
            _playerConfig.destruction = 2f;

            _enemyConfig = ScriptableObject.CreateInstance<EnemyConfig>();
            _enemyConfig.maxHealth = Health;
            _enemyConfig.invulnerableTime = 0f;
        }

        [TearDown]
        public void 清理参数()
        {
            EventBus<WorldPolarityChanged>.Clear();
            EventBus<AfterimageLinkHit>.Clear();
            EventBus<PlayerHealthChanged>.Clear();

            Object.DestroyImmediate(_playerConfig);
            Object.DestroyImmediate(_enemyConfig);
        }

        [Test]
        public void 玩家子弹_伤敌人()
        {
            EnemyLogic enemy = new(_motor, _enemyConfig, null, true);

            Assert.IsTrue(enemy.TakeDamage(new Damage(1f, DamageSource.PlayerBullet, Vector2.right)), "玩家子弹应该能打掉敌人的血");
            Assert.AreEqual(Health - 1, enemy.CurHealth, "扣血量 = Damage.Amount");

            enemy.Dispose();
        }

        [Test]
        public void 敌人子弹_不伤敌人()
        {
            EnemyLogic enemy = new(_motor, _enemyConfig, null, true);

            Assert.IsFalse(enemy.TakeDamage(new Damage(2f, DamageSource.EnemyBullet, Vector2.right)), "同籍子弹不许伤到敌人，两只怪对射不能互相掉血");
            Assert.AreEqual(Health, enemy.CurHealth, "血量一点不动");

            enemy.Dispose();
        }

        [Test]
        public void 玩家子弹_不伤玩家()
        {
            PlayerLogic player = 造玩家();

            Assert.IsFalse(player.TakeDamage(new Damage(2f, DamageSource.PlayerBullet, Vector2.right)), "玩家自己的子弹（含弹反回来的）不许伤到玩家");
            Assert.AreEqual(Health, player.CurHealth, "血量一点不动");

            player.Dispose();
        }

        [Test]
        public void 敌人子弹_伤玩家_数值取破坏力()
        {
            PlayerLogic player = 造玩家();

            Assert.IsTrue(player.TakeDamage(new Damage(99f, DamageSource.EnemyBullet, Vector2.right)), "敌人子弹应该能伤到玩家");
            Assert.AreEqual(Health - (int)_playerConfig.destruction, player.CurHealth, "掉血量 = 玩家配置的破坏力，敌弹自带的 99 不作数");

            player.Dispose();
        }

        [Test]
        public void 子弹的伤害源_按阵营分()
        {
            BulletLogic system = new();

            system.Launch(new BulletShooter(0, 0, BulletFaction.Player),
                new BulletSpawn(Vector2.zero, Vector2.right, 10f, 1f));
            system.Launch(new BulletShooter(1, 0, BulletFaction.Enemy),
                new BulletSpawn(Vector2.zero, Vector2.left, 10f, 1f));

            Assert.IsTrue(system.TryTakeRequest(out BulletRequest playerShot), "玩家的发射请求要先被取走");
            Assert.IsTrue(system.TryTakeRequest(out BulletRequest enemyShot), "敌人的发射请求同样");

            Assert.AreEqual(DamageSource.PlayerBullet, playerShot.Damage.Source, "玩家这一侧开出来的子弹记成玩家子弹");
            Assert.AreEqual(DamageSource.EnemyBullet, enemyShot.Damage.Source, "敌人这一侧开出来的子弹记成敌人子弹");
        }

        private PlayerLogic 造玩家() =>
            new(_motor, _playerConfig, new InputBuffer(0.1f, 60), null, null);

        private sealed class FakeMotor : IMoveMotor
        {
            public float BodyHeight => 0.9f;

            public float FeetOffsetY => 0f;

            public int FaceSign => 1;

            public void MoveTowardsX(Vector2 direction, float speed) { }
            public void BrakeTowardsX() { }
            public void AddImpulse(Vector2 deltaVelocity) { }
            public void AddExternalVelocity(Vector2 velocity) { }
            public void SetVelocityX(float speed) { }
            public void SetVelocityY(float speed) { }
            public void SnapVelocity(Vector2 velocity) { }
            public void SetSpeedScaleX(float scale) { }
            public void ScaleVelocityY(float multiplier) { }
            public void SetGravityEnable(bool enable) { }
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
