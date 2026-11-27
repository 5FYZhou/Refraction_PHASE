using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Refraction.MapEditor
{
    public enum GroundChangeAction
    {
        Regenerate,
        Keep,
        RebuildAll
    }

    public static class MapDataSynchronizer
    {
        public static bool HasChanged(LevelMapData data, List<MapSpriteData> scanned)
        {
            if (data == null) return false;
            string current = GroundHashCalculator.CalculateGroundHash(scanned);
            return !string.IsNullOrEmpty(data.groundHash) && data.groundHash != current;
        }

        public static GroundChangeAction Ask(string sceneName)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "检测到 Ground 变化",
                sceneName + " 的原始关卡与上次扫描不同，请选择处理方式。",
                "重新生成底图",
                "保留当前地图",
                "重建全部数据");
            if (choice == 1) return GroundChangeAction.Keep;
            if (choice == 2)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "二次确认",
                    "重建将清空 " + sceneName + " 的全部人工绘制与擦除记录，确定继续？",
                    "确定重建", "取消");
                return confirm ? GroundChangeAction.RebuildAll : GroundChangeAction.Keep;
            }
            return GroundChangeAction.Regenerate;
        }
    }
}
