using UnityEngine;

namespace Refraction.Logic.Level
{
    /// <summary>存档点账本：只记最后一次存档的关卡与落点，死亡时拿它决定原地回位还是回上一关</summary>
    /// <remarks>关卡 id 为 0 表示还没存过档；落点已经是玩家枢轴，直接喂给 SpawnAt，不要再减脚底偏移</remarks>
    public sealed class CheckpointBook
    {
        private int _levelId;
        private Vector2 _pivot;

        /// <summary>记一次存档。关卡 id 合法才收，返回是否真的记下了</summary>
        public bool Save(int levelId, Vector2 pivot)
        {
            if (levelId <= 0) return false;

            _levelId = levelId;
            _pivot = pivot;
            return true;
        }

        /// <summary>取回存档：没存过返回 false，此时 pivot 与 levelId 都是默认值</summary>
        public bool TryGetRespawn(out Vector2 pivot, out int levelId)
        {
            pivot = _pivot;
            levelId = _levelId;

            return _levelId > 0;
        }
    }
}
