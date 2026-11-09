using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Move
{
    /// <summary>状态只拿窄接口</summary>
    public interface IStateMove
    {
        void MoveTowardsX(Vector2 direction, float speed);
        void BrakeTowardsX();
        void AddImpulse(Vector2 deltaVelocity);

        /// <summary>叠加外部速度：不覆盖常规速度，逐帧衰减</summary>
        void AddExternalVelocity(Vector2 velocity);

        void SetVelocityX(float speed);
        void SetVelocityY(float speed);
        void SnapVelocity(Vector2 velocity);
        void SetSpeedScaleX(float scale);
        void ScaleVelocityY(float multiplier);

        void SetGravityEnable(bool enable);
        void SetGravity(float gravity);

        /// <summary>身体高度倍率（1=站立），脚底不动</summary>
        void SetBodyScale(float scale);

        void CommitGates(in MoveGates gates);
    }

    public interface IMoveMotor : IStateMove
    {
        /// <summary>站立碰撞体高度（不含运行时压缩）</summary>
        float BodyHeight { get; }

        float FeetOffsetY { get; }

        void BeginStep(float now, float deltaTime);
        void Commit();
        void FaceTowards(Vector2 dir);
        void ApplyGravity(in LogicContext ctx);

        /// <summary>瞬移并直写速度</summary>
        void SpawnAt(Vector2 position, Vector2 velocity);

        /// <summary>头顶空间是否够从蹲姿站起</summary>
        bool CanStandUp(float standHeight, float crouchHeight);

        /// <summary>直接覆盖速度</summary>
        void ApplyGates();
    }

    public interface IMoveLedger
    {
        Vector2 Velocity { get; }

        Vector2 SubmittedDelta { get; }

        Vector2 FrameStartVelocity { get; }

        /// <summary>速度乘数，1=不缩放；帧首复位</summary>
        float SpeedScaleX { get; set; }

        /// <summary>帧首：读真值、清空累加区、复位速度乘数</summary>
        void BeginStep(float now, float deltaTime);

        /// <summary>帧末：有提交才写出一次</summary>
        void Commit();

        /// <summary>一次性速度变化，不乘 Δt</summary>
        void AddImpulse(Vector2 deltaVelocity);

        /// <summary>持续力：单位/秒²，本帧贡献 = 该值 × Δt</summary>
        void AddForce(Vector2 acceleration);

        void AddExternalVelocity(Vector2 velocity);

        void SetVelocity(Vector2 velocity);
    }
}
