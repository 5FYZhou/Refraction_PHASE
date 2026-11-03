using Refraction.Data;
using Refraction.Logic;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Presentation
{
    /// <summary>
    /// 地面探测参数：圆形射线（CircleCast）从脚下向下扫。
    /// 属于表现层的物理查询参数，逻辑层不感知；改这里即可调手感。
    /// </summary>
    [System.Serializable]
    public sealed class GroundProbeSettings
    {
        [Tooltip("探测圆的参考点，相对角色根节点的偏移。\n圆的中心会自动抬高一个半径，所以这里填脚底位置即可（1×1 角色填 y = -0.5）。")]
        public Vector2 origin = new(0f, -0.5f);

        [Tooltip("探圆半径，实际检测宽度 = 半径 × 2。")]
        [Min(0.01f)]
        public float radius = 0.4f;

        [Tooltip("从脚底继续向下探查的距离。")]
        [Min(0f)]
        public float distance = 0.08f;

        [Tooltip("能站立的最大坡度。倾角超过这个值的面不算地面（斜坡可以，竖直墙面不行）。")]
        [Range(0f, 89f)]
        public float maxSlopeAngle = 50f;

        [Tooltip("地面所在的层。")]
        public LayerMask layerMask = ~0;
    }

    /// <summary>
    /// 墙面探测参数：胶囊射线（CapsuleCast）朝角色面向方向扫。
    /// 方向与探测区域都会随角色朝向左右镜像。
    /// </summary>
    [System.Serializable]
    public sealed class WallProbeSettings
    {
        [Tooltip("探测胶囊的中心，相对角色根节点的偏移。\nx 会随角色朝向镜像，y 不镜像。")]
        public Vector2 origin = Vector2.zero;

        [Tooltip("胶囊半径，实际厚度 = 半径 × 2。")]
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

    [RequireComponent(typeof(Rigidbody2D))]
    public class MoveMotor : MonoBehaviour, IMoveMotor
    {
        [SerializeField]
        private PlayerConfig config;

        [Header("地面检测 · 圆形射线")]
        [SerializeField]
        private GroundProbeSettings groundProbe = new();

        [Header("墙面检测 · 胶囊射线")]
        [SerializeField]
        private WallProbeSettings wallProbe = new();

        private Rigidbody2D _rb;
        private MoveLedger _ledger;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private Collider2D[] _selfColliders = new Collider2D[0];

        private bool _grounded;
        private bool _touchingWall;
        private int _wallSide = 1;
        private RaycastHit2D _groundHit;
        private RaycastHit2D _wallHit;

        public Rigidbody2D Rb { get => _rb; }

        public float Speed => config.speed;

        /// <summary>最近一次探测：是否站在地面。</summary>
        public bool Grounded => _grounded;

        /// <summary>最近一次探测：是否贴墙。</summary>
        public bool TouchingWall => _touchingWall;

        /// <summary>最近一次探测：墙所在方向，-1 左，1 右。</summary>
        public int WallSide => _wallSide;

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
            _rb = GetComponent<Rigidbody2D>();
            _selfColliders = GetComponentsInChildren<Collider2D>(true);
            _ledger = new MoveLedger(
                readVelocity: () => _rb.velocity,
                writeVelocity: v => _rb.velocity = v);
            _ledger.Configure(config);
        }

        /// <summary>
        /// 采样一次世界：先圆形射线探地面，再沿当前朝向做胶囊射线探墙。
        /// 结果打包成逻辑层的 <see cref="WorldInfo"/>，由表现层（PlayerController）放进 LogicContext。
        /// </summary>
        public WorldInfo SampleWorld()
        {
            _grounded = ProbeGround();

            int side = FacingSign;
            _touchingWall = ProbeWall(side, out _wallHit);
            _wallSide = side;

            return new WorldInfo(_grounded, _touchingWall, _wallSide, _ledger.Velocity, FacingSign);
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

        public void MoveTowards(Vector2 dir, float speed)
        {
            FaceTowards(dir);
            _ledger.MoveTowards(dir, speed);
        }

        public void AddImpulse(Vector2 deltaVelocity)
        {
            _ledger.AddImpulse(deltaVelocity);
        }

        public void StopMove()
        {
            _ledger.StopMove();
        }

        public void SetVelocityX(float speed) => _ledger.SetVelocityX(speed);

        public void SetVelocityY(float velocityY) => _ledger.SetVelocityY(velocityY);

        public void Commit()
        {
            _ledger.Commit();
        }

        public void ApplyGravity(in LogicContext ctx, bool jumpReleased) => _ledger.ApplyGravity(ctx, jumpReleased);

        public void SnapVelocity(Vector2 velocity) => _ledger.SnapVelocity(velocity, SetFace);

        public void SetGravity(bool enable) => _ledger.SetGravity(enable);

        private void SetFace(Vector2 face)
        {
            Face = face;
        }

        #region 物理检测
        private bool ProbeGround()
        {
            Vector2 feet = (Vector2)transform.position + groundProbe.origin;
            Vector2 center = feet + Vector2.up * groundProbe.radius;

            int count = Physics2D.CircleCastNonAlloc(
                center,
                groundProbe.radius,
                Vector2.down,
                _hits,
                groundProbe.distance,
                groundProbe.layerMask);

            float minUp = Mathf.Cos(groundProbe.maxSlopeAngle * Mathf.Deg2Rad);
            float nearest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider == null || IsSelf(hit.collider)) continue;
                // 只认法线足够朝上的面，把墙和陡坡排除掉
                if (Vector2.Dot(hit.normal, Vector2.up) < minUp) continue;
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                _groundHit = hit;
                found = true;
            }

            return found;
        }

        private bool ProbeWall(int side, out RaycastHit2D result)
        {
            Vector2 origin = (Vector2)transform.position
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
                if (hit.collider == null || IsSelf(hit.collider)) continue;
                // 面必须基本正对角色，避免把地面/缓坡当成墙
                if (Vector2.Dot(hit.normal, -dir) < minFacing) continue;
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                result = hit;
            }

            return result.collider != null;
        }

        private bool IsSelf(Collider2D col)
        {
            for (int i = 0; i < _selfColliders.Length; i++)
            {
                if (_selfColliders[i] == col) return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            bool playing = Application.isPlaying;

            // 地面：起点圆 + 终点圆 + 连接线 + 中心射线
            Vector2 feet = (Vector2)transform.position + groundProbe.origin;
            Vector2 gStart = feet + Vector2.up * groundProbe.radius;
            Vector2 gEnd = gStart + Vector2.down * groundProbe.distance;

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

            // 墙面：起点胶囊 + 终点胶囊 + 连接线 + 方向箭头
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
