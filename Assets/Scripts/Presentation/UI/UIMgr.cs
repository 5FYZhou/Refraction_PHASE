using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Presentation.UI;
using Refraction.Presentation;
using UnityEngine.TextCore.Text;

namespace Refraction.Presentation.UI
{
    public enum E_UILayer
    {
        Scene,
        Bottom,
        Middle,
        Top,
        System,
    }

    /// 预制体名必须等于面板类名（按 typeof(T).Name 找）
    public class UIMgr
    {
        private abstract class BasePanelInfo
        {
            public bool isHide;
            public abstract BasePanel Panel { get; }
            public bool CanBeHide => Panel != null && Panel.CanBeHideByKey;

            public abstract void Hide(bool isDestroy);
        }

        private class PanelInfo<T> : BasePanelInfo where T : BasePanel
        {
            public T panel;
            public UnityAction<T> callBack;
            public E_UILayer Layer => panel.Layer;

            private readonly UIMgr _owner;

            public PanelInfo(UIMgr owner, UnityAction<T> callBack)
            {
                _owner = owner;
                this.callBack += callBack;
            }

            public override BasePanel Panel => panel;

            public override void Hide(bool isDestroy)
            {
                _owner.HidePanel<T>();
            }
        }


        public Camera uiCamera;
        private Canvas uiCanvas;
        private EventSystem uiEventSystem;


        public bool IsReady { get; private set; }


        private bool _warnedDuplicateEventSystem;

        private Transform transitionLayer;
        private Transform bottomLayer;
        private Transform middleLayer;
        private Transform topLayer;
        private Transform systemLayer;

        // 资源 Key = Resources 相对路径，不带扩展名
        private const string UI_CAMERA_KEY = "ui/UICamera";
        private const string UI_CANVAS_KEY = "ui/Canvas";
        private const string UI_EVENT_SYS_KEY = "ui/EventSystem";
        private const string UI_PANEL_PREFIX = "ui/Panel/";

        private readonly Dictionary<string, BasePanelInfo> panelDic = new Dictionary<string, BasePanelInfo>();

        // 整栈倒出暂存，避免每次分配
        private readonly List<BasePanelInfo> _stackScratch = new();

        private readonly Dictionary<E_UILayer, Stack<BasePanelInfo>> openPanels = new()
        {
            { E_UILayer.Scene, new Stack<BasePanelInfo>() },
            { E_UILayer.Bottom, new Stack<BasePanelInfo>() },
            { E_UILayer.Middle, new Stack<BasePanelInfo>() },
            { E_UILayer.Top, new Stack<BasePanelInfo>() },
            { E_UILayer.System, new Stack<BasePanelInfo>() }
        };

        public UIMgr() { }

        /// 装配三件套，唯一调用点 GameRoot
        public void Init()
        {
            if (IsReady)
            {
                Debug.LogError("[UI] UIMgr.Init 被调用了两次：它只该由 GameRoot 调一次。");
                return;
            }

            uiCamera = GameObject.Instantiate(GameRoot.Instance.ResMgr.Load<GameObject>(UI_CAMERA_KEY)).GetComponent<Camera>();
            GameObject.DontDestroyOnLoad(uiCamera.gameObject);

            uiCanvas = GameObject.Instantiate(GameRoot.Instance.ResMgr.Load<GameObject>(UI_CANVAS_KEY)).GetComponent<Canvas>();
            uiCanvas.worldCamera = uiCamera;
            GameObject.DontDestroyOnLoad(uiCanvas.gameObject);

            transitionLayer = uiCanvas.transform.Find("Transition");
            bottomLayer = uiCanvas.transform.Find("Bottom");
            middleLayer = uiCanvas.transform.Find("Middle");
            topLayer = uiCanvas.transform.Find("Top");
            systemLayer = uiCanvas.transform.Find("System");

            DisableSceneEventSystems();

            uiEventSystem = GameObject.Instantiate(GameRoot.Instance.ResMgr.Load<GameObject>(UI_EVENT_SYS_KEY)).GetComponent<EventSystem>();
            GameObject.DontDestroyOnLoad(uiEventSystem.gameObject);

            IsReady = true;
        }

        /// 拆除三件套，幂等
        public void Dispose()
        {
            if (!IsReady) return;

            IsReady = false;

            foreach (KeyValuePair<string, BasePanelInfo> kv in panelDic)
            {
                //AssetModule.Release(UI_PANEL_PREFIX + kv.Key);
            }

            panelDic.Clear();

            foreach (Stack<BasePanelInfo> stack in openPanels.Values)
            {
                stack.Clear();
            }

            if (uiEventSystem != null) GameObject.Destroy(uiEventSystem.gameObject);
            if (uiCanvas != null) GameObject.Destroy(uiCanvas.gameObject);
            if (uiCamera != null) GameObject.Destroy(uiCamera.gameObject);

            uiEventSystem = null;
            uiCanvas = null;
            uiCamera = null;
            bottomLayer = null;
            middleLayer = null;
            topLayer = null;
            systemLayer = null;
        }


        /// 必须 includeInactive，否则失活那份漏掉、激活后警告又回来
        private void DisableSceneEventSystems()
        {
            // 必须在实例化 EventSystem 之前
            EventSystem[] sceneEventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>(true);

            foreach (EventSystem sceneEventSystem in sceneEventSystems)
            {
                if (sceneEventSystem == uiEventSystem)
                    continue;

                sceneEventSystem.gameObject.SetActive(false);

                if (_warnedDuplicateEventSystem)
                    continue;

                _warnedDuplicateEventSystem = true;
                Debug.LogError(
                    "[UI] 场景里已经有一份 EventSystem（以及建 UI 文本时 Unity 自动生成的 Canvas），" +
                    "UIMgr 已停用场景那份、改用自己从 Resources 创建并跨场景常驻的那一份；" +
                    "否则 Unity 会每帧刷一条「There are 2 event systems in the scene.」。\n" +
                    "清理方法：在 Hierarchy 里删掉场景自带的 EventSystem 与那个自动生成的 Canvas —— " +
                    "UI 的 Camera / Canvas / EventSystem 三件套由 UIMgr 从 Assets/Resources/ui/ 自动创建，" +
                    "场景里不需要第二份。\n" +
                    "这条只报一次（本局不会再刷）。",
                    sceneEventSystem);
            }
        }

        public Transform GetLayerFather(E_UILayer layer)
        {
            switch (layer)
            {
                case E_UILayer.Scene:
                    Debug.LogWarning("场景面板不改走查找父层级");
                    return null;
                case E_UILayer.Bottom:
                    return bottomLayer;
                case E_UILayer.Middle:
                    return middleLayer;
                case E_UILayer.Top:
                    return topLayer;
                case E_UILayer.System:
                    return systemLayer;
                default:
                    return null;
            }
        }

        /// <summary>场景面板自注册：UIMgr 只登记不实例化</summary>
        public void RegisterPanel<T>(T panel) where T : BasePanel
        {
            if (panel == null) return;

            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName)) return;

            PanelInfo<T> info = new(this, null) { panel = panel };
            panelDic.Add(panelName, info);
            openPanels[panel.Layer].Push(info);
        }

        /// <summary>场景销毁时注销，否则同名牌会拿到已销毁实例</summary>
        public void UnregisterPanel(BasePanel panel)
        {
            if (panel == null) return;

            string key = null;
            BasePanelInfo target = null;

            foreach (KeyValuePair<string, BasePanelInfo> kv in panelDic)
            {
                if (!ReferenceEquals(kv.Value.Panel, panel)) continue;

                key = kv.Key;
                target = kv.Value;
                break;
            }

            if (key == null) return;

            panelDic.Remove(key);
            RemoveFromOpenStacks(target);
        }

        /// <summary>Stack 不支持按项删除：整栈倒出再压回</summary>
        private void RemoveFromOpenStacks(BasePanelInfo target)
        {
            foreach (Stack<BasePanelInfo> stack in openPanels.Values)
            {
                if (stack.Count == 0) continue;

                _stackScratch.Clear();
                while (stack.Count > 0) _stackScratch.Add(stack.Pop());

                for (int i = _stackScratch.Count - 1; i >= 0; i--)
                {
                    if (!ReferenceEquals(_stackScratch[i], target)) stack.Push(_stackScratch[i]);
                }
            }
        }

        // ShowPanel 对已开面板会重复压栈
        public bool IsPanelOpen<T>() where T : BasePanel
        {
            string panelName = typeof(T).Name;

            return panelDic.TryGetValue(panelName, out BasePanelInfo info)
                && info.Panel != null
                && !info.isHide;
        }

        public void ShowPanel<T>(UnityAction<T> callBack = null) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                ClearUISelection();
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                if (panelInfo.panel == null)
                {
                    panelInfo.isHide = false;

                    if (callBack != null)
                        panelInfo.callBack += callBack;
                }
                else
                {
                    if (!panelInfo.panel.gameObject.activeSelf)
                        panelInfo.panel.gameObject.SetActive(true);

                    panelInfo.panel.gameObject.transform.SetAsLastSibling();
                    panelInfo.panel.ShowMe();
                    panelInfo.isHide = false;
                    callBack?.Invoke(panelInfo.panel);
                    openPanels[panelInfo.Layer].Push(panelInfo);
                }
                return;
            }

            panelDic.Add(panelName, new PanelInfo<T>(this, callBack));

            // 协程必须由常驻的 GameRoot 托管
            GameRoot.Instance.ResMgr.LoadAsync<GameObject>("ui/Panel/" + panelName, (res) =>
            {

                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                // 落地前已被 Hide：丢弃结果并摘键
                if (panelInfo.isHide)
                {
                    panelDic.Remove(panelName);
                    return;
                }

                Transform father = GetLayerFather(panelInfo.Layer);
                // 层级非法就兜到 Middle，否则会挂到 Canvas 根上盖住其他层
                if (father == null)
                    father = middleLayer;
                GameObject panelObj = GameObject.Instantiate(res, father, false);

                T panel = panelObj.GetComponent<T>();
                panel.ShowMe();
                panelInfo.callBack?.Invoke(panel);
                // 回调只发一次
                panelInfo.callBack = null;
                panelInfo.panel = panel;

            });
        }

        public void HidePanel<T>(bool isDestory = false) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                ClearUISelection();
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                if (panelInfo.panel == null)
                {
                    panelInfo.isHide = true;
                    panelInfo.callBack = null;
                }
                else
                {
                    if (panelInfo.isHide)
                        return;

                    panelInfo.isHide = true;
                    if (isDestory)
                    {
                        GameObject.Destroy(panelInfo.panel.gameObject);
                        panelDic.Remove(panelName);
                    }
                    else
                        panelInfo.panel.gameObject.SetActive(false);
                    panelInfo.panel.HideMe();
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
                else if (!panelInfo.isHide)
                {
                    callBack?.Invoke(panelInfo.panel);
                }
            }
            else
            {
                Debug.LogWarning($"面板{typeof(T)}还未被加载");
            }
        }


        public static void AddCustomEventListener(UIBehaviour control, EventTriggerType type, UnityAction<BaseEventData> callBack)
        {
            EventTrigger trigger = control.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = control.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = type;
            entry.callback.AddListener(callBack);

            trigger.triggers.Add(entry);
        }


        public bool TryCloseTopmostPanel()
        {
            // 场景面板不可被关闭
            E_UILayer[] layers = { E_UILayer.System, E_UILayer.Top, E_UILayer.Middle, E_UILayer.Bottom };
            foreach (var layer in layers)
            {
                var stack = openPanels[layer];

                while (stack.Count > 0)
                {
                    BasePanelInfo info = stack.Peek();
                    if (info.Panel == null || info.isHide)
                    {
                        stack.Pop();
                        continue;
                    }

                    if (!info.Panel.CanBeHideByKey)
                    {
                        return false;
                    }
                    stack.Pop();
                    info.Hide(false);
                    return true;
                }
            }
            return false;
        }

        public void CloseAllPanels()
        {
            foreach (Stack<BasePanelInfo> stack in openPanels.Values)
            {
                while (stack.Count > 0)
                {
                    BasePanelInfo info = stack.Pop();

                    if (info.Panel == null || info.isHide)
                        continue;

                    info.Hide(false);
                }
            }
        }

        private void ClearUISelection()
        {
            uiEventSystem?.SetSelectedGameObject(null);
        }


        private void SetUIInputEnabled(bool enabled)
        {
            if (uiEventSystem == null) return;

            // 关闭输入模块：按钮、滑条、点击拖拽全都不派发
            var module = uiEventSystem.GetComponent<BaseInputModule>();
            if (module != null)
            {
                if (module.enabled != enabled)
                    module.enabled = enabled;
            }

            if (!enabled) uiEventSystem.SetSelectedGameObject(null);
        }

    }
}
