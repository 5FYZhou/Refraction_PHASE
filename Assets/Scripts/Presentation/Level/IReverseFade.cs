namespace Refraction.Presentation.Level
{
    /// <summary>反转演出的淡入目标：由 LevelRoot 每渲染帧喂一次 0→1 的进度，实现方自己乘进透明度</summary>
    public interface IReverseFade
    {
        void ApplyFade(float scale);
    }
}
