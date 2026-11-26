using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Refraction.Presentation.UI
{
    /// <summary>悬停时左右两张装饰图同时淡入、移开淡出；时长与端点透明度改文件头 const 即可</summary>
    [RequireComponent(typeof(Button))]
    public sealed class BtnHoverFade : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float FadeInDuration = 0.15f;
        private const float FadeOutDuration = 0.25f;
        private const float IdleAlpha = 0f;
        private const float HoverAlpha = 1f;

        [SerializeField] private Image leftImage;
        [SerializeField] private Image rightImage;

        private Coroutine _fade;
        private float _alpha;

        private void Awake()
        {
            if (leftImage == null) leftImage = FindImage("ImgLeft");
            if (rightImage == null) rightImage = FindImage("ImgRight");
        }

        private void OnEnable()
        {
            _alpha = IdleAlpha;
            ApplyAlpha();
        }

        private void OnDisable()
        {
            if (_fade == null) return;

            StopCoroutine(_fade);
            _fade = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            FadeTo(HoverAlpha, FadeInDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            FadeTo(IdleAlpha, FadeOutDuration);
        }

        private Image FindImage(string childName)
        {
            Transform child = transform.Find(childName);

            return child != null ? child.GetComponent<Image>() : null;
        }

        private void FadeTo(float target, float duration)
        {
            if (_fade != null) StopCoroutine(_fade);

            _fade = StartCoroutine(FadeRoutine(target, duration));
        }

        private IEnumerator FadeRoutine(float target, float duration)
        {
            float from = _alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                _alpha = Mathf.Lerp(from, target, duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f);

                ApplyAlpha();

                yield return null;
            }

            _alpha = target;

            ApplyAlpha();

            _fade = null;
        }

        private void ApplyAlpha()
        {
            SetAlpha(leftImage);
            SetAlpha(rightImage);
        }

        private void SetAlpha(Image image)
        {
            if (image == null) return;

            Color color = image.color;
            color.a = Mathf.Clamp01(_alpha);
            image.color = color;
        }
    }
}
