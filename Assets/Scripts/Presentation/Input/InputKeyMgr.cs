using System;
using System.Collections.Generic;
using System.IO;
using Refraction.Logic.Input;
using UnityEngine;

namespace Refraction.Presentation.Input
{
    /// <summary>自定义按键：JSON 存 persistentDataPath，没有存档时直接拿 InputSys 现有绑定当默认值</summary>
    public sealed class InputKeyMgr
    {
        private const string FileName = "keybinds.json";

        [Serializable]
        private sealed class Entry
        {
            public string tag;
            public string key;
        }

        [Serializable]
        private sealed class FileData
        {
            public List<Entry> keys = new();
        }

        private static readonly PlayerKeyTag[] AllTags =
        {
            PlayerKeyTag.MoveUp,
            PlayerKeyTag.MoveDown,
            PlayerKeyTag.MoveLeft,
            PlayerKeyTag.MoveRight,
            PlayerKeyTag.Jump,
            PlayerKeyTag.Dash,
            PlayerKeyTag.Attack,
            PlayerKeyTag.Crouch,
            PlayerKeyTag.Charge,
            PlayerKeyTag.Reverse
        };

        private readonly Dictionary<PlayerKeyTag, string> _keys = new();
        private readonly InputProvider _provider;
        private readonly string _path;

        public InputKeyMgr(InputProvider provider)
        {
            _provider = provider;
            _path = Path.Combine(Application.persistentDataPath, FileName);
        }

        public static IReadOnlyList<PlayerKeyTag> Tags => AllTags;

        /// <summary>没存档只填表不下发覆盖：手键盘的默认值会顺手盖掉方向键那套分段</summary>
        public void Load()
        {
            FileData data = ReadData();

            if (data?.keys == null)
            {
                for (int i = 0; i < AllTags.Length; i++) _keys[AllTags[i]] = _provider.GetKey(AllTags[i]);

                return;
            }

            for (int i = 0; i < AllTags.Length; i++)
            {
                PlayerKeyTag tag = AllTags[i];
                string key = FindEntry(data, tag);

                _keys[tag] = key ?? _provider.GetKey(tag);
            }

            for (int i = 0; i < AllTags.Length; i++) _provider.SetKey(AllTags[i], _keys[AllTags[i]]);
        }

        public string GetKey(PlayerKeyTag tag)
        {
            return _keys.TryGetValue(tag, out string key) ? key : null;
        }

        /// <summary>一键只能挂一个动作：被抢的那个清空，动作还在但不再触发</summary>
        public void SetKey(PlayerKeyTag tag, string keyName)
        {
            _keys[tag] = keyName;
            _provider.SetKey(tag, keyName);

            if (!string.IsNullOrEmpty(keyName))
            {
                for (int i = 0; i < AllTags.Length; i++)
                {
                    PlayerKeyTag other = AllTags[i];
                    if (other == tag) continue;
                    if (!string.Equals(GetKey(other), keyName, StringComparison.OrdinalIgnoreCase)) continue;

                    _keys[other] = null;
                    _provider.SetKey(other, null);
                }
            }

            Save();
        }

        private static string FindEntry(FileData data, PlayerKeyTag tag)
        {
            string name = tag.ToString();

            for (int i = 0; i < data.keys.Count; i++)
            {
                if (data.keys[i].tag == name) return data.keys[i].key ?? string.Empty;
            }

            return null;
        }

        private FileData ReadData()
        {
            if (!File.Exists(_path)) return null;

            try
            {
                string json = File.ReadAllText(_path);

                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<FileData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputKeyMgr] 读存档失败 {_path}：{e.Message}");

                return null;
            }
        }

        private void Save()
        {
            FileData data = new();

            for (int i = 0; i < AllTags.Length; i++)
            {
                data.keys.Add(new Entry { tag = AllTags[i].ToString(), key = GetKey(AllTags[i]) ?? string.Empty });
            }

            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputKeyMgr] 写存档失败 {_path}：{e.Message}");
            }
        }
    }
}
