using Refraction.Logic.Level;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>存档点：用自己的触发器认玩家身体，颜色随世界极性取反（rgb 取反、alpha 不动）</summary>
    public sealed class Spawn : MonoBehaviour, IReverse
    {
        [Tooltip("勾上后玩家一踏进触发范围就自动存档，不用按 W。")]
        [SerializeField]
        private bool autoSave;

        private Collider2D _zone;
        private Collider2D _playerBody;
        private bool _resolved;
        private GameRoot _root;

        public bool AutoSave => autoSave;

        /// <summary>锚点位置，按地面上的一点理解：调用方要自己补上脚底到枢轴的偏移</summary>
        public Vector2 Point => transform.position;

        /// <summary>玩家身体此刻是否压在里面；只认 PlayerMotor.BodyCollider，HurtCollider 之类的子碰撞体不算</summary>
        public bool HasPlayer { get; private set; }

        private void Awake()
        {
            _zone = GetComponent<Collider2D>();

            if (_zone == null)
            {
                Debug.LogWarning($"[Spawn] {name} 上没有 Collider2D，玩家站进来也不会存档。");
                return;
            }

            if (_zone.isTrigger) return;

            _zone.isTrigger = true;
            Debug.LogWarning($"[Spawn] {name} 的碰撞体不是 Trigger，已临时改成 Trigger；请在 Inspector 里勾上，否则它会挡住玩家。");
        }

        private void Start()
        {
            Attach();
        }

        private void OnDestroy()
        {
            LevelRoot.Current?.UnregisterReverse(this);
        }

        private void OnDisable() => HasPlayer = false;

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!_resolved) ResolvePlayerBody(other);
            if (other != _playerBody) return;

            HasPlayer = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != _playerBody) return;

            HasPlayer = false;
        }

        public void Attach()
        {
            _root ??= GameRoot.Instance;
            LevelRoot.Current?.RegisterReverse(this);
        }

        public void Apply(WorldPolarity world)
        {
            _root.Effects.SetInvert(transform, world == WorldPolarity.Black);
        }

        /// <summary>玩家身体碰撞体是恒定的：只解析一次，之后每个物理步只做引用比较，不再 GetComponent</summary>
        private void ResolvePlayerBody(Collider2D other)
        {
            MoveMotor motor = other.GetComponentInParent<MoveMotor>();
            if (motor == null) return;

            _playerBody = motor.BodyCollider;
            _resolved = _playerBody != null;
        }
    }
}
