using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace Refraction.Presentation.UI
{
    public abstract class BasePanel : MonoBehaviour
    {
        protected GameRoot root;

        protected Dictionary<string, UIBehaviour> components = new();

        private readonly HashSet<string> _ignoreName = new()
    {
        "Button",
        "Image","RawImage","Background","Checkmark","Label",
        "Text (Legacy)","Arrow","Placeholder","Fill","Handle",
        "Viewport","Scrollbar Horizontal","Scrollbar Vertical"
    };

        public abstract E_UILayer Layer { get; }
        public abstract bool CanBeHideByKey { get; }
        public abstract bool LiveWithScene { get; }

        protected virtual void Awake()
        {
            components.Clear();
            FindComponentsOnChildren();
            RegisterSelf();
            ApplyTxtStyle();
        }

        protected virtual void OnDestroy()
        {
            UnRegisterSelf();
        }

        protected virtual void RegisterSelf()
        {
            root = GameRoot.Instance;
        }

        protected virtual void UnRegisterSelf()
        {
        }

        #region 基础行为
        public virtual void ShowMe()
        {

        }

        public virtual void HideMe()
        {

        }

        protected virtual void OnButtonClicked(string name)
        {
        }

        protected virtual void OnSliderValueChange(string sliderName, float value) { }


        protected virtual string SetInitialTxt(string name)
        {
            return "Empty";
        }

        protected virtual bool KeepAuthoredTxt(string name)
        {
            return false;
        }

        private void FindComponentsOnChildren()
        {
            var componentsInChildren = GetComponentsInChildren<UIBehaviour>(true);

            foreach (var component in componentsInChildren)
            {
                // 精确匹配名字：重名子物体带 (1) 后缀，会落到下面的 Graphic 分支
                if (_ignoreName.Contains(component.name))
                {
                    continue;
                }

                components[component.name] = component;

                if (component is Button button)
                {
                    string buttonName = component.name;

                    button.onClick.AddListener(() =>
                    {
                        OnButtonClicked(buttonName);
                    });
                }
                else if (component is TMP_Text text)
                {
                    if (!KeepAuthoredTxt(component.name))
                        text.text = SetInitialTxt(component.name);
                }
                else if (component is Graphic)
                {
                    // 纯图形不绑定也不告警，否则会把真正的绑定缺失盖掉
                }
                else if (component is LayoutGroup || component is Mask || component is RectMask2D)
                {
                }
                else if (component is Slider slider)
                {
                    slider.onValueChanged.AddListener((value) =>
                    {
                        OnSliderValueChange(slider.name, value);
                    });
                }
                else
                {
                    Debug.LogWarning($"BasePanel:无{component.name}组件{component}的绑定操作");
                }
            }
        }

        protected T GetComponent<T>(string name) where T : UIBehaviour
        {
            if (components.TryGetValue(name, out var component))
            {
                return component as T;
            }

            Debug.LogWarning(
                $"[{GetType().Name}] 找不到 UI 组件：{name}"
            );

            return null;
        }

        #endregion

        #region UI样式

        [Tooltip("面板文本样式，默认正文。Default 表示不动预制体里各自的字号与字体")]
        [SerializeField]
        protected TxtStyle txtStyle = TxtStyle.Body;

        /// Awake 里统一套一遍，覆盖预制体里各自调的字号
        private void ApplyTxtStyle()
        {
            if (txtStyle == TxtStyle.Default) return;

            TMP_FontAsset font = root.UIMgr.GetFont(txtStyle);
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                UIStyle.SetTxtStyle(texts[i], txtStyle, font);
            }
        }

        #endregion
    }
}
