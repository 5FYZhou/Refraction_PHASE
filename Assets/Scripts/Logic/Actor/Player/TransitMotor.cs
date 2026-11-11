using Refraction.Data;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>过门后的运动：按目标门朝向接管落点前的移动，落点与朝向来自 DoorEntry</summary>
    /// <remarks>假输入靠替换 InputSnapshot 实现，结束前玩家输入一律不生效；朝上的门只解一次抛物线，并在状态机之后把 vy 钉回去</remarks>
    public sealed class TransitMotor
    {
        private enum Mode
        {
            None,
            Run,
            Launch,
            Dash
        }

        /// <summary>到达判定容差（世界单位）</summary>
        private const float ArriveEpsilon = 0.05f;

        /// <summary>水平距离趋近 0 时的飞行时间下限，防止 vy 解成无穷大</summary>
        private const float MinAirTime = 0.02f;

        private readonly IMoveMotor _motor;
        private readonly PlayerConfig _config;
        private readonly MoveGroup _moveGroup;

        private Mode _mode;
        private Vector2 _landing;
        private int _dirX;
        private float _beginTime;
        private bool _pendingDash;
        private Vector2 _dashDir = Vector2.zero;

        private float _launchVy;
        private float _flightTime;
        private float _deadline;
        private bool _launchApplied;
        private bool _xDone;

        public bool IsActive => _mode != Mode.None;

        public TransitMotor(IMoveMotor motor, PlayerConfig config, MoveGroup moveGroup)
        {
            _motor = motor;
            _config = config;
            _moveGroup = moveGroup;
        }

        /// <summary>瞬移完成后调用一次，按门朝向决定用哪种过门运动；朝下的门什么都不做</summary>
        /// <remarks>enterFace 必须由触发那扇门的玩家提供：切场景后新玩家是从预制体新建的，朝向还没被写过，读它只会恒定拿到 1</remarks>
        public void Begin(in DoorEntry entry, MovementStateTag enterTag, int enterFace, in LogicContext ctx)
        {
            _mode = Mode.None;
            _pendingDash = false;
            _launchApplied = false;
            _xDone = false;
            _beginTime = ctx.GameTime.now;

            Vector2 facing = entry.Facing;

            // 按主轴分流：门在编辑器里手摆的 89.5° 之类的角度，不能按 x != 0 去判
            if (Mathf.Abs(facing.y) > Mathf.Abs(facing.x))
            {
                if (facing.y > 0f) BeginVertical(entry, enterFace);
                return;
            }

            if (facing.x != 0f) BeginHorizontal(entry, enterTag);
        }

        /// <summary>替换本帧输入并驱动竖直速度；返回的 ctx 才是状态机该看到的世界</summary>
        public LogicContext Apply(in LogicContext ctx)
        {
            if (_mode == Mode.None) return ctx;

            // 冲刺重启必须在帧首做：DashState.Enter 写的是账本，晚于 Commit 会被下一次 BeginStep 清掉
            if (_pendingDash)
            {
                _pendingDash = false;
                _moveGroup.StartDashFromTransit(_dashDir, ctx);
            }

            if (_mode == Mode.Dash) return ctx;

            // 水平到了就收掉假输入，别一路推着玩家冲过落点
            InputSnapshot fake = new(new Vector2(_xDone ? 0f : _dirX, 0f), false, false, false, false, false, false);
            return new LogicContext(fake, ctx.WorldInfo, ctx.GameTime);
        }

        /// <summary>帧末判到达：到点即交还控制，玩家的真实输入下一帧恢复</summary>
        public void AfterTick(in LogicContext ctx)
        {
            switch (_mode)
            {
                case Mode.None:
                    return;

                case Mode.Dash:
                    // 冲刺按状态机自己走完，不再判落点；不加 return 会掉进 Run 分支，_dirX 为 0 时当帧就结束
                    if (_moveGroup.CurrentTag != MovementStateTag.Dash) _mode = Mode.None;
                    return;

                case Mode.Run:
                {
                    // 用预测位置判：帧首位置落后一帧，按当前水平速度会冲过落点
                    float nextX = ctx.WorldInfo.Pos.x + ctx.WorldInfo.FrameStartVelocity.x * ctx.GameTime.deltaTime;
                    if ((_landing.x - nextX) * _dirX <= ArriveEpsilon) _mode = Mode.None;
                    return;
                }

                case Mode.Launch:
                {
                    // 状态机在 Tick 里会按“跳跃键是否按住”砍竖直速度、按上升/顶点换重力，
                    // 过门期间这些都不该生效：在它之后把抛物线钉回来，再判到达
                    if (!_launchApplied)
                    {
                        _launchApplied = true;
                        _motor.SetVelocityY(_launchVy);
                    }

                    _motor.SetGravity(_config.fallGravity);

                    // 与 Run 一样用预测位置：帧首位置是上一帧的，按 6 米/秒会整整晚一帧
                    float nextX = ctx.WorldInfo.Pos.x + ctx.WorldInfo.FrameStartVelocity.x * ctx.GameTime.deltaTime;
                    if ((_landing.x - nextX) * _dirX <= ArriveEpsilon) _xDone = true;

                    // 按起飞方向判“穿过落点高度”，不能写成 <=：从下方上升时它一开始就成立
                    bool yDone = _launchVy >= 0f
                        ? FeetY(ctx) >= _landing.y - ArriveEpsilon
                        : FeetY(ctx) <= _landing.y + ArriveEpsilon;

                    // 落到地面（落点本身贴地）或超时都强行收尾：宁可差一点，也不能把控制权永远扣着
                    bool blocked = ctx.WorldInfo.Grounded && ctx.GameTime.now - _beginTime > 0.1f;
                    bool timeout = ctx.GameTime.now - _beginTime > _deadline;

                    if ((_xDone && yDone) || blocked || timeout) _mode = Mode.None;
                    return;
                }
            }
        }

        private void BeginHorizontal(in DoorEntry entry, MovementStateTag enterTag)
        {
            if (!entry.HasLeft)
            {
                Debug.LogError("[TransitMotor] 左右朝向的门必须设置左落点，本次不做传送运动。");
                return;
            }

            _landing = entry.LeftLanding;

            // 其余状态一律接管：落点前不响应玩家输入
            _dirX = StepDir(_landing.x - entry.Point.x);

            if (_dirX == 0)
            {
                Debug.LogError("[TransitMotor] 左落点与门点 x 相同，没有可跑的水平距离，本次不做传送运动。");
                return;
            }

            _mode = Mode.Run;
            _motor.SetGravityEnable(true);
            _motor.SetVelocityX(_dirX * _config.moveSpeed);
        }

        private void BeginVertical(in DoorEntry entry, int enterFace)
        {
            if (!entry.HasLeft && !entry.HasRight)
            {
                Debug.LogError("[TransitMotor] 朝上的门必须设置左右落点至少一个，本次不做传送运动。");
                return;
            }

            if (entry.HasLeft && entry.HasRight)
                _landing = enterFace >= 0 ? entry.RightLanding : entry.LeftLanding;
            else
                _landing = entry.HasRight ? entry.RightLanding : entry.LeftLanding;

            _dirX = StepDir(_landing.x - entry.Point.x);

            if (_dirX == 0)
            {
                Debug.LogError("[TransitMotor] 朝上的门落点与门点 x 相同，算不出飞行时间，本次不做传送运动。");
                return;
            }

            // 抛物线只解一次：按状态机的水平加速曲线估出飞到落点的时间，再反解竖直初速度
            _flightTime = TimeToReachX(entry.Point.x);
            _launchVy = SolveVelocityY(entry.Point.y, _flightTime);
            _deadline = _flightTime + 0.8f;

            _mode = Mode.Launch;
            _motor.SetGravityEnable(true);
        }

        /// <summary>按 MoveTowardsX 的加速曲线估飞行时间：起步先加速，不能直接拿 moveSpeed 去除水平距离</summary>
        private float TimeToReachX(float fromX)
        {
            float dx = Mathf.Abs(_landing.x - fromX);
            float speed = Mathf.Max(_config.moveSpeed, 0.01f);
            float accel = _config.moveAcceleration;

            // 零惯性：当帧就是 moveSpeed
            if (accel <= 0f) return Mathf.Max(dx / speed, MinAirTime);

            float rampTime = speed / accel;
            float rampDist = 0.5f * accel * rampTime * rampTime;

            // 没加速完就到落点，或加速完还要匀速走一段
            float t = dx <= rampDist
                ? Mathf.Sqrt(2f * dx / accel)
                : rampTime + (dx - rampDist) / speed;

            return Mathf.Max(t, MinAirTime);
        }

        /// <summary>反解落到落点高度所需的竖直初速度；超过 maxRiseSpeed 会被重力钳制，只能报错并尽力而为</summary>
        private float SolveVelocityY(float fromFeetY, float t)
        {
            float dy = _landing.y - fromFeetY;
            float vy = (dy + 0.5f * _config.fallGravity * t * t) / t;

            if (vy > _config.maxRiseSpeed)
            {
                Debug.LogError($"[TransitMotor] 落点需要竖直初速度 {vy:F1}，超过 maxRiseSpeed {_config.maxRiseSpeed:F1}，本次打不到落点。");
                return _config.maxRiseSpeed;
            }

            return vy;
        }

        private float FeetY(in LogicContext ctx) => ctx.WorldInfo.Pos.y + _motor.FeetOffsetY;

        private static int StepDir(float delta)
        {
            if (delta > ArriveEpsilon) return 1;
            if (delta < -ArriveEpsilon) return -1;
            return 0;
        }
    }
}
