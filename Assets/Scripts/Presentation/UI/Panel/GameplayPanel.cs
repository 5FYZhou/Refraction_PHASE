using Refraction.Logic.Events;
using Refraction.Logic.Level;
using UnityEngine;
using UnityEngine.UI;

namespace Refraction.Presentation.UI
{
    /// <summary>场景级玩法面板：血图标按 Lose 播碎裂动画，极性条 = Mask 宽 × 当前值/上限；世界变黑时血图标与条底走特效 _OpenInvert</summary>
    public sealed class GameplayPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Scene;

        public override bool CanBeHideByKey => false;

        public override bool LiveWithScene => true;

        /// <summary>ImgH1..ImgH5 从左往右排，序号大的先碎</summary>
        private const string IconPrefix = "ImgH";

        private const int IconCount = 5;

        private const string LoseParam = "Lose";

        private const string PolarityNumName = "ImgPolarityNum";

        private const string PolarityBgName = "ImgPolarityBG";

        private const string PolarityBgExtraName = "ImgPolarityBG (1)";

        private readonly Image[] _icons = new Image[IconCount];

        private readonly Animator[] _anims = new Animator[IconCount];

        private readonly Image[] _invertibles = new Image[IconCount + 2];

        private RectTransform _polarityMask;
        private float _polarityFullWidth;

        private int _alive = IconCount;

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < IconCount; i++)
            {
                _icons[i] = GetComponent<Image>(IconPrefix + (i + 1));
                if (_icons[i] != null) _anims[i] = _icons[i].GetComponent<Animator>();

                _invertibles[i] = _icons[i];
            }

            Image num = GetComponent<Image>(PolarityNumName);

            if (num != null) _polarityMask = num.rectTransform.parent as RectTransform;
            if (_polarityMask != null) _polarityFullWidth = _polarityMask.sizeDelta.x;

            _invertibles[IconCount] = GetComponent<Image>(PolarityBgName);
            _invertibles[IconCount + 1] = GetComponent<Image>(PolarityBgExtraName);

            ApplyInvert(root != null && root.World == WorldPolarity.Black);
            ApplyPolarity(0, 1);
            ApplyHealth(_alive);
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
            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);

            ApplyHealth(_alive);
        }

        private void OnDisable()
        {
            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
            EventBus<PlayerPolarityChanged>.Unsubscribe(OnPolarityChanged);
            EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);
        }

        private void OnWorldPolarityChanged(WorldPolarityChanged evt)
        {
            ApplyInvert(evt.World == WorldPolarity.Black);
        }

        private void ApplyInvert(bool black)
        {
            if (root == null) return;

            for (int i = 0; i < _invertibles.Length; i++)
            {
                root.Effects?.SetInvert(_invertibles[i], black);
            }
        }

        private void OnPolarityChanged(PlayerPolarityChanged evt)
        {
            ApplyPolarity(evt.cur, evt.max);
        }

        private void ApplyPolarity(int cur, int max)
        {
            if (_polarityMask == null) return;

            float ratio = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;

            Vector2 size = _polarityMask.sizeDelta;
            size.x = _polarityFullWidth * ratio;
            _polarityMask.sizeDelta = size;
        }

        private void OnHealthChanged(PlayerHealthChanged evt)
        {
            ApplyHealth(evt.cur);
        }

        private void ApplyHealth(int cur)
        {
            _alive = Mathf.Clamp(cur, 0, IconCount);

            for (int i = 0; i < IconCount; i++)
            {
                if (_anims[i] == null) continue;

                _anims[i].SetBool(LoseParam, i >= _alive);
            }
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnSetting":
                    GameRoot.Instance.UIMgr.ShowPanel<SettingsPanel>();
                    return;
            }
        }
    }
}
