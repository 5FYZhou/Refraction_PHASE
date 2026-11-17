using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>蓄力条：Charging 把 Charge/ChargeValue 从左往右填满，Charged 后 BG 与 Value 的透明度一起呼吸</summary>
    /// <remarks>缩放绕自身枢轴做，居中枢轴的条会从中间向两边长（看着不是进度条），所以要按枢轴比例反推位置补偿；宽度与枢轴都取自 sprite，换图不必改代码</remarks>
    public sealed class ChargeBarView
    {
        private const string ChargeName = "Charge";
        private const string BgName = "ChargeBG";
        private const string BgLegacyName = "ChageBG";
        private const string ValueName = "ChargeValue";

        private const float BreathHz = 1.2f;
        private const float BreathMinAlpha = 0.25f;

        private readonly Transform _value;
        private readonly SpriteRenderer _bg;
        private readonly SpriteRenderer _valueRenderer;

        private readonly Vector3 _baseScale;
        private readonly Vector3 _basePos;
        private readonly float _spriteWidth;
        private readonly float _pivotRatio;
        private readonly Color _bgColor;
        private readonly Color _valueColor;

        private bool _shown = true; // 初始记“已显示”，构造里那次隐藏才会真的落地，否则开局就顶着一条满格蓝条
        private float _fill = -1f;
        private float _alpha = -1f;
        private float _breathTime;
        private bool _warned;

        public ChargeBarView(Transform owner)
        {
            if (owner == null) return;

            Transform charge = owner.Find(ChargeName);
            if (charge == null)
            {
                WarnMissing();
                return;
            }

            Transform bg = charge.Find(BgName);
            if (bg == null) bg = charge.Find(BgLegacyName);

            _value = charge.Find(ValueName);
            _bg = bg != null ? bg.GetComponent<SpriteRenderer>() : null;
            _valueRenderer = _value != null ? _value.GetComponent<SpriteRenderer>() : null;

            if (_valueRenderer == null || _valueRenderer.sprite == null)
            {
                WarnMissing();
                return;
            }

            _baseScale = _value.localScale;
            _basePos = _value.localPosition;

            Sprite sprite = _valueRenderer.sprite;
            _spriteWidth = sprite.bounds.size.x;
            _pivotRatio = sprite.rect.width > 0f ? sprite.pivot.x / sprite.rect.width : 0.5f;

            if (_bg != null) _bgColor = _bg.color;
            _valueColor = _valueRenderer.color;

            Show(false);
        }

        public void Tick(ChargePhase stage, float progress, float deltaTime)
        {
            if (_valueRenderer == null) return;

            bool visible = stage != ChargePhase.None;
            Show(visible);
            if (!visible) return;

            if (stage == ChargePhase.Charged)
            {
                SetFill(1f);

                _breathTime += deltaTime * BreathHz;
                SetAlpha(Mathf.Lerp(BreathMinAlpha, 1f, Mathf.Cos(_breathTime * Mathf.PI * 2f) * 0.5f + 0.5f));
                return;
            }

            _breathTime = 0f;
            SetAlpha(1f);
            SetFill(progress);
        }

        private void SetFill(float progress)
        {
            float p = Mathf.Clamp01(progress);
            if (Mathf.Approximately(p, _fill)) return;

            _fill = p;

            float scaleX = _baseScale.x * p;
            _value.localScale = new Vector3(scaleX, _baseScale.y, _baseScale.z);

            Vector3 pos = _basePos;
            pos.x = _basePos.x - _pivotRatio * _spriteWidth * (_baseScale.x - scaleX);
            _value.localPosition = pos;
        }

        private void SetAlpha(float alpha)
        {
            if (Mathf.Approximately(alpha, _alpha)) return;

            _alpha = alpha;

            if (_bg != null)
            {
                Color bg = _bgColor;
                bg.a = _bgColor.a * alpha;
                _bg.color = bg;
            }

            Color value = _valueColor;
            value.a = _valueColor.a * alpha;
            _valueRenderer.color = value;
        }

        private void Show(bool visible)
        {
            if (visible == _shown) return;

            _shown = visible;

            if (_bg != null) _bg.enabled = visible;
            if (_valueRenderer != null) _valueRenderer.enabled = visible;

            if (visible) return;

            _fill = -1f;
            _alpha = -1f;
            _breathTime = 0f;
        }

        private void WarnMissing()
        {
            if (_warned) return;

            _warned = true;
            Debug.LogWarning("[ChargeBarView] 层级里缺少 Charge/ChargeValue（或没挂 SpriteRenderer 与 sprite）：蓄力时不会出现进度条。");
        }
    }
}
