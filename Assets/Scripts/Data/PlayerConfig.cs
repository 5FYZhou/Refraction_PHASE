using UnityEngine;

namespace Refraction.Data
{
    /// <summary>
    /// 玩家角色的全部可调参数。逻辑层只读取、不修改，改这里即可调手感。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Refraction/Settings/Player")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("移动")]
        [Tooltip("水平移动速度")]
        [Min(0f)]
        public float speed = 10f;

        [Header("冲刺")]
        [Tooltip("冲刺时叠加到当前速度上的冲量")]
        [Min(0f)]
        public float dashSpeed;

        [Tooltip("冲刺状态的持续时间")]
        [Min(0f)]
        public float dashDuration;

        [Tooltip("两次冲刺之间的冷却时间\n设成 0 表示没有冷却，可以连续冲刺。")]
        [Min(0f)]
        public float dashCooldown;

        [Header("输入缓冲")]
        [Tooltip("冲刺按键的缓冲窗口，单位 秒。\n在这段时间内按下的冲刺会被记住：即使当时正在冷却中，冷却一结束就自动补触发，不用玩家重新按。\n建议 0.1 ~ 0.15。设成 0 表示不缓冲，必须精确按在可用的那一帧。")]
        [Min(0f)]
        public float dashBufferTime;

        [Tooltip("输入缓冲区的总回看窗口，单位 秒，决定最多往回记多久的按键。")]
        [Min(0f)]
        public float inputBufferTime;
    }
}
