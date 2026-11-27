using System.Collections.Generic;
using UnityEngine;

namespace Refraction.MapEditor
{
    public interface IMapCommand
    {
        void Execute();
        void Undo();
    }

    public abstract class PixelCommand : IMapCommand
    {
        private struct PixelEdit
        {
            public int index;
            public Color before;
            public Color after;
        }

        private readonly Dictionary<Texture2D, List<PixelEdit>> _edits = new();

        public bool HasChanges => _edits.Count > 0;

        public void Record(Texture2D texture, int index, Color before, Color after)
        {
            if (texture == null || index < 0 || index >= texture.width * texture.height) return;
            if (before == after) return;
            if (!_edits.TryGetValue(texture, out List<PixelEdit> list))
            {
                list = new List<PixelEdit>();
                _edits[texture] = list;
            }
            list.Add(new PixelEdit { index = index, before = before, after = after });
        }

        public void Execute()
        {
            Apply(true);
        }

        public void Undo()
        {
            Apply(false);
        }

        private void Apply(bool forward)
        {
            foreach (KeyValuePair<Texture2D, List<PixelEdit>> pair in _edits)
            {
                Texture2D texture = pair.Key;
                Color[] pixels = texture.GetPixels();
                foreach (PixelEdit edit in pair.Value) pixels[edit.index] = forward ? edit.after : edit.before;
                texture.SetPixels(pixels);
                texture.Apply(false, false);
            }
        }
    }
}
