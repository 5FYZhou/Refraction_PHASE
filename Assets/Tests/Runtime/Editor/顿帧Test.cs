using NUnit.Framework;
using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using Refraction.Logic.Input;
using Refraction.Logic.Move;
using Refraction.Logic.Service;
using UnityEngine;

namespace Refraction.Tests
{
    /// <summary>顿帧：弹反成功要卡一下，且后来的短请求不许把正在走的长顿帧截断</summary>
    public sealed class 顿帧Test
    {
        private const float Eps = 1e-4f;

        private PlayerConfig _config;
        private 假时钟 _clock;
        private int _requests;
        private float _last;

        [SetUp]
        public void 建立参数()
        {
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _config.reboundHitStopTime = 0.08f;

            _clock = new();
            _requests = 0;

            EventBus<RequestHitStop>.Subscribe(记请求);
        }

        [TearDown]
        public void 清理参数()
        {
            EventBus<RequestHitStop>.Unsubscribe(记请求);
            EventBus<RequestHitStop>.Clear();
            EventBus<PlayerPolarityChanged>.Clear();

            Object.DestroyImmediate(_config);
        }

        [Test]
        public void 弹反成功_发一次顿帧_时长取配置()
        {
            PlayerLogic player = new(new 空电机(), _config, new InputBuffer(0.1f, 60), null, null);

            player.OnReboundReflected(Vector2.right);

            Assert.AreEqual(1, _requests, "弹回一颗敌弹就顿一次，打不回去的不算");
            Assert.AreEqual(_config.reboundHitStopTime, _last, Eps, "顿帧时长取配置值");

            player.Dispose();
        }

        [Test]
        public void 顿帧服务_短请求不截断正在走的长顿帧()
        {
            PauseService pause = new(_clock);
            pause.Init();

            HitStopService hitStop = new(pause);
            hitStop.Init();

            EventBus<RequestHitStop>.Publish(new(0.2f));
            Assert.IsTrue(hitStop.IsActive, "收到请求就冻结");
            Assert.AreEqual(0f, _clock.TimeScale, Eps, "冻结就是 timeScale 归零");

            hitStop.Tick(0f, 0.1f);

            EventBus<RequestHitStop>.Publish(new(0.05f));
            hitStop.Tick(0f, 0.09f);
            Assert.IsTrue(hitStop.IsActive, "取剩余与新请求的较大者，0.2 秒不该被 0.05 截断");

            hitStop.Tick(0f, 0.02f);
            Assert.IsFalse(hitStop.IsActive, "按 0.2 秒到期，不是 0.15");
            Assert.AreEqual(1f, _clock.TimeScale, Eps, "解冻后 timeScale 回 1");

            hitStop.Dispose();
            pause.Dispose();
        }

        private void 记请求(RequestHitStop evt)
        {
            _requests++;
            _last = evt.seconds;
        }

        private sealed class 假时钟 : IGameTime
        {
            public float TimeScale { get; private set; } = 1f;

            public float UnscaledDeltaTime => 1f / 60f;

            public void SetTimeScale(float scale) => TimeScale = scale;
        }

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
