using Refraction.Logic.Actor;

namespace Refraction.Presentation.Actor
{
    /// <summary>运行时敌人契约：EnemyMgr 只按它驱动、回收与复位，不认识具体敌人类型</summary>
    /// <remarks>不放 Attach / Transform / MonoBehaviour 生命周期：装配与场景对象归控制器自己</remarks>
    public interface IManagedEnemy
    {
        EnemySpawnState SpawnState { get; }

        bool IsDead { get; }

        void ManagedTick(float deltaTime);

        void Recycle();

        void ResetToSpawn();
    }
}
