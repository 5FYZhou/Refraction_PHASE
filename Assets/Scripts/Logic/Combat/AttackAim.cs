using Refraction.Data;
using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>弹道解析：水平分量取朝向，竖直输入决定是否斜 45°，后坐力沿弹道反向</summary>
    public static class AttackAim
    {
        /// <summary>45° 的 cos 与 sin：写死避免每次开火开方</summary>
        private const float Diagonal = 0.70710678f;

        /// <summary>竖直输入超过这个值才算斜射，吸收摇杆漂移</summary>
        private const float VerticalThreshold = 0.5f;

        /// <summary>face 只取 -1 或 1；vertical 为上正下负的原始输入</summary>
        public static Vector2 Direction(int face, float vertical)
        {
            float x = face >= 0 ? 1f : -1f;

            if (vertical > VerticalThreshold) return new Vector2(x * Diagonal, Diagonal);
            if (vertical < -VerticalThreshold) return new Vector2(x * Diagonal, -Diagonal);

            return new Vector2(x, 0f);
        }

        /// <summary>后坐力：沿弹道反向，水平 / 斜上 / 斜下各读一份配置</summary>
        public static Vector2 RecoilSpeed(Vector2 direction, PlayerConfig config)
        {
            float speed = direction.y > 0f ? config.recoilSpeedUp
                : direction.y < 0f ? config.recoilSpeedDown
                : config.recoilSpeedX;

            return -direction * speed;
        }
    }
}
