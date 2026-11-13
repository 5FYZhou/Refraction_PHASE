using Refraction.Logic.Events;
using UnityEngine;

namespace Refraction.Logic.Service
{
    /// <summary>顿帧：收到 HitStopRequested 就冻结游戏，攒够时长自己解冻</summary>
    /// <remarks>计时只能用未缩放时长——冻结期间 deltaTime 恒为 0，用它就永远解不开；
    /// 同一 owner 重复 PushFreeze 是 no-op，所以多次请求只延长计时、不会叠出第二层冻结</remarks>
    public sealed class HitStopService : IService
    {
        private readonly PauseService _pause;

        private float _remaining;

        public HitStopService(PauseService pause)
        {
            _pause = pause;
        }

        public bool IsActive => _remaining > 0f;

        /// <summary>剩余冻结时长（秒），只读</summary>
        public float Remaining => _remaining;

        public void Init()
        {
            _remaining = 0f;
            EventBus<HitStopRequested>.Subscribe(OnRequested);
        }

        public void Dispose()
        {
            EventBus<HitStopRequested>.Unsubscribe(OnRequested);

            _remaining = 0f;
            _pause.PopFreeze(this);
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (_remaining <= 0f) return;

            _remaining -= unscaledDeltaTime;

            if (_remaining > 0f) return;

            _remaining = 0f;
            _pause.PopFreeze(this);
        }

        /// <summary>取剩余与新请求的较大者：短请求不该把正在走的长顿帧截断</summary>
        public void Request(float seconds)
        {
            if (seconds <= 0f) return;

            if (seconds > _remaining) _remaining = seconds;

            _pause.PushFreeze(this);
            Debug.Log("request HitStop " +  seconds);
        }

        private void OnRequested(HitStopRequested evt) => Request(evt.seconds);
    }
}
