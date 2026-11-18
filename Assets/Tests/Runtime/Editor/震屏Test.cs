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
    /// <summary>受击震屏：冲量 = 配置强度 × 击退方向，时长取配置；没真扣血就不许震屏</summary>
    public sealed class 震屏Test
    {
        private const float Eps = 1e-3f;

        private PlayerConfig _config;
        private 空电机 _motor;
        private int _requests;
        private RequestCameraShake _last;

        [SetUp]
        public void 建立参数()
        {
            _motor = new();

            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.maxHealth = 3;
            _config.invulnerableTime = 0f;
            _config.cameraShakeIntensity = 2f;
            _config.cameraShakeDuration = 0.15f;

            _requests = 0;
            EventBus<RequestCameraShake>.Subscribe(记请求);
        }

        [TearDown]
        public void 清理参数()
        {
            EventBus<RequestCameraShake>.Unsubscribe(记请求);
            EventBus<RequestCameraShake>.Clear();
            EventBus<RequestWorldReverse>.Clear();
            EventBus<PlayerPolarityChanged>.Clear();
            EventBus<PlayerHealthChanged>.Clear();

            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 受击震屏_强度取配置_方向取击退方向()
        {
            PlayerLogic player = 造玩家();

            Vector2 knockDir = new Vector2(-1f, 0.5f).normalized;
            player.TakeDamage(new Damage(1f, DamageSource.Melee, knockDir, 6f));

            Assert.AreEqual(1, _requests, "真扣到血就要发一次震屏请求");
            Assert.AreEqual(CameraShakeMode.Directional, _last.mode, "受击走单向冲量，按击退方向推一下");
            Assert.AreEqual(_config.cameraShakeDuration, _last.duration, Eps, "时长取配置值");
            Assert.AreEqual(knockDir.x * _config.cameraShakeIntensity, _last.impulse.x, Eps, "冲量横向 = 击退方向 × 配置强度");
            Assert.AreEqual(knockDir.y * _config.cameraShakeIntensity, _last.impulse.y, Eps, "冲量纵向 = 击退方向 × 配置强度");

            player.Dispose();
        }

        [Test]
        public void 零伤害纯击退_不震屏()
        {
            PlayerLogic player = 造玩家();

            player.TakeDamage(new Damage(0f, DamageSource.Melee, Vector2.right, 6f));

            Assert.AreEqual(0, _requests, "没扣血只是推开，不算受击");

            player.Dispose();
        }

        [Test]
        public void 无敌窗内再挨打_不震屏()
        {
            _config.invulnerableTime = 1f;
            PlayerLogic player = 造玩家();

            Assert.IsTrue(player.TakeDamage(new Damage(1f, DamageSource.Melee, Vector2.right, 6f)), "第一下真扣血");
            Assert.AreEqual(1, _requests, "第一下震屏");

            Assert.IsFalse(player.TakeDamage(new Damage(1f, DamageSource.Contact, Vector2.right, 6f)), "无敌窗内挡掉");
            Assert.AreEqual(1, _requests, "被挡掉的那次不许再震");

            player.Dispose();
        }

        [Test]
        public void 世界反转_按世界震屏参数发请求()
        {
            _config.maxPolarity = 2;
            _config.polarityPerHit = 1;
            _config.worldShakeIntensity = 3f;
            _config.worldShakeDuration = 0.4f;

            PlayerLogic player = 造玩家();
            player.OnHitLanded();
            player.OnHitLanded();

            player.FixedTick(反转帧(1f));

            Assert.AreEqual(1, _requests, "极性攒满时按反转键，要震一次屏");
            Assert.AreEqual(CameraShakeMode.Omni, _last.mode, "世界反转走四向抖动");
            Assert.AreEqual(_config.worldShakeDuration, _last.duration, Eps, "时长取世界震屏配置");
            Assert.AreEqual(_config.worldShakeIntensity, _last.impulse.magnitude, Eps, "冲量模长 = 世界震屏强度");

            player.Dispose();
        }

        [Test]
        public void 世界震屏强度为零_不发请求()
        {
            _config.maxPolarity = 2;
            _config.polarityPerHit = 1;
            _config.worldShakeIntensity = 0f;

            PlayerLogic player = 造玩家();
            player.OnHitLanded();
            player.OnHitLanded();

            player.FixedTick(反转帧(1f));

            Assert.AreEqual(0, _requests, "强度配 0 就是关掉世界震屏，别发空请求");

            player.Dispose();
        }

        private static LogicContext 反转帧(float now)
        {
            InputSnapshot input = new(Vector2.zero, false, false, false, false, false, false, false, true, false);
            WorldInfo world = new(true, false, Vector2.zero, 1, Vector2.zero);

            return new LogicContext(input, world, new GameTime(now, 0.02f));
        }

        private void 记请求(RequestCameraShake evt)
        {
            _requests++;
            _last = evt;
        }

        private PlayerLogic 造玩家() =>
            new(_motor, _config, new InputBuffer(0.1f, 60), null, null);

        private sealed class 空电机 : IMoveMotor
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
