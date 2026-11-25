using Refraction.Logic.Actor;
using Refraction.Presentation.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人管理壳：出生快照、登记、驱动守卫、回收与刷新外壳；玩法一律留给派生</summary>
    public abstract class ManagedEnemyController : MonoBehaviour, IManagedEnemy
    {
        private bool _attached;
        private bool _dead;
        private EnemySpawnState _spawn;

        public EnemySpawnState SpawnState => _spawn;

        public bool IsDead => _dead;

        public bool Attached => _attached;

        protected GameRoot Root { get; private set; }

        /// <summary>存活判定归派生：壳不知道这只敌人的血量藏在哪</summary>
        protected abstract bool BodyAlive { get; }

        private void Start()
        {
            Root = GameRoot.Instance;

            Assemble();

            _attached = true;
            _spawn = CaptureSpawn();

            EnemyMgr.Join(this);
        }

        private void OnDestroy()
        {
            OnDispose();

            EnemyMgr.Leave(this);
        }

        public void ManagedTick(float deltaTime)
        {
            if (!_attached || _dead) return;

            OnTick(deltaTime);

            if (!BodyAlive) _dead = true;
        }

        public void Recycle()
        {
            OnRecycle();

            gameObject.SetActive(false);
        }

        public void ResetToSpawn()
        {
            if (!_attached) return;

            // 先关：搬位置时不补 Exit；末尾重开：对当前重叠补发 Enter
            gameObject.SetActive(false);
            _dead = false;

            ResetBody();

            gameObject.SetActive(true);
        }

        /// <summary>出生快照：位置 / 朝向 / 形态 / 满血由派生给，壳只存不解释</summary>
        protected abstract EnemySpawnState CaptureSpawn();

        /// <summary>装配：建逻辑、接受击盒与攻击盒、建子弹池</summary>
        protected abstract void Assemble();

        /// <summary>每物理帧的玩法推进；未装配或已死时壳不会调到这里</summary>
        protected abstract void OnTick(float deltaTime);

        /// <summary>回收前的手尾：停自己开的持续表现；发射出去的子弹自己飞完</summary>
        protected virtual void OnRecycle()
        {
        }

        /// <summary>复位本体：重建逻辑、清弹、归位、上色；激活与清死亡标记由壳负责</summary>
        protected abstract void ResetBody();

        /// <summary>销毁前的手尾：逻辑与子弹容器</summary>
        protected virtual void OnDispose()
        {
        }
    }
}
