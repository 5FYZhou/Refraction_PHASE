using System;
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

        private bool _loading;
        private int _pendingDoorId;
        private MovementStateTag _pendingEntryTag;
        private int _pendingEntryFace;

        public LevelManager(int id, IDoorReader doorReader = null)
        {
            _curId = id;
            _doorReader = doorReader;

            if (id <= 0)
            {
                Debug.LogError($"[LevelManager] 当前关卡 id 非法（{id}），无法推进到下一关。检查关卡场景名是否为 Level_正整数。");
            }
        }

        /// <summary>上一次成功发起的切场景请求里，目标场景指定的门 id；0 表示没指定。</summary>
        public int PendingDoorId => _pendingDoorId;

        public MovementStateTag PendingEntryTag => _pendingEntryTag;

        /// <summary>玩家触发那扇门时的朝向（-1 左 / 1 右）。</summary>
        public int PendingEntryFace => _pendingEntryFace;

        public void Init()
        {
            EventBus<RequestNextDoor>.Subscribe(OnRequestNextLevel);
        }

        public void Dispose()
        {
            EventBus<RequestNextDoor>.Unsubscribe(OnRequestNextLevel);
            _loading = false;
            _pendingDoorId = 0;
            _pendingEntryTag = MovementStateTag.None;
            _pendingEntryFace = 0;
        }

        private void OnRequestNextLevel(RequestNextDoor evt)
        {
            // 玩家身上多个碰撞体会让门触发连续进来多次，用 _loading 挡住，避免同一帧多次 LoadScene
            if (_loading) return;

            // 门可以直接指定任意目标关；没指定才退回命名约定里的下一关
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

            _loading = true;
            _pendingDoorId = evt.nextDoor;
            _pendingEntryTag = evt.enterTag;
            _pendingEntryFace = evt.enterFace;

            try
            {
                SceneManager.LoadScene(nextName);
            }
            catch (Exception ex)
            {
                // 加载失败要放开 _loading 与落点，否则这扇门以及后面所有门都会永久失效
                _loading = false;
                _pendingDoorId = 0;
                _pendingEntryFace = 0;
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
