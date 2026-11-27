namespace Refraction.Logic.Actor
{
    /// <summary>Boss 在编辑器里对着调的几个半径：Scene 按它们画圈，逻辑只读，不进配置</summary>
    public readonly struct MmflyRange
    {
        public readonly float Dive;

        public readonly float Barrage;

        public readonly float Summon;

        public readonly float SummonRadius;

        public readonly float Ideal;

        public MmflyRange(float dive, float barrage, float summon, float summonRadius, float ideal)
        {
            Dive = dive;
            Barrage = barrage;
            Summon = summon;
            SummonRadius = summonRadius;
            Ideal = ideal;
        }
    }
}
