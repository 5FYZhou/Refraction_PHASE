namespace Refraction.MapEditor
{
    public interface IWorldMapCommand
    {
        void Execute(WorldMapData data);
        void Undo(WorldMapData data);
    }
}
