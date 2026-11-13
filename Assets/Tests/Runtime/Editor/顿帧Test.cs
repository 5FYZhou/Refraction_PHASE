using NUnit.Framework;
using Refraction.Logic.Events;
using Refraction.Logic.Service;

namespace Refraction.Tests
{
    public sealed class 顿帧Test
    {
        private 记缩放的时钟 _clock;
        private PauseService _pause;
        private HitStopService _hitStop;

        [SetUp]
        public void 建立服务()
        {
            _clock = new 记缩放的时钟();
            _pause = new PauseService(_clock);
            _pause.Init();

            _hitStop = new HitStopService(_pause);
            _hitStop.Init();
        }

        [TearDown]
        public void 拆除服务()
        {
            _hitStop.Dispose();
            _pause.Dispose();
        }

        [Test]
        public void 事件请求_冻结并在时长走完后自动解冻()
        {
            EventBus<HitStopRequested>.Publish(new(0.1f));
            Assert.AreEqual(0f, _clock.Scale, 1e-4f, "收到请求就该冻结");

            _hitStop.Tick(0f, 0.06f);
            Assert.AreEqual(0f, _clock.Scale, 1e-4f, "时长没走完仍冻结");

            _hitStop.Tick(0f, 0.06f);
            Assert.AreEqual(1f, _clock.Scale, 1e-4f, "累计超过 0.1 秒后自动恢复");
            Assert.IsFalse(_hitStop.IsActive, "解冻后不再持有冻结");
        }

        [Test]
        public void 冻结期间_靠未缩放时长计时()
        {
            EventBus<HitStopRequested>.Publish(new(0.1f));

            _hitStop.Tick(0f, 0.06f);
            Assert.IsTrue(_hitStop.IsActive, "缩放时长为 0 时未缩放时长照走，否则永远解不开");

            _hitStop.Tick(1f, 0.06f);
            Assert.IsFalse(_hitStop.IsActive, "解冻只看未缩放时长，缩放那个参数不参与计时");
        }

        [Test]
        public void 后到的短请求_不截断正在走的长顿帧()
        {
            EventBus<HitStopRequested>.Publish(new(0.2f));
            _hitStop.Tick(0f, 0.1f);

            EventBus<HitStopRequested>.Publish(new(0.05f));
            _hitStop.Tick(0f, 0.06f);
            Assert.IsTrue(_hitStop.IsActive, "取剩余与新请求的较大者：若被改成覆盖，0.06 > 0.05 就已经解冻了");

            _hitStop.Tick(0f, 0.06f);
            Assert.IsFalse(_hitStop.IsActive, "按 0.2 秒到期");
        }

        [Test]
        public void 零或负时长_当作没请求()
        {
            int before = _clock.SetCalls;

            EventBus<HitStopRequested>.Publish(new(0f));
            EventBus<HitStopRequested>.Publish(new(-1f));

            Assert.IsFalse(_hitStop.IsActive, "0 与负数都不冻结");
            Assert.AreEqual(before, _clock.SetCalls, "没请求就不该去动 timeScale");
            Assert.AreEqual(1f, _clock.Scale, 1e-4f, "timeScale 仍是正常速度");
        }

        [Test]
        public void 与菜单暂停共存_顿帧结束只解自己那层()
        {
            object menu = new();
            _pause.PushFreeze(menu);

            EventBus<HitStopRequested>.Publish(new(0.05f));
            _hitStop.Tick(0f, 0.06f);

            Assert.IsTrue(_pause.IsPaused, "顿帧结束不代表菜单暂停结束");
            Assert.AreEqual(0f, _clock.Scale, 1e-4f, "菜单还压着一层，不能恢复 timeScale");

            _pause.PopFreeze(menu);
            Assert.AreEqual(1f, _clock.Scale, 1e-4f, "菜单关掉才真正恢复");
        }

        [Test]
        public void 拆除后再发事件_不再冻结()
        {
            _hitStop.Dispose();

            EventBus<HitStopRequested>.Publish(new(0.1f));

            Assert.IsFalse(_hitStop.IsActive, "退订之后事件不该再生效，否则服务拆了还能冻住游戏");
            Assert.AreEqual(1f, _clock.Scale, 1e-4f, "不能留下冻结");
        }

        private sealed class 记缩放的时钟 : IGameTime
        {
            public float Scale = 1f;

            public int SetCalls;

            public float UnscaledDeltaTime => 0f;

            public float TimeScale => Scale;

            public void SetTimeScale(float scale)
            {
                Scale = scale;
                SetCalls++;
            }
        }
    }
}
