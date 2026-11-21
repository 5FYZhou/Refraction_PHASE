using System;
using UnityEngine;

namespace Refraction.Data
{
    /// <summary>音效编号：AudioConfig 靠它查 Resources/Sound 下的文件名</summary>
    public enum AudioId
    {
        None = 0,

        /// <summary>冲刺（黑突进也用它）</summary>
        Dash,

        /// <summary>玩家命中敌人身体</summary>
        HitBody,

        /// <summary>命中硬壳</summary>
        HitShell,

        /// <summary>挥剑</summary>
        SwordSwing,

        /// <summary>剑气（光波）</summary>
        SwordWave,

        /// <summary>移动循环音</summary>
        Move,

        /// <summary>落地</summary>
        Land,

        /// <summary>跳跃（含二段跳）</summary>
        Jump,

        /// <summary>场景 BGM 1</summary>
        BgmScene1,

        /// <summary>场景 BGM 2</summary>
        BgmScene2,

        /// <summary>场景 BGM 3</summary>
        BgmScene3,

        /// <summary>Boss BGM 1</summary>
        BgmBoss1,

        /// <summary>Boss BGM 2</summary>
        BgmBoss2,

        /// <summary>Boss BGM 3</summary>
        BgmBoss3,
    }

    /// <summary>音频配置：枚举到文件名的唯一映射，外加音量与 BGM 淡变时长</summary>
    /// <remarks>
    /// 资产放在 Resources/Data/AudioConfig.asset。缺资产时退回脚本内的默认表并报警告，
    /// 声音仍然能响，只是改配置得回到代码里。文件名不带扩展名，相对 Resources/Sound。
    /// </remarks>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Refraction/Settings/Audio")]
    public sealed class AudioConfig : ScriptableObject
    {
        private const string ResourcePath = "Data/AudioConfig";

        private static AudioConfig _default;

        [Serializable]
        public struct Entry
        {
            [Tooltip("音效编号")]
            public AudioId id;

            [Tooltip("Resources/Sound 下的文件名，不带扩展名")]
            public string fileName;
        }

        [Tooltip("枚举到文件名的映射：同一编号必须唯一，文件名留空表示这一条不生效。")]
        [SerializeField]
        private Entry[] entries = DefaultEntries();

        [Tooltip("背景音乐音量，0~1")]
        [Range(0f, 1f)]
        public float bgmVolume = 0.6f;

        [Tooltip("音效音量，0~1")]
        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        [Tooltip("BGM 切换的淡入淡出时长（秒），0 = 瞬时切换")]
        [Min(0f)]
        public float bgmFadeDuration = 0.3f;

        public Entry[] Entries => entries ?? Array.Empty<Entry>();

        public static AudioConfig Default
        {
            get
            {
                if (_default != null) return _default;

                _default = Resources.Load<AudioConfig>(ResourcePath);

                if (_default == null)
                {
                    _default = CreateInstance<AudioConfig>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    Debug.LogWarning($"[音频] 缺 Resources/{ResourcePath}.asset：临时用脚本默认表，资产里改不到。");
                }

                return _default;
            }
        }

        private static Entry[] DefaultEntries() => new[]
        {
            new Entry { id = AudioId.Dash, fileName = "冲刺" },
            new Entry { id = AudioId.HitBody, fileName = "命中身体" },
            new Entry { id = AudioId.HitShell, fileName = "命中硬壳" },
            new Entry { id = AudioId.SwordSwing, fileName = "挥剑" },
            new Entry { id = AudioId.SwordWave, fileName = "剑气(光波)" },
            new Entry { id = AudioId.Move, fileName = "移动" },
            new Entry { id = AudioId.Land, fileName = "落地" },
            new Entry { id = AudioId.Jump, fileName = "跳跃" },
            new Entry { id = AudioId.BgmScene1, fileName = "scene 1" },
            new Entry { id = AudioId.BgmScene2, fileName = "scene 2" },
            new Entry { id = AudioId.BgmScene3, fileName = "scene 3" },
            new Entry { id = AudioId.BgmBoss1, fileName = "boss fight 1" },
            new Entry { id = AudioId.BgmBoss2, fileName = "boss fight 2" },
            new Entry { id = AudioId.BgmBoss3, fileName = "boss fight 3" },
        };
    }
}
