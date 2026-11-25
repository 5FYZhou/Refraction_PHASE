using Refraction.Foundation;
using UnityEngine;

namespace Refraction.Data
{
    /// <summary>敌人三根判定盒的统一配置：预制体上的触发器勾选与层设置全被它覆盖</summary>
    /// <remarks>
    /// 身体盒排除 Player，攻击盒排除 Enemy，探测盒只认 Player；只有身体盒排不排同类走 bodyExcludeEnemy 开关。
    /// 资产放在 Resources/Data/EnemyColliderConfig.asset。
    /// </remarks>
    [CreateAssetMenu(fileName = "EnemyColliderConfig", menuName = "Refraction/Settings/EnemyCollider")]
    public sealed class EnemyColliderConfig : ScriptableObject
    {
        private static EnemyColliderConfig _default;

        [Tooltip("身体盒（实体，非触发器）的子物体名：按名字找，找不到只报警告。")]
        public string bodyChildName = "BodyCollider";

        [Tooltip("攻击盒（触发器，接触伤害）的子物体名。")]
        public string attackChildName = "AttackCollider";

        [Tooltip("探测盒（触发器，视线范围）的子物体名。")]
        public string detectChildName = "DetectCollider";

        [Tooltip("玩家层号（Project Settings ▸ Tags and Layers 的顺序）：探测盒只强制包含这一层。")]
        public int playerLayer = 7;

        [Tooltip("敌人层号：身体盒与攻击盒都把它排除，敌人之间不推挤、子弹不打同类。")]
        public int enemyLayer = 8;

        [Tooltip("身体盒是否排除敌人层（只影响身体盒）：关掉 = 同类之间会互相挡住、挤不开。攻击盒与探测盒不受它影响。")]
        public bool bodyExcludeEnemy = true;

        public int PlayerMask => 1 << playerLayer;

        public int EnemyMask => 1 << enemyLayer;

        public int BodyExcludeMask => PlayerMask | (bodyExcludeEnemy ? EnemyMask : 0);

        public int NotPlayerMask => ~PlayerMask;

        public static EnemyColliderConfig Default
        {
            get
            {
                if (_default != null) return _default;

                _default = Resources.Load<EnemyColliderConfig>(ResPath.EnemyColliderConfig);

                if (_default == null)
                {
                    _default = CreateInstance<EnemyColliderConfig>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    Debug.LogWarning($"[敌人碰撞体] 缺 Resources/{ResPath.EnemyColliderConfig}.asset：临时用脚本默认值，资产里改不到。");
                }

                return _default;
            }
        }
    }
}
