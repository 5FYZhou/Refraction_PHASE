using Refraction.Logic.Events;
using Refraction.Logic.Level;
using TMPro;
using UnityEngine;

namespace Refraction.Presentation.UI
{
    /// <summary>场景级提示面板：世界变黑时文本按 1-rgb 反色，原色只缓存一次，来回切换不累积</summary>
    public sealed class TipPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Scene;

        public override bool CanBeHideByKey => false;

        public override bool LiveWithScene => true;

        private TMP_Text[] _texts;
        private Color[] _authored;

        protected override void Awake()
        {
            base.Awake();

            _texts = GetComponentsInChildren<TMP_Text>(true);
            _authored = new Color[_texts.Length];

            for (int i = 0; i < _texts.Length; i++) _authored[i] = _texts[i].color;

            ApplyInvert(root != null && root.World == WorldPolarity.Black);
        }

        private void OnEnable()
        {
            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);
        }

        private void OnDisable()
        {
            EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);
        }

        private void OnWorldPolarityChanged(WorldPolarityChanged evt)
        {
            ApplyInvert(evt.World == WorldPolarity.Black);
        }

        private void ApplyInvert(bool black)
        {
            if (_texts == null) return;

            for (int i = 0; i < _texts.Length; i++)
            {
                if (_texts[i] == null) continue;

                Color color = _authored[i];

                _texts[i].color = black
                    ? new Color(1f - color.r, 1f - color.g, 1f - color.b, color.a)
                    : color;
            }
        }
    }
}
