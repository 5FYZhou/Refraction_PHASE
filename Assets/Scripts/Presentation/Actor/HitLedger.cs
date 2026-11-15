using System.Collections.Generic;

namespace Refraction.Presentation.Actor
{
    /// <summary>一次冲刺的命中账本：同一个受击盒只放行一次，换一段冲刺由持有方清空</summary>
    public sealed class HitLedger
    {
        private readonly HashSet<HurtBox> _hit = new();

        public void Clear() => _hit.Clear();

        public bool TryMark(HurtBox target) => target != null && _hit.Add(target);
    }
}
