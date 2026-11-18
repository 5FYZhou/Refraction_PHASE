using Refraction.Data;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人三根判定盒的装配：参数全在 EnemyColliderConfig 资产里，预制体上勾成什么样都会被覆盖</summary>
    /// <remarks>每根子物体下所有 Collider2D 一起改，换形状时禁掉的旧盒也能被追上。</remarks>
    public static class EnemyColliderSetup
    {
        private static readonly Collider2D[] Empty = new Collider2D[0];

        public static string DetectChildName => EnemyColliderConfig.Default.detectChildName;

        public static void Apply(Transform root) => Apply(root, EnemyColliderConfig.Default);

        public static void Apply(Transform root, EnemyColliderConfig config)
        {
            config ??= EnemyColliderConfig.Default;

            Collider2D[] boxes = BoxesOf(root, config.bodyChildName);

            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].isTrigger = false;
                boxes[i].excludeLayers = config.BodyExcludeMask;
            }

            boxes = BoxesOf(root, config.attackChildName);

            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].isTrigger = true;
                boxes[i].excludeLayers = config.EnemyMask;
            }

            boxes = BoxesOf(root, config.detectChildName);

            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].isTrigger = true;
                boxes[i].includeLayers = config.PlayerMask;
                boxes[i].excludeLayers = config.NotPlayerMask;
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

            return child.GetComponentsInChildren<Collider2D>();
        }
    }
}
