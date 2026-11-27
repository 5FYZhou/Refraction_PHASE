using System.Collections.Generic;

namespace Refraction.MapEditor
{
    public sealed class MapHistory
    {
        private const int MaxSteps = 50;

        private readonly List<IMapCommand> _stack = new();
        private int _cursor;

        public bool CanUndo => _cursor > 0;
        public bool CanRedo => _cursor < _stack.Count - 1;

        public void Push(IMapCommand command)
        {
            if (command == null) return;
            if (command is PixelCommand pixel && !pixel.HasChanges) return;
            while (_cursor < _stack.Count - 1) _stack.RemoveAt(_stack.Count - 1);
            _stack.Add(command);
            while (_stack.Count > MaxSteps) _stack.RemoveAt(0);
            _cursor = _stack.Count;
        }

        public IMapCommand Undo()
        {
            if (!CanUndo) return null;
            IMapCommand command = _stack[_cursor - 1];
            command.Undo();
            _cursor--;
            return command;
        }

        public IMapCommand Redo()
        {
            if (!CanRedo) return null;
            IMapCommand command = _stack[_cursor];
            command.Execute();
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
