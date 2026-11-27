namespace Refraction.Presentation.UI
{
    /// <summary>顶层地图：按住 Tab 淡入、松开淡出，Esc 不关它；显隐由 GameRoot 按逻辑层的 MapOpen 驱动</summary>
    public sealed class MapPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Bottom;

        public override bool CanBeHideByKey => false;

        public override bool LiveWithScene => false;

        protected override float FadeTime => 0.15f;
    }
}
