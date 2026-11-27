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
        private sealed class PixelEdit
        {
            public Color before;
            public Color after;
        }

        private readonly Dictionary<Texture2D, Dictionary<int, PixelEdit>> _edits = new();

        public IEnumerable<Texture2D> EditedTextures => _edits.Keys;

        public bool HasChanges
        {
            get
            {
                bool any = false;
                var emptyTextures = new List<Texture2D>();
                foreach (KeyValuePair<Texture2D, Dictionary<int, PixelEdit>> pair in _edits)
                {
                    var reverted = new List<int>();
                    foreach (KeyValuePair<int, PixelEdit> edit in pair.Value)
                    {
                        if (edit.Value.before == edit.Value.after) reverted.Add(edit.Key);
                    }
                    foreach (int index in reverted) pair.Value.Remove(index);
                    if (pair.Value.Count == 0) emptyTextures.Add(pair.Key);
                    else any = true;
                }
                foreach (Texture2D texture in emptyTextures) _edits.Remove(texture);
                return any;
            }
        }

        public void Record(Texture2D texture, int index, Color before, Color after)
        {
            if (texture == null || index < 0 || index >= texture.width * texture.height) return;
            if (before == after) return;
            if (!_edits.TryGetValue(texture, out Dictionary<int, PixelEdit> edits))
            {
                edits = new Dictionary<int, PixelEdit>();
                _edits[texture] = edits;
            }
            if (edits.TryGetValue(index, out PixelEdit existing))
            {
                existing.after = after;
                return;
            }
            edits[index] = new PixelEdit { before = before, after = after };
        }

        public void ApplyTextures()
        {
            foreach (Texture2D texture in _edits.Keys)
            {
                if (texture != null) texture.Apply(false, false);
            }
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
            foreach (KeyValuePair<Texture2D, Dictionary<int, PixelEdit>> pair in _edits)
            {
                Texture2D texture = pair.Key;
                if (texture == null || pair.Value.Count == 0) continue;
                int width = texture.width;
                int minX = width;
                int minY = texture.height;
                int maxX = -1;
                int maxY = -1;
                foreach (int index in pair.Value.Keys)
                {
                    int x = index % width;
                    int y = index / width;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
                int blockWidth = maxX - minX + 1;
                int blockHeight = maxY - minY + 1;
                Color[] pixels = texture.GetPixels(minX, minY, blockWidth, blockHeight);
                foreach (KeyValuePair<int, PixelEdit> edit in pair.Value)
                {
                    int x = edit.Key % width - minX;
                    int y = edit.Key / width - minY;
                    pixels[y * blockWidth + x] = forward ? edit.Value.after : edit.Value.before;
                }
                texture.SetPixels(minX, minY, blockWidth, blockHeight, pixels);
                texture.Apply(false, false);
            }
        }
    }
}
