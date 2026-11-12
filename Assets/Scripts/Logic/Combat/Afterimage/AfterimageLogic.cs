using System.Collections.Generic;
using Refraction.Data;
using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>残影系统：持有账本，决定这一帧生不生成，并把相邻残影切成可画的连线</summary>
    /// <remarks>账本只存数据，上限顶替、过期清理、连线是否成立都在这里判；表现层只读结果，不再参与决策</remarks>
    public sealed class AfterimageLogic : IAfterimageSpawner
    {
        private readonly PlayerConfig _config;
        private readonly AfterimageLedger _ledger = new();

        private int _nextIndex;
        private bool _armed;

        public AfterimageLogic(PlayerConfig config)
        {
            _config = config;
        }

        public AfterimageLedger Ledger => _ledger;

        /// <summary>连线段数：不足两枚为 0，两枚为 1，再多就是逐对相邻</summary>
        public int LinkCapacity => _ledger.Count < 2 ? 0 : _ledger.Count == 2 ? 1 : _ledger.Count;

        public void Arm() => _armed = true;

        /// <summary>按时间清过期的；残影只按生成顺序过期，扫到还活着的就能停</summary>
        public void Tick(float now)
        {
            while (_ledger.Count > 0 && _ledger[0].IsExpired(now)) _ledger.RemoveFirst();
        }

        /// <summary>落一枚残影：只有蓄满且没被这次冲刺消费过才落，落完就清掉待释放</summary>
        public bool TrySpawn(Vector2 pos, float now, int facing)
        {
            if (!_armed) return false;
            if (_config.maxAfterimages < 1) return false;

            while (_ledger.Count >= _config.maxAfterimages) _ledger.RemoveFirst();

            _ledger.Add(new Afterimage(pos, now, _config.afterimageDuration, _nextIndex++, facing));
            _armed = false;

            return true;
        }

        /// <summary>这枚残影还在不在场上：子弹每走一段都要先问一次</summary>
        public bool IsAlive(int index) => IndexOf(index) >= 0;

        /// <summary>按序号取落点：表现层要沿某一对残影画线、算判定，需要它们的坐标</summary>
        public bool TryGetPos(int index, out Vector2 pos)
        {
            int i = IndexOf(index);

            pos = i >= 0 ? _ledger[i].Pos : default;

            return i >= 0;
        }

        /// <summary>从入口残影开始按生成顺序规划整条传输路径，断链处就是路径末尾</summary>
        /// <remarks>相邻两枚的间距用感应范围判，和折跃连线同一个口径；绕回入口那一格不算，路径最多覆盖场上所有残影</remarks>
        public int BuildTransfer(int entryIndex, List<TransferNode> path)
        {
            path.Clear();

            int start = IndexOf(entryIndex);
            if (start < 0) return 0;

            path.Add(NodeAt(start));

            int cur = start;
            float sense = _config.afterimageLinkSenseRadius;

            for (int guard = 0; guard < _ledger.Count; guard++)
            {
                int next = (cur + 1) % _ledger.Count;
                if (next == start) break;
                if (!WithinSense(cur, next, sense)) break;

                path.Add(NodeAt(next));
                cur = next;
            }

            return path.Count;
        }

        /// <summary>取第 i 段连线；超出感应范围返回 false</summary>
        /// <remarks>afterimageLinkSenseRadius &lt;= 0 不限距离；两端重合也返回 false</remarks>
        public bool TryGetLink(int i, out Vector2 from, out Vector2 to)
        {
            from = default;
            to = default;

            if (i < 0 || i >= LinkCapacity) return false;

            from = _ledger[i].Pos;
            to = _ledger[(i + 1) % _ledger.Count].Pos;

            float sqr = (to - from).sqrMagnitude;
            if (sqr <= 1e-6f) return false;

            float sense = _config.afterimageLinkSenseRadius;
            return sense <= 0f || sqr <= sense * sense;
        }

        public void Clear()
        {
            _ledger.Clear();
            _armed = false;
        }

        private int IndexOf(int index)
        {
            for (int i = 0; i < _ledger.Count; i++)
            {
                if (_ledger[i].Index == index) return i;
            }

            return -1;
        }

        private TransferNode NodeAt(int i)
        {
            Afterimage snap = _ledger[i];
            return new TransferNode(snap.Index, snap.Pos, snap.Facing);
        }

        private bool WithinSense(int a, int b, float sense)
        {
            float sqr = (_ledger[b].Pos - _ledger[a].Pos).sqrMagnitude;

            if (sqr <= 1e-6f) return false;

            return sense <= 0f || sqr <= sense * sense;
        }
    }
}
