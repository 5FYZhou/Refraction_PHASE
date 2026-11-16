# 敌人 E_First（毛毛虫）

> 依据代码：`Logic/Actor/E_First/E_FirstLogic.cs`（继承 `EnemyLogicBase`）、`E_FirstMoveGroup`、`E_FirstAttackState`。
> 与 `Enemy` / `E_Second` 零共用：只复用 `EnemyLogicBase`、`EnemyTargetInfo`、`StateGroup`、`MoveMotor`、`BulletLogic` 这些框架件。
> 数值全在 `Assets/Resources/Data/E_FirstConfig.asset`（需自己新建）。

## 一、文件

| 层 | 文件 |
| --- | --- |
| 数据 | `Data/E_FirstConfig.cs` |
| 逻辑 | `Logic/Actor/E_First/E_FirstLogic.cs`、`E_FirstMoveGroup.cs`、`E_FirstStateTag.cs`、`E_FirstArea.cs` |
| 状态 | `Logic/Actor/E_First/State/E_FirstPatrolState.cs`、`E_FirstAttackState.cs` |
| 表现 | `Presentation/Move/E_FirstMotor.cs`、`Presentation/Actor/E_First/E_FirstController.cs` |

## 二、每物理帧顺序

1. 采样世界：`E_FirstMotor` 以 `View/Bottom` 为脚底探地面（踩地贴回并清零 vy）、探墙、探朝向那一侧的悬崖。
2. `BeginStep` → 条件组 → `OnAiTick`（只有开关打开时才走，站桩连丢目标计时都冻住）。
3. 写入本帧视线结果与巡逻范围。
4. 切态判定 → 当前状态 `Tick` → 重力 → `Commit`。

视线由 `DetectCollider` 触发器的进 / 出事件维护；玩家位置每帧从 `PlayerController` 取，所以**出视线之后仍然知道玩家在哪**，后摇才能继续朝那边爬。

## 三、状态与切态

只有两个状态，没有追击态。

| 机制 | 条件 | 下一态 |
| --- | --- | --- |
| 抢占 | 巡逻中 · 白世界 · 玩家在视线内 | 攻击 |
| Fallback | 攻击走完（丢目标 / 转黑） | 巡逻 |
| Fallback | 首帧 | 巡逻 |

- 攻击**不被抢占**：起手后一定走完前摇 + 攻击 + 后摇。后摇结束时若玩家仍在视线内，由 `E_FirstAttackState` 内部直接重进前摇（同状态再切会被 `ChangeState` 的幂等挡掉），所以是**无冷却连射**。
- 转黑**当帧收手**：`Tick` 第一行判 `Hostile`，不成立就结束攻击，下一帧回巡逻。

## 四、巡逻

- 只在踩地时推进，离地一律刹车（坠落、被顶飞时不会在空中飘）。
- 只动 x；竖直速度交给**逻辑重力**（`E_FirstConfig.enableGravity`）。
- 重力只有一套：`MoveMotor.InitRb` 会把刚体的 `gravityScale` 强制写成 0，靠 `MoveLedger.ApplyGravity` 施加。**`enableGravity` 没勾 = 两套重力都没有，虫子离地后悬空不动**（`E_FirstMotor.Awake` 会打一条 warning 提醒）。
- 方向：到框边、贴墙、前方是悬崖，三者任一就掉头。
- 范围：`EnemyController` 式的 `patrolOffset` + `patrolSize` 方框，运行时以进游戏那一刻的站位快照成世界坐标。**`patrolSize.x ≤ 0`** → 不启用框，只靠墙与悬崖掉头。
- 框按**身体中心**判，边界只是"中心点的活动范围"：身体本身有半径，站到框边时半个身子会在框外，这是正常的。
- 越过框边那几帧（掉头惯性会多滑出 0.02~0.05 单位）**照旧用同一个框判**，巡逻与后摇读的都是这一次写进来的结果：出框那一帧就会朝框内掉头，被顶飞、或者框画在身侧，也是一样往回走，框不会因为"身体在框外"而失效。

## 五、攻击（白）

三段自带计时，`E_FirstAttackPhase` 在状态内部维护：

| 阶段 | 行为 | 时长 |
| --- | --- | --- |
| 前摇 Windup | 水平立定，跟着玩家转身；**结束时锁子弹方向** | `windupTime` |
| 攻击 Strike | 立定，进这一帧从枪口发一颗子弹 | `strikeTime` |
| 后摇 Recover | 看得见玩家就朝玩家爬，进到 `recoverChaseRadius` 以内就站住；被框边 / 墙 / 悬崖挡住也原地 | `recoverTime` |

- 子弹方向 = 前摇结束那帧的 `玩家位置 − 自身位置` 归一化；玩家正好重合时退回到朝向。
- 枪口 = 自身位置 + 控制器上的 `bulletMuzzle`（x 随朝向镜像，Editor 里画成黄色小圈）。
- `bulletSpeed = 0` 或预制体没给子弹 → 动作照走，不开火。
- 后摇爬速用 `recoverChaseSpeed`，**恒速**（`SetVelocityX` 直接写，不走 `moveAcceleration` 逼近，所以起步不会冲一下）；只动 x；不踩地时不动。
- 后摇爬到 `recoverChaseRadius`（两个身体中心的 x 距离）就停：再爬会把玩家顶走。留 0 就等于一路爬到玩家身上。

## 六、黑与白

- **白**：上面整套。
- **黑**：对玩家没有反应，一直巡逻。攻击进行中也当帧结束。

## 七、颜色

平时不配色：`View` 里摆的是什么颜色就是什么颜色，**世界反转为黑时按 `rgb = 1 - rgb` 取反**（alpha 不动），回到白世界原样还原。初始色在 `EnemyTint` 构造时（Awake）从 `View` 下所有 `SpriteRenderer` 缓存一次，之后只做取反、不改缓存值。

只有攻击三段读资产：`Assets/Resources/Data/Enemy/EnemyVisualConfig.asset`，前摇 / 攻击 / 后摇 × 黑白共 6 个字段，三只敌人共用一份。资产没建时用脚本里的默认值。

| 展示态 | 白 | 黑 |
| --- | --- | --- |
| 前摇 Windup | `windupWhite` 橙 | `windupBlack` 暗红 |
| 攻击 Strike | `strikeWhite` 近白 | `strikeBlack` 深灰 |
| 后摇 Recover | `recoverWhite` 灰蓝 | `recoverBlack` 墨青 |

巡逻 / 追击 / 返回一律走初始色（黑世界取反），**不查资产**。`EnemyTint.Apply` 只在展示态或极性变化时写一次，`View` 下所有 `SpriteRenderer`（含未激活）一起改。毛毛虫黑世界永远进不了攻击，所以它实际上用不到那 6 个字段，全程是初始色的取反。

## 八、预制体与配置（脚本之外要做的事）

- 根节点：`Rigidbody2D` + `E_FirstMotor` + `E_FirstController`；`config` 只挂在 `E_FirstController` 上，装配时注入给 `E_FirstMotor`。
- **`E_FirstConfig.enableGravity` 必须勾上**：刚体的 `gravityScale` 被 `MoveMotor` 强制归零，没开逻辑重力这只虫子就永远悬在半空（离地不掉、踩不到地面判定）。
- `maxFallSpeed` 建议 20 左右：它同时是下落速度上限，调太小会像羽毛一样飘下来。
- 子物体按名字找，不在 Inspector 里连线：`View`（显示）、`BodyCollider`（身体，非触发器）、`DetectCollider`（**视线范围**）、`AttackCollider`（接触伤害，可选）。后三根的名字与层都由 `EnemyColliderConfig` 资产决定，`EnemyColliderSetup` 在 Awake 里按它统一改写（探测盒只认 Player 层、攻击盒排除 Enemy 层），预制体上勾什么都会被覆盖。
- 三只敌人共用两份资产，**要在 `Assets/Resources/Data/Enemy/` 下建**（右键 Create ▸ Refraction ▸ Settings ▸ EnemyCollider / EnemyVisual），文件名必须正好是 `EnemyColliderConfig` / `EnemyVisualConfig`：代码按 Resources 路径加载，改名或换目录就退回脚本默认值并打 warning。
- `View` 下建议放 `Bottom` / `Top` 两个空物体：脚底探测、贴地、悬崖探针都以 Bottom 为基准；缺了退回碰撞体底边（贴地有偏差，会打一条 warning）。
- **`E_FirstMotor` 的 `groundProbe.layerMask` 与 `wallProbe.layerMask` 必须分开勾**：地面填 Ground、墙面填 Wall。留默认 `Everything` 会把玩家与其它敌人的碰撞体当成地面，虫子会踩在别人头上。
- `bulletPrefab` 留空 = 不开火（前摇与后摇照走）。

## 九、尚未实现

- 受击表现与死亡演出：血打空直接 `Destroy`。
- 后摇爬行不绕障：只看前方一格有没有墙 / 悬崖，卡在台阶下会一直顶着。
- 没有追击态：玩家跑出视线就回巡逻，不会追过去（按需求）。
