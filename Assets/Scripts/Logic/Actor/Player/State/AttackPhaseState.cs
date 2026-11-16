using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>黑形态近战的三段骨架：前摇 → 本体 → 后摇，三段都活在同一只状态里，段长为 0 就当场跳过</summary>
    /// <remarks>段推进放在 AttackGroup 每帧判定之前（Advance），"前摇走满进本体"与原来多状态接力落在同一帧；
    /// 前摇与本体不许被别的攻击抢占，只有后摇 Cancelable</remarks>
    public abstract class AttackPhaseState : BaseState<AttackStateTag, LogicContext>
    {
        protected enum Stage
        {
            Prepare,
            Active,
            Recover,
            Done
        }

        private float _stageStart;

        protected Stage CurrentStage { get; private set; } = Stage.Done;

        /// <summary>只有后摇能被别的攻击取消</summary>
        public bool Cancelable => CurrentStage == Stage.Recover;

        /// <summary>前摇与本体压住移动组</summary>
        public virtual bool BlocksMovement => CurrentStage < Stage.Recover;

        /// <summary>整段（含后摇）都挡跳跃与冲刺</summary>
        public virtual bool BlocksActions => CurrentStage != Stage.Done;

        public bool InPrepare => CurrentStage == Stage.Prepare;

        public bool InActive => CurrentStage == Stage.Active;

        /// <summary>前摇挨打就整段作废（只有突进这么做，普攻挨打照常砍出去）</summary>
        public virtual bool AbortsOnHurt => false;

        protected abstract float PrepareTime { get; }

        protected abstract float ActiveTime { get; }

        protected abstract float RecoverTime { get; }

        public override void Enter(in LogicContext ctx)
        {
            CurrentStage = Stage.Prepare;
            _stageStart = ctx.GameTime.now;

            OnPrepareEnter(in ctx);

            if (PrepareTime <= 0f) Advance(in ctx);
        }

        /// <summary>由 AttackGroup 每帧推进：段走满就换下一段，命中 / 撞墙由子类在 OnAdvance 里提前跳段</summary>
        public void Advance(in LogicContext ctx)
        {
            OnAdvance(in ctx);

            float now = ctx.GameTime.now;

            while (CurrentStage != Stage.Done && now - _stageStart >= LengthOf(CurrentStage))
            {
                _stageStart += LengthOf(CurrentStage);
                CurrentStage++;

                EnterStage(in ctx);
            }
        }

        public override void Tick(in LogicContext ctx) => OnTick(in ctx);

        public override bool IsDone(in LogicContext ctx) => CurrentStage == Stage.Done;

        public override void Exit() => OnExit();

        protected virtual void OnPrepareEnter(in LogicContext ctx) { }

        protected virtual void OnActiveEnter(in LogicContext ctx) { }

        protected virtual void OnRecoverEnter(in LogicContext ctx) { }

        /// <summary>每帧推进段之前调一次：命中、撞墙这类要提前结束本体的判断放这里</summary>
        protected virtual void OnAdvance(in LogicContext ctx) { }

        protected virtual void OnTick(in LogicContext ctx) { }

        protected virtual void OnExit() { }

        /// <summary>本体提前收场，直接进后摇</summary>
        protected void SkipToRecover(in LogicContext ctx)
        {
            CurrentStage = Stage.Recover;
            _stageStart = ctx.GameTime.now;

            OnRecoverEnter(in ctx);
        }

        private void EnterStage(in LogicContext ctx)
        {
            if (CurrentStage == Stage.Active) OnActiveEnter(in ctx);
            else if (CurrentStage == Stage.Recover) OnRecoverEnter(in ctx);
        }

        private float LengthOf(Stage stage)
        {
            if (stage == Stage.Prepare) return PrepareTime;
            if (stage == Stage.Active) return ActiveTime;
            if (stage == Stage.Recover) return RecoverTime;

            return 0f;
        }
    }
}
