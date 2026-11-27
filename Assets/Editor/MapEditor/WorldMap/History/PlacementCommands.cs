using System.Collections.Generic;
using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class AddPlacementCommand : IWorldMapCommand
    {
        private readonly LevelMapPlacement _placement;
        private readonly int _index;

        public AddPlacementCommand(LevelMapPlacement placement, int index)
        {
            _placement = placement;
            _index = index;
        }

        public void Execute(WorldMapData data)
        {
            if (data == null || _placement == null || data.levels.Contains(_placement)) return;
            data.levels.Insert(Mathf.Clamp(_index, 0, data.levels.Count), _placement);
        }

        public void Undo(WorldMapData data)
        {
            if (data == null || _placement == null) return;
            data.levels.Remove(_placement);
        }
    }

    public sealed class RemovePlacementCommand : IWorldMapCommand
    {
        private readonly LevelMapPlacement _placement;
        private readonly int _index;
        private readonly List<MapConnection> _connections = new();
        private readonly List<int> _indices = new();

        public RemovePlacementCommand(LevelMapPlacement placement, int index)
        {
            _placement = placement;
            _index = index;
        }

        public void RecordConnection(MapConnection connection, int index)
        {
            _connections.Add(connection);
            _indices.Add(index);
        }

        public bool HasConnections => _connections.Count > 0;

        public void Execute(WorldMapData data)
        {
            if (data == null) return;
            if (_placement != null) data.levels.Remove(_placement);
            foreach (MapConnection connection in _connections) data.connections.Remove(connection);
        }

        public void Undo(WorldMapData data)
        {
            if (data == null) return;
            if (_placement != null && !data.levels.Contains(_placement))
                data.levels.Insert(Mathf.Clamp(_index, 0, data.levels.Count), _placement);
            for (int i = 0; i < _connections.Count; i++)
            {
                MapConnection connection = _connections[i];
                if (connection == null || data.connections.Contains(connection)) continue;
                data.connections.Insert(Mathf.Clamp(_indices[i], 0, data.connections.Count), connection);
            }
        }
    }

    public sealed class MovePlacementCommand : IWorldMapCommand
    {
        private readonly LevelMapPlacement _placement;
        private readonly Vector2 _from;
        private Vector2 _to;

        public MovePlacementCommand(LevelMapPlacement placement, Vector2 from, Vector2 to)
        {
            _placement = placement;
            _from = from;
            _to = to;
        }

        public bool CanMerge(LevelMapPlacement placement, Vector2 currentPosition)
        {
            return _placement == placement && _to == currentPosition;
        }

        public void Retarget(Vector2 to)
        {
            _to = to;
        }

        public void Execute(WorldMapData data)
        {
            if (_placement != null) _placement.position = _to;
        }

        public void Undo(WorldMapData data)
        {
            if (_placement != null) _placement.position = _from;
        }
    }
}
