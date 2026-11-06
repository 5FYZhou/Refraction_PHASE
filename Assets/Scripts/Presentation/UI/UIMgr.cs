using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Refraction.Presentation.UI
{
    public enum E_UILayer
    {
        Bottom,
        Middle,
        Top,
        System,
    }

    /// <summary>预制体名必须等于面板类名：这里用 typeof(T).Name 去找资源，改名不报错只是加载不到</summary>
    public class UIMgr
    {
        private abstract class BasePanelInfo { }

        private class PanelInfo<T> : BasePanelInfo where T : BasePanel
        {
            public T panel;
            public UnityAction<T> callBack;
            public bool isHide;

            public PanelInfo(UnityAction<T> callBack)
            {
                this.callBack += callBack;
            }
        }


        public Camera uiCamera;
        private Canvas uiCanvas;
        private EventSystem uiEventSystem;

        private Transform bottomLayer;
        private Transform middleLayer;
        private Transform topLayer;
        private Transform systemLayer;

        private Dictionary<string, BasePanelInfo> panelDic = new Dictionary<string, BasePanelInfo>();

        private UIMgr()
        {
            uiCamera = GameObject.Instantiate(GameRoot.Instance.ResMgr.Load<GameObject>("ui/UICamera")).GetComponent<Camera>();
            // UI 相机过场景不移除：面板常驻，换场景后没有它就整个 UI 不可见
            GameObject.DontDestroyOnLoad(uiCamera.gameObject);

            uiCanvas = GameObject.Instantiate(GameRoot.Instance.ResMgr.Load<GameObject>("ui/Canvas")).GetComponent<Canvas>();
            uiCanvas.worldCamera = uiCamera;
            GameObject.DontDestroyOnLoad(uiCanvas.gameObject);

            bottomLayer = uiCanvas.transform.Find("Bottom");
            middleLayer = uiCanvas.transform.Find("Middle");
            topLayer = uiCanvas.transform.Find("Top");
            systemLayer = uiCanvas.transform.Find("System");

            uiEventSystem = GameObject.Instantiate(GameRoot.Instance.ResMgr.Load<GameObject>("ui/EventSystem")).GetComponent<EventSystem>();
            GameObject.DontDestroyOnLoad(uiEventSystem.gameObject);
        }

        public Transform GetLayerFather(E_UILayer layer)
        {
            switch (layer)
            {
                case E_UILayer.Bottom:
                    return bottomLayer;
                case E_UILayer.Middle:
                    return middleLayer;
                case E_UILayer.Top:
                    return topLayer;
                case E_UILayer.System:
                    return systemLayer;
                default:
                    // 未知层级返回 null，由调用方决定兜底到哪一层
                    return null;
            }
        }

        /// <summary>isSync 目前没有生效，总是走异步：callBack 可能当场回调，也可能等资源落地后才补发</summary>
        public void ShowPanel<T>(E_UILayer layer = E_UILayer.Middle, UnityAction<T> callBack = null, bool isSync = true) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                if (panelInfo.panel == null)
                {
                    // 异步没落地就被 Hide：置回 false，回调攒在记录上等落地统一补发
                    panelInfo.isHide = false;

                    if (callBack != null)
                        panelInfo.callBack += callBack;
                }
                else
                {
                    if (!panelInfo.panel.gameObject.activeSelf)
                        panelInfo.panel.gameObject.SetActive(true);

                    panelInfo.panel.ShowMe();
                    callBack?.Invoke(panelInfo.panel);
                }
                return;
            }

            // 先占键再加载：加载期间再 Show/Hide 才命中同一条记录，而不是新建一条
            panelDic.Add(panelName, new PanelInfo<T>(callBack));

            GameRoot.Instance.ResMgr.LoadAsync<GameObject>("ui/Panel/" + panelName, (res) =>
            {

                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                // 落地前已被 Hide：丢弃这次结果并摘键，别再实例化
                if (panelInfo.isHide)
                {
                    panelDic.Remove(panelName);
                    return;
                }

                Transform father = GetLayerFather(layer);
                // 层级非法就兜到 Middle，否则面板会挂到 Canvas 根上盖住其他层
                if (father == null)
                    father = middleLayer;
                GameObject panelObj = GameObject.Instantiate(res, father, false);

                T panel = panelObj.GetComponent<T>();
                panel.ShowMe();
                panelInfo.callBack?.Invoke(panel);
                // 回调只发一次，发完置空不再持有外部引用
                panelInfo.callBack = null;
                panelInfo.panel = panel;

            });
        }

        public void HidePanel<T>(bool isDestory = false) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                if (panelInfo.panel == null)
                {
                    // 加载中隐藏：置 isHide 并丢掉已攒的回调，落地时整条记录会被摘掉
                    panelInfo.isHide = true;
                    panelInfo.callBack = null;
                }
                else
                {
                    panelInfo.panel.HideMe();
                    if (isDestory)
                    {
                        GameObject.Destroy(panelInfo.panel.gameObject);
                        panelDic.Remove(panelName);
                    }
                    // 不销毁只失活，下次 ShowPanel 直接激活复用
                    else
                        panelInfo.panel.gameObject.SetActive(false);
                }
            }
        }

        public void GetPanel<T>(UnityAction<T> callBack) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                if (panelInfo.panel == null)
                {
                    panelInfo.callBack += callBack;
                }
                else if (!panelInfo.isHide)// 已隐藏就不回调，调用方会一直等不到
                {
                    callBack?.Invoke(panelInfo.panel);
                }
            }
        }


        public static void AddCustomEventListener(UIBehaviour control, EventTriggerType type, UnityAction<BaseEventData> callBack)
        {
            // 复用同一个 EventTrigger：多挂一个会让同一事件按组件数量重复触发
            EventTrigger trigger = control.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = control.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = type;
            entry.callback.AddListener(callBack);

            trigger.triggers.Add(entry);
        }
    }
}
