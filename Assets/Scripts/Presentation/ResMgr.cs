using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Refraction.Presentation
{
    public abstract class ResInfoBase
    {
        // 归零才可卸载；减成负数说明 Add/Sub 没配对
        public int refCount;
    }

    public class ResInfo<T> : ResInfoBase
    {
        public T asset;
        // 异步落地时一次性补发，发完置空；同步抢先加载也走这里补发
        public UnityAction<T> callBack;
        public Coroutine coroutine;
        // 计数归零时是否当场卸载；false 就留给 UnloadUnusedAssets 统一收
        public bool isDel;


        public void AddRefCount()
        {
            ++refCount;
        }

        public void SubRefCount()
        {
            --refCount;
            if (refCount < 0)
                Debug.LogError("引用计数小于0了，请检查使用和卸载是否配对执行");
        }
    }


    public class ResMgr
    {
        // 加载中与已加载的全部记录
        private Dictionary<string, ResInfoBase> resDic = new Dictionary<string, ResInfoBase>();

        private ResMgr() { }

        /// <summary>同步加载：键 = 路径 + "_" + 类型名，所有读写点必须逐字一致，改一处就查不到</summary>
        public T Load<T>(string path) where T : UnityEngine.Object
        {
            string resName = path + "_" + typeof(T).Name;
            ResInfo<T> info;
            if (!resDic.ContainsKey(resName))
            {
                T res = Resources.Load<T>(path);
                info = new ResInfo<T>();
                info.asset = res;
                info.AddRefCount();
                resDic.Add(resName, info);
                return res;
            }
            else
            {
                info = resDic[resName] as ResInfo<T>;
                info.AddRefCount();
                // 命中正在异步加载的记录：停协程当场补上，排队中的回调必须此刻立刻补发，否则调用方永远等不到
                if (info.asset == null)
                {
                    GameRoot.Instance.StopCoroutine(info.coroutine);
                    T res = Resources.Load<T>(path);
                    info.asset = res;
                    info.callBack?.Invoke(res);
                    info.callBack = null;
                    info.coroutine = null;
                    return res;
                }
                else
                {
                    return info.asset;
                }
            }
        }

        /// <summary>异步加载：未落地先占键，回调攒在记录上等落地统一发</summary>
        public void LoadAsync<T>(string path, UnityAction<T> callBack) where T : UnityEngine.Object
        {
            string resName = path + "_" + typeof(T).Name;
            ResInfo<T> info;
            if (!resDic.ContainsKey(resName))
            {
                info = new ResInfo<T>();
                info.AddRefCount();
                resDic.Add(resName, info);
                info.callBack += callBack;
                info.coroutine = GameRoot.Instance.StartCoroutine(ReallyLoadAsync<T>(path));
            }
            else
            {
                info = resDic[resName] as ResInfo<T>;
                info.AddRefCount();
                if (info.asset == null)
                    info.callBack += callBack;
                else
                    callBack?.Invoke(info.asset);
            }
        }

        private IEnumerator ReallyLoadAsync<T>(string path) where T : UnityEngine.Object
        {
            ResourceRequest rq = Resources.LoadAsync<T>(path);
            yield return rq;

            string resName = path + "_" + typeof(T).Name;
            if (resDic.ContainsKey(resName))
            {
                ResInfo<T> resInfo = resDic[resName] as ResInfo<T>;
                resInfo.asset = rq.asset as T;

                // 落地时引用已被撤光：按 isDel 决定当场丢还是等统一回收
                if (resInfo.refCount == 0)
                    UnloadAsset<T>(path, resInfo.isDel, null, false);
                else
                {
                    resInfo.callBack?.Invoke(resInfo.asset);
                    resInfo.callBack = null;
                    resInfo.coroutine = null;
                }
            }

        }

        /// <summary>非泛型版本：键用运行时类型名，与泛型版本共用同一张表</summary>
        public void LoadAsync(string path, Type type, UnityAction<UnityEngine.Object> callBack)
        {
            string resName = path + "_" + type.Name;
            ResInfo<UnityEngine.Object> info;
            if (!resDic.ContainsKey(resName))
            {
                info = new ResInfo<UnityEngine.Object>();
                info.AddRefCount();
                resDic.Add(resName, info);
                info.callBack += callBack;
                info.coroutine = GameRoot.Instance.StartCoroutine(ReallyLoadAsync(path, type));
            }
            else
            {
                info = resDic[resName] as ResInfo<UnityEngine.Object>;
                info.AddRefCount();
                if (info.asset == null)
                    info.callBack += callBack;
                else
                    callBack?.Invoke(info.asset);
            }
        }

        private IEnumerator ReallyLoadAsync(string path, Type type)
        {
            ResourceRequest rq = Resources.LoadAsync(path, type);
            yield return rq;

            string resName = path + "_" + type.Name;
            if (resDic.ContainsKey(resName))
            {
                ResInfo<UnityEngine.Object> resInfo = resDic[resName] as ResInfo<UnityEngine.Object>;
                resInfo.asset = rq.asset;
                // 落地时引用已被撤光：按 isDel 决定当场丢还是等统一回收
                if (resInfo.refCount == 0)
                    UnloadAsset(path, type, resInfo.isDel, null, false);
                else
                {
                    resInfo.callBack?.Invoke(resInfo.asset);
                    resInfo.callBack = null;
                    resInfo.coroutine = null;
                }
            }
        }

        /// <summary>引用计数 -1；只有归零且 isDel 才当场卸载</summary>
        public void UnloadAsset<T>(string path, bool isDel = false, UnityAction<T> callBack = null, bool isSub = true)
        {
            string resName = path + "_" + typeof(T).Name;
            if (resDic.ContainsKey(resName))
            {
                ResInfo<T> resInfo = resDic[resName] as ResInfo<T>;
                if (isSub)
                    resInfo.SubRefCount();
                resInfo.isDel = isDel;
                if (resInfo.asset != null && resInfo.refCount == 0 && resInfo.isDel)
                {
                    resDic.Remove(resName);
                    Resources.UnloadAsset(resInfo.asset as UnityEngine.Object);
                }
                else if (resInfo.asset == null)// 还在异步加载中
                {
                    // 只摘回调、别 Remove：协程落地后还要按这个键回来查表
                    if (callBack != null)
                        resInfo.callBack -= callBack;

                }
            }
        }

        public void UnloadAsset(string path, Type type, bool isDel = false, UnityAction<UnityEngine.Object> callBack = null, bool isSub = true)
        {
            string resName = path + "_" + type.Name;
            if (resDic.ContainsKey(resName))
            {
                ResInfo<UnityEngine.Object> resInfo = resDic[resName] as ResInfo<UnityEngine.Object>;
                if (isSub)
                    resInfo.SubRefCount();
                resInfo.isDel = isDel;
                if (resInfo.asset != null && resInfo.refCount == 0 && resInfo.isDel)
                {
                    resDic.Remove(resName);
                    Resources.UnloadAsset(resInfo.asset);
                }
                else if (resInfo.asset == null)// 还在异步加载中
                {
                    // 只摘回调、别 Remove：协程落地后还要按这个键回来查表
                    if (callBack != null)
                        resInfo.callBack -= callBack;
                }
            }
        }

        public void UnloadUnusedAssets(UnityAction callBack)
        {
            GameRoot.Instance.StartCoroutine(ReallyUnloadUnusedAssets(callBack));
        }

        private IEnumerator ReallyUnloadUnusedAssets(UnityAction callBack)
        {
            // 先把自己的表摘干净再交给 Resources：表里存的是强引用，留着就永远算"被使用"
            List<string> list = new List<string>();
            foreach (string path in resDic.Keys)
            {
                if (resDic[path].refCount == 0)
                    list.Add(path);
            }
            foreach (string path in list)
            {
                resDic.Remove(path);
            }

            AsyncOperation ao = Resources.UnloadUnusedAssets();
            yield return ao;
            callBack();
        }

        public int GetRefCount<T>(string path)
        {
            string resName = path + "_" + typeof(T).Name;
            if (resDic.ContainsKey(resName))
            {
                return (resDic[resName] as ResInfo<T>).refCount;
            }
            return 0;
        }


        public void ClearDic(UnityAction callBack)
        {
            GameRoot.Instance.StartCoroutine(ReallyClearDic(callBack));
        }

        private IEnumerator ReallyClearDic(UnityAction callBack)
        {
            resDic.Clear();
            AsyncOperation ao = Resources.UnloadUnusedAssets();
            yield return ao;
            callBack();
        }
    }
}
