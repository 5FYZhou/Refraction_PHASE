using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>Boss mmfly：黑白两套技能池 + 决策 / 观察 / 逼近循环，状态推进交给 MmflyMoveGroup</summary>
    /// <remarks>没有巡逻也没有脱战：进战斗靠视线触发器，进入后不再退出；技能冷却全从起手那一帧算</remarks>
    public sealed class MmflyLogic : EnemyLogicBase
    {
        private const float FaceDeadZone = 0.05f;
        private const float DirEps = 1e-6f;

        private readonly MmflyConfig _config;
        private readonly MmflyRange _range;
        private readonly MmflyMoveGroup _moveGroup;
        private readonly Cooldown _diveCooldown = new();
        private readonly Cooldown _barrageCooldown = new();
        private readonly Cooldown _summonCooldown = new();
        private readonly Cooldown _reboundCooldown = new();
        private readonly System.Random _rng = new();
        private readonly Vector2 _muzzle;

        private EnemyTargetInfo _target;
        private IMinionHost _minions;
        private bool _engaged;
        private MmflyStateTag _pending;

        public MmflyLogic(
            IMoveMotor motor,
            MmflyConfig config,
            IHurtView hurtView,
            bool black = false,
            bool aiEnabled = true,
            in MmflyRange range = default,
            Vector2 muzzle = default)
            : base(motor, config, hurtView, aiEnabled, black)
        {
            _config = config;
            _range = range;
            _muzzle = muzzle;
            _moveGroup = new MmflyMoveGroup(this, motor, config);
        }

        public MmflyStateTag CurrentTag => _moveGroup.Current;

        /// <summary>当前技能走到哪一段：非技能态为 None，表现层只读它转视角</summary>
        public MmflySkillPhase Phase => _moveGroup.Phase;

        /// <summary>进过战斗区就再也不退出去</summary>
        public bool Engaged => _engaged;

        public override bool Alert => _engaged;

        /// <summary>二阶段：血量掉到阈值比例以下，决策更快、逼近更凶</summary>
        public bool Phase2 => CurHealth <= MaxHealth * _config.phase2HealthRatio;

        public float DecideInterval => _config.decideInterval * (Phase2 ? _config.phase2DecideScale : 1f);

        public float ApproachSpeed => _config.approachSpeed * (Phase2 ? _config.phase2ApproachScale : 1f);

        public float IdealDistance => _range.Ideal;

        public float ObserveDriftSpeed => _config.observeDriftSpeed;

        public float ApproachSway => _config.approachSway;

        /// <summary>下一个要切的状态：决策 / 观察结束时写入，状态组取走后清零</summary>
        public void SetPending(MmflyStateTag tag) => _pending = tag;

        public MmflyStateTag TakePending()
        {
            MmflyStateTag tag = _pending;
            _pending = MmflyStateTag.None;

            return tag;
        }

        /// <summary>技能前摇转身体跟人：俯冲蓄力与弹幕前摇，锁定后由表现层保持那个角</summary>
        public bool AimAtTarget =>
            Phase == MmflySkillPhase.Windup && (CurrentTag == MmflyStateTag.Dive || CurrentTag == MmflyStateTag.Barrage);

        /// <summary>攻击段：方向已在前摇结束那一帧锁死，表现层停止跟人</summary>
        public bool AimLocked =>
            Phase == MmflySkillPhase.Strike && (CurrentTag == MmflyStateTag.Dive || CurrentTag == MmflyStateTag.Barrage);

        /// <summary>召唤前摇：表现层据它抖动 + 颜色闪烁</summary>
        public bool SummonWarning => CurrentTag == MmflyStateTag.Summon && Phase == MmflySkillPhase.Windup;

        public Vector2 DiveDir => _moveGroup.DiveDir;

        /// <summary>转向要看的点：优先受击盒中心，缺了退回玩家枢轴</summary>
        public Vector2 AimPoint => _target.HalfSize.sqrMagnitude > 0f ? _target.BodyCenter : TargetPos;

        public override bool ChargingShot =>
            !Black && CurrentTag == MmflyStateTag.Barrage && Phase == MmflySkillPhase.Windup;

        public override float ChargingTime => _config.barrageWindupTime;

        protected override float ContactDamage => _config.contactDamage;

        protected override float ContactInterval => _config.contactInterval;

        protected override float ContactKnockback => _config.contactKnockback;

        /// <summary>表现层唯一入口：写入本帧玩家信息，再走基类帧序</summary>
        /// <remarks>战斗区就是视线触发器：第一次看见玩家即开战，之后哪怕出视线也照样追着最后位置打</remarks>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target)
        {
            _target = target;
            SetSight(target.Known, target.Visible, target.Pos);

            if (target.Visible) _engaged = true;

            FixedTick(ctx);
        }

        /// <summary>水平朝向对准玩家；玩家正上 / 正下方时保持原朝向，别让 x 分量抖动把朝向翻来覆去</summary>
        public void FaceTarget(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) <= FaceDeadZone) return;

            Motor.FaceTowards(new Vector2(Mathf.Sign(delta.x), 0f));
        }

        /// <summary>技能池里随机一个；一个都不满足就 None。多个同时可用时等概率</summary>
        public MmflyStateTag PickSkill(Vector2 pos, float now)
        {
            int seen = 0;
            MmflyStateTag picked = MmflyStateTag.None;

            if (DiveReady(pos, now) && Take(ref seen)) picked = MmflyStateTag.Dive;
            if (BarrageReady(pos, now) && Take(ref seen)) picked = MmflyStateTag.Barrage;
            if (SummonReady(pos, now) && Take(ref seen)) picked = MmflyStateTag.Summon;

            return picked;
        }

        /// <summary>决策结果：技能优先，其次距离超上限就逼近，否则观察</summary>
        public MmflyStateTag DecideNext(Vector2 pos, float now)
        {
            MmflyStateTag skill = PickSkill(pos, now);

            if (skill != MmflyStateTag.None) return skill;

            return InRange(pos, _range.Ideal) ? MmflyStateTag.Observe : MmflyStateTag.Approach;
        }

        public void BeginDive(float now) => _diveCooldown.MarkUsed(now, _config.diveCooldown);

        public void BeginBarrage(float now) => _barrageCooldown.MarkUsed(now, _config.barrageCooldown);

        public void BeginSummon(float now) => _summonCooldown.MarkUsed(now, _config.summonCooldown);

        /// <summary>弹反：只有黑形态能弹，冷却之内只能弹一发</summary>
        public bool CanRebound(float now) => Black && _reboundCooldown.CanUse(now);

        public void MarkRebounded(float now) => _reboundCooldown.MarkUsed(now, _config.reboundCooldown);

        public void BindMinions(IMinionHost host) => _minions = host;

        public int MinionRoomCount =>
            _minions == null ? 0 : Mathf.Max(0, _config.summonLimit - _minions.AliveCount);

        /// <summary>召唤攻击段：名额不够就少召，一个名额都没有就空走</summary>
        public void SummonOnce()
        {
            if (_minions == null) return;

            int count = Mathf.Min(_config.summonCount, MinionRoomCount);

            if (count > 0) _minions.Summon(count);
        }

        /// <summary>观察时长：在区间里随机</summary>
        public float RollObserveTime()
        {
            float min = Mathf.Min(_config.observeTimeMin, _config.observeTimeMax);
            float max = Mathf.Max(_config.observeTimeMin, _config.observeTimeMax);

            return Mathf.Lerp(min, max, (float)_rng.NextDouble());
        }

        /// <summary>观察漂移方向：随机，但去掉朝玩家靠近的那一半，免得观察变成慢慢贴脸</summary>
        public Vector2 RollDriftDir(Vector2 pos)
        {
            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
            Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));

            return Vector2.Dot(dir, TargetPos - pos) > 0f ? -dir : dir;
        }

        /// <summary>后摇浮动：方向随机、距离在区间内随机，时长由距离 / 速度除出来</summary>
        public void RollFloat(out Vector2 dir, out float duration)
        {
            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
            dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float min = Mathf.Min(_config.floatDistanceMin, _config.floatDistanceMax);
            float max = Mathf.Max(_config.floatDistanceMin, _config.floatDistanceMax);
            float distance = Mathf.Lerp(min, max, (float)_rng.NextDouble());

            duration = distance / Mathf.Max(0.01f, _config.floatSpeed);
        }

        public Vector2 Muzzle(Vector2 pos, int sign) =>
            pos + new Vector2(Mathf.Abs(_muzzle.x) * sign, _muzzle.y);

        /// <summary>一波散射：以 dir 为中心左右对称张开，spreadDegrees 是相邻两发的夹角</summary>
        public void FireWave(Vector2 origin, Vector2 dir, int count, float spreadDegrees)
        {
            if (Launcher == null || _config.bulletSpeed <= 0f || count <= 0) return;

            float half = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Fire(origin, Rotate(dir, (i - half) * spreadDegrees));
            }
        }

        /// <summary>俯冲落点锁定时用的方向：两点重合就退回朝向，别让零向量把速度清零</summary>
        public Vector2 LockedDirTo(Vector2 from, int face)
        {
            Vector2 delta = TargetPos - from;
            float distance = delta.magnitude;

            return distance > DirEps ? delta / distance : new Vector2(face, 0f);
        }

        protected override void StopOnSpot(in LogicContext ctx) => Motor.SnapVelocity(Vector2.zero);

        protected override void OnAiTick(in LogicContext ctx) => _moveGroup.Tick(in ctx);

        private bool DiveReady(Vector2 pos, float now) =>
            Black && _diveCooldown.CanUse(now) && InRange(pos, _range.Dive);

        private bool BarrageReady(Vector2 pos, float now) =>
            !Black && _barrageCooldown.CanUse(now) && InRange(pos, _range.Barrage);

        private bool SummonReady(Vector2 pos, float now) =>
            !Black && _summonCooldown.CanUse(now) && _minions != null && _minions.Available &&
            MinionRoomCount > 0 && InRange(pos, _range.Summon);

        private bool InRange(Vector2 pos, float range) =>
            (TargetPos - pos).sqrMagnitude <= range * range;

        /// <summary>蓄水池抽样：第 seen 个候选以 1/(seen+1) 顶替，让可用技能等概率被选中</summary>
        private bool Take(ref int seen)
        {
            bool picked = _rng.Next(seen + 1) == seen;

            seen++;

            return picked;
        }

        private void Fire(Vector2 origin, Vector2 dir)
        {
            Launcher.Launch(new BulletSpawn(
                origin,
                dir,
                _config.bulletSpeed,
                _config.bulletDamage,
                _config.bulletKnockback));
        }

        private static Vector2 Rotate(Vector2 dir, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            return new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
        }
    }
}
