using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>彩面地面：只在指定极性的世界存在，另一极性下整块消失（不参与反色），碰撞体一并关掉</summary>
    public sealed class ColorGround : MonoBehaviour, IReverse, IReverseFade
    {
        /// <summary>地面层级，与 TagManager 第 7 项 Ground 对应，索引 6</summary>
        private const int GroundLayer = 6;

        [Tooltip("这面地面存在于哪个世界：白 = 只在白世界踩得到，黑 = 只在黑世界踩得到。")]
        [SerializeField]
        private WorldPolarity showIn = WorldPolarity.White;

        private SpriteRenderer _sprite;
        private Collider2D _collider;
        private Color _designColor = Color.white;
        private WorldPolarity _world = WorldPolarity.White;
        private float _fade = 1f;

        private void Awake()
        {
            Init();
        }

        private void Start()
        {
            Attach();
        }

        private void OnDestroy()
        {
            LevelRoot.Current?.UnregisterReverse(this);
            LevelRoot.Current?.UnregisterReverseFade(this);
        }

        public void Attach()
        {
            LevelRoot.Current?.RegisterReverse(this);
            LevelRoot.Current?.RegisterReverseFade(this);
        }

        public void Apply(WorldPolarity world)
        {
            _world = world;
            Refresh();
        }

        public void ApplyFade(float scale)
        {
            _fade = scale;
            Refresh();
        }

        private void Refresh()
        {
            bool visible = _world == showIn;

            if (_sprite != null)
            {
                // 先取色再决定显隐：隐藏那一拍也要把颜色刷对，不然切回来会闪一帧旧色
                Color color = _world == WorldPolarity.Black ? Color.black : Color.white;
                color.a = _designColor.a * _fade;

                _sprite.color = color;
                _sprite.enabled = visible;
            }

            if (_collider != null) _collider.enabled = visible;
        }

        private void Init()
        {
            gameObject.layer = GroundLayer;

            _sprite = GetComponent<SpriteRenderer>();

            if (_sprite == null)
            {
                Debug.LogWarning($"[ColorGround] {name} 上没有 SpriteRenderer，显现与消失只能靠碰撞体开关。");
            }
            else
            {
                _designColor = _sprite.color;
            }

            _collider = GetComponent<Collider2D>();

            if (_collider != null) return;

            _collider = gameObject.AddComponent<BoxCollider2D>();

            if (_sprite != null && _sprite.sprite != null)
            {
                Bounds bounds = _sprite.sprite.bounds;
                BoxCollider2D box = (BoxCollider2D)_collider;
                box.size = bounds.size;
                box.offset = bounds.center;
            }
        }
    }
}
