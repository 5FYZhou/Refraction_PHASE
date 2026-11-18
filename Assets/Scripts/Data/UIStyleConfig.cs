using System;
using UnityEngine;

namespace Refraction.Data
{
    /// <summary>UI 文本样式。Default 完全不动预制体，其余按 UIStyleConfig 里的同一条目套用</summary>
    public enum TxtStyle
    {
        Default = 0,
        Title = 1,
        Body = 2,
        FontOnly = 3,
    }

    /// <summary>UI 样式表：一种样式一条，字体只记 Resources 路径，由 UIMgr 转交 ResMgr 加载</summary>
    /// <remarks>缺条目或缺资产时调用方一律跳过，界面保留预制体里的排版，不会整块变成默认字</remarks>
    [CreateAssetMenu(fileName = "UIStyleConfig", menuName = "Refraction/Settings/UIStyleConfig")]
    public sealed class UIStyleConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public TxtStyle style;

            [Tooltip("Resources 相对路径、不带扩展名；留空表示不换字体，沿用预制体里的")]
            public string fontResPath;

            [Tooltip("只换字体：字号与字重保持预制体里各自的排版，FontOnly 这条样式就靠它")]
            public bool fontOnly;

            [Tooltip("字号，TMP 的磅值；面板勾了 OnlyFont 时不生效")]
            [Min(1f)]
            public float fontSize = 20f;

            [Tooltip("加粗；面板勾了 OnlyFont 时不生效")]
            public bool bold;
        }

        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();

        /// <summary>没登记这条样式时返回 null</summary>
        public Entry Find(TxtStyle style)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];

                if (entry != null && entry.style == style) return entry;
            }

            return null;
        }
    }
}
