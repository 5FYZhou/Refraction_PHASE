# Boss mmfly（飞行 Boss）

> 依据代码：`Logic/Actor/Mmfly/MmflyLogic.cs`（目标 / 极性 / 技能池 / 冷却）、`MmflyMoveGroup`（七态切换）、
> `State/MmflyIdleState`、`MmflyDecideState`、`MmflyObserveState`、`MmflyApproachState`、`MmflyDiveState`、`MmflyBarrageState`、`MmflySummonState`。
> 表现层：`Presentation/Actor/Mmfly/MmflyController.cs`（装配、喂玩家位置、转 View、召唤预警、弹反转发）、
> `MmflyMinionField.cs`（小怪生成与计数）、`MmflyRebound.cs`（弹反触发器转发）。
> 执行器复用 `Presentation/Move/SpliterMotor.cs`（无重力、只探墙）。
> 数值全在 `MmflyConfig` 资产里（`Assets/Resources/Data/Enemy/MmflyConfig.asset`，需自己新建）。

与小怪零共用：只复用 `EnemyLogicBase`、`EnemyTargetInfo`、`StateGroup`、`MoveMotor`、`BulletLogic`、`EnemyTint` / `EnemySight` / `ReboundFlash` 这些框架件。

## 一、开战与结束

| 机制 | 条件 |
| --- | --- |
| 开战 | 玩家进入 `DetectCollider` 触发器（第一次 `Visible`）→ `Engaged` 置真，**之后不再清除** |
| 结束 | 没有脱战出口；只有血量归零被 `EnemyMgr` 回收 |

- 没有巡逻范围、没有返回态：战斗区域就是视线触发器，触发器画多大就是能发现玩家的范围。
- 出视线之后 `EnemySight` 仍保留玩家引用，`TargetPos` 继续更新，所以 Boss 不会因为玩家绕后而呆住。
- Boss 的 `Alert` 直接等于 `Engaged`，丢目标计时对它没有意义。

## 二、每物理帧顺序

1. `SpliterMotor.SampleWorld`：探墙（身体周围圆环），地面恒为 false。
2. `BeginStep` → 条件组 → `MmflyLogic.FixedTick(ctx, target)` 写入本帧视线并锁存开战。
3. `MmflyMoveGroup.Tick`：先抢占（只有 Idle → 决策），再判 `IsDone`，最后 Tick 当前状态。
4. `ApplyGravity`（配置关重力，空转）→ `Commit`。
5. 渲染帧：`RenderTick` 按技能相位转 View、做召唤抖动与颜色闪烁。

## 三、决策循环

| 状态 | 做什么 | 结束 / 去哪 |
| --- | --- | --- |
| Idle | 原地悬浮 | 开战当帧被抢占 → 决策 |
| Decide | 悬浮面向玩家，等 `decideInterval` | 结束时按下方优先级选一个 |
| Observe | 缓慢漂移（方向随机且不朝玩家），面向玩家 | 时长到：距离 > 理想距离 → 逼近，否则回决策 |
| Approach | 朝玩家直线飞 + 轻微横向摆动 | 进到理想距离 → 决策；途中技能就绪 → 直接起手 |
| Dive / Barrage / Summon | 技能三段 | 后摇结束 → 决策 |

决策优先级（`MmflyLogic.DecideNext`）：

1. 技能池里挑一个；多个同时满足就**等概率随机**（蓄水池抽样）。
2. 玩家距离 > `idealDistance` → 逼近。
3. 否则 → 观察。

- **技能不进池的条件**：极性不符、冷却没好、玩家超出该技能的触发距离；召唤还要场上小怪没到上限。
- **逼近中断**：逼近态每帧查一次技能池，就绪就停住并直接进该技能前摇——不再等决策间隔，视为"提前进入决策"。
- Boss 在非技能时间从不锁玩家直线追击：观察态漂移、逼近态限速，两者都不改朝向以外的战斗参数。
- **二阶段**：`CurHealth ≤ maxHealth × phase2HealthRatio` 时决策间隔 × `phase2DecideScale`、逼近速度 × `phase2ApproachScale`，随血量实时生效，没有单独的二阶段状态。

## 四、黑 · 俯冲

| 阶段 | 行为 | 时长 |
| --- | --- | --- |
| 前摇 | 悬停蓄力，水平朝向跟玩家；**结束那一帧锁落点与俯冲距离** | `diveWindupTime` |
| 攻击 | 沿锁定方向匀速飞，飞满锁定距离或撞墙即结束 | 距离 / `diveSpeed` |
| 后摇 | 不移动、不转向、不索敌，可被攻击 | `diveRecoverTime` |

- 锁定距离 = 前摇结束那一刻两者连线的长度（下限 0.5 单位），所以玩家贴脸时也能冲出一小段。
- 冷却 `diveCooldown` 从**前摇起手那一帧**算，前摇 / 俯冲 / 后摇都在间隔内。
- 触发距离 `diveRange` 与 `idealDistance` 独立：前者决定能不能进技能池，后者决定要不要逼近。两者都在控制器上，Scene 里画圈。

## 五、黑 · 弹反（被动，不在技能池）

- 玩家阵营子弹进入 `ReboundCollider` 触发器 → 弹反：`Bullet.TryReflect` 改写阵营与伤害，沿原路径飞回。
- 只有黑形态能弹，冷却 `reboundCooldown` 从触发那一刻算。
- **不打断当前行为**：不进决策、不占前摇后摇，弹完继续原来那一段。
- 弹反成功才亮 `ReboundFlash`（半透明红），冷却挡掉的那次没有反馈。

## 六、白 · 弹幕

| 阶段 | 行为 | 时长 |
| --- | --- | --- |
| 前摇 | 原地不动，只把身体转向玩家；结束那一帧锁方向 | `barrageWindupTime` |
| 攻击 | 第一波当帧打出，之后每 `waveInterval` 一波，共 `waveCount` 波 | 波次间隔 |
| 后摇 | 朝随机方向匀速浮动，浮动期间不追不打 | 随机浮动距离 / `floatSpeed` |

- 每波 `bulletsPerWave` 颗，以锁定方向为中心左右对称散开，相邻两颗夹角 `spreadAngle`。
- 冷却 `barrageCooldown` 从起手那一帧算。

## 七、白 · 召唤

| 阶段 | 行为 | 时长 |
| --- | --- | --- |
| 前摇 | 身体抖动 + 颜色在黑白之间闪烁预警 | `summonWindupTime` |
| 攻击 | 当帧召出 `summonCount` 只小怪（名额不足就少召） | 瞬时 |
| 后摇 | 随机方向匀速浮动 | 随机浮动距离 / `floatSpeed` |

- 小怪从 `minionFlyPrefab`（飞行）与 `minionCrawlerPrefab`（爬行）里随机取一种，形态（黑 / 白）也随机，在 `summonRadius` 圆面内随机落点。
- 落点用 `minionBlockMask` 做净空检测，撞墙就重摇（最多 12 次，摇不到就少召一只）。
- **上限**：场上这只 Boss 召出且还活着的小怪数 ≥ `summonLimit` 时，召唤不进技能池。
- 小怪是独立注册进 `EnemyMgr` 的普通敌人；Boss 回收 / 全体刷新时连同它们一起清掉。

## 八、表现层

- View 旋转：俯冲与弹幕的前摇实时转向玩家（±90° 钳制），锁定后停在该角，出技能转回初始角。
- 召唤预警：View 局部位置做正弦抖动，同时按 `0.09s` 间隔在黑白之间翻色，子物体一律由 `EnemyTint` 上色。
- 蓄力粒子只给白形态弹幕前摇（`ChargingShot`）。
- 受击闪白、接触伤害、攻击盒都走 `EnemyLogicBase` + `HitBoxBinder`，与其它敌人一致。

## 九、预制体要求

| 子物体 / 组件 | 用途 |
| --- | --- |
| `View` | 所有 `SpriteRenderer` 的容器，换色与旋转都以它为根 |
| `BodyCollider` | 身体碰撞体，`EnemyColliderSetup` 会重写层与触发器 |
| `AttackCollider` | 攻击盒，受击方按 `contactDamage` 结算 |
| `DetectCollider` | 视线触发器，**同时就是战斗区域** |
| `ReboundCollider` | 弹反范围触发器（黑形态），缺了只警告、不弹反 |
| `SpliterMotor` | 执行器，挂在根节点上并拖进 `moveMotor` |

控制器可调项：`config`、`moveMotor`、`blackForm`、`bulletPrefab`、`bulletMuzzle`、`chargeInColor`、`viewRoot`、`viewTurnSpeed`、
`reboundFlashTime`、`minionFlyPrefab`、`minionCrawlerPrefab`、`minionBlockMask`、`minionClearance`。

**所有要画出来的半径都在控制器上**（不进 `MmflyConfig`）：`idealDistance`、`diveRange`、`barrageRange`、`summonRange`、`summonRadius`。
选中 Boss 时 Scene 里逐个画圈，逻辑判定与召唤落点读的是同一份值（装配时打包成 `MmflyRange` 传进 `MmflyLogic`）。

## 十、尚未实现

- 眩晕：需求里提到"眩晕结束进决策"，当前没有眩晕来源，状态组里也没有对应出口。
- 二阶段没有额外的演出或技能，只有决策间隔与逼近速度两个倍率。
- 召唤不区分小怪归属：上限只算这只 Boss 自己召出来的，场上其它小怪不算。
