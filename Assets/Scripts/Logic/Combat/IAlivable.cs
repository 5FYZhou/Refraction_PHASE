
using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>最基本生命体征，是"谁是活的"唯一判据</summary>
    public interface IAlivable
    {
        bool IsAlive { get; }
    }

    public interface IAttackable
    {
        void TakeDamage(in Damage damage);
    }

    public interface IAttack
    {
        Damage ProduceDamage();
    }
}
