using System;
using System.Collections.Generic;
using Refraction.Logic.Actor;
using Refraction.Logic.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Refraction.Logic.Level
{
    /// <summary>关卡推进：收到 RequestNextDoor 后加载门指定的场景，门没指定则按 SceneName 的命名约定推导下一关，场景不存在只报错并留在本关</summary>
    /// <remarks>目标就是当前关时不重载，改为把玩家传送到本场景里 nextDoor 指定的门；门查询全部委托给 IDoorReader，逻辑层不认识 Door</remarks>
    public class LevelManager
    {
        private readonly int _curId;
        private readonly IDoorReader _doorReader;
        private readonly ISceneTransfer _transfer;
        private readonly List<IReverse> _reverses = new();
        private readonly WorldPolarity _initialWorld;

        private WorldPolarity _world = WorldPolarity.White;

        private bool _loading;

        public LevelManager(int id, IDoorReader doorReader = null, WorldPolarity world = WorldPolarity.White, ISceneTransfer transfer = null)
        {
            _curId = id;
            _doorReader = doorReader;
            _transfer = transfer;
            _initialWorld = world;
            _world = world;

            if (id <= 0)
            {
                Debug.LogError($"[LevelManager] 当前关卡 id 非法（{id}），无法推进到下一关。检查关卡场景名是否为 Level_正整数。");
            }
        }

        /// <summary>当前世界极性：跨关保留，进入关卡时由常驻根给定</summary>
        public WorldPolarity World => _world;

        public bool IsBlackWorld => _world == WorldPolarity.Black;

        public void Init()
        {
            EventBus<RequestNextDoor>.Subscribe(OnRequestNextLevel);
            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);

            EventBus<WorldPolarityChanged>.Publish(new(_world));
        }

        public void Dispose()
        {
            EventBus<RequestNextDoor>.Unsubscribe(OnRequestNextLevel);
            EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);
            _reverses.Clear();
            _world = _initialWorld;
            _loading = false;
        }

        /// <summary>登记要随世界换色的对象，登记时就套一次当前极性</summary>
        public void RegisterReverse(IReverse reverse)
        {
            if (reverse == null || _reverses.Contains(reverse)) return;

            _reverses.Add(reverse);
            reverse.Apply(_world);
        }

        private void OnWorldPolarityChanged(WorldPolarityChanged evt)
        {
            _world = evt.World;

            for (int i = 0; i < _reverses.Count; i++)
            {
                try
                {
                    _reverses[i].Apply(_world);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void OnRequestNextLevel(RequestNextDoor evt)
        {
            // 玩家身上多个碰撞体会让门触发连续进来多次，用 _loading 挡住，避免同一帧多次 LoadScene
            if (_loading) return;

            int nextId;

            if (evt.nextLevel > 0)
            {
                nextId = evt.nextLevel;
            }
            else if (_curId > 0)
            {
                nextId = _curId + 1;
            }
            else
            {
                Debug.LogError($"[LevelManager] 当前关卡 id 与门指定的目标关卡都非法（当前 {_curId}，门指定 {evt.nextLevel}），无法推进。");
                return;
            }

            // 目标就是当前关：不重载，直接挪到指定的门——重载会丢光本关状态，且本帧内根本看不到效果
            if (nextId == _curId)
            {
                TeleportToDoor(evt.nextDoor, evt.enterTag, evt.enterFace);
                return;
            }

            string nextName = SceneName.GetLevelName(nextId);

            if (!Application.CanStreamedLevelBeLoaded(nextName))
            {
                Debug.LogError(
                    $"[LevelManager] 没有下一关场景 \"{nextName}\"（当前关卡 id = {_curId}）。" +
                    "确认场景已按 Level_正整数 命名并加入 Build Settings。");
                return;
            }

            // 目标门要由常驻的 GameRoot 带走：本对象随旧场景销毁，加载完成时再也问不到
            if (_transfer == null)
            {
                Debug.LogError($"[LevelManager] 没有跨场景通道（ISceneTransfer），无法把目标门带进 \"{nextName}\"，本次不加载。");
                return;
            }

            _loading = true;

            _transfer.RequestLevelLoad(evt.nextDoor, evt.enterTag, evt.enterFace);

            try
            {
                SceneManager.LoadScene(nextName);
            }
            catch (Exception ex)
            {
                // 加载失败要放开 _loading，否则这扇门以及后面所有门都会永久失效
                _loading = false;
                Debug.LogError($"[LevelManager] 加载场景 \"{nextName}\" 失败：{ex.Message}");
            }
        }

        /// <summary>同关内的门对门传送：用 RequestPlayerPosChange 发出去而不是直接抓玩家，逻辑层不认识玩家实体</summary>
        private void TeleportToDoor(int doorId, MovementStateTag enterTag, int enterFace)
        {
            // 门指向本关却没给目标门（旧数据里 nextDoor 普遍是 0）：宁可报错也别把玩家丢到 (0,0)
            if (doorId <= 0)
            {
                Debug.LogError($"[LevelManager] 门指向当前关（id = {_curId}），但没有指定目标门（nextDoor = {doorId}），玩家原地不动。");
                return;
            }

            if (_doorReader == null)
            {
                Debug.LogError("[LevelManager] 装配时没有提供门读取器（IDoorReader），无法在本关内传送。");
                return;
            }

            // 关卡 id + 门 id 才唯一确定一扇门：读取器绑在别的关卡时，同号的门其实是另一扇，按 id 落过去就是错的位置
            if (_doorReader.LevelId != _curId)
            {
                Debug.LogError($"[LevelManager] 门读取器绑定在关卡 {_doorReader.LevelId}，当前关卡是 {_curId}，拒绝按门 id 传送。");
                return;
            }

            if (!_doorReader.TryGetEntry(doorId, out DoorEntry entry))
            {
                Debug.LogError($"[LevelManager] 关卡 {_curId} 里找不到 id = {doorId} 的门，玩家原地不动。");
                return;
            }

            EventBus<RequestPlayerPosChange>.Publish(new RequestPlayerPosChange(entry, enterTag, enterFace));
        }
    }
}
