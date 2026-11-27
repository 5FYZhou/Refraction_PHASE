# 敌人 Mantis（螳螂）

> 依据代码：`Logic/Actor/Mantis/MantisLogic.cs`（目标 / 巡逻框 / 弹反 / 伤害请求）、`MantisMoveGroup`（三态切换）、
> `State/MantisPatrolState`、`MantisChaseState`、`MantisAttackState`。
> 表现层：`Presentation/Actor/Mantis/MantisController.cs`（装配、喂玩家位置、前方范围伤害结算、弹反转发）、`MantisRebound.cs`。
> 执行器复用 `Presentation/Move/WarriorMotor.cs`（地面：探地贴地 + 探墙 + 探前方悬崖，身体取 `BodyCollider` 并集）。
> 数值在 `MantisConfig`（`Assets/Resources/Data/Enemy/MantisConfig.asset`，需自己新建）；**要画出来的两个半径与攻击框在控制器上**。

## 一、状态与切换

| 机制 | 条件 | 下一态 |
| --- | --- | --- |
| 抢占 | 巡逻中 · 玩家在视线内 | 追击 |
| 抢占 | 非攻击中 · `Alert` 且进到停止距离 | 攻击 |
| Fallback | 追击结束（丢目标超时） | 巡逻 |
| Fallback | 攻击结束 | 还 `Alert` 就接着追，否则回巡逻 |

- `Alert` = 看得见 || 丢目标未超时（`loseTargetTime`），所以玩家出视线后还会朝最后位置追一段。
- 攻击**不被抢占**：起手后一定走完前摇 + 位移 + 后摇。**没有攻击冷却**：后摇结束若还在停止距离内，下一帧就再起手。
- 追击朝**最后已知位置**走；玩家在悬崖对面时停在崖边，不会追下去。

## 二、巡逻

- 只在踩地时沿 x 推进，离地一律刹车。
- 巡逻框 = 控制器上的 `patrolOffset` + `patrolSize`，运行时快照成世界坐标，**只判左右边界**。
- 掉头条件：到框边、贴墙、前方是悬崖（只在面朝方向正是前进方向时认后两者）。
- **不设框（`patrolSize.x ≤ 0`）→ 按整平台巡逻**；**被位移或击退带出初始框 → 永久退回整平台**（`_areaLost`，容差 = 掉头滑行 + 一帧位移）。

## 三、黑形态攻击

| 阶段 | 行为 | 时长 |
| --- | --- | --- |
| 前摇 | 立定不动，跟着玩家转身；**结束那一帧结算第一次前方范围伤害**并开始前冲 | `blackWindupTime` |
| 位移 | 朝面朝方向匀速前冲 | `dashTime` × `dashSpeed` |
| — | 位移结束当帧**结算第二次前方范围伤害** | — |
| 后摇 | 立定不动，不追不转向 | `blackRecoverTime` |

- 形态在进攻击态那一帧锁死，中途翻极性不改动作。
- 黑形态可弹反：玩家子弹进 `ReboundCollider` 就按原轨迹弹回，冷却 `reboundCooldown`，**不打断当前攻击**。

## 四、白形态攻击

| 阶段 | 行为 | 时长 |
| --- | --- | --- |
| 前摇 | 立定不动跟着转身；**结束那一帧锁定玩家位置并开出第一枪**，同时开始前冲 | `whiteWindupTime` |
| 位移 | 朝面朝方向匀速前冲（速度、时长与黑共用 `dashSpeed` / `dashTime`） | `dashTime` |
| — | 位移结束当帧**重新锁定玩家位置开第二枪** | — |
| 后摇 | 朝位移反方向超快后撤，速度 = `dashSpeed × dashBackMultiplier` | `whiteRecoverTime` |

- 前后摇时长与黑**各用各的字段**，互不影响。
- 子弹方向 = 锁定位置 − 当时自身位置；玩家与自身重合时退回面朝方向。枪口偏移在控制器上，x 随开火方向镜像。
- 白形态不能弹反（`CanRebound` 只认黑）。

## 五、前方范围伤害的结算路径

1. 逻辑在攻击段的两个时机调用 `RequestHit()`（只举旗，不算几何）。
2. `MantisController.OnTick` 在 `FixedTick` 之后取旗，调 `ApplyAreaDamage()`。
3. 区域 = 以身体为基准、朝面朝方向摆的矩形：中心 `(face × attackRange / 2, attackCenterY)`，尺寸 `(attackRange, attackHeight)`。
4. `Physics2D.OverlapBoxNonAlloc` 找 `HurtBox`，同一帧里同一个 `IHurtable` 只吃一次；伤害方向按面朝侧。
5. 伤害走 `DamageSource.Contact`，所以敌人之间不会互相伤害（`EnemyLogicBase.TakeDamage` 挡掉该来源）。

> 逻辑层不碰物理：范围多大、落点在哪由控制器在 Scene 里画出来，这是两只"看得见的框"。

## 六、弹反

- 范围来自预制体子物体 `ReboundCollider` 上的触发器；`MantisRebound` 只做消息转发，找不到时只警告、不弹反。
- 触发条件：玩家阵营子弹、黑形态、冷却就绪。命中后 `Bullet.TryReflect` 改写阵营与伤害，沿原路径飞回。
- 弹成功才亮 `ReboundFlash`（半透明红）；冷却挡掉的那次没有反馈。

## 七、预制体要求

| 子物体 / 组件 | 用途 |
| --- | --- |
| `View` / `View/Bottom` / `View/Top` | 换色根、脚底与头顶参考点（执行器探地用） |
| `BodyCollider` | 身体碰撞体（执行器按它算身体并集） |
| `AttackCollider` | 攻击盒（接触伤害，走 `contactInterval`） |
| `DetectCollider` | 视线触发器 |
| `ReboundCollider` | 弹反范围触发器（黑形态），缺了只警告 |
| `WarriorMotor` | 执行器，挂在根节点上并拖进 `moveMotor` |

控制器可调项：`config`、`moveMotor`、`blackForm`、`bulletPrefab`、`bulletMuzzle`、`chargeInColor`、
`patrolOffset`、`patrolSize`、`stopDistance`、`attackRange`、`attackHeight`、`attackCenterY`、`reboundFlashTime`。

## 八、尚未实现

- 攻击没有冷却与次数上限：站定距离内会按"前摇 → 位移 → 后摇"无限循环。
- 位移撞墙不会提前收手（交给物理拦），定时走完才结算第二段。
- 没有攻击的蓄力粒子以外的预警表现；白形态两枪之间只有位移。
