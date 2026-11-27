using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class MapAutoSave
    {
        private const double IdleSeconds = 1.0;

        private readonly Dictionary<string, LevelMapData> _pending = new();
        private readonly Dictionary<string, double> _dirtyAt = new();

        public void MarkDirty(LevelMapData data)
        {
            if (data == null) return;
            data.isDirty = true;
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
            var pending = new List<LevelMapData>(_pending.Values);
            foreach (LevelMapData data in pending) RequestSave(data);
        }
    }
}
