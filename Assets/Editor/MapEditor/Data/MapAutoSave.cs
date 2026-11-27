using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class MapAutoSave
    {
        private const double IdleSeconds = 1.0;
        private const string PrefKey = "Refraction.MapEditor.AutoSave";

        private readonly Dictionary<string, LevelMapData> _pending = new();
        private readonly Dictionary<string, double> _dirtyAt = new();

        public bool Enabled { get; private set; }

        public void LoadEnabled()
        {
            Enabled = EditorPrefs.GetBool(PrefKey, false);
        }

        public void SetEnabled(bool value)
        {
            Enabled = value;
            EditorPrefs.SetBool(PrefKey, value);
        }

        public void MarkDirty(LevelMapData data)
        {
            MarkDirty(data, MapDirtyFlags.All);
        }

        public void MarkDirty(LevelMapData data, MapDirtyFlags flags)
        {
            if (data == null || flags == MapDirtyFlags.None) return;
            data.isDirty = true;
            data.dirtyFlags |= flags;
            EditorUtility.SetDirty(data);
            _pending[data.sceneGuid] = data;
            _dirtyAt[data.sceneGuid] = EditorApplication.timeSinceStartup;
        }

        public void RequestSave(LevelMapData data)
        {
            if (data == null) return;
            _pending.Remove(data.sceneGuid);
            _dirtyAt.Remove(data.sceneGuid);
            MapDataManager.Save(data);
        }

        public void SavePending()
        {
            if (!Enabled) return;
            double now = EditorApplication.timeSinceStartup;
            var ready = new List<LevelMapData>();
            foreach (KeyValuePair<string, double> pair in _dirtyAt)
            {
                if (now - pair.Value < IdleSeconds) continue;
                if (_pending.TryGetValue(pair.Key, out LevelMapData data)) ready.Add(data);
            }
            foreach (LevelMapData data in ready) RequestSave(data);
        }

        public void Flush()
        {
            if (!Enabled) return;
            var pending = new List<LevelMapData>(_pending.Values);
            foreach (LevelMapData data in pending) RequestSave(data);
        }
    }
}
