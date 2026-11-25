using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Refraction.Presentation.UI
{
    /// <summary>全屏黑幕：切关与复活共用一盏，淡入淡出全部走 unscaled 时间（转场期间 timeScale = 0）</summary>
    /// <remarks>运行时建在 UIMgr 的 System 层下，值就靠拉伸铺满 + SetAsLastSibling 压住其它层，所以没有预制体。</remarks>
    public sealed class TransitionMask : MonoBehaviour
    {
        [Tooltip("淡入/淡出时长（秒，不受 timeScale 影响）。低于 0.05 会看出跳帧")]
        [SerializeField] private float fadeDuration = 0.25f;

        [Tooltip("等不到「关卡装配完 + 玩家就位」时的兜底上限（秒）：超时也淡出，避免永久黑屏")]
        [SerializeField] private float readyTimeout = 3f;

        private Image _image;
        private Coroutine _blink;

        public float FadeDuration => fadeDuration;

        public float ReadyTimeout => readyTimeout;

        public bool IsCovered => _image != null && _image.color.a >= 0.999f;

        public static TransitionMask Create(Transform parent)
        {
            GameObject go = new("TransitionMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TransitionMask));
            go.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TransitionMask mask = go.GetComponent<TransitionMask>();
            mask.Init();
            return mask;
        }

        private void Init()
        {
            _image = GetComponent<Image>();

            SetAlpha(0f);
        }

        /// 当帧拉满：调用方紧接着就能换场景，玩家看不到中间那一帧
        public void Cover()
        {
            StopBlink();

            SetAlpha(1f);
        }

        /// <summary>同帧就位的复活用：立刻全黑，下一帧起淡出</summary>
        public void Blink()
        {
            Cover();

            _blink = StartCoroutine(CoBlink());
        }

        public IEnumerator CoFadeIn()
        {
            yield return CoFade(_image.color.a, 1f);
        }

        public IEnumerator CoFadeOut()
        {
            yield return CoFade(_image.color.a, 0f);
        }

        private IEnumerator CoBlink()
        {
            // 黑要真画出来一帧，否则复活前后同帧，等于没暗
            yield return null;

            yield return CoFade(1f, 0f);

            _blink = null;
        }

        private IEnumerator CoFade(float from, float to)
        {
            if (_image == null || Mathf.Approximately(from, to)) yield break;

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                SetAlpha(Mathf.Lerp(from, to, fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f));

                yield return null;
            }

            SetAlpha(to);
        }

        private void StopBlink()
        {
            if (_blink == null) return;

            StopCoroutine(_blink);
            _blink = null;
        }

        private void SetAlpha(float alpha)
        {
            if (_image == null) return;

            alpha = Mathf.Clamp01(alpha);

            _image.color = new Color(0f, 0f, 0f, alpha);

            // 透明时别吃掉点击：只有真正挡着画面才拦射线
            _image.raycastTarget = alpha > 0.001f;

            if (_image.raycastTarget) transform.SetAsLastSibling();
        }
    }
}
