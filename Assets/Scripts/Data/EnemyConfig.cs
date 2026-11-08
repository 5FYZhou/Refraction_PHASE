using UnityEngine;

namespace Refraction.Data
{
    /// <summary>敌人参数：目前只有 CharacterConfig 那套通用移动值，敌人专属项（索敌、巡逻、出手间隔）等接入时再加</summary>
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Refraction/Settings/Enemy")]
    public sealed class EnemyConfig : CharacterConfig
    {
    }
}
