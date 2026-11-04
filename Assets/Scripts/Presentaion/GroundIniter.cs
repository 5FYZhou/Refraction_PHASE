using UnityEngine;

namespace Refraction.Presentation
{
    /// <summary>
    /// 地面初始化：把摆好的子物体统统变成可探测的地面。
    /// 场景里子物体只负责位置和缩放（Transform + SpriteRenderer），
    /// 碰撞框和层级由这里统一补齐，避免手动一个个挂。
    /// </summary>
    public class GroundIniter : MonoBehaviour
    {
        /// <summary>地面层级。与 ProjectSettings/TagManager 中第 7 项 Ground 对应，索引为 6。</summary>
        private const int GroundLayer = 6;

        public void Init()
        {
            // 递归取到所有子物体（含未激活），跳过挂脚本的根节点本身
            Transform[] children = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == transform) continue;

                child.gameObject.layer = GroundLayer;
                EnsureCollider(child.gameObject);
            }
        }

        /// <summary>
        /// 保证物体上有且只有一个 BoxCollider2D。已有的话原样保留，
        /// 那是美术/关卡手动调过的，不要覆盖。
        /// </summary>
        private static void EnsureCollider(GameObject target)
        {
            if (target.TryGetComponent(out BoxCollider2D _)) return;

            BoxCollider2D box = target.AddComponent<BoxCollider2D>();

            // 新建的碰撞框是 1×1，按精灵实际尺寸补齐。
            // 否则非 1×1 的精灵会踩空或者陷进地面里。
            if (target.TryGetComponent(out SpriteRenderer spriteRenderer)
                && spriteRenderer.sprite != null)
            {
                Bounds bounds = spriteRenderer.sprite.bounds;
                box.size = bounds.size;
                box.offset = bounds.center;
            }
        }
    }
}
