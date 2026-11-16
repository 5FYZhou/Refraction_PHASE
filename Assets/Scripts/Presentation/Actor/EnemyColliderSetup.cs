using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人三根碰撞体的统一配置：预制体上的勾选与层错配都会被覆盖，改规则只改这里</summary>
    /// <remarks>
    /// 所有 Collider2D 一起改，不管预制体上启用了几颗：换形状时禁掉旧盒也能被追上。
    /// 身体盒排除 Player 与 Enemy（敌人之间、敌人与玩家都不靠物理推挤），攻击盒排除 Enemy 层，
    /// 探测盒只认 Player 层（同类与场景物不再进视线）。
    /// </remarks>
    public static class EnemyColliderSetup
    {
        public const string BodyChildName = "BodyCollider";

        public const string AttackChildName = "AttackCollider";

        public const string DetectChildName = "DetectCollider";

        private const int PlayerLayer = 7;

        private const int EnemyLayer = 8;

        private static readonly int PlayerOnly = 1 << PlayerLayer;

        private static readonly int EnemyOnly = 1 << EnemyLayer;

        private static readonly int PlayerAndEnemy = PlayerOnly | EnemyOnly;

        private static readonly int NotPlayer = ~PlayerOnly;

        private static readonly Collider2D[] Empty = new Collider2D[0];

        public static void Apply(Transform root)
        {
            Collider2D[] boxes = BoxesOf(root, BodyChildName);

            for (int i = 0; i < boxes.Length; i++) boxes[i].excludeLayers = PlayerAndEnemy;

            boxes = BoxesOf(root, AttackChildName);

            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].isTrigger = true;
                boxes[i].excludeLayers = EnemyOnly;
            }

            boxes = BoxesOf(root, DetectChildName);

            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].isTrigger = true;
                boxes[i].includeLayers = PlayerOnly;
                boxes[i].excludeLayers = NotPlayer;
            }
        }

        private static Collider2D[] BoxesOf(Transform root, string childName)
        {
            Transform child = root.Find(childName);

            if (child == null)
            {
                Debug.LogWarning($"[{root.name}] 找不到 {childName} 子物体：碰撞体配置没落上。");
                return Empty;
            }

            return child.GetComponents<Collider2D>();
        }
    }
}
