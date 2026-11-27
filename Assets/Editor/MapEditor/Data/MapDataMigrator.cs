using Vector2Int = UnityEngine.Vector2Int;

namespace Refraction.MapEditor
{
    public static class MapDataMigrator
    {
        public static bool Migrate(LevelMapData data)
        {
            if (data == null) return false;
            bool changed = false;
            if (data.dataVersion < 2)
            {
                data.dataVersion = 2;
                changed = true;
            }
            if (string.IsNullOrEmpty(data.groundHash) && data.sourceSprites != null
                && data.sourceSprites.Count > 0 && !string.IsNullOrEmpty(data.sourceSprites[0].spriteGuid))
            {
                data.groundHash = GroundHashCalculator.CalculateGroundHash(data.sourceSprites);
                changed = true;
            }
            if (data.groundEraseMask != null && data.paintEraseMask == null)
            {
                Vector2Int size = new Vector2Int(data.groundEraseMask.width, data.groundEraseMask.height);
                data.paintEraseMask = MapGenerator.CreateBlank("PaintEraseMask", size.x, size.y);
                changed = true;
            }
            return changed;
        }
    }
}
