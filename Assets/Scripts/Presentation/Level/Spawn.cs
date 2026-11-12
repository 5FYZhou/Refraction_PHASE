using System.Collections;
using UnityEngine;

namespace Refraction.Presentation.Level
{
    /// <summary>存档点：挂在场景的 Spawn 对象上，用自己的触发器认玩家身体，存档成功时闪一下黄再回原色</summary>
    public sealed class Spawn : MonoBehaviour
    {
        private static readonly Color SavedColor = new(1f, 0.92f, 0.3f, 1f);

        [Tooltip("存档成功后高亮停留的时长（秒）。")]
        [Min(0f)]
        [SerializeField]
        private float flashSeconds = 0.4f;

        private Collider2D _zone;
        private SpriteRenderer _renderer;
        private Color _baseColor;
        private Collider2D _playerBody;
        private bool _resolved;
        private Coroutine _flashRoutine;

        /// <summary>锚点位置，按地面上的一点理解：调用方要自己补上脚底到枢轴的偏移</summary>
        public Vector2 Point => transform.position;

        /// <summary>玩家身体此刻是否压在里面；只认 PlayerMotor.BodyCollider，HurtCollider 之类的子碰撞体不算</summary>
        public bool HasPlayer { get; private set; }

        private void Awake()
        {
            _zone = GetComponent<Collider2D>();
            _renderer = GetComponent<SpriteRenderer>();

            if (_renderer != null) _baseColor = _renderer.color;

            if (_zone == null)
            {
                Debug.LogWarning($"[Spawn] {name} 上没有 Collider2D，玩家站进来也不会存档。");
                return;
            }

            if (_zone.isTrigger) return;

            _zone.isTrigger = true;
            Debug.LogWarning($"[Spawn] {name} 的碰撞体不是 Trigger，已临时改成 Trigger；请在 Inspector 里勾上，否则它会挡住玩家。");
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

        /// <summary>玩家身体碰撞体是恒定的：只解析一次，之后每个物理步只做引用比较，不再 GetComponent</summary>
        private void ResolvePlayerBody(Collider2D other)
        {
            MoveMotor motor = other.GetComponentInParent<MoveMotor>();
            if (motor == null) return;

            _playerBody = motor.BodyCollider;
            _resolved = _playerBody != null;
        }

        public void Flash()
        {
            if (_renderer == null || flashSeconds <= 0f) return;

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _renderer.color = SavedColor;
            yield return new WaitForSeconds(flashSeconds);
            _renderer.color = _baseColor;
            _flashRoutine = null;
        }
    }
}
