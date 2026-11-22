using System;
using UnityEngine;

namespace Refraction.Data
{
    /// <summary>音效编号：AudioConfig 靠它查 Resources/Sound 下的文件名</summary>
    public enum AudioId
    {
        None = 0,

        /// <summary>冲刺（黑突进也用它）</summary>
        Dash = 1,

        /// <summary>玩家命中敌人身体</summary>
        HitBody = 2,

        /// <summary>命中硬壳</summary>
        HitShell = 3,

        /// <summary>挥剑</summary>
        SwordSwing = 4,

        /// <summary>剑气（光波）</summary>
        SwordWave = 5,

        /// <summary>移动循环音</summary>
        Move = 6,

        /// <summary>落地</summary>
        Land = 7,

        /// <summary>跳跃（含二段跳）</summary>
        Jump = 8,

        /// <summary>受伤：多条随机取一条</summary>
        Hurt = 9,

        /// <summary>死亡</summary>
        Death = 10,

        /// <summary>存档成功</summary>
        SaveBell = 11,

        /// <summary>玩家弹反成功</summary>
        WeaponClash = 12,

        /// <summary>敌人弹反成功</summary>
        WeaponClashEnemy = 13,

        /// <summary>场景 BGM 1</summary>
        BgmScene1 = 101,

        /// <summary>场景 BGM 2</summary>
        BgmScene2 = 102,

        /// <summary>场景 BGM 3</summary>
        BgmScene3 = 103,

        /// <summary>Boss BGM 1</summary>
        BgmBoss1 = 104,

        /// <summary>Boss BGM 2</summary>
        BgmBoss2 = 105,

        /// <summary>Boss BGM 3</summary>
        BgmBoss3 = 106,
    }

    /// <summary>音频配置：枚举到文件名的唯一映射，外加音量与 BGM 淡变时长</summary>
    /// <remarks>资产放 Resources/Data/AudioConfig.asset，缺则退回脚本默认表；文件名不带扩展名、相对 Resources/Sound；配多条时一次性音效随机取，循环音与 BGM 取第一条。</remarks>
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

            [Tooltip("Resources/Sound 下的文件名，不带扩展名。配多条 = 每次随机取一条；第一条也用于循环音与 BGM")]
            public string[] fileNames;
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
            new Entry { id = AudioId.Dash, fileNames = new[] { "冲刺" } },
            new Entry { id = AudioId.HitBody, fileNames = new[] { "命中身体" } },
            new Entry { id = AudioId.HitShell, fileNames = new[] { "命中硬壳" } },
            new Entry { id = AudioId.SwordSwing, fileNames = new[] { "挥剑" } },
            new Entry { id = AudioId.SwordWave, fileNames = new[] { "剑气(光波)" } },
            new Entry { id = AudioId.Move, fileNames = new[] { "移动" } },
            new Entry { id = AudioId.Land, fileNames = new[] { "落地" } },
            new Entry { id = AudioId.Jump, fileNames = new[] { "跳跃" } },
            new Entry { id = AudioId.Hurt, fileNames = new[] { "受伤1", "受伤2", "受伤3" } },
            new Entry { id = AudioId.Death, fileNames = new[] { "死亡1" } },
            new Entry { id = AudioId.SaveBell, fileNames = new[] { "短铃铛" } },
            new Entry { id = AudioId.WeaponClash, fileNames = new[] { "武器碰撞" } },
            new Entry { id = AudioId.WeaponClashEnemy, fileNames = new[] { "武器碰撞2" } },
            new Entry { id = AudioId.BgmScene1, fileNames = new[] { "scene 1" } },
            new Entry { id = AudioId.BgmScene2, fileNames = new[] { "scene 2" } },
            new Entry { id = AudioId.BgmScene3, fileNames = new[] { "scene 3" } },
            new Entry { id = AudioId.BgmBoss1, fileNames = new[] { "boss fight 1" } },
            new Entry { id = AudioId.BgmBoss2, fileNames = new[] { "boss fight 2" } },
            new Entry { id = AudioId.BgmBoss3, fileNames = new[] { "boss fight 3" } },
        };
    }
}
