using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>碎裂地面：玩家踩上后支撑时间走完就碎，恢复时间走完再长回来；计时期间透明度闪烁提示</summary>
    /// <remarks>只借用场景根拿物理帧驱动；换色走 IReverse，闪烁与计时都在 FixedTick 里按固定步长推进</remarks>
    public sealed class WeakGround : MonoBehaviour, ISceneRoot, IPhysicsTicked, IReverse
    {
        /// <summary>地面层级，与 TagManager 第 7 项 Ground 对应，索引 6</summary>
        private const int GroundLayer = 6;

        [Tooltip("踩踏判定区：贴着顶面往上这一段（米）里有玩家碰撞体就算踩上。")]
        [Min(0.01f)]
        [SerializeField]
        private float standProbeHeight = 0.25f;

        [Tooltip("踩上后多久碎裂（秒）。计时一旦开始就不回退，中途跳开也照碎。")]
        [Min(0.01f)]
        [SerializeField]
        private float supportTime = 1.2f;

        [Tooltip("碎裂后多久恢复（秒）。")]
        [Min(0f)]
        [SerializeField]
        private float recoverTime = 2f;

        [Tooltip("闪烁一次的时长（秒），一次是明→暗→明。")]
        [Min(0.02f)]
        [SerializeField]
        private float blinkPeriod = 0.24f;

        [Tooltip("闪到最暗时保留多少不透明度，按预制体原有 alpha 的比例算。")]
        [Range(0f, 1f)]
        [SerializeField]
        private float blinkMinAlpha = 0.25f;

        [Tooltip("玩家所在的层号（Project Settings ▸ Tags and Layers 的顺序）。")]
        [SerializeField]
        private int playerLayer = 7;

        private SpriteRenderer _sprite;
        private Collider2D _collider;
        private Color _designColor = Color.white;
        private GameRoot _root;
        private WorldPolarity _world = WorldPolarity.White;
        private int _playerMask;
        private float _time;
        private float _alpha = 1f;
        private bool _started;
        private bool _broken;

        public int Order => SceneOrder.World;

        private void Awake()
        {
            Init();
        }

        private void Start()
        {
            Attach();
            _root?.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_root == null) return;

            _root.UnregisterSceneRoot(this);
            _root.UnregisterReverse(this);
        }

        public void Attach()
        {
            _root ??= GameRoot.Instance;
            _root?.RegisterReverse(this);
        }

        public void Apply(WorldPolarity world)
        {
            _world = world;
            SetAlpha(_alpha);
        }

        public void FixedTick(float deltaTime)
        {
            if (_collider == null) return;

            if (_broken)
            {
                _time += deltaTime;

                if (_time < recoverTime) return;

                if (!PlayerInside()) Restore();
                return;
            }

            if (!_started)
            {
                if (!PlayerOnTop()) return;

                _started = true;
            }

            _time += deltaTime;

            if (_time >= supportTime)
            {
                Break();
                return;
            }

            SetAlpha(Blink());
        }

        private float Blink()
        {
            float period = Mathf.Max(0.02f, blinkPeriod);
            float phase = Mathf.PingPong(_time / period * 2f, 1f);

            return Mathf.Lerp(1f, blinkMinAlpha, phase);
        }

        private void Break()
        {
            _broken = true;
            _time = 0f;

            SetAlpha(0f);
            _collider.enabled = false;
        }

        private void Restore()
        {
            _broken = false;
            _started = false;
            _time = 0f;

            SetAlpha(1f);
            _collider.enabled = true;
        }

        private void SetAlpha(float alpha)
        {
            _alpha = Mathf.Clamp01(alpha);

            if (_sprite == null) return;

            // 底色跟着世界走，只有透明度归这里管，反色与闪烁互不覆盖
            Color color = _world == WorldPolarity.Black ? Color.black : Color.white;
            color.a = _designColor.a * _alpha;
            _sprite.color = color;
        }

        /// <summary>只探顶面那一层薄片：探到方块侧面不算踩上，玩家从下面顶上来也不算</summary>
        private bool PlayerOnTop()
        {
            Bounds bounds = _collider.bounds;
            float height = Mathf.Max(0.01f, standProbeHeight);

            Vector2 center = new(bounds.center.x, bounds.max.y + height * 0.5f);
            Vector2 size = new(bounds.size.x, height);

            return Physics2D.OverlapBox(center, size, 0f, _playerMask) != null;
        }

        /// <summary>人还在方块体积里时不能恢复：碰撞体凭空长出来会把人顶飞或卡进地面</summary>
        private bool PlayerInside()
        {
            Bounds bounds = _collider.bounds;

            return Physics2D.OverlapBox(bounds.center, bounds.size, 0f, _playerMask) != null;
        }

        private void Init()
        {
            gameObject.layer = GroundLayer;

            _sprite = GetComponent<SpriteRenderer>();

            if (_sprite == null)
            {
                Debug.LogWarning($"[WeakGround] {name} 上没有 SpriteRenderer，碎裂过程看不到透明度变化。");
            }
            else
            {
                _designColor = _sprite.color;
            }

            _collider = GetComponent<Collider2D>();

            if (_collider == null) _collider = AddBox();

            if (_collider.isTrigger)
            {
                Debug.LogWarning($"[WeakGround] {name} 的碰撞体是 Trigger，玩家踩不上去，碎裂不会发生。");
            }

            _playerMask = 1 << playerLayer;
        }

        private BoxCollider2D AddBox()
        {
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();

            // 新建的框是 1×1，按精灵实际尺寸补齐，否则非 1×1 的精灵会踩空或陷进地面
            if (_sprite != null && _sprite.sprite != null)
            {
                Bounds bounds = _sprite.sprite.bounds;
                box.size = bounds.size;
                box.offset = bounds.center;
            }

            return box;
        }
    }
}
