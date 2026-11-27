using UnityEngine;

namespace Refraction.MapEditor
{
    public sealed class ConnectionTool : IWorldMapTool
    {
        private string _first;

        public void OnDown(WorldToolContext context)
        {
            LevelMapPlacement placement = WorldMapRenderer.HitPlacement(context.Data, context.World);
            if (placement == null)
            {
                _first = null;
                context.Message = "点击一个关卡作为连接起点。";
                return;
            }
            if (_first == null)
            {
                _first = placement.sceneGuid;
                context.Message = "再点击另一个关卡完成连接。";
                return;
            }
            if (_first == placement.sceneGuid)
            {
                _first = null;
                context.Message = "已取消连接。";
                return;
            }
            LevelMapPlacement from = WorldMapRenderer.FindPlacement(context.Data, _first);
            if (from == null)
            {
                _first = placement.sceneGuid;
                context.Message = "起点关卡已移除，已重新选择起点。";
                return;
            }
            LevelMapData targetLevel = MapDataManager.Load(placement.sceneGuid);
            LevelMapData fromLevel = MapDataManager.Load(_first);
            var connection = new MapConnection
            {
                fromSceneGuid = _first,
                toSceneGuid = placement.sceneGuid,
                fromPosition = WorldMapRenderer.LevelSize(fromLevel) * 0.5f,
                toPosition = WorldMapRenderer.LevelSize(targetLevel) * 0.5f
            };
            context.Data.connections.Add(connection);
            context.SelectedConnection = context.Data.connections.Count - 1;
            context.SelectedGuid = null;
            context.Changed = true;
            context.Message = "已建立连接。";
            _first = null;
        }

        public void OnDrag(WorldToolContext context)
        {
        }

        public void OnUp(WorldToolContext context)
        {
        }
    }
}
