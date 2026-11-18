# 敌人 Warrior

> 依据代码：`Logic/Actor/Warrior/WarriorLogic.cs`（继承 `EnemyLogicBase`）、`WarriorMoveGroup`、`WarriorAttackState`、`WarriorArc`。
> 与 `Enemy` / `E_First` / `E_Second` 零共用逻辑，只复用 `EnemyLogicBase`、`EnemyTargetInfo`、`EnemyArea`、`EnemyStateTag`、`EnemyVisual`、`StateGroup`、`MoveMotor`、`BulletLogic` 这些框架件。
> 数值全在 `Assets/Resources/Data/WarriorConfig.asset`（需自己新建）。

## 一、文件

| 层 | 文件 |
| --- | --- |
| 数据 | `Data/WarriorConfig.cs` |
| 逻辑 | `Logic/Actor/Warrior/` → `WarriorLogic.cs`、`WarriorMoveGroup.cs`、`WarriorAttackPhase.cs`、`WarriorArc.cs` |
| 状态 | `Logic/Actor/Warrior/State/` → `WarriorPatrolState.cs`、`WarriorAttackState.cs` |
| 表现 | `Presentation/Move/WarriorMotor.cs`、`Presentation/Actor/Warrior/` → `WarriorController.cs`、`WarriorRebound.cs` |

## 二、每物理帧顺序

1. 采样世界：`WarriorMotor` 以 `View/Bottom` 为脚底探地面（踩地贴回并清零 vy）、探墙、探朝向那一侧的悬崖。
2. `BeginStep` → 条件组 → `OnAiTick`（站桩时整段跳过）。
3. 写入本帧视线结果与巡逻范围。
4. 切态判定 → 当前状态 `Tick` → 重力 → `Commit`。

视线由 `DetectCollider` 触发器的进出维护（`EnemySight`），玩家位置每帧现取，所以出视线之后仍然知道玩家在哪。

## 三、状态与切态

只有两个状态，没有追击 / 返回态。

| 机制 | 条件 | 下一态 |
| --- | --- | --- |
| 抢占 | 巡逻中 · 可见 · 冷却已好 · 距离 ≤ `pounceRange` | 攻击 |
| Fallback | 攻击走完 / 撞墙中断 / 首帧 | 巡逻 |

- 「可见」= 玩家在 `DetectCollider` 里；黑白的区别不在这里，而在攻击内部走哪套阶段。
- 攻击**不被抢占**：起手后一定走完自己那套阶段。
- 距离用两个中心之间的欧氏距离，不是横向距离。

巡逻态只在踩地时沿 x 推进；到巡逻框的左右边界、贴墙、前方悬崖，三者任一就掉头。框在 `Start` 里快照成世界坐标，掉头不再因为一帧越界就丢框（容差 = 掉头刹停的滑行 + 一帧位移）；但**被扑击或击退带出框外就永久退回整平台**，之后按墙与悬崖巡逻，不再回到框里。`patrolSize` 两个分量都是 0 时从一开始就走整平台。

## 四、黑形态：扑击

阶段 `Windup → Pounce → Recover`，全部由 `WarriorAttackState` 内部计时。

| 阶段 | 行为 | 时长 |
| --- | --- | --- |
| 前摇 Windup | 水平立定；**开始那一帧把玩家当时的坐标锁成落点** | `windupTime` |
| 扑击 Pounce | 沿起点 → 锁定落点的抛物线推进，每帧重写速度 | 由 `pounceSpeed` 与弧长决定 |
| 后摇 Recover | 水平立定 | `recoverTime` |

- 轨迹期间**关掉重力**，曲线完全由代码给，否则引擎会再拉一遍、落点对不上；进入后摇与退出状态时都把重力还回去。
- 推进按**弧长**算：`ds = pounceSpeed · Δt / |dP/ds|`，所以陡的地方 s 走得慢，总速度恒定。
- 落点是「前摇开始那一刻玩家在哪」，玩家后续移动不追击。

## 五、白形态：后跃 + 悬停 + 射击

阶段 `Leap → Hover → Leap`，**没有前摇后摇**。

- **Leap（前半程）**：先按黑形态的方式算出「起点 → 玩家」的抛物线，再把轨迹翻到另一侧 —— 实际落点是 `(2·起点x − 玩家x, 起点y)`，即水平位移反向、回到起跳高度，跳开的距离与原本要扑的距离相等。拱高、速度都与黑色一致。
- **起跳就面朝玩家**，整段后跃只写速度、不碰朝向 —— 于是全程都是「倒着走」：移动方向与朝向相反。
- **Hover**：推进到抛物线顶点（s = 0.5）就停住，清零速度、保持关重力，悬停 `hoverTime`。这期间**每帧实时转向玩家**，玩家横移也会跟着转。
- 悬停结束：朝**玩家当前位置**发一颗子弹（朝向已对准玩家）。
- **Leap（后半程）**：保持悬停时那张脸（朝玩家），但沿**同一条抛物线**往背离玩家的方向落回起跳高度 —— 移动方向与朝向相反，看起来是倒着飘回去。落地后动作结束、恢复重力、进冷却。

> 「以玩家所在的 x 轴直线为对称轴」我落成「水平位移反向、高度不变」。如果你要的是别的几何（比如落点 = 玩家位置关于怪物对称的点），改 `BeginLeap` 里那一行即可。

## 六、弹反（仅黑形态）

- **触发范围**：预制体子物体 `ReboundCollider` 的触发器，`WarriorController` 在 `Awake` 里补挂 `WarriorRebound` 转发器。
- **判定**：进入范围的碰撞体属于 `Bullet` 且 `PlayerOwned == true`（玩家阵营的飞行子弹）。
- **条件**：`Black`（黑形态）且当前**不在攻击状态**（前摇 / 扑击 / 后摇都不弹）且弹反冷却已好。
- **效果**：复用 `Bullet.TryReflect` —— 按子弹当前速度反向再发一发（速度不变、伤害与击退沿用原弹），原弹回收。反射出去的那发算**敌方子弹**，能打玩家。
- 冷却 `reboundCooldown` 在成功弹反那一刻起算；被无敌窗之类挡掉不影响（这里只判自己的条件）。
- 弹反需要 `bulletPrefab`：反射走的是 Warrior 自己的 `BulletLogic`，没有它就弹不出去（子弹仍会照常穿过并打到本体）。

## 七、撞墙中断

扑击与后跃期间每帧检查 `WorldInfo.TouchingWall`：

- 撞到墙 → **立即结束整个动作**，不进后摇，同一帧把扑击冷却标记为已用。
- 正常走完也进同一份冷却，所以「贴着墙反复起跳」不会发生。
- 探针探的是**朝向那一侧**：黑形态朝向跟着切线走，撞墙能正常中断；白形态全程朝玩家，而移动是背离玩家的，所以**白形态整段撞墙都不中断**（探的是玩家那一侧的墙）。

## 八、参数

**在预制体组件上（不进配置资产，Scene 视图里画出来）**

| 字段 | 含义 |
| --- | --- |
| `pounceRange` | 触发扑击的距离（两中心之间），以自身为圆心画圆，黑白共用 |
| `bulletMuzzle` | 出膛点局部偏移，x 随开火方向镜像；Inspector 里画十字 |
| `patrolOffset` / `patrolSize` | 巡逻框，运行时快照成世界坐标 |
| `blackForm` | 这只怪起始形态；世界每换一次极性翻一态 |

**在 `WarriorConfig`**

| 字段 | 含义 |
| --- | --- |
| `patrolSpeed` | 巡逻速度，只动 x |
| `pounceApexHeight` | 抛物线最高点相对起跳点的抬升；玩家与怪物同高时就是顶点 y 差 |
| `pounceSpeed` | 沿轨迹推进的速度（按弧长） |
| `pounceCooldown` | 扑击冷却，从动作结束（含撞墙中断）起算 |
| `windupTime` | 黑 · 前摇 |
| `recoverTime` | 黑 · 后摇 |
| `hoverTime` | 白 · 顶点悬停时长 |
| `bulletSpeed` / `bulletDamage` / `bulletKnockback` | 白形态子弹 |
| `reboundCooldown` | 弹反冷却 |
| `contactDamage` / `contactInterval` / `contactKnockback` | 接触伤害 |

## 九、预制体与配置（脚本之外要做的事）

- 根节点：`Rigidbody2D` + `WarriorMotor` + `WarriorController`；`config` 指到新建的 `WarriorConfig`。
- 子物体按名字找：`View`（含 `Bottom` / `Top`）、`BodyCollider`（非触发器）、`DetectCollider`（触发器，视线）、`AttackCollider`（触发器，接触伤害）、**`ReboundCollider`（触发器，弹反范围）**。
- **身体 = `BodyCollider` 自身与其下所有启用的 `Collider2D` 的并集**：脚底偏移取并集的最低点，站立高度取并集的上下跨度（有 `View/Top` 与 `View/Bottom` 时优先量这两点）。想拼出多块形状就把碰撞体挂在 `BodyCollider` 下面，禁用的（`enabled = false`）不参与。`WarriorMotor.bodyRoot` 留空即按名字找，也可以直接拖节点。
- `pounceRange` 与 `bulletMuzzle` 都在 `WarriorController` 上，不进配置资产：选中敌人就能在 Scene 视图看到扑击半径的圆和枪口十字，边拖数值边对。
- `WarriorController.blackForm` 决定这只怪起始是黑还是白；世界每换一次极性它会翻一态（基类 `_black = !_black`）。
- `WarriorMotor` 的 `groundProbe.layerMask` 与 `wallProbe.layerMask` **必须分开勾**（Ground / Wall）。默认 `Everything` 会把玩家和别的敌人当成地面。
- `ReboundCollider` 的层别放进玩家子弹的排除层（`Default` / 玩家层之外的层都收得到）；它的 `includeLayers` 保持默认即可。
- `bulletPrefab` 留空 → 白形态不开火、也不能弹反。

## 十、尚未实现

- 扑击与后跃都不做落点预测：撞墙才中断，撞到别的敌人 / 平台边缘只靠物理推挤。
- 白形态全程撞墙不中断（探针跟着朝玩家的朝向走，而移动是背离玩家）。
- 悬停结束后直接落回地面，没有落地保护与受击打断。
- 受击与死亡没有演出：血打空直接 `Destroy`。
