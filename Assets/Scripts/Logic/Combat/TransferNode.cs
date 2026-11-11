using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>传输路径上的一枚残影：序号用来验活，位置与朝向用来走位和出射</summary>
    public readonly struct TransferNode
    {
        public readonly int Index;

        public readonly Vector2 Pos;

        public readonly int Facing;

        public TransferNode(int index, Vector2 pos, int facing)
        {
            Index = index;
            Pos = pos;
            Facing = facing;
        }
    }
}
