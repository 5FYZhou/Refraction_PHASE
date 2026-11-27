# 敌人 Fortress（要塞）

> 依据代码：`Logic/Actor/Fortress/FortressLogic.cs`（目标 / 巡逻框 / 弹幕冷却 / 弹反冷却）、`FortressMoveGroup`（三态切换）、
> `State/FortressPatrolState`、`FortressChaseState`、`FortressBarrageState`。
> 表现层：`Presentation/Actor/Fortress/FortressController.cs`（装配、采样、弹幕开火、弹反转发）、`FortressRebound.cs`。
> 执行器复用 `Presentation/Move/EnemyMotor.cs`（地面：探地贴地 + 探墙 + 探前方悬崖）。
> 巡逻 / 追击 / 索敌规则与 `Enemy` 保持一致，但代码**不继承 Enemy**，是独立的一套（同 E_First / Warrior / Spliter）。
> 数值在 `FortressConfig`（`Assets/Resources/Data/Enemy/FortressConfig.asset`，需自己新建）；**要画的两个半径在控制器上**。

## 一、状态与切换

| 机制 | 条件 | 下一态 |
| --- | --- | --- |
| 抢占 | 白形态 · `Alert` · 弹幕冷却就绪 · 进到弹幕距离 | 攻击 |
| 抢占 | 巡逻中 · 玩家在视线内 · 不是黑形态 | 追击 |
| Fallback | 弹幕结束 | 还 `Alert` 且非黑 → 追击，否则回巡逻 |
| Fallback | 追击结束（丢目标超时 / 转黑） | 巡逻 |

- `Alert` = 看得见 || 丢目标未超时（`loseTargetTime`），出视线后还会朝最后位置追一段。
- 黑形态不追（`ChaseAllowed = !Black`），只巡逻 + 弹反，和 `Enemy` 的黑世界规则一致。
- 攻击**不被抢占**：起手后一定走完前摇 + 多波散射 + 后摇。

## 二、巡逻与追击

- 巡逻只在踩地时沿 x 推进，离地刹车；到框边、贴墙、前方是悬崖就掉头。
- 巡逻框 = 控制器上的 `patrolOffset` + `patrolSize`，运行时快照成世界坐标，只判左右边界；不设框就靠墙与悬崖掉头（整平台）。
- 追击朝**最后已知位置**走，贴到 `stopDistance` 刹住；玩家在悬崖对面时停在崖边。

## 三、白形态 · 散射弹幕

| 阶段 | 行为 | 时长 |
| --- | --- | --- |
| 前摇 | 原地不动，只把身体转向玩家；**结束那一帧锁方向** | `barrageWindupTime` |
| 攻击 | 第一波当帧打出，之后每 `waveInterval` 一波，共 `waveCount` 波 | 波次间隔 |
| 后摇 | **原地不动**，不追不转向 | `barrageRecoverTime` |

- 锁的是「玩家当前位置 − 自身位置」的方向（含纵向）；玩家正好重合时退回朝向。
- 每波 `bulletsPerWave` 颗，以锁定方向为中心左右对称散开，相邻两颗夹角 `spreadAngle`。
- 冷却 `barrageCooldown` 从**前摇起手那一帧**算；触发距离 `barrageRange` 在控制器上，Scene 画圈。
- 开火前摇会给蓄力粒子（`ChargingShot`），和其它远程敌人一致。

## 四、黑形态 · 弹反

- 玩家阵营子弹进入 `ReboundCollider` 触发器 → `Bullet.TryReflect` 改写阵营与伤害，**沿原路径飞回**。
- 只有黑形态能弹，冷却 `reboundCooldown` 从触发那一刻算。
- **不打断当前行为**：不进前摇后摇、不改状态。
- 弹成功才亮 `ReboundFlash`（半透明红），冷却挡掉的那次没有反馈。

## 五、预制体要求

| 子物体 / 组件 | 用途 |
| --- | --- |
| `View` / `View/Bottom` / `View/Top` | 换色根；脚底与头顶参考点（执行器探地用，`EnemyMotor` 按名字找） |
| `BodyCollider` | 身体碰撞体 |
| `AttackCollider` | 攻击盒（接触伤害） |
| `DetectCollider` | 视线触发器 |
| `ReboundCollider` | 弹反范围触发器（黑形态），缺了只警告、不弹反 |
| `EnemyMotor` | 执行器，挂在根节点上并拖进 `moveMotor` |

控制器可调项：`config`、`moveMotor`、`blackForm`、`OpenAI`、`patrolOffset`、`patrolSize`、
`stopDistance`、`barrageRange`、`bulletPrefab`、`bulletMuzzle`、`chargeInColor`、`reboundFlashTime`。

## 六、尚未实现

- 弹幕前摇 / 后摇时长需求里没给数值，用了配置里的默认值（0.5s / 0.4s），可自由调。
- 弹幕期间不做转向（锁方向后身体朝向也不再变）。
- 黑形态除了弹反没有任何输出手段，只有接触伤害。
