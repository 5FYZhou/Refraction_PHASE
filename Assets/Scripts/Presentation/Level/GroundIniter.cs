using System.Collections.Generic;
using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Presentation
{
    /// <summary>把子物体统一补成可探测地面：场景只管摆位置和缩放，Layer 与碰撞框由这里补</summary>
    public class GroundIniter : MonoBehaviour, IReverse
    {
        /// <summary>地面层级，与 TagManager 第 7 项 Ground 对应，索引 6</summary>
        private const int GroundLayer = 6;

        private static readonly Color WhiteColor = Color.white;

        private static readonly Color BlackColor = Color.black;

        private readonly List<SpriteRenderer> _sprites = new();

        private void Awake()
        {
            Init();
        }

        public void Init()
        {
            // true = 未激活的子物体也要算，否则隐藏的地面踩不到
            Transform[] children = GetComponentsInChildren<Transform>(true);

            _sprites.Clear();

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == transform) continue;

                child.gameObject.layer = GroundLayer;
                EnsureCollider(child.gameObject);

                if (child.TryGetComponent(out SpriteRenderer sprite)) _sprites.Add(sprite);
            }
        }

        public void Apply(WorldPolarity world)
        {
            Color color = world == WorldPolarity.Black ? BlackColor : WhiteColor;

            for (int i = 0; i < _sprites.Count; i++)
            {
                SpriteRenderer sprite = _sprites[i];
                if (sprite == null) continue;

                // 只换色调，各自的透明度要留着
                color.a = sprite.color.a;
                sprite.color = color;
            }
        }

        /// <summary>已有 BoxCollider2D 就原样保留：那是关卡手调的，覆盖会把地形改掉</summary>
        private static void EnsureCollider(GameObject target)
        {
            if (target.TryGetComponent(out BoxCollider2D _)) return;

            BoxCollider2D box = target.AddComponent<BoxCollider2D>();

            // 新建的框是 1×1，按精灵实际尺寸补齐，否则非 1×1 的精灵会踩空或陷进地面
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
