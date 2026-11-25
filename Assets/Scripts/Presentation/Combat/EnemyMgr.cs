using System.Collections.Generic;
using Refraction.Data;
using Refraction.Presentation.Actor;

namespace Refraction.Presentation.Combat
{
    /// <summary>战斗域的敌人登记簿：稳定顺序驱动、死亡回收、全体刷新；不碰场景对象，也不认识具体敌人</summary>
    /// <remarks>进入本关时的启动交接靠 Pending：敌人 Start 早于 CombatRoot.Attach，先排队，等本对象建好再收编</remarks>
    public sealed class EnemyMgr
    {
        private static readonly List<IManagedEnemy> Pending = new();

        private static EnemyMgr _active;

        private readonly List<IManagedEnemy> _alive = new();
        private readonly List<IManagedEnemy> _dead = new();

        private int _resetCount;

        public EnemyMgr()
        {
            _active = this;
            AdoptPending();
        }

        public IReadOnlyList<IManagedEnemy> Active => _alive;

        public int ActiveCount => _alive.Count;

        public int DeadCount => _dead.Count;

        public int ResetCount => _resetCount;

        /// <summary>敌人出生后自己报上来；还没有战斗域时先排队，等 EnemyMgr 建好</summary>
        public static void Join(IManagedEnemy enemy)
        {
            if (enemy == null) return;

            if (_active != null)
            {
                _active.Register(enemy);
                return;
            }

            if (!Pending.Contains(enemy)) Pending.Add(enemy);
        }

        /// <summary>敌人被销毁（换场景、编辑器删对象）时退订，排队表里也要摘掉</summary>
        public static void Leave(IManagedEnemy enemy)
        {
            if (enemy == null) return;

            Pending.Remove(enemy);
            _active?.Unregister(enemy);
        }

        public void Register(IManagedEnemy enemy)
        {
            if (enemy == null) return;
            if (_alive.Contains(enemy) || _dead.Contains(enemy)) return;

            _alive.Add(enemy);
        }

        public void Unregister(IManagedEnemy enemy)
        {
            if (enemy == null) return;

            _alive.Remove(enemy);
            _dead.Remove(enemy);
        }

        /// <summary>驱动一圈：本帧死亡的当场迁进回收表并停用，活着的按注册顺序原地压实</summary>
        public void FixedTick(float deltaTime)
        {
            int kept = 0;

            for (int i = 0; i < _alive.Count; i++)
            {
                IManagedEnemy enemy = _alive[i];

                enemy.ManagedTick(deltaTime);

                if (enemy.IsDead)
                {
                    _dead.Add(enemy);
                    GameRoot.Instance?.Audio?.EnqueueSfx(AudioId.EnemyDeath);
                    enemy.Recycle();
                    continue;
                }

                _alive[kept++] = enemy;
            }

            if (kept < _alive.Count) _alive.RemoveRange(kept, _alive.Count - kept);
        }

        /// <summary>渲染帧：转朝向这类表现不进物理帧，只有实现了 IRenderTicked 的敌人才接</summary>
        public void RenderTick(float deltaTime)
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i] is IRenderTicked ticked) ticked.RenderTick(deltaTime);
            }
        }

        /// <summary>全体刷新：存活的复位，回收表里的复位并重新入列；顺序与注册顺序一致</summary>
        public void ResetAll()
        {
            for (int i = 0; i < _alive.Count; i++) _alive[i].ResetToSpawn();

            for (int i = 0; i < _dead.Count; i++)
            {
                IManagedEnemy enemy = _dead[i];
                enemy.ResetToSpawn();
                _alive.Add(enemy);
            }

            _dead.Clear();
            _resetCount++;
        }

        public void Dispose()
        {
            _alive.Clear();
            _dead.Clear();

            // 新战斗域已经建好时不许再清排队表：旧场景的销毁可能排在新场景入队之后
            if (_active != this) return;

            _active = null;
            Pending.Clear();
        }

        private void AdoptPending()
        {
            for (int i = 0; i < Pending.Count; i++) Register(Pending[i]);

            Pending.Clear();
        }
    }
}
