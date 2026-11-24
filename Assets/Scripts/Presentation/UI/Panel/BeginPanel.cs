using Refraction.Logic.Events;
using Refraction.Logic.Level;

namespace Refraction.Presentation.UI
{
    /// <summary>开始面板：按钮只发请求，转场与场景装载全部交给 GameRoot，这里不直接 LoadScene</summary>
    /// <remarks>场景自带实例并自注册；Layer 取 Scene 是因为它的父级是场景 Canvas，不归 UIMgr 的四层管</remarks>
    public sealed class BeginPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Scene;

        public override bool CanBeHideByKey => false;

        public override bool LiveWithScene => true;

        private const string StartButtonName = "BtnStart";

        private const string SettingButtonName = "BtnSetting";

        private const string ExitButtonName = "BtnExit";

        private bool _starting;

        protected override void RegisterSelf()
        {
            base.RegisterSelf();
            root.UIMgr.RegisterPanel(this);
        }

        protected override void UnRegisterSelf()
        {
            // 退出播放时 GameRoot 可能先于本面板销毁
            if (root == null) return;

            root.UIMgr.UnregisterPanel(this);
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case StartButtonName:
                    StartGame();
                    break;
                case SettingButtonName:
                    root.UIMgr.ShowPanel<SettingsPanel>();
                    break;
                case ExitButtonName:
                    QuitGame();
                    break;
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        /// <summary>转场期间重复点击会被 GameRoot 的转场锁拒绝并报错，这里先挡住</summary>
        private void StartGame()
        {
            if (_starting) return;

            _starting = true;

            EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Game));
        }
    }
}
