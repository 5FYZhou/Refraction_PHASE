using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>极性值：只记当前值与上限，加值在满值处截断，不会为负</summary>
    public sealed class PolarityGauge
    {
        private readonly int _max;

        private int _value;

        /// <remarks>上限至少 1：0 会让所有加值都落在 0，等于整套系统静默失效</remarks>
        public PolarityGauge(int max) => _max = Mathf.Max(1, max);

        public int Value => _value;

        public int Max => _max;

        public void Add(int amount)
        {
            if (amount <= 0) return;

            // 与剩余容量比大小而不是先加：一次加爆也不会让 int 溢出成负数
            if (amount >= _max - _value) _value = _max;
            else _value += amount;
        }

        /// <remarks>跨关接住上一个值：配表把上限调小后进关不会带着超额极性</remarks>
        public void Set(int value) => _value = Mathf.Clamp(value, 0, _max);

        public void Clear() => _value = 0;
    }
}
