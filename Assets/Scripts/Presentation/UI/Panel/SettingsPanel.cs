using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Unity.VisualScripting;

namespace Refraction.Presentation.UI
{
    /// <summary>暂停总设置面板：Esc 打开；开子面板时自身淡出但不关闭，子面板关掉再淡入，关它才解除暂停</summary>
    public sealed class SettingsPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;

        public override bool CanBeHideByKey => true;

        public override bool LiveWithScene => false;

        protected override float FadeTime => 0.15f;

        private const string KeySetButtonName = "BtnKeySet";

        private const string AudioSetButtonName = "BtnAudioSet";

        private const string CloseButtonName = "BtnClose";

        private const string ReturnMenuButtonName = "BtnReturnMenu";

        public override void HideMe()
        {
            if (root == null) return;

            root.Game.Resume();
        }

        public void FadeBackIn()
        {
            FadeInSilently();
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case CloseButtonName:
                    root.UIMgr.HidePanel<SettingsPanel>();
                    break;
                case KeySetButtonName:
                    OpenSub<KeySetPanel>();
                    break;
                case AudioSetButtonName:
                    OpenSub<AudioSetPanel>();
                    break;
                case ReturnMenuButtonName:
                    EventBus<RequestLoadScene>.Publish(new(SceneName.Begin));
                    break;
            }
        }

        private void OpenSub<T>() where T : BasePanel
        {
            FadeOutSilently();
            root.UIMgr.ShowPanel<T>();
        }
    }

    /// <summary>子设置面板共用壳：Esc 与关闭按钮都落到 CloseSelf，关掉后让总面板淡回来</summary>
    public abstract class SettingSubPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;

        public override bool CanBeHideByKey => true;

        public override bool LiveWithScene => false;

        protected override float FadeTime => 0.15f;

        private const string CloseButtonName = "BtnClose";

        protected abstract void CloseSelf();

        public override void HideMe()
        {
            if (root == null) return;

            root.UIMgr.GetPanel<SettingsPanel>(panel => panel.FadeBackIn());
        }

        protected override void OnButtonClicked(string name)
        {
            if (name != CloseButtonName) return;

            CloseSelf();
        }
    }
}
