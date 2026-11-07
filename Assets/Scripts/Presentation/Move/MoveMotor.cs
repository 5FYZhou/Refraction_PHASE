using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Presentation
{
    #region 物理检测参数定义
    /// <summary>地面探测参数：圆形射线从脚下向下扫，只归表现层，逻辑层不感知</summary>
    [System.Serializable]
    public sealed class GroundProbeSettings
    {
        [Tooltip("探测圆最低点相对脚底（View/Bottom）的高度。\n留一点余量起扫：脚陷进地面几厘米时，探针不会从地面内部开始扫（那样会扫出空结果，人被判成离地）。")]
        [Min(0f)]
        public float lift = 0.06f;

        [Tooltip("探圆半径")]
        [Min(0.01f)]
        public float radius = 0.42f;

        [Tooltip("脚底以下多深以内还算踩地：探针最远扫到脚底下方这个距离。\n同时也是贴地吸附允许一帧拉回的最大悬空量。")]
        [Min(0f)]
        public float distance = 0.08f;

        [Tooltip("能站立的最大坡度。倾角超过这个值的面不算地面（斜坡可以，竖直墙面不行）。")]
        [Range(0f, 89f)]
        public float maxSlopeAngle = 50f;

        [Tooltip("地面所在的层。")]
        public LayerMask layerMask = ~0;
    }

    /// <summary>墙面探测参数：胶囊射线沿朝向扫，origin.x 与扫描方向都随朝向镜像，少镜像一边就只剩单向探墙</summary>
    [System.Serializable]
    public sealed class WallProbeSettings
    {
        [Tooltip("探测胶囊的中心，相对角色根节点的偏移。\nx 随角色朝向镜像")]
        public Vector2 origin = Vector2.zero;

        [Tooltip("胶囊半径")]
        [Min(0.01f)]
        public float radius = 0.28f;

        [Tooltip("胶囊总高。建议小于角色身高，否则下滑时会刮到地面误判成墙。")]
        [Min(0.02f)]
        public float height = 0.8f;

        [Tooltip("沿角色面向方向探查的距离。")]
        [Min(0f)]
        public float distance = 0.06f;

        [Tooltip("墙面法线与朝向方向允许的最大夹角。\n超过这个角度的面（斜坡、地面）不算墙。")]
        [Range(0f, 89f)]
        public float maxTiltAngle = 30f;

        [Tooltip("墙所在的层。")]
        public LayerMask layerMask = ~0;
    }

    /// <summary>头顶探测参数：圆形射线从蹲姿头顶扫到站姿头顶，用来判断松开下蹲后能不能站起</summary>
    [System.Serializable]
    public sealed class HeadProbeSettings
    {
        [Tooltip("探测圆的半径。取接近身体半宽的数值，太小会漏掉窄梁，太大会在门框边上误判。")]
        [Min(0.01f)]
        public float radius = 0.42f;

        [Tooltip("探测圆的偏移，相对蹲姿头顶（圆心已经自动抬了一个 radius，所以 0 = 圆底正好贴蹲姿头顶）。\nx 随角色朝向镜像，y 向上为正；填负数等于把起扫点往下挪，会让上端漏检。")]
        public Vector2 offset = Vector2.zero;

        [Tooltip("阻挡站立的层。玩家自己那层可以放进来，自身的碰撞体会被代码排除。")]
        public LayerMask layerMask = ~0;
    }
    #endregion

    /// <summary>表现层的移动执行器：只做物理查询与写刚体，移动规则和帧序见 Docs/说明/玩家状态机.md</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MoveMotor : MonoBehaviour, IMoveMotor
    {
        [Tooltip("玩家参数资产。移动速度、重力、冲刺等数值都从这里读，运行时只读不改。")]
        [SerializeField]
        private PlayerConfig config;

        [SerializeField]
        private Collider2D bodyCollider;

        [Header("贴地吸附")]
        [Tooltip("踩地时把刚体放回地面上。\n关掉就退回纯物理落地：脚下离地几厘米也会被判成踩地，而逻辑层踩地后不再施加重力，角色会一直悬在那一高度。")]
        [SerializeField]
        private bool snapToGround = true;

        [Tooltip("吸附后脚底与地面之间保留的间隙（米）。\n0.005 与 Box2D 的 linear slop 相同，正好是物理引擎自己落地后的静止间隙。")]
        [Min(0f)]
        [SerializeField]
        private float groundSkin = 0.005f;

        [Tooltip("贴地纠偏的死区（米）：脚底与目标的偏差没超过它就不动。\n没有死区时，物理求解器每帧留下的那点误差都会触发一次瞬移，身体就会原地微抖。")]
        [Min(0f)]
        [SerializeField]
        private float snapDeadZone = 0.012f;

        [Header("身体参考点")]
        [Tooltip("脚底参考点（View 下的 Bottom）。地面探测、贴地、蹲起都以它为基准。\n留空则按名字在 View 下自动找，再找不到退回用碰撞体底边。")]
        [SerializeField]
        private Transform feetRef;

        [Tooltip("头顶参考点（View 下的 Top）。站立身高由它与脚底参考点量出，头顶探测从蹲姿头顶往上扫。\n留空则按名字自动找，再找不到退回用碰撞体高度。")]
        [SerializeField]
        private Transform headRef;

        [Header("地面检测 · 圆形射线")]
        [Tooltip("探地面用的圆形射线参数。脚下的圆会向下扫一小段，只看朝上的面。")]
        [SerializeField]
        private GroundProbeSettings groundProbe = new();

        [Header("墙面检测 · 胶囊射线")]
        [Tooltip("探墙用的胶囊射线参数。沿角色朝向扫，只看基本正对角色的面。")]
        [SerializeField]
        private WallProbeSettings wallProbe = new();

        [Header("头顶检测 · 圆形射线")]
        [Tooltip("起身前探头顶用的圆形射线参数。蹲姿头顶到站姿头顶之间只要有阻挡，就站不起来。")]
        [SerializeField]
        private HeadProbeSettings headProbe = new();

        private Rigidbody2D _rb;
        private MoveLedger _ledger;

        private Vector2 _baseColliderSize;
        private Vector2 _baseColliderOffset;
        private float _baseColliderBottom;
        private float _bodyScale = 1f;

        private float _baseBodyHeight;
        private float _baseWallHeight;
        private float _baseWallOriginY;
        private bool _warnedBodyShape;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

        private bool _grounded;
        private bool _touchingWall;
        private int _wallSide = 1;
        private float _groundGap;
        private RaycastHit2D _groundHit;
        private RaycastHit2D _wallHit;

        /// <summary>有顶底参考点就量参考点，没有就量碰撞体</summary>
        public float BodyHeight => _baseBodyHeight;

        /// <summary>脚底到枢轴的高度差</summary>
        public float FeetOffsetY => feetRef != null ? feetRef.position.y - transform.position.y : ColliderBottomOffset();

        private Vector2 _face;
        public Vector2 Face
        {
            get => _face;
            set
            {
                if (value.sqrMagnitude <= 0f) return;

                _face = value;

                if (value.x == 0f) return;

                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (value.x < 0f ? -1f : 1f);
                transform.localScale = scale;
            }
        }

        /// <summary>角色朝向符号：-1 朝左，1 朝右。朝向还没定过时退回到缩放方向。</summary>
        public int FacingSign
        {
            get
            {
                if (Mathf.Abs(_face.x) > Mathf.Epsilon) return _face.x < 0f ? -1 : 1;
                return transform.localScale.x < 0f ? -1 : 1;
            }
        }

        private void Awake()
        {
            Require.Assigned(config, nameof(MoveMotor), nameof(config));
            Require.Assigned(bodyCollider, nameof(Collider2D), nameof(bodyCollider));

            InitRb();

            CacheRefs();
            CacheBodyMetrics();
            _ledger = new MoveLedger(
                readVelocity: () => _rb.velocity,
                writeVelocity: v => _rb.velocity = v);
            _ledger.Configure(config);
        }

        private void InitRb()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f; 
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        /// <summary>采样一次世界：先探地面并贴回，再探墙——贴地会改刚体位置，顺序反了墙探针量到的是没贴地的身体</summary>
        public WorldInfo SampleWorld()
        {
            _grounded = ProbeGround();

            if (_grounded) SettleOnGround();

            int facing = FacingSign;
            _touchingWall = ProbeWall(facing, out _wallHit);
            _wallSide = facing;

            return new WorldInfo(_grounded, _touchingWall, _wallSide, _rb.velocity, facing, _rb.position);
        }

        public void BeginStep(float now, float deltaTime)
        {
            _ledger.BeginStep(now, deltaTime);
        }

        public void FaceTowards(Vector2 dir)
        {
            if (dir.sqrMagnitude <= 0f) return;
            Face = dir;
        }

        public void MoveTowardsX(Vector2 dir, float speed)
        {
            FaceTowards(dir);
            _ledger.MoveTowardsX(dir, speed);
        }

        public void AddImpulse(Vector2 deltaVelocity)
        {
            _ledger.AddImpulse(deltaVelocity);
        }

        public void AddExternalVelocity(Vector2 velocity) => _ledger.AddExternalVelocity(velocity);

        public void BrakeTowardsX()
        {
            _ledger.BrakeTowardsX();
        }

        public void SetVelocityX(float speed) => _ledger.SetVelocityX(speed);

        public void SetVelocityY(float velocityY) => _ledger.SetVelocityY(velocityY);

        public void Commit()
        {
            _ledger.Commit();
        }

        public Vector2 LedgerVelocity => _ledger != null ? _ledger.Velocity : Vector2.zero;

        public void ApplyGravity(in LogicContext ctx) => _ledger.ApplyGravity(ctx);

        public void SnapVelocity(Vector2 velocity) => _ledger.SnapVelocity(velocity, SetFace);

        public void ScaleVelocityY(float multiplier) => _ledger.ScaleVelocityY(multiplier);

        public void SetSpeedScaleX(float scale) => _ledger.SpeedScaleX = scale;

        public void SetGravityEnable(bool enable) => _ledger.SetGravityEnable(enable);

        /// <summary>必须在 ApplyGravity 之前调：ledger 在同一帧读这个值，晚了要等下一帧才生效</summary>
        public void SetGravity(float gravity) => _ledger.SetGravity(gravity);

        private void SetFace(Vector2 face)
        {
            Face = face;
        }

        public void SpawnAt(Vector2 position, Vector2 velocity)
        {
            _rb.position = position;
            _rb.velocity = velocity;
            _ledger.ResetExternal();
        }

        #region 身体尺寸
        /// <summary>身体高度倍率的下限，防止配置写 0 把碰撞体压成一条线。</summary>
        private const float MinBodyScale = 0.05f;

        /// <summary>改碰撞体 size 与 offset 压身高，不压 transform.scale：非等比缩放会让胶囊退化，脚底跟着漂</summary>
        /// <remarks>脚底由 offset 补回、原地不动；竖直胶囊高度不得小于宽度，压到更矮时宽度跟着收；墙探针 height 与 origin.y 同步，否则蹲着还用站立高度探墙</remarks>
        public void SetBodyScale(float scale)
        {
            if (bodyCollider == null) return;

            scale = Mathf.Clamp(scale, MinBodyScale, 1f);

            // 幂等：差值小于 0.0005 视为没变——过渡期每帧都调，改形状等于让物理重算接触，白改会引入小扰动
            if (Mathf.Abs(scale - _bodyScale) < 0.0005f) return;
            _bodyScale = scale;

            Vector2 size = _baseColliderSize;
            size.y = _baseColliderSize.y * scale;

            switch (bodyCollider)
            {
                case CapsuleCollider2D capsule when capsule.direction == CapsuleDirection2D.Vertical:
                    size.x = Mathf.Min(_baseColliderSize.x, size.y);
                    capsule.size = size;
                    capsule.offset = new Vector2(_baseColliderOffset.x, _baseColliderBottom + size.y * 0.5f);
                    break;

                case BoxCollider2D box:
                    box.size = size;
                    box.offset = new Vector2(_baseColliderOffset.x, _baseColliderBottom + size.y * 0.5f);
                    break;

                default:
                    WarnBodyShape();
                    return;
            }

            // 形状绕中点缩，墙探针跟着下移
            float centerDrop = (_baseColliderSize.y - size.y) * 0.5f
                               * Mathf.Abs(bodyCollider.transform.lossyScale.y);
            wallProbe.height = _baseWallHeight * scale;
            wallProbe.origin.y = _baseWallOriginY - centerDrop;
        }

        /// <summary>头顶能否腾出 crouchHeight 到 standHeight 的空间：圆心比蹲姿头顶再抬一个 radius，圆的底边才正好压在头上</summary>
        /// <remarks>触发器（门、区域）与自身碰撞体都不算阻挡：只靠 layerMask 排除，谁把 mask 设成 ~0 就永远站不起来</remarks>
        public bool CanStandUp(float standHeight, float crouchHeight)
        {
            if (bodyCollider == null) return true;

            float radius = headProbe.radius;
            int mirror = FacingSign;

            Vector2 origin = new(
                HeadWorldX() + headProbe.offset.x * mirror,
                FeetWorldY() + crouchHeight + headProbe.offset.y + radius);

            float distance = Mathf.Max(0f, standHeight - crouchHeight);

            int count = Physics2D.CircleCastNonAlloc(
                origin, radius, Vector2.up, _hits, distance, headProbe.layerMask);

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;   // 自己（碰撞体挂在子物体上的情况）
                return false;
            }

            return true;
        }

        /// <summary>没连线参考点才按名字找 View/Bottom 与 View/Top；改层级要连 PlayerController 一起改，那边按同一套名字找</summary>
        private void CacheRefs()
        {
            if (feetRef != null && headRef != null) return;

            Transform view = transform.Find(ViewNode);
            if (view == null) return;

            if (feetRef == null) feetRef = view.Find(BottomNode);
            if (headRef == null) headRef = view.Find(TopNode);
        }

        private const string ViewNode = "View";
        private const string BottomNode = "Bottom";
        private const string TopNode = "Top";

        /// <summary>记下站立时的碰撞体与墙探针尺寸，蹲下只改运行时值，起身靠这些还原</summary>
        private void CacheBodyMetrics()
        {
            if (bodyCollider == null) return;

            _baseColliderSize = ReadColliderSize(bodyCollider);
            _baseColliderOffset = ReadColliderOffset(bodyCollider);
            _baseColliderBottom = _baseColliderOffset.y - _baseColliderSize.y * 0.5f;
            _bodyScale = 1f;

            _baseWallHeight = wallProbe.height;
            _baseWallOriginY = wallProbe.origin.y;

            _baseBodyHeight = feetRef != null && headRef != null
                ? Mathf.Abs(headRef.position.y - feetRef.position.y)
                : (LocalColliderTop() - LocalColliderBottom())
                  * Mathf.Abs(bodyCollider.transform.lossyScale.y);
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

        /// <summary>脚底世界高度：基准取刚体而非 transform——贴地写的是刚体，Transform 要等物理步结束才跟上，混用会拿上一帧身体纠偏</summary>
        private float FeetWorldY()
        {
            if (feetRef != null) return _rb.position.y + (feetRef.position.y - transform.position.y);

            return bodyCollider != null ? _rb.position.y + ColliderBottomOffset() : _rb.position.y;
        }

        /// <summary>头顶参考点的世界横向位置，同样以刚体位置为基准</summary>
        private float HeadWorldX()
        {
            if (headRef == null) return _rb.position.x;

            return _rb.position.x + (headRef.position.x - transform.position.x);
        }

        /// <summary>碰撞体最低点相对根节点的高度（世界单位）：挂在子物体上时要把 localPosition 与子缩放算进来，只乘根缩放会把脚算错、贴地时把人往下拽</summary>
        private float ColliderBottomOffset()
        {
            if (bodyCollider == null) return -1f;

            Transform body = bodyCollider.transform;
            float rootScaleY = Mathf.Abs(transform.lossyScale.y);

            if (body == transform) return LocalColliderBottom() * rootScaleY;

            return body.localPosition.y * rootScaleY
                 + LocalColliderBottom() * Mathf.Abs(body.lossyScale.y);
        }

        private void WarnBodyShape()
        {
            if (_warnedBodyShape) return;

            _warnedBodyShape = true;
            Debug.LogWarning(
                "[MoveMotor] 身体碰撞体只支持竖直胶囊或矩形，当前形状无法改变高度，已忽略本次 SetBodyScale。\n" +
                "请把玩家身上的 Collider2D 换成 CapsuleCollider2D（Direction = Vertical）或 BoxCollider2D。");
        }
        #endregion

        #region 物理检测
        private bool ProbeGround()
        {
            float feetY = FeetWorldY();
            Vector2 center = new(_rb.position.x, feetY + groundProbe.lift + groundProbe.radius);
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
                if (hit.collider == null) continue;
                if (Vector2.Dot(hit.normal, Vector2.up) < minUp) continue;
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                _groundHit = hit;
                found = true;
            }

            if (!found) return false;

            // 脚底到地面的距离：正=悬空，负=陷进地面
            _groundGap = nearest - groundProbe.lift;
            return true;
        }

        /// <summary>踩地后贴回地面并清零 vy：探针把脚下 0.08 内都算踩地，物理静止间隙只有 0.005，不贴就悬在半空且越跳越矮</summary>
        /// <remarks>只改位置与 vy，水平速度不动；偏差在 snapDeadZone 内跳过，否则每帧瞬移会抖</remarks>
        private void SettleOnGround()
        {
            if (!snapToGround) return;

            float error = _groundGap - groundSkin;   // 正=悬空要下移，负=陷入要上抬

            if (Mathf.Abs(error) > snapDeadZone)
            {
                Vector2 pos = _rb.position;
                pos.y -= error;
                _rb.position = pos;
            }

            if (config.enableGravity && _rb.velocity.y != 0f)
            {
                Vector2 velocity = _rb.velocity;
                _rb.velocity = new Vector2(velocity.x, 0f);
            }
        }

        /// <summary>碰撞体最低点相对根节点的局部高度（未乘缩放）；未知形状退回世界包围盒，宁可差一点也别把方向算反</summary>
        private float LocalColliderBottom()
        {
            switch (bodyCollider)
            {
                case CapsuleCollider2D capsule:
                    float half = capsule.size.y * 0.5f;
                    return capsule.offset.y - half;

                case BoxCollider2D box:
                    return box.offset.y - box.size.y * 0.5f;

                case CircleCollider2D circle:
                    return circle.offset.y - circle.radius;

                default:
                    return bodyCollider != null
                        ? bodyCollider.bounds.min.y - transform.position.y
                        : -1f;
            }
        }

        /// <summary>碰撞体最高点相对根节点的局部高度（未乘缩放），改分支时必须与 LocalColliderBottom 同步，否则身高会算错</summary>
        private float LocalColliderTop()
        {
            switch (bodyCollider)
            {
                case CapsuleCollider2D capsule:
                    float half = capsule.size.y * 0.5f;
                    return capsule.offset.y + half;

                case BoxCollider2D box:
                    return box.offset.y + box.size.y * 0.5f;

                case CircleCollider2D circle:
                    return circle.offset.y + circle.radius;

                default:
                    return bodyCollider != null
                        ? bodyCollider.bounds.max.y - transform.position.y
                        : 1f;
            }
        }

        private bool ProbeWall(int side, out RaycastHit2D result)
        {
            Vector2 origin = (Vector2)_rb.position
                             + new Vector2(wallProbe.origin.x * side, wallProbe.origin.y);
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
            float nearest = float.MaxValue;
            result = default;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider == null) continue;
                if (Vector2.Dot(hit.normal, -dir) < minFacing) continue;
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                result = hit;
            }

            return result.collider != null;
        }

        private void OnDrawGizmosSelected()
        {
            bool playing = Application.isPlaying;

            // 非播放时没有刚体位置，退回 transform 与碰撞体底边
            float feetY = playing ? FeetWorldY() : transform.position.y + ColliderBottomOffset();
            Vector2 gStart = new(transform.position.x, feetY + groundProbe.lift + groundProbe.radius);
            Vector2 gEnd = gStart + Vector2.down * (groundProbe.lift + groundProbe.distance);

            Gizmos.color = playing && _grounded
                ? new Color(0.2f, 1f, 0.45f, 0.95f)
                : new Color(1f, 0.85f, 0.2f, 0.95f);

            Gizmos.DrawWireSphere(gStart, groundProbe.radius);
            Gizmos.DrawWireSphere(gEnd, groundProbe.radius);
            Gizmos.DrawLine(gStart, gEnd);
            Gizmos.DrawLine(gStart + Vector2.left * groundProbe.radius, gEnd + Vector2.left * groundProbe.radius);
            Gizmos.DrawLine(gStart + Vector2.right * groundProbe.radius, gEnd + Vector2.right * groundProbe.radius);

            if (playing && _grounded)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.45f, 1f);
                Gizmos.DrawSphere(_groundHit.point, 0.03f);
            }

            int side = playing ? _wallSide : FacingSign;
            Vector2 wOrigin = (Vector2)transform.position
                              + new Vector2(wallProbe.origin.x * side, wallProbe.origin.y);
            Vector2 wDir = new(side, 0f);
            Vector2 wEnd = wOrigin + wDir * wallProbe.distance;

            Gizmos.color = playing && _touchingWall
                ? new Color(1f, 0.45f, 0.2f, 0.95f)
                : new Color(0.2f, 0.75f, 1f, 0.95f);

            DrawCapsuleWire(wOrigin, wallProbe.radius, wallProbe.height);
            DrawCapsuleWire(wEnd, wallProbe.radius, wallProbe.height);
            Gizmos.DrawLine(wOrigin, wEnd);
            Gizmos.DrawLine(
                wOrigin + Vector2.up * (wallProbe.height * 0.5f),
                wEnd + Vector2.up * (wallProbe.height * 0.5f));
            Gizmos.DrawLine(
                wOrigin + Vector2.down * (wallProbe.height * 0.5f),
                wEnd + Vector2.down * (wallProbe.height * 0.5f));

            if (playing && _touchingWall)
            {
                Gizmos.color = new Color(1f, 0.45f, 0.2f, 1f);
                Gizmos.DrawSphere(_wallHit.point, 0.03f);
            }

            // 头顶：圆心已抬了一个 radius，圆底边贴着站立头顶
            if (bodyCollider != null)
            {
                float headY = playing ? feetY + _baseBodyHeight : bodyCollider.bounds.max.y;
                Vector2 head = new(
                    transform.position.x + headProbe.offset.x * FacingSign,
                    headY + headProbe.offset.y + headProbe.radius);

                Gizmos.color = new Color(0.65f, 0.4f, 1f, 0.95f);
                Gizmos.DrawWireSphere(head, headProbe.radius);
            }
        }

        private static void DrawCapsuleWire(Vector3 center, float radius, float height)
        {
            float half = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 up = Vector3.up * half;

            Gizmos.DrawWireSphere(center + up, radius);
            Gizmos.DrawWireSphere(center - up, radius);
            Gizmos.DrawLine(center + up + Vector3.left * radius, center - up + Vector3.left * radius);
            Gizmos.DrawLine(center + up + Vector3.right * radius, center - up + Vector3.right * radius);
        }
        #endregion
    }
}
