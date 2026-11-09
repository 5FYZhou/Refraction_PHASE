using Refraction.Logic.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Refraction.Presentation.UI
{
    /// <summary>场景级玩法面板：目前只画玩家血量，5 个图标白=有血、黑=无血</summary>
    /// <remarks>血量只从 PlayerHealthChanged 事件来，面板不持有玩家，删掉它不影响战斗</remarks>
    public sealed class GameplayPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Scene;

        public override bool CanBeHideByKey => false;

        public override bool LiveWithScene => true;

        /// <summary>图标名按 ImgH1..ImgH5 排，1 号在最左，序号小的先掉</summary>
        private const string IconPrefix = "ImgH";

        private const int IconCount = 5;

        private static readonly Color AliveColor = Color.white;

        private static readonly Color EmptyColor = Color.black;

        private readonly Image[] _icons = new Image[IconCount];

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < IconCount; i++)
            {
                _icons[i] = GetComponent<Image>(IconPrefix + (i + 1));
            }
        }

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

        private void OnEnable()
        {
            EventBus<PlayerHealthChanged>.Subscribe(OnHealthChanged);
        }

        private void OnDisable()
        {
            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
        }

        private void OnHealthChanged(PlayerHealthChanged evt)
        {
            int alive = Mathf.Clamp(evt.cur, 0, IconCount);

            for (int i = 0; i < IconCount; i++)
            {
                if (_icons[i] == null) continue;

                _icons[i].color = i < alive ? AliveColor : EmptyColor;
            }
        }
    }
}
