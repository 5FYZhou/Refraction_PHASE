using System;
using Refraction.Foundation;
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

        /// <summary>蓄力开始</summary>
        Charge = 14,

        /// <summary>世界反转</summary>
        WorldReverse = 15,

        /// <summary>折跃连线被子弹触发</summary>
        LinkTrigger = 16,

        /// <summary>敌人死亡</summary>
        EnemyDeath = 17,

        /// <summary>残影冲刺</summary>
        AfterimageDash = 18,

        /// <summary>白攻击命中</summary>
        WhiteAttackHit = 19,

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

        /// <summary>主界面 BGM</summary>
        BgmMenu = 107,
    }

    /// <summary>音频配置：全局音量与抖动参数在前，枚举到文件名的配对表在后</summary>
    /// <remarks>资产放 Resources/Data/Settings/AudioConfig.asset，缺则退回脚本默认表；文件名不带扩展名、相对 Resources/Sound；配多条时一次性音效随机取，循环音与 BGM 取第一条。</remarks>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Refraction/Settings/Audio")]
    public sealed class AudioConfig : ScriptableObject
    {
        private static AudioConfig _default;

        [Serializable]
        public struct Entry
        {
            [Tooltip("音效编号")]
            public AudioId id;

            [Tooltip("Resources/Sound 下的文件名，不带扩展名。配多条 = 每次随机取一条；第一条也用于循环音与 BGM")]
            public string[] fileNames;

            [Tooltip("这一条自己的音量（0~1），播放时与上面的全局音量相乘")]
            [Range(0f, 1f)]
            public float volume;

            [Tooltip("这一条是否加随机音量与音调；上面 randomVariation 关掉时一律不抖")]
            public bool randomize;

            [Tooltip("同一编号是否共用一个 AudioSource：true = 下次播放打断上一条并从头再播，false = 各占一条源可重叠")]
            public bool single;
        }

        [Tooltip("背景音乐音量，0~1")]
        [Range(0f, 1f)]
        public float bgmVolume = 0.6f;

        [Tooltip("音效音量，0~1")]
        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        [Tooltip("BGM 切换的淡入淡出时长（秒），0 = 瞬时切换")]
        [Min(0f)]
        public float bgmFadeDuration = 0.3f;

        [Tooltip("随机抖动的总开关：关掉后所有音效都不抖，逐条开关见下方配对表")]
        public bool randomVariation = true;

        [Tooltip("音量抖动幅度：在条目音量上乘 1 ± 该值，0 = 不抖")]
        [Range(0f, 0.5f)]
        public float randomVolumeRange = 0.08f;

        [Tooltip("音调抖动幅度：播放音高乘 1 ± 该值，0 = 不抖")]
        [Range(0f, 0.5f)]
        public float randomPitchRange = 0.06f;

        [Tooltip("枚举到文件名的映射：同一编号必须唯一，文件名留空表示这一条不生效。")]
        [SerializeField]
        private Entry[] entries = DefaultEntries();

        public Entry[] Entries => entries ?? Array.Empty<Entry>();

        public static AudioConfig Default
        {
            get
            {
                if (_default != null) return _default;

                _default = Resources.Load<AudioConfig>(ResPath.AudioConfig);

                if (_default == null)
                {
                    _default = CreateInstance<AudioConfig>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                    Debug.LogWarning($"[音频] 缺 Resources/{ResPath.AudioConfig}.asset：临时用脚本默认表，资产里改不到。");
                }

                return _default;
            }
        }

        private static Entry[] DefaultEntries() => new[]
        {
            Make(AudioId.Dash, true, "冲刺"),
            Make(AudioId.HitBody, true, "命中身体"),
            Make(AudioId.HitShell, false, "命中硬壳"),
            Make(AudioId.SwordSwing, true, "挥剑"),
            Make(AudioId.SwordWave, false, "剑气(光波)"),
            Make(AudioId.Move, false, "移动"),
            Make(AudioId.Land, true, "落地"),
            Make(AudioId.Jump, true, "跳跃"),
            Make(AudioId.Hurt, false, "受伤1", "受伤2", "受伤3"),
            Make(AudioId.Death, false, "死亡1"),
            Make(AudioId.SaveBell, false, "短铃铛"),
            Make(AudioId.WeaponClash, true, "武器碰撞"),
            Make(AudioId.WeaponClashEnemy, true, "武器碰撞2"),
            Solo(AudioId.Charge, "充能"),
            Make(AudioId.WorldReverse, false, "反转"),
            Make(AudioId.LinkTrigger, true, "折跃连线触发"),
            Make(AudioId.EnemyDeath, false, "敌人死亡"),
            Make(AudioId.AfterimageDash, true, "残影冲刺"),
            Make(AudioId.WhiteAttackHit, true, "白攻击.命中"),
            Make(AudioId.BgmScene1, false, "scene 1"),
            Make(AudioId.BgmScene2, false, "scene 2"),
            Make(AudioId.BgmScene3, false, "scene 3"),
            Make(AudioId.BgmBoss1, false, "boss fight 1"),
            Make(AudioId.BgmBoss2, false, "boss fight 2"),
            Make(AudioId.BgmBoss3, false, "boss fight 3"),
            Make(AudioId.BgmMenu, false, "主界面"),
        };

        private static Entry Make(AudioId id, bool randomize, params string[] fileNames) => new()
        {
            id = id,
            fileNames = fileNames,
            volume = 1f,
            randomize = randomize,
        };

        private static Entry Solo(AudioId id, params string[] fileNames) => new()
        {
            id = id,
            fileNames = fileNames,
            volume = 1f,
            single = true,
        };
    }
}
