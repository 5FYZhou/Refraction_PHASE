using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>弹反范围：黑形态按住攻击键就开，松键或到 reboundMaxTime 即关，关掉当帧起算 reboundCooldown</summary>
    /// <remarks>每帧都调 ShowRebound（不只在换向时），表现层要跟着转身改镜像，靠它自己的等值判断省掉重复写 transform</remarks>
    public sealed class ReboundGroup
    {
        private readonly PlayerConfig _config;
        private readonly IReboundView _view;
        private readonly Cooldown _cooldown = new();

        private float _closeAt;

        /// <summary>本轮按住已经用过一次：按住不放跨过冷却也不自动重开，必须松手再按</summary>
        private bool _spent;

        public ReboundGroup(PlayerConfig config, IReboundView view)
        {
            _config = config;
            _view = view;
        }

        public bool IsOpen { get; private set; }

        public BlackAttackKind Kind { get; private set; }

        public int Side { get; private set; }

        public void Tick(in LogicContext ctx, bool black, bool crouching = false)
        {
            if (!ctx.InputSnapshot.AttackHeld)
            {
                _spent = false;

                if (IsOpen) Close(in ctx);

                return;
            }

            if (IsOpen)
            {
                if (!black || ctx.GameTime.now >= _closeAt) Close(in ctx);
                else Show(in ctx, crouching);

                return;
            }

            if (!black || _spent) return;
            if (!_cooldown.CanUse(ctx.GameTime.now)) return;

            _closeAt = _config.reboundMaxTime > 0f
                ? ctx.GameTime.now + _config.reboundMaxTime
                : float.PositiveInfinity;

            _spent = true;
            IsOpen = true;

            Show(in ctx, crouching);
        }

        private void Show(in LogicContext ctx, bool crouching)
        {
            Kind = KindOf(ctx.InputSnapshot.Move, crouching);
            Side = SideOf(Kind, in ctx);

            _view?.ShowRebound(Kind, Side);
        }

        private void Close(in LogicContext ctx)
        {
            IsOpen = false;

            _cooldown.MarkUsed(ctx.GameTime.now, _config.reboundCooldown);
            _view?.HideRebound();
        }

        private static BlackAttackKind KindOf(Vector2 move, bool crouching)
        {
            if (move.y > 0.5f) return BlackAttackKind.Up;
            if (move.y < -0.5f) return BlackAttackKind.Down;

            return crouching ? BlackAttackKind.Level : BlackAttackKind.Ahead;
        }

        /// <summary>斜向（W / S）的左右跟着当前速度走，不看朝向；静止才回落到朝向</summary>
        private static int SideOf(BlackAttackKind kind, in LogicContext ctx)
        {
            if (kind == BlackAttackKind.Ahead || kind == BlackAttackKind.Level)
            {
                if (ctx.InputSnapshot.Move.x < -0.5f) return -1;
                if (ctx.InputSnapshot.Move.x > 0.5f) return 1;

                return ctx.WorldInfo.Face;
            }

            float vx = ctx.WorldInfo.FrameStartVelocity.x;

            if (Mathf.Abs(vx) > 0.01f) return vx < 0f ? -1 : 1;

            return ctx.WorldInfo.Face;
        }
    }
}
