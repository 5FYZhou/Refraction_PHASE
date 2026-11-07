using UnityEngine;

namespace Refraction.Logic.Move
{
    /// <summary>外部速度：后坐力、击退等冲量单独记一份</summary>
    /// <remarks>提交时加到常规速度上一并写回，帧首再剥掉</remarks>
    public sealed class ExternalVelocity
    {
        /// <summary>判定冲量已被物理吃掉的阈值</summary>
        private const float ConsumedEpsilon = 0.01f;

        private Vector2 _value;
        private Vector2 _applied;
        private Vector2 _lastWritten;

        public Vector2 Value => _value;

        /// <summary>与上次提交不同：衰减也要写回</summary>
        public bool Dirty => _value != _applied;

        public void Add(Vector2 velocity) => _value += velocity;

        public void Decay(float deceleration, float deltaTime)
        {
            _value = Vector2.MoveTowards(_value, Vector2.zero, deceleration * deltaTime);
        }

        /// <summary>帧首：真值减去上帧加进去的部分，被物理清零的冲量一并丢掉</summary>
        /// <remarks>上帧写进去的就是 0 时，raw 为 0 是常规与外部互相抵消的结果，不是撞墙，冲量要留着</remarks>
        public Vector2 Strip(Vector2 raw)
        {
            bool cancelledX = Mathf.Abs(_lastWritten.x) < ConsumedEpsilon;
            bool cancelledY = Mathf.Abs(_lastWritten.y) < ConsumedEpsilon;

            if (!cancelledX && _applied.x != 0f && Mathf.Abs(raw.x) < ConsumedEpsilon)
            {
                _value.x = 0f;
                _applied.x = 0f;
            }

            if (!cancelledY && _applied.y != 0f && Mathf.Abs(raw.y) < ConsumedEpsilon)
            {
                _value.y = 0f;
                _applied.y = 0f;
            }

            return raw - _applied;
        }

        /// <summary>提交后记账</summary>
        public void MarkApplied(Vector2 written)
        {
            _applied = _value;
            _lastWritten = written;
        }

        /// <summary>瞬移后清空</summary>
        public void Clear()
        {
            _value = Vector2.zero;
            _applied = Vector2.zero;
            _lastWritten = Vector2.zero;
        }
    }
}
