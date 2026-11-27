using System.Collections.Generic;
using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>Boss 的小怪场：保存召唤出来的实例，负责存活计数、落点避墙与清场</summary>
    /// <remarks>存活判据是 activeSelf：EnemyMgr 回收只 SetActive(false)，不销毁也不出列</remarks>
    public sealed class MmflyMinionField : IMinionHost
    {
        private const int PlaceTries = 12;

        private readonly List<ManagedEnemyController> _alive = new();
        private readonly System.Random _rng = new();
        private readonly Transform _boss;
        private readonly Transform _root;
        private readonly GameObject _flyPrefab;
        private readonly GameObject _crawlerPrefab;
        private readonly LayerMask _blockMask;
        private readonly float _clearance;
        private readonly float _radius;

        public MmflyMinionField(
            Transform boss,
            GameObject flyPrefab,
            GameObject crawlerPrefab,
            LayerMask blockMask,
            float clearance,
            float radius)
        {
            _boss = boss;
            _root = boss != null ? boss.parent : null;
            _flyPrefab = flyPrefab;
            _crawlerPrefab = crawlerPrefab;
            _blockMask = blockMask;
            _clearance = Mathf.Max(0f, clearance);
            _radius = Mathf.Max(0f, radius);
        }

        public bool Available => _flyPrefab != null || _crawlerPrefab != null;

        public int AliveCount
        {
            get
            {
                int count = 0;

                for (int i = _alive.Count - 1; i >= 0; i--)
                {
                    ManagedEnemyController minion = _alive[i];

                    if (minion == null)
                    {
                        _alive.RemoveAt(i);
                        continue;
                    }

                    if (!minion.gameObject.activeSelf || minion.IsDead) continue;

                    count++;
                }

                return count;
            }
        }

        public void Summon(int count)
        {
            if (_boss == null || count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = PickPrefab();

                if (prefab == null) return;
                if (!TryPlace(out Vector2 pos)) continue;

                GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity, _root);
                ManagedEnemyController minion = go.GetComponent<ManagedEnemyController>();

                if (minion == null)
                {
                    Object.Destroy(go);
                    continue;
                }

                // Start 还没跑，这时定形态才会进出生快照；预制体若存成未激活，先定形态再唤醒
                minion.PrepareSpawn(_rng.Next(2) == 0);

                if (!go.activeSelf) go.SetActive(true);

                _alive.Add(minion);
            }
        }

        /// <summary>清场：Destroy 延迟到帧末，不会改到 EnemyMgr 正在遍历的那份列表</summary>
        public void Clear()
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                ManagedEnemyController minion = _alive[i];

                if (minion != null) Object.Destroy(minion.gameObject);
            }

            _alive.Clear();
        }

        private GameObject PickPrefab()
        {
            bool hasFly = _flyPrefab != null;
            bool hasCrawler = _crawlerPrefab != null;

            if (!hasFly) return hasCrawler ? _crawlerPrefab : null;
            if (!hasCrawler) return _flyPrefab;

            return _rng.Next(2) == 0 ? _flyPrefab : _crawlerPrefab;
        }

        /// <summary>圆面内随机落点：半径开根号才是均匀分布，否则会往圆心堆</summary>
        private bool TryPlace(out Vector2 pos)
        {
            for (int i = 0; i < PlaceTries; i++)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float radius = _radius * Mathf.Sqrt((float)_rng.NextDouble());
                Vector2 point = (Vector2)_boss.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (Physics2D.OverlapCircle(point, _clearance, _blockMask) != null) continue;

                pos = point;

                return true;
            }

            pos = default;

            return false;
        }
    }
}
