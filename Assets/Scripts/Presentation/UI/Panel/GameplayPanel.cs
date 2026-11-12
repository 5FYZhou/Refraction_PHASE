using Refraction.Logic.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Refraction.Presentation.UI
{
    /// <summary>场景级玩法面板：血图标有血=亮、无血=暗，反转互换；极性条宽度 = 满宽 × 当前值/上限</summary>
    /// <remarks>血量与极性各走一个事件，面板不持有玩家</remarks>
    public sealed class GameplayPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Scene;

        public override bool CanBeHideByKey => false;

        public override bool LiveWithScene => true;

        /// <summary>图标名 ImgH1..ImgH5，序号小的先掉</summary>
        private const string IconPrefix = "ImgH";

        private const int IconCount = 5;

        private const string PolarityNumName = "ImgPolarityNum";

        private readonly Image[] _icons = new Image[IconCount];

        private RectTransform _polarityNum;
        private float _polarityFullWidth;

        private Color _aliveColor = Color.white;

        private Color _emptyColor = Color.black;

        private int _alive = IconCount;

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < IconCount; i++)
            {
                _icons[i] = GetComponent<Image>(IconPrefix + (i + 1));
            }

            Image num = GetComponent<Image>(PolarityNumName);
            if (num != null)
            {
                _polarityNum = num.rectTransform;
                _polarityFullWidth = _polarityNum.sizeDelta.x;
            }

            ApplyPolarity(0, 1);
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
            EventBus<PlayerPolarityChanged>.Subscribe(OnPolarityChanged);
            EventBus<WorldReversed>.Subscribe(OnWorldReversed);
        }

        private void OnDisable()
        {
            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
            EventBus<PlayerPolarityChanged>.Unsubscribe(OnPolarityChanged);
            EventBus<WorldReversed>.Unsubscribe(OnWorldReversed);
        }

        private void OnPolarityChanged(PlayerPolarityChanged evt)
        {
            ApplyPolarity(evt.cur, evt.max);
        }

        private void ApplyPolarity(int cur, int max)
        {
            if (_polarityNum == null) return;

            float ratio = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;

            Vector2 size = _polarityNum.sizeDelta;
            size.x = _polarityFullWidth * ratio;
            _polarityNum.sizeDelta = size;
        }

        private void OnHealthChanged(PlayerHealthChanged evt)
        {
            _alive = Mathf.Clamp(evt.cur, 0, IconCount);
            ApplyColors();
        }

        private void OnWorldReversed(WorldReversed evt)
        {
            Color alive = _aliveColor;
            _aliveColor = _emptyColor;
            _emptyColor = alive;

            ApplyColors();
        }

        private void ApplyColors()
        {
            for (int i = 0; i < IconCount; i++)
            {
                if (_icons[i] == null) continue;

                _icons[i].color = i < _alive ? _aliveColor : _emptyColor;
            }
        }
    }
}
