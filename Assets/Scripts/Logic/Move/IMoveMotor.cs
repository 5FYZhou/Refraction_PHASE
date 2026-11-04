using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Move
{
    public interface IStateMove
    {
        void MoveTowardsX(Vector2 direction, float speed);
        void BrakeTowardsX();
        void AddImpulse(Vector2 deltaVelocity);

        void SetVelocityX(float speed);
        void SetVelocityY(float speed);
        void SnapVelocity(Vector2 velocity);
        void ScaleVelocityY(float multiplier);

        void SetGravityEnable(bool enable);
        void SetGravity(float gravity);
    }

    public interface IMoveMotor : IStateMove
    {
        void BeginStep(float now, float deltaTime);
        void Commit();
        void FaceTowards(Vector2 dir);
        void ApplyGravity(in LogicContext ctx);
        void SetPosition(Vector2 position);
    }

    public interface IMoveLedger
    {
        Vector2 Velocity { get; }

        Vector2 SubmittedDelta { get; }

        Vector2 FrameStartVelocity { get; }

        /// <summary>速度乘数，1=不缩放；帧首复位</summary>
        /// <remarks>写口夹取 NaN→1、负数→0、&gt;1→1</remarks>
        float SpeedScale { get; set; }

        /// <summary>帧首：读真值、清空累加区、复位速度乘数</summary>
        void BeginStep(float now, float deltaTime);

        /// <summary>帧末：有提交才写出一次</summary>
        void Commit();

        /// <summary>累加一次冲量：一次性速度变化，不乘 Δt</summary>
        void AddImpulse(Vector2 deltaVelocity);

        /// <summary>累加一次持续力：单位/秒²，本帧贡献 = 该值 × Δt</summary>
        void AddForce(Vector2 acceleration);

        void SetVelocity(Vector2 velocity);
    }
}
