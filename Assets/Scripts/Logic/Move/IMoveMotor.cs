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
        void SetSpeedScaleX(float scale);
        void ScaleVelocityY(float multiplier);

        void SetGravityEnable(bool enable);
        void SetGravity(float gravity);
    }

    public interface IMoveMotor : IStateMove
    {
        /// <summary>站立姿态下的碰撞体高度（世界单位，不含运行时压缩）。头顶探测用它在蹲高与站高之间留空间。</summary>
        float BodyHeight { get; }

        float FeetOffsetY { get; }

        void BeginStep(float now, float deltaTime);
        void Commit();
        void FaceTowards(Vector2 dir);
        void ApplyGravity(in LogicContext ctx);

        /// <summary>瞬移并直写速度</summary>
        void SpawnAt(Vector2 position, Vector2 velocity);

        /// <summary>身体高度缩到 scale 倍（1 = 站立）：碰撞体与墙探针的竖直尺寸一起缩放，脚底位置不动</summary>
        void SetBodyScale(float scale);

        /// <summary>头顶是否有足够空间从 crouchHeight 站起来到 standHeight</summary>
        bool CanStandUp(float standHeight, float crouchHeight);
    }

    public interface IMoveLedger
    {
        Vector2 Velocity { get; }

        Vector2 SubmittedDelta { get; }

        Vector2 FrameStartVelocity { get; }

        /// <summary>速度乘数，1=不缩放；帧首复位</summary>
        /// <remarks>写口夹取 NaN→1、负数→0、&gt;1→1</remarks>
        float SpeedScaleX { get; set; }

        /// <summary>帧首：读真值、清空累加区、复位速度乘数</summary>
        void BeginStep(float now, float deltaTime);

        /// <summary>帧末：有提交才写出一次</summary>
        void Commit();

        /// <summary>一次性速度变化，不乘 Δt</summary>
        void AddImpulse(Vector2 deltaVelocity);

        /// <summary>持续力：单位/秒²，本帧贡献 = 该值 × Δt</summary>
        void AddForce(Vector2 acceleration);

        void SetVelocity(Vector2 velocity);
    }
}
