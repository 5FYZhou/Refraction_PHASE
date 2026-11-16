#if UNITY_EDITOR
using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人编辑器绘制：巡逻框、追击框与视线范围，三只敌人共用一套配色</summary>
    public static class EnemyGizmos
    {
        public static readonly Color Patrol = new(0.35f, 1f, 0.45f, 0.9f);

        public static readonly Color Chase = new(1f, 0.45f, 0.35f, 0.9f);

        public static readonly Color Sight = new(0.4f, 0.85f, 1f, 0.6f);

        public static readonly Color Muzzle = Color.red;

        /// <summary>编辑态：按 Inspector 里的局部偏移与尺寸画，跟着敌人一起动</summary>
        public static void DrawZone(Transform root, Vector2 offset, Vector2 size, Color color)
        {
            if (size.x <= 0.01f) return;

            Gizmos.color = color;
            Gizmos.DrawWireCube(root.position + (Vector3)offset, new Vector2(size.x, Mathf.Max(size.y, 0.2f)));
        }

        /// <summary>运行态：画逻辑真正在用的那份快照，框钉在世界上，敌人怎么走都不动</summary>
        public static void DrawArea(in EnemyArea area, Color color)
        {
            if (!area.HasBounds) return;

            Gizmos.color = color;
            Gizmos.DrawWireCube(
                new Vector3(area.Center.x, area.Center.y, 0f),
                new Vector2(area.HalfSize.x * 2f, Mathf.Max(area.HalfSize.y * 2f, 0.2f)));
        }

        /// <summary>出膛点：x 随当前朝向镜像，与开火时同一套算法</summary>
        public static void DrawMuzzle(Transform root, Vector2 offset)
        {
            float sign = root.lossyScale.x >= 0f ? 1f : -1f;

            Vector3 point = root.position + new Vector3(Mathf.Abs(offset.x) * sign, offset.y, 0f);

            Gizmos.color = Muzzle;
            Gizmos.DrawWireSphere(point, 0.15f);
            Gizmos.DrawLine(point + Vector3.left * 0.25f, point + Vector3.right * 0.25f);
            Gizmos.DrawLine(point + Vector3.down * 0.25f, point + Vector3.up * 0.25f);
        }

        /// <summary>视线范围：DetectCollider 上的触发器轮廓，尺寸在子物体上改</summary>
        public static void DrawSight(Transform root, string childName)
        {
            Transform detect = root.Find(childName);
            if (detect == null) return;

            Collider2D zone = detect.GetComponent<Collider2D>();
            if (zone == null) return;

            Gizmos.color = Sight;

            if (zone is BoxCollider2D box)
            {
                Gizmos.DrawWireCube(detect.TransformPoint(box.offset), Vector3.Scale(box.size, detect.lossyScale));
                return;
            }

            Bounds bounds = zone.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
#endif
