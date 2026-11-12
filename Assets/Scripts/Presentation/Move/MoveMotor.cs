using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Presentation
{
    /// <summary>表现层移动执行器基类：只做刚体初始化与速度账本，物理探测与身体尺寸由子类给，移动规则和帧序见 Docs/说明/玩家状态机.md</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class MoveMotor : MonoBehaviour, IMoveMotor
    {
        [Tooltip("参数资产。移动速度、重力、冲刺等数值都从这里读，运行时只读不改。")]
        [SerializeField]
        private CharacterConfig config;

        private Rigidbody2D _rb;
        private MoveLedger _ledger;

        private Vector2 _face;

        private MoveGates _gates;
        private bool _hasGates;

        protected CharacterConfig Config => config;

        /// <summary>已初始化为 Dynamic 且 gravityScale = 0 的刚体：重力由账本统一施加，物理重力再叠一层跳跃高度就失控</summary>
        protected Rigidbody2D Body => _rb;

        /// <summary>站立碰撞体高度（不含运行时压缩）</summary>
        public abstract float BodyHeight { get; }

        /// <summary>脚底到枢轴的高度差</summary>
        public abstract float FeetOffsetY { get; }

        /// <summary>身体高度倍率（1=站立），脚底不动</summary>
        public abstract void SetBodyScale(float scale);

        public virtual Collider2D BodyCollider => null;

        /// <summary>头顶空间是否够从蹲姿站起</summary>
        public abstract bool CanStandUp(float standHeight, float crouchHeight);

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
        public int FaceSign
        {
            get
            {
                if (Mathf.Abs(_face.x) > Mathf.Epsilon) return _face.x < 0f ? -1 : 1;
                return transform.localScale.x < 0f ? -1 : 1;
            }
        }

        protected virtual void Awake()
        {
            Require.Assigned(config, nameof(MoveMotor), nameof(config));

            InitRb();

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
            bool grounded = SampleGround();

            int facing = FaceSign;
            bool touchingWall = SampleWall(facing);
            bool ledgeAhead = SampleLedge(facing);

            return new WorldInfo(grounded, touchingWall, facing, _rb.velocity, facing, _rb.position, ledgeAhead);
        }

        /// <summary>探朝向那一侧的悬崖，默认没有：只有不想走下平台的角色才实现</summary>
        protected virtual bool SampleLedge(int side) => false;

        /// <summary>探地面并返回是否踩地；贴回地面必须在这里做，逻辑层一旦读到踩地就不再施加重力，人会长悬在半空</summary>
        protected abstract bool SampleGround();

        /// <summary>探墙并返回是否贴墙；side 是角色朝向（-1 左 / 1 右），探针与扫描方向都得随它镜像</summary>
        protected abstract bool SampleWall(int side);

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

        public void CommitGates(in MoveGates gates)
        {
            _gates = gates;
            _hasGates = true;
        }

        /// <summary>帧末最后一步：门禁写的是绝对速度，排在 Commit 之后才不会被子类状态与重力覆盖</summary>
        public void ApplyGates()
        {
            if (!_hasGates) return;

            _hasGates = false;
            _ledger.ApplyGates(in _gates);
        }

        public void SpawnAt(Vector2 position, Vector2 velocity)
        {
            _rb.position = position;
            _rb.velocity = velocity;
            _ledger.ResetExternal();
        }
    }
}
