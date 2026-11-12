using UnityEngine;

namespace Refraction.Presentation
{
    /// <summary>敌人移动执行器：地面与墙面探测、贴地吸附都在这一层，逻辑层只收 WorldInfo</summary>
    /// <remarks>身体碰撞体按名字找 Enemy/BodyCollider，不在 Inspector 里连线；脚下那圈自检必须排掉自身碰撞体，否则永远判成踩地</remarks>
    public sealed class EnemyMotor : MoveMotor
    {
        private const string BodyChildName = "BodyCollider";
        private const string ViewChildName = "View";
        private const string BottomChildName = "Bottom";
        private const string TopChildName = "Top";

        [SerializeField]
        private Collider2D bodyCollider;

        [Header("身体参考点")]
        [Tooltip("脚底参考点（View 下的 Bottom）：地面探测、贴地、悬崖探针都以它为基准。\n留空按名字自动找，再找不到退回用碰撞体底边。")]
        [SerializeField]
        private Transform feetRef;

        [Tooltip("头顶参考点（View 下的 Top）：站立身高由它与脚底参考点量出。\n留空按名字自动找，再找不到退回用碰撞体高度。")]
        [SerializeField]
        private Transform headRef;

        [Header("贴地吸附")]
        [Tooltip("踩地时把刚体放回地面。关掉就退回纯物理落地，脚下留几厘米空隙仍算踩地，逻辑层不再施加重力，敌人会悬着。")]
        [SerializeField]
        private bool snapToGround = true;

        [Tooltip("吸附后脚底与地面之间保留的间隙（米）。0.005 与 Box2D 的 linear slop 相同，正好是物理引擎落地后的静止间隙。")]
        [Min(0f)]
        [SerializeField]
        private float groundSkin = 0.005f;

        [Tooltip("贴地纠偏的死区（米）：偏差没超过它就不动，否则求解器每帧留下的误差都会触发一次瞬移，身体原地微抖。")]
        [Min(0f)]
        [SerializeField]
        private float snapDeadZone = 0.012f;

        [Header("地面检测 · 圆形射线")]
        [SerializeField]
        private GroundProbeSettings groundProbe = new();

        [Header("墙面检测 · 胶囊射线")]
        [Tooltip("origin.x 从身体中心往侧面量：敌人身体是半径 1 的圆，探针要贴着边缘才不会在离墙半米时就掉头。")]
        [SerializeField]
        private WallProbeSettings wallProbe = new()
        {
            origin = new Vector2(0.9f, 0f),
            radius = 0.25f,
            height = 1f
        };

        [Header("悬崖检测 · 向下探针")]
        [Tooltip("探针在脚底前方多远（单位），随朝向镜像：要贴着身体前沿，太靠内得半个身子悬空才响。")]
        [Min(0f)]
        [SerializeField]
        private float ledgeAhead = 0.9f;

        [Tooltip("脚底以下探多深（米）：这个深度内都探不到朝上的面就算悬崖。\n比一帧的位移大得多才不会冲出去，也别大到把下一层平台当成地面。")]
        [Min(0f)]
        [SerializeField]
        private float ledgeDepth = 0.6f;

        [Tooltip("探针圆半径：比脚底探圆小，免得把平台侧壁当成地面。")]
        [Min(0.01f)]
        [SerializeField]
        private float ledgeRadius = 0.12f;

        private float _feetOffset;
        private float _bodyHeight;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

        /// <summary>站立高度（有 Top 与 Bottom 就量参考点，否则用碰撞体高度）</summary>
        public override float BodyHeight => _bodyHeight;

        /// <summary>脚底到枢轴的高度差（正常为负）</summary>
        public override float FeetOffsetY =>
            feetRef != null ? feetRef.position.y - transform.position.y : _feetOffset;

        protected override void Awake()
        {
            base.Awake();

            // 探测盒挂在身体下面，身体一转盒子跟着歪；物理帧位置由账本驱动，插值交给刚体
            Body.freezeRotation = true;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Stay 靠物理回调，双方睡着就断
            Body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            CacheRefs();
            CacheBody();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Inspector 改值时同步刷新，保证 gizmo 立刻反映 Bottom/Top 与碰撞体改动
            CacheRefs();
            CacheBodySilent();
        }
#endif

        /// <summary>敌人不蹲，身高固定：这里留空实现，换签名时由表现层自己吸收</summary>
        public override void SetBodyScale(float scale) { }

        public override bool CanStandUp(float standHeight, float crouchHeight) => true;

        /// <summary>没连线参考点才按名字找 View/Bottom 与 View/Top；改层级要连 EnemyController 一起改，两边找的是同一批名字</summary>
        private void CacheRefs()
        {
            if (feetRef != null && headRef != null) return;

            Transform view = transform.Find(ViewChildName);
            if (view == null) return;

            if (feetRef == null) feetRef = view.Find(BottomChildName);
            if (headRef == null) headRef = view.Find(TopChildName);

#if UNITY_EDITOR
            // 编辑态不刷警告（OnValidate/OnDrawGizmosSelected 会反复触发）
            if (!Application.isPlaying) return;
#endif

            if (feetRef == null)
            {
                Debug.LogWarning($"[EnemyMotor] 找不到 {ViewChildName}/{BottomChildName}：脚底退回碰撞体底边，贴地与悬崖探针会有偏差。");
            }
        }

        private void CacheBody()
        {
            if (!CacheBodyCore())
            {
                Debug.LogError($"[EnemyMotor] 找不到 {BodyChildName} 子物体上的碰撞体：地面与墙面探测全部失效，敌人会直接往下掉。");
            }
        }

        /// <summary>静默版：编辑态 gizmo 用，不刷错误日志</summary>
        private void CacheBodySilent() => CacheBodyCore();

        /// <summary>算脚底偏移与站立高度；找不到碰撞体返回 false</summary>
        private bool CacheBodyCore()
        {
            if (bodyCollider == null)
            {
                Transform body = transform.Find(BodyChildName);
                if (body != null) bodyCollider = body.GetComponent<Collider2D>();
            }

            if (bodyCollider == null) return false;

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

            return true;
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

        /// <summary>gizmo 与脚底高度用：运行态且刚体已初始化才取刚体位置，否则退回 transform</summary>
        private Vector2 SafeBodyPosition()
        {
            if (Application.isPlaying && Body != null)
                return Body.position;
            return transform.position;
        }

        /// <summary>脚底世界高度：运行/编辑通用，优先参考点，其次碰撞体底边</summary>
        private float ResolveFeetWorldY()
        {
            if (feetRef != null) return feetRef.position.y;
            return SafeBodyPosition().y + _feetOffset;
        }

        /// <summary>运行态脚底世界高度：基准取刚体而不是 transform，贴地写的是刚体，Transform 要等物理步结束才跟上</summary>
        private float FeetWorldY() => ResolveFeetWorldY();

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

        /// <summary>贴回地面并清零 vy：偏差在死区内跳过，只改位置与竖直速度，横向不动</summary>
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

        /// <summary>探墙：side 是朝向（-1 左 / 1 右），探针与扫描方向都随它镜像</summary>
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
        /// <remarks>探针要贴着身体前沿（ledgeAhead ≈ 半径），太靠内会等半个身子悬空才响，人就掉下去了</remarks>
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
            // 编辑态兜底：Awake 未跑时参考点/碰撞体可能还没缓存
            if (!Application.isPlaying)
            {
                CacheRefs();
                CacheBodySilent();
            }

            float feetY = ResolveFeetWorldY();
            Vector2 basePos = SafeBodyPosition();

            Vector2 start = new(basePos.x, feetY + groundProbe.lift + groundProbe.radius);
            Vector2 end = start + Vector2.down * (groundProbe.lift + groundProbe.distance);

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            Gizmos.DrawWireSphere(start, groundProbe.radius);
            Gizmos.DrawWireSphere(end, groundProbe.radius);
            Gizmos.DrawLine(start, end);

            float r = wallProbe.radius;
            float h = Mathf.Max(wallProbe.height, r * 2f);
            float halfStraight = (h - r * 2f) * 0.5f;

            foreach (int side in new[] { -1, 1 })
            {
                Vector2 origin = basePos + new Vector2(wallProbe.origin.x * side, wallProbe.origin.y);
                Vector2 dir = new(side, 0f);
                Vector2 tip = origin + dir * wallProbe.distance;

                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.95f);
                DrawWireCapsule(origin, r, halfStraight);
                DrawWireCapsule(tip, r, halfStraight);
                Gizmos.DrawLine(origin, tip);
            }

            Gizmos.color = new Color(1f, 0.4f, 0.75f, 0.95f);

            foreach (int side in new[] { -1, 1 })
            {
                Vector2 ledgeStart = new(
                    basePos.x + ledgeAhead * side,
                    feetY + groundProbe.lift + ledgeRadius);
                Vector2 ledgeEnd = ledgeStart + Vector2.down * (groundProbe.lift + ledgeDepth);

                Gizmos.DrawWireSphere(ledgeStart, ledgeRadius);
                Gizmos.DrawWireSphere(ledgeEnd, ledgeRadius);
                Gizmos.DrawLine(ledgeStart, ledgeEnd);
            }
        }

        /// <summary>在 XZ 平面画竖直胶囊线框：上下两个圆 + 左右两条竖线</summary>
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