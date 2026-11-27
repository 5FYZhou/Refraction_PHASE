using System.Collections.Generic;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class MapHistory
    {
        private sealed class Snapshot
        {
            public int width;
            public int height;
            public Color[] paint;
            public Color[] mask;
        }

        private const int MaxSteps = 32;

        private readonly List<Snapshot> _stack = new();
        private int _cursor;

        public bool CanUndo => _cursor > 0;
        public bool CanRedo => _cursor < _stack.Count - 1;

        public void Record(LevelMapData data)
        {
            if (data == null || data.paintTexture == null || data.eraseMask == null) return;
            Texture2D paint = data.paintTexture;
            Texture2D mask = data.eraseMask;
            if (paint.width != mask.width || paint.height != mask.height) return;

            while (_cursor < _stack.Count - 1) _stack.RemoveAt(_stack.Count - 1);
            _stack.Add(new Snapshot
            {
                width = paint.width,
                height = paint.height,
                paint = paint.GetPixels(),
                mask = mask.GetPixels()
            });
            while (_stack.Count > MaxSteps) _stack.RemoveAt(0);
            _cursor = _stack.Count - 1;
        }

        public bool Undo(LevelMapData data)
        {
            if (!CanUndo) return false;
            _cursor--;
            return Apply(data, _stack[_cursor]);
        }

        public bool Redo(LevelMapData data)
        {
            if (!CanRedo) return false;
            _cursor++;
            return Apply(data, _stack[_cursor]);
        }

        public void Clear()
        {
            _stack.Clear();
            _cursor = 0;
        }

        private static bool Apply(LevelMapData data, Snapshot snapshot)
        {
            if (data == null || data.paintTexture == null || data.eraseMask == null) return false;
            Texture2D paint = data.paintTexture;
            Texture2D mask = data.eraseMask;
            if (paint.width != snapshot.width || paint.height != snapshot.height
                || mask.width != snapshot.width || mask.height != snapshot.height) return false;

            paint.SetPixels(snapshot.paint);
            paint.Apply(false, false);
            mask.SetPixels(snapshot.mask);
            mask.Apply(false, false);
            return true;
        }
    }
}
