using UnityEngine;

namespace Refraction.Presentation
{
    /// <summary>毛毛虫执行器：探地面并贴回、探墙、探前方悬崖；身体按名字找 BodyCollider，脚底优先 View/Bottom</summary>
    public sealed class E_FirstMotor : MoveMotor
    {
        private const string BodyChildName = "BodyCollider";
        private const string ViewChildName = "View";
        private const string BottomChildName = "Bottom";
        private const string TopChildName = "Top";

        [Tooltip("身体碰撞体：留空按名字找 BodyCollider 子物体。")]
        [SerializeField]
        private Collider2D bodyCollider;

        [Tooltip("脚底参考点（View/Bottom）：地面探测、贴地、悬崖探针都以它为基准。\n留空按名字找，再找不到退回碰撞体底边（贴地会有偏差）。")]
        [SerializeField]
        private Transform feetRef;

        [Tooltip("头顶参考点（View/Top）：站立身高由它与脚底量出；留空按名字找。")]
        [SerializeField]
        private Transform headRef;

        [Header("贴地吸附")]
        [Tooltip("踩地时把刚体放回地面。关掉就退回纯物理落地，脚底留几厘米空隙仍算踩地，逻辑层不再施加重力，虫子会悬着。")]
        [SerializeField]
        private bool snapToGround = true;

        [Tooltip("吸附后脚底与地面保留的间隙：0.005 与 Box2D 的 linear slop 相同，就是物理落地后的静止间隙。")]
        [Min(0f)]
        [SerializeField]
        private float groundSkin = 0.005f;

        [Tooltip("贴地纠偏的死区：偏差没超过它就不动，否则求解器每帧的误差都会触发一次瞬移，身体原地微抖。")]
        [Min(0f)]
        [SerializeField]
        private float snapDeadZone = 0.012f;

        [Header("地面检测 · 圆形射线")]
        [SerializeField]
        private GroundProbeSettings groundProbe = new()
        {
            radius = 0.3f,
            distance = 0.5f
        };

        [Header("墙面检测 · 胶囊射线")]
        [Tooltip("origin.x 从身体中心往侧面量：探针要贴着身体边缘，才不会离墙半米就掉头。")]
        [SerializeField]
        private WallProbeSettings wallProbe = new()
        {
            origin = new Vector2(0.4f, 0.35f),
            radius = 0.18f,
            height = 0.5f,
            distance = 0.08f
        };

        [Header("悬崖检测 · 向下探针")]
        [Tooltip("探针在脚底前方多远，随朝向镜像：要贴着身体前沿，太靠内得半个身子悬空才响。")]
        [Min(0f)]
        [SerializeField]
        private float ledgeAhead = 0.5f;

        [Tooltip("脚底以下探多深：这个深度内都探不到朝上的面就算悬崖。")]
        [Min(0f)]
        [SerializeField]
        private float ledgeDepth = 0.6f;

        [Tooltip("悬崖探针圆半径：比脚底探圆小，免得把平台侧壁当成地面。")]
        [Min(0.01f)]
        [SerializeField]
        private float ledgeRadius = 0.12f;

        private float _feetOffset;
        private float _bodyHeight;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

        public override float BodyHeight => _bodyHeight;

        public override float FeetOffsetY =>
            feetRef != null ? feetRef.position.y - transform.position.y : _feetOffset;

        protected override void Awake()
        {
            base.Awake();

            // 探测盒挂在身体下面，身体一转盒子跟着歪；物理帧位置由账本驱动，插值交给刚体
            Body.freezeRotation = true;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            CacheRefs();
            CacheBody();
        }

        protected override void OnConfigBound()
        {
            if (!Config.enableGravity)
            {
                Debug.LogWarning($"[E_FirstMotor] {name} 的配置没勾 enableGravity：刚体重力已被强制为 0，虫子离地后不会下落。");
            }
        }

        public override void SetBodyScale(float scale) { }

        public override bool CanStandUp(float standHeight, float crouchHeight) => true;

        private void CacheRefs()
        {
            if (feetRef != null && headRef != null) return;

            Transform view = transform.Find(ViewChildName);
            if (view == null) return;

            if (feetRef == null) feetRef = view.Find(BottomChildName);
            if (headRef == null) headRef = view.Find(TopChildName);

            if (feetRef == null && Application.isPlaying)
            {
                Debug.LogWarning($"[E_FirstMotor] 找不到 {ViewChildName}/{BottomChildName}：脚底退回碰撞体底边，贴地与悬崖探针会有偏差。");
            }
        }

        private void CacheBody()
        {
            if (bodyCollider == null)
            {
                Transform body = transform.Find(BodyChildName);
                if (body != null) bodyCollider = body.GetComponent<Collider2D>();
            }

            if (bodyCollider == null)
            {
                Debug.LogError($"[E_FirstMotor] 找不到 {BodyChildName} 子物体上的碰撞体：地面与墙面探测全部失效，虫子会直接往下掉。");
                return;
            }

            Vector2 size = ReadColliderSize(bodyCollider);
            Vector2 offset = ReadColliderOffset(bodyCollider);

            Transform bodyT = bodyCollider.transform;
            float childScaleY = Mathf.Abs(bodyT.lossyScale.y);
            float rootScaleY = Mathf.Abs(transform.lossyScale.y);
            float bottomLocal = offset.y - size.y * 0.5f;

            _feetOffset = bodyT == transform
                ? bottomLocal * rootScaleY
                : bodyT.localPosition.y * rootScaleY + bottomLocal * childScaleY;

            _bodyHeight = feetRef != null && headRef != null
                ? Mathf.Abs(headRef.position.y - feetRef.position.y)
                : size.y * childScaleY;
        }

        private static Vector2 ReadColliderSize(Collider2D col)
        {
            switch (col)
            {
                case CapsuleCollider2D capsule: return capsule.size;
                case BoxCollider2D box: return box.size;
                case CircleCollider2D circle: return Vector2.one * (circle.radius * 2f);
                default: return Vector2.one;
            }
        }

        private static Vector2 ReadColliderOffset(Collider2D col)
        {
            switch (col)
            {
                case CapsuleCollider2D capsule: return capsule.offset;
                case BoxCollider2D box: return box.offset;
                case CircleCollider2D circle: return circle.offset;
                default: return Vector2.zero;
            }
        }

        /// <summary>脚底世界高度：基准取刚体而不是 transform，贴地写的是刚体，Transform 要等物理步结束才跟上</summary>
        private float FeetWorldY() =>
            feetRef != null ? feetRef.position.y : Body.position.y + _feetOffset;

        /// <summary>探地面并贴回：踩地后逻辑层不再施加重力，不贴回就会悬在探针允许的间隙里</summary>
        protected override bool SampleGround()
        {
            if (bodyCollider == null) return false;

            Vector2 center = new(Body.position.x, FeetWorldY() + groundProbe.lift + groundProbe.radius);
            float travel = groundProbe.lift + groundProbe.distance;

            int count = Physics2D.CircleCastNonAlloc(
                center,
                groundProbe.radius,
                Vector2.down,
                _hits,
                travel,
                groundProbe.layerMask);

            float minUp = Mathf.Cos(groundProbe.maxSlopeAngle * Mathf.Deg2Rad);
            float nearest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (Vector2.Dot(hit.normal, Vector2.up) < minUp) continue;
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                found = true;
            }

            if (!found) return false;

            SettleOnGround(nearest - groundProbe.lift);
            return true;
        }

        private void SettleOnGround(float gap)
        {
            if (!snapToGround) return;

            float error = gap - groundSkin;   // 正=悬空要下移，负=陷入要上抬

            if (Mathf.Abs(error) > snapDeadZone)
            {
                Vector2 pos = Body.position;
                pos.y -= error;
                Body.position = pos;
            }

            if (Config.enableGravity && Body.velocity.y != 0f)
            {
                Vector2 velocity = Body.velocity;
                Body.velocity = new Vector2(velocity.x, 0f);
            }
        }

        protected override bool SampleWall(int side)
        {
            if (bodyCollider == null) return false;

            Vector2 origin = (Vector2)Body.position + new Vector2(wallProbe.origin.x * side, wallProbe.origin.y);
            Vector2 dir = new(side, 0f);
            Vector2 size = new(wallProbe.radius * 2f, Mathf.Max(wallProbe.height, wallProbe.radius * 2f));

            int count = Physics2D.CapsuleCastNonAlloc(
                origin,
                size,
                CapsuleDirection2D.Vertical,
                0f,
                dir,
                _hits,
                wallProbe.distance,
                wallProbe.layerMask);

            float minFacing = Mathf.Cos(wallProbe.maxTiltAngle * Mathf.Deg2Rad);

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (Vector2.Dot(hit.normal, -dir) < minFacing) continue;

                return true;
            }

            return false;
        }

        /// <summary>探悬崖：脚底前方这个深度内没有任何朝上的面就算悬崖，平台侧壁不算</summary>
        protected override bool SampleLedge(int side)
        {
            if (bodyCollider == null) return false;

            Vector2 origin = new(
                Body.position.x + ledgeAhead * side,
                FeetWorldY() + groundProbe.lift + ledgeRadius);

            int count = Physics2D.CircleCastNonAlloc(
                origin,
                ledgeRadius,
                Vector2.down,
                _hits,
                groundProbe.lift + ledgeDepth,
                groundProbe.layerMask);

            float minUp = Mathf.Cos(groundProbe.maxSlopeAngle * Mathf.Deg2Rad);

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (Vector2.Dot(hit.normal, Vector2.up) < minUp) continue;

                return false;
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 basePos = Application.isPlaying ? Body.position : (Vector2)transform.position;
            float feetY = feetRef != null ? feetRef.position.y : basePos.y + _feetOffset;

            Vector2 start = new(basePos.x, feetY + groundProbe.lift + groundProbe.radius);
            Vector2 end = start + Vector2.down * (groundProbe.lift + groundProbe.distance);

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            Gizmos.DrawWireSphere(start, groundProbe.radius);
            Gizmos.DrawWireSphere(end, groundProbe.radius);
            Gizmos.DrawLine(start, end);

            float r = wallProbe.radius;
            float h = Mathf.Max(wallProbe.height, r * 2f);
            float halfStraight = (h - r * 2f) * 0.5f;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.95f);

            foreach (int side in new[] { -1, 1 })
            {
                Vector2 origin = basePos + new Vector2(wallProbe.origin.x * side, wallProbe.origin.y);
                Vector2 dir = new(side, 0f);
                Vector2 tip = origin + dir * wallProbe.distance;

                DrawWireCapsule(origin, r, halfStraight);
                DrawWireCapsule(tip, r, halfStraight);
                Gizmos.DrawLine(origin, tip);
            }

            Gizmos.color = new Color(1f, 0.4f, 0.75f, 0.95f);

            foreach (int side in new[] { -1, 1 })
            {
                Vector2 ledgeStart = new(basePos.x + ledgeAhead * side, feetY + groundProbe.lift + ledgeRadius);
                Vector2 ledgeEnd = ledgeStart + Vector2.down * (groundProbe.lift + ledgeDepth);

                Gizmos.DrawWireSphere(ledgeStart, ledgeRadius);
                Gizmos.DrawWireSphere(ledgeEnd, ledgeRadius);
                Gizmos.DrawLine(ledgeStart, ledgeEnd);
            }
        }

        private static void DrawWireCapsule(Vector2 center, float radius, float halfStraight)
        {
            Vector2 top = center + Vector2.up * halfStraight;
            Vector2 bottom = center + Vector2.down * halfStraight;

            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);

            Gizmos.DrawLine(top + Vector2.left * radius, bottom + Vector2.left * radius);
            Gizmos.DrawLine(top + Vector2.right * radius, bottom + Vector2.right * radius);
        }
    }
}
