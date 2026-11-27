using System.Collections.Generic;

namespace Refraction.MapEditor
{
    public sealed class WorldMapHistory
    {
        private const int MaxSteps = 50;

        private readonly List<IWorldMapCommand> _stack = new();
        private int _cursor;

        public bool CanUndo => _cursor > 0;
        public bool CanRedo => _cursor < _stack.Count - 1;

        public void Push(IWorldMapCommand command)
        {
            if (command == null) return;
            while (_cursor < _stack.Count - 1) _stack.RemoveAt(_stack.Count - 1);
            _stack.Add(command);
            while (_stack.Count > MaxSteps) _stack.RemoveAt(0);
            _cursor = _stack.Count;
        }

        public IWorldMapCommand Peek()
        {
            if (!CanUndo || CanRedo) return null;
            return _stack[_cursor - 1];
        }

        public IWorldMapCommand Undo(WorldMapData data)
        {
            if (!CanUndo) return null;
            IWorldMapCommand command = _stack[_cursor - 1];
            command.Undo(data);
            _cursor--;
            return command;
        }

        public IWorldMapCommand Redo(WorldMapData data)
        {
            if (!CanRedo) return null;
            IWorldMapCommand command = _stack[_cursor];
            command.Execute(data);
            _cursor++;
            return command;
        }

        public void Clear()
        {
            _stack.Clear();
            _cursor = 0;
        }
    }
}
