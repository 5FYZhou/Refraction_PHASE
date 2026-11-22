using UnityEngine;

namespace Refraction.Presentation
{
    /// <summary>Spliter 执行器：不探地、不贴地，只在身体周围一圈圆环里探墙</summary>
    public sealed class SpliterMotor : MoveMotor
    {
        [Header("撞墙探针")]
        [Tooltip("内圈半径（单位）：圆环的内径，取身体碰撞体半径。\n比这还近的只可能是自己的身体或已经嵌进墙里，都不算探到墙。")]
        [Min(0.01f)]
        [SerializeField]
        private float wallInnerRadius = 0.45f;

        [Tooltip("外圈半径（单位）：圆环的外径，两圈之间就是撞墙判定区。\n圆环不分方向，斜着甚至竖着撞墙都查得到。")]
        [Min(0.02f)]
        [SerializeField]
        private float wallOuterRadius = 0.75f;

        [Tooltip("墙面所在的层：必须勾上，留空等于永远探不到墙，返回态就永远撞不了墙。")]
        [SerializeField]
        private LayerMask wallMask;

        private readonly Collider2D[] _hits = new Collider2D[8];

        public override float BodyHeight => wallInnerRadius * 2f;

        public override float FeetOffsetY => 0f;

        public override void SetBodyScale(float scale) { }

        public override bool CanStandUp(float standHeight, float crouchHeight) => true;

        protected override void Awake()
        {
            base.Awake();

            Body.freezeRotation = true;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        /// <summary>飞行怪没有地面：永远判空，逻辑层据此不施加重力也不贴回</summary>
        protected override bool SampleGround() => false;

        /// <summary>身体周围两圈之间的圆环里有没有墙；内圈里的算贴身或已嵌进去，不算探到</summary>
        protected override bool SampleWall(int side)
        {
            float inner = Mathf.Min(wallInnerRadius, wallOuterRadius);
            float innerSq = inner * inner;

            Vector2 origin = Body.position;

            int count = Physics2D.OverlapCircleNonAlloc(origin, wallOuterRadius, _hits, wallMask);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null || hit.isTrigger) continue;
                if (hit.transform.IsChildOf(transform)) continue;

                Vector2 delta = hit.ClosestPoint(origin) - origin;

                // 内圈里：最近的墙面就在身体边上（或已经嵌进去），不算撞墙
                if (delta.sqrMagnitude < innerSq) continue;

                return true;
            }

            return false;
        }

        /// <summary>两个同心圆都画：里圈是身体半径，两圈之间那圈就是撞墙判定区</summary>
        private void OnDrawGizmosSelected()
        {
            Vector2 origin = Body != null ? Body.position : (Vector2)transform.position;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.45f);
            Gizmos.DrawWireSphere(origin, wallInnerRadius);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.95f);
            Gizmos.DrawWireSphere(origin, wallOuterRadius);
        }
    }
}
