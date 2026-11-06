using Refraction.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Move
{
    public sealed class MoveLedger : IMoveLedger
    {
        private readonly System.Func<Vector2> _readVelocity;
        private readonly System.Action<Vector2> _writeVelocity;

        private Vector2 _frameStart;
        private Vector2 _delta;
        private Vector2 _accel;
        private float _speedScaleX = 1f;
        private float _now;
        private float _dt = 0.02f;
        private bool _enableGravity;
        private float _curGravity;

        public MoveLedger(System.Func<Vector2> readVelocity, System.Action<Vector2> writeVelocity)
        {
            _readVelocity = readVelocity;
            _writeVelocity = writeVelocity;
        }

        public PlayerConfig Config { get; private set; }

        public float Now => _now;

        public float DeltaTime => _dt;

        /// <remarks>config 折算成 Motion 快照，事后改 SO 不生效</remarks>
        public void Configure(PlayerConfig config)
        {
            Config = config;
            _enableGravity = config.enableGravity;
            _curGravity = config.fallGravity;
        }

        public Vector2 FrameStartVelocity => _frameStart;

        public Vector2 SubmittedDelta => _delta + _accel * _dt;

        public Vector2 Velocity => _frameStart + SubmittedDelta;

        /// <summary>速度乘数，1=不缩放；写口夹取 NaN→1、负数→0、&gt;1→1</summary>
        public float SpeedScaleX
        {
            get => _speedScaleX;
            set
            {
                if (float.IsNaN(value))
                {
                    _speedScaleX = 1f;
                    return;
                }

                _speedScaleX = value < 0f ? 0f : (value > 1f ? 1f : value);
            }
        }

        /// <remarks>时刻与 Δt 由驱动方给出</remarks>
        public void BeginStep(float now, float deltaTime)
        {
            _now = now;
            _dt = deltaTime;
            _frameStart = _readVelocity();
            _delta = Vector2.zero;
            _accel = Vector2.zero;

            // 乘数一次性：上一帧声明的到这一帧开头就失效，门禁必须每帧重新提交，否则"这一帧被推了一下"会变成"从此一直被限速"。
            _speedScaleX = 1f;
        }

        public void Commit()
        {
            // 零提交帧不写速度，没有变更就不覆盖引擎。
            if (!HasSubmission) return;

            _writeVelocity(_frameStart + SubmittedDelta);
        }

        public void AddImpulse(Vector2 deltaVelocity) => _delta += deltaVelocity;

        public void AddForce(Vector2 acceleration) => _accel += acceleration;

        public void SnapVelocity(Vector2 velocity, System.Action<Vector2> facing)
        {
            SetVelocity(velocity);

            if (velocity.x == 0f && velocity.y == 0f) return;

            facing?.Invoke(velocity);
        }

        public void ScaleVelocityY(float multiplier)
        {
            SetVelocityY(Velocity.y * multiplier);
        }

        public void SetVelocity(Vector2 velocity)
        {
            SetVelocityX(velocity.x);
            SetVelocityY(velocity.y);
        }

        public void MoveDirection(Vector2 direction, float speed)
        {
            SetVelocity(direction.normalized * speed);
        }

        public void StopMove()
        {
            SetVelocity(Vector2.zero);
        }

        /// <summary>移动层的"走"：有惯性按加速度逼近，零惯性当帧直达，只修改x轴速度；方向可未归一化，零向量表示没有期望方向</summary>
        public void MoveTowardsX(Vector2 direction, float speed)
        {
            float target = direction.x == 0f ? 0f
                : Mathf.Sign(direction.x) * speed * _speedScaleX;

            if (Config.moveAcceleration <= 0f)
            {
                SetVelocityX(target);
                return;
            }

            ApproachX(target);
        }

        public void BrakeTowardsX()
        {
            if (Config.moveAcceleration <= 0f)
            {
                SetVelocityX(0f);
                return;
            }

            ApproachX(0f);
        }

        /// <summary>
        /// 向目标水平速度渐进逼近。
        /// </summary>
        private void ApproachX(float target)
        {
            float current = Velocity.x;

            float acceleration;

            if (Mathf.Approximately(target, 0f))
            {
                // 松开方向键：减速
                acceleration = Config.moveDeceleration;
            }
            else if (current * target < 0f)
            {
                // 反向输入：转向
                acceleration = Config.turnAcceleration;
            }
            else
            {
                // 起步或同方向加速
                acceleration = Config.moveAcceleration;
            }

            float maxStep = acceleration * _dt;
            float delta = Mathf.Clamp(
                target - current,
                -maxStep,
                maxStep
            );

            _delta.x += delta;
        }

        public void SetVelocityX(float vx)
        {
            _accel.x = 0f;
            _delta.x = vx - _frameStart.x;
        }

        /// <summary>只改竖直速度，横向不动；起跳给一次向上速度用这个，避免把横移抹掉</summary>
        public void SetVelocityY(float vy)
        {
            _accel.y = 0f;
            _delta.y = vy - _frameStart.y;
        }

        private bool HasSubmission => _delta.x != 0f || _delta.y != 0f || _accel.x != 0f || _accel.y != 0f;


        public void SetGravityEnable(bool enable)
        {
            // 配置禁用重力后，运行时也无法重新启用
            if (!Config.enableGravity) return;
            _enableGravity = enable;
        }

        public void ApplyGravity(in LogicContext ctx)
        {
            if (!_enableGravity) return;

            // 站在地面且没有向上运动时，不再施加重力
            if (ctx.WorldInfo.Grounded && Velocity.y <= 0f)
            {
                SetVelocityY(0f);
                return;
            }

            _accel.y -= _curGravity;

            if (Velocity.y > Config.maxRiseSpeed) SetVelocityY(Config.maxRiseSpeed);
            else if (Velocity.y < -Config.maxFallSpeed) SetVelocityY(-Config.maxFallSpeed);
        }

        public void SetGravity(float gravity)
        {
            _curGravity = gravity;
        }
    }
}
