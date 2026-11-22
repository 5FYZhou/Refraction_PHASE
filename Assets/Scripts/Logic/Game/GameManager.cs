using System.Collections.Generic;
using Refraction.Logic.Events;
using Refraction.Logic.Service;
using Refraction.Logic;
using UnityEngine;

namespace Refraction.Logic.Game
{
    /// <summary>游戏状态机：底座互斥，Paused 叠加其上；切状态连带切面板、发暂停/恢复意图，是唯一入口。面板走注入的 UIMgr，本类不认识 GameRoot。</summary>
    public sealed class GameManager
    {
        private readonly List<GameState> _overlays = new(2);
        private readonly PauseService _pauseService;

        private GameState _base = GameState.None;
        private GameState _fallbackBase;
        private GameState _sceneTransitionTargetState;
        private bool _hasSceneTransitionTarget;
        private bool _sceneTransitionFadeFinished;
        private bool _sceneTransitionFailed;

        public GameManager(PauseService pauseService)
        {
            _pauseService = pauseService;
        }

        // 投影：转场 > 栈顶叠加层 > 底座
        public GameState CurState => _base == GameState.SceneTransition
            ? GameState.SceneTransition
            : _overlays.Count > 0 ? _overlays[_overlays.Count - 1] : _base;

        private bool IsInTransition => _base == GameState.SceneTransition;

        public bool IsSceneBusy => IsInTransition;

        public void RequestState(GameState state)
        {
            ChangeState(state);
        }

        public void ChangeState(GameState newState)
        {
            if (IsInTransition)
            {
                Debug.LogError($"[Game] 场景转场期间拒绝直接切换到状态 {newState}。");
                return;
            }

            if (IsOverlay(newState))
            {
                PushOverlay(newState);
                return;
            }

            if (newState == GameState.None)
            {
                Debug.LogError("[Game] None 是未初始化值，不能作为运行时状态。");
                return;
            }

            if (newState == GameState.SceneTransition)
            {
                Debug.LogError("[Game] 转场只能经 BeginSceneTransition 进入。");
                return;
            }

            ApplyBase(newState, true);
        }

        private bool IsOverlay(GameState state)
        {
            return state == GameState.Paused;
        }

        // 转场期间或与栈顶同态时返回 false
        public bool PushOverlay(GameState state)
        {
            if (!IsOverlay(state))
            {
                Debug.LogError($"[Game] {state} 不是叠加层状态。");
                return false;
            }

            if (IsInTransition || _overlays.Contains(state)) return false;

            GameState before = CurState;
            _overlays.Add(state);
            if (FlagsOf(state).freezes) _pauseService.PushFreeze(state);
            PublishIfChanged(before);
            return true;
        }

        // 转场期间、空栈或栈顶与 expected 不符时返回 false
        public bool PopOverlay(GameState expected)
        {
            if (IsInTransition || _overlays.Count == 0) return false;
            if (_overlays[_overlays.Count - 1] != expected) return false;

            GameState before = CurState;
            _overlays.RemoveAt(_overlays.Count - 1);
            _pauseService.PopFreeze(expected);
            PublishIfChanged(before);
            return true;
        }

        public void Resume()
        {
            PopOverlay(GameState.Paused);
        }

        public void BeginSceneTransition()
        {
            if (IsInTransition) return;

            _fallbackBase = _base;
            _hasSceneTransitionTarget = false;
            _sceneTransitionFadeFinished = false;
            _sceneTransitionFailed = false;
            ApplyBase(GameState.SceneTransition, false);
        }

        public void SetSceneTransitionTargetState(GameState targetState)
        {
            if (targetState == GameState.None
                || targetState == GameState.Exit
                || targetState == GameState.SceneTransition)
            {
                Debug.LogError($"[Game] 场景入口状态无效：{targetState}。");
                return;
            }

            if (!IsInTransition)
            {
                ApplyEntry(targetState);
                return;
            }

            _sceneTransitionTargetState = targetState;
            _hasSceneTransitionTarget = true;
            TryFinishSceneTransition();
        }

        // 遮罩未淡出时供 UI 预摆入口面板
        public bool TryGetPendingSceneEntryState(out GameState state)
        {
            state = _sceneTransitionTargetState;

            return IsInTransition && _hasSceneTransitionTarget;
        }

        public void FinishSceneTransition(bool success)
        {
            if (!IsInTransition) return;

            _sceneTransitionFailed = !success;
            _sceneTransitionFadeFinished = true;
            TryFinishSceneTransition();
        }

        private void ApplyBase(GameState state, bool clearOverlays)
        {
            GameState before = CurState;

            _pauseService.PopFreeze(_base);
            if (clearOverlays) ClearOverlayFreezes();

            _base = state;
            if (clearOverlays) _overlays.Clear();

            if (FlagsOf(state).freezes) _pauseService.PushFreeze(state);

            PublishIfChanged(before);
        }

        // 入口 Paused 落成 Running 底座 + Paused 层
        private void ApplyEntry(GameState state)
        {
            GameState before = CurState;

            _pauseService.PopFreeze(_base);
            ClearOverlayFreezes();

            _base = state == GameState.Paused ? GameState.Running : state;
            _overlays.Clear();

            if (FlagsOf(_base).freezes) _pauseService.PushFreeze(_base);

            if (state == GameState.Paused)
            {
                _overlays.Add(GameState.Paused);
                _pauseService.PushFreeze(GameState.Paused);
            }

            PublishIfChanged(before);
        }

        private void TryFinishSceneTransition()
        {
            if (!_sceneTransitionFadeFinished) return;

            bool failed = _sceneTransitionFailed;
            GameState targetState;

            if (failed)
            {
                targetState = _fallbackBase;
            }
            else
            {
                if (!_hasSceneTransitionTarget) return;

                targetState = _sceneTransitionTargetState;
            }

            _hasSceneTransitionTarget = false;
            _sceneTransitionFadeFinished = false;
            _sceneTransitionFailed = false;

            // 失败只回滚底座，覆盖层原样保留
            if (failed)
            {
                ApplyBase(targetState, false);
                return;
            }

            ApplyEntry(targetState);
        }

        private void ClearOverlayFreezes()
        {
            for (int i = 0; i < _overlays.Count; i++)
            {
                _pauseService.PopFreeze(_overlays[i]);
            }
        }

        private static (bool inputAllowed, bool freezes) FlagsOf(GameState state)
        {
            switch (state)
            {
                case GameState.Running:
                    return (true, false);
                case GameState.Menu:
                case GameState.Paused:
                case GameState.SceneTransition:
                    return (false, true);
                default:
                    Debug.LogError($"[Game] 状态 {state} 未声明输入与冻结口径，按禁输入+冻结处理。");
                    return (false, true);
            }
        }

        private void PublishIfChanged(GameState before)
        {
            GameState now = CurState;

            if (now == before) return;

            (bool inputAllowed, bool _) = FlagsOf(now);

            EventBus<GameStateChanged>.Publish(new GameStateChanged(now, inputAllowed, IsInTransition, now == GameState.Paused));
        }
    }
}
