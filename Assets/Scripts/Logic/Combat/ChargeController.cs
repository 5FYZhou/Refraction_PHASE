using Refraction.Data;
using Refraction.Foundation;
using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>蓄力阶段：None 空闲，Charging 前摇中，Charged 蓄满等冲刺释放</summary>
    public enum ChargePhase
    {
        None = 0,
        Charging,
        Charged
    }

    /// <summary>充能控制器：按下沿起蓄，走满转蓄满并保持，冲刺起手时把残影落到起手位置</summary>
    /// <remarks>不是状态组：没有基础态与抢占，阶段全靠内部计时推进，由 PlayerLogic 每物理帧驱动一次；
    /// 松手不取消，蓄满也不会自己回落，唯一的释放动作是冲刺</remarks>
    public sealed class ChargeController
    {
        private readonly PlayerConfig _config;
        private readonly Cooldown _cooldown = new();
        private readonly AfterimageLedger _afterimages = new();

        private float _now;
        private float _enterTime;
        private ChargePhase _phase;

        public ChargeController(PlayerConfig config)
        {
            _config = config;
        }

        public ChargePhase Phase => _phase;

        public AfterimageLedger Afterimages => _afterimages;

        public bool IsCharging => _phase == ChargePhase.Charging;

        public bool IsCharged => _phase == ChargePhase.Charged;

        /// <summary>0 = 没在充能，1 = 前摇走满；表现层拿它做充能染色</summary>
        public float Progress
        {
            get
            {
                if (_phase == ChargePhase.Charged) return 1f;
                if (_phase != ChargePhase.Charging || _config.chargePrepareTime <= 0f) return 0f;

                return Mathf.Clamp01((_now - _enterTime) / _config.chargePrepareTime);
            }
        }

        /// <summary>每个物理帧一次；dashStarted 只在冲刺起手那一帧为真，蓄满时才落影</summary>
        public void Tick(in LogicContext ctx, bool dashStarted)
        {
            _now = ctx.GameTime.now;
            _afterimages.Prune(_now);

            switch (_phase)
            {
                case ChargePhase.Charged:
                    if (dashStarted) Release(in ctx);
                    return;

                case ChargePhase.Charging:
                    if (_now - _enterTime >= _config.chargePrepareTime) _phase = ChargePhase.Charged;
                    return;
            }

            if (!ctx.InputSnapshot.ChargePressed) return;
            if (!_cooldown.CanUse(_now)) return;

            if (_config.chargePrepareTime <= 0f)
            {
                _phase = ChargePhase.Charged;
                return;
            }

            _enterTime = _now;
            _phase = ChargePhase.Charging;
        }

        public void ClearAfterimages() => _afterimages.Clear();

        /// <summary>冲刺起手位置落影：WorldInfo.Pos 是本帧帧首位置，正好是还没迈出去的那一点</summary>
        private void Release(in LogicContext ctx)
        {
            _afterimages.Place(
                ctx.WorldInfo.Pos,
                ctx.GameTime.now,
                _config.maxAfterimages,
                _config.afterimageDuration);

            _cooldown.MarkUsed(ctx.GameTime.now, _config.chargeCooldown);
            _phase = ChargePhase.None;
        }
    }
}
