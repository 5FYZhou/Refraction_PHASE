# 敌人 E_Second（飞行）

> 依据代码：`E_SecondLogic`（目标 / 领地 / 极性）、`E_SecondMoveGroup`（四态切换）、`E_SecondPatrolState` / `E_SecondChaseState` / `E_SecondReturnState` / `E_SecondAttackState`。
> 表现层：`E_SecondController`（装配、喂玩家位置、换色）、`E_SecondMotor`（飞行执行器，只探墙）。
> 数值全在 `E_SecondConfig` 资产里。

这套逻辑与 `Enemy` 完全独立：不共用 `EnemyLogic` / `EnemyMoveGroup` / 任何 Enemy 状态，只共用 `ActorLogic`、`MoveMotor`、`StateGroup`、`BulletLogic` 这些框架件，以及探测触发器的转发器（`EnemyDetectZone` 认 `IDetectSink`，三只敌人统一由 `EnemySight` 实现，控制器不再各自管视线）。

## 一、三种范围

| 范围 | 是什么 | 作用 | 参数 |
| --- | --- | --- | --- |
| 视线 | `DetectCollider` 子物体上的触发器（与 Enemy 同一套转发器） | 玩家进触发器就进追击 / 攻击，出去只关视线 | 预制体上画触发器尺寸 |
| 巡逻框 | 编辑器画的方框 | 只在它的 x 区间里来回飞，也是“返回态”的落点 | `patrolOffset` + `patrolSize`（controller，局部坐标 → 运行时快照成世界坐标） |
| 追击框 | 编辑器画的方框，通常比巡逻框大 | 玩家 x 或 y 出框立刻脱战 | `chaseOffset` + `chaseSize`（同上） |

- 视线走触发器而不是半径，和 `Enemy` 保持一致：碰撞体画多大就是看多远，不用在参数里对数字。
- 追击框单独一份，是“追出去一段再回头”的缰绳：玩家出了巡逻框但还在追击框里时继续追，出追击框才算脱战。
- **巡逻框只判 x**（巡逻是同一水平线上的往返，高度只用来画框）；**追击框 x、y 都判**，玩家从上面或下面飞出去一样算脱战。追击框高度留 0 就退化成只判 x。
- 巡逻框不启用（`patrolSize.x ≤ 0`）时巡逻只朝一个方向飞、撞墙掉头；追击框宽度留 0 表示无界 —— 只要玩家还在台上就一直追。

## 二、每物理帧顺序

1. `E_SecondMotor.SampleWorld`：探墙（身体周围两圈之间的圆环，见第十节），地面恒为 false。
2. `BeginStep` → 条件组 → `OnFixedTick`。
3. `E_SecondLogic.FixedTick(ctx, target)` 写入本帧玩家位置与“在不在视线里”，然后走状态组。
4. 状态组：先抢占，再判 `IsDone`，最后 Tick 当前状态。
5. `ApplyGravity`（配置关掉重力，这里是空转）→ `Commit`。
6. 表现层比对 `Visual` 与 `Black`，任一变化就给 `View` 下所有 `SpriteRenderer` 上色。

## 三、四态与切换

| 机制 | 条件 | 下一态 |
| --- | --- | --- |
| 抢占 | 巡逻 / 返回中 · 玩家在视线内 | 追击 |
| 抢占 | 追击中 · 玩家在视线内 | 攻击 |
| Fallback | 追击结束（玩家 x 或 y 出追击框）| 已在框外：身体在巡逻框里回巡逻，出去回返回 |
| Fallback | 攻击结束（后摇结束时看不见玩家）| 玩家还在追击框里 → **接着追击**；否则同上 |
| Fallback | 返回结束（到达 / 撞墙）| 同上 |
| Fallback | 首帧 | 同上（还没发现过玩家时：框内巡逻、框外返回）|

- **追击只有一条结束条件：玩家的 x 或 y 出了追击框**（`E_SecondChaseState.IsDone => !E_SecondLogic.Alert`，`Alert` 用 `EnemyArea.Contains` 判二维）。触地、撞墙、被墙挡住原地不动都不算结束。
- Fallback 也按这条判：只要玩家还在追击框里就回追击，所以攻击打完（看不见人）也不会就地放弃。
- 攻击不被抢占：起手后一定走完前摇 + 攻击 + 后摇。
- 攻击**没有冷却**：后摇结束只要还看得见就原地重进前摇（`E_SecondAttackState.EndRecover`）；看不见才置 `_done`，交给 Fallback。

## 四、巡逻

- 只动 x 轴：每帧 `SetVelocityY(0)`，所以上浮或击退留下的高度就是新的巡逻高度。
- 方向：越出框的 `[MinX, MaxX]`、或身体周围那圈圆环里探到墙就掉头；掉头那一帧不算墙（读的是旧一侧的数据）。
  - 圆环不分方向，脚下的地面也算墙：贴地摆的出生点会让它在原地反复转身，摆放要求见第十节。

## 五、追击

- 朝玩家**直线**飞（斜向也飞），速度用 `moveSpeed`。
- 距离 ≤ `chaseStopDistance` 就悬停，再近会把玩家顶走。
- 始终面朝玩家：`SnapVelocity` 会按速度方向改朝向，所以之后再把水平朝向盖回来；玩家正上/正下方时保持原朝向。
- **结束条件只有「玩家出追击框」**：x 出去或 y 出去都算，追击不看墙、不看地面、不看视线。贴地飞、顶着墙飞、被墙挡住速度归零，都仍在追击态（在框里就继续追）。
- 看不见玩家也照追：位置每帧由表现层喂进来，所以它会朝玩家当前所在位置飞，不存在"记住最后看到的位置"。

## 六、返回

- 朝巡逻框中心直线飞，到达（≤ 0.2 单位）就结束 → 巡逻。
- **圆环里探到墙**（不分方向），就以当前位置为新的巡逻中心，尺寸不变（`E_SecondLogic.Recenter`），然后结束 → 巡逻。这条是"回不去就地把家安下来"，避免顶着墙抖动。
  - 重定位时**追击框按同一段位移一起平移**（尺寸不变、相对关系不变）：家搬到哪，缰绳就跟到哪，否则安家之后立刻脱战。追击框宽度留 0（无界）时不动它。
- 返回途中玩家进视线 → 抢占进追击。

## 七、攻击（按敌人自身形态二选一）

形态来自控制器 Inspector 的 `blackForm`（勾上 = 黑形态），构造时注入 `EnemyLogicBase`；世界每换一次极性它翻到另一态，所以「世界黑、敌人白」是成立的。

**黑（冲刺）**
1. 前摇 `blackWindupTime`：悬停、跟着玩家转身。
2. 攻击：前摇结束那一帧锁定方向（朝玩家位置的单位向量），以 `blackDashSpeed` 冲向这个方向，**每帧重写速度**。
   - 结束条件只有三条：**真实命中玩家**、撞到墙/地面、冲满 `blackDashMaxTime` 秒（安全上限，防止冲进虚空一直飞）。
   - 「真实命中」= `AttackBox` 结算成功、**玩家的血真的掉了一格**，然后 `IHitLanded.OnHitLanded()` 回调到 `E_SecondLogic`，冲刺在下一物理帧读到标记就收手。不再做几何上的"贴近就算撞到"。
   - 因此玩家在**无敌窗**里、或者这只怪的**接触间隔**（`contactInterval`，默认 1 秒）还没到时，命中回调根本不会来 —— 这一刀会直接从玩家身上穿过去，只能等撞墙或超时收手。这是刻意的：宁可冲过头，也不要在没造成伤害时假装撞上。
   - 「撞到墙」看身体周围那圈圆环，不分方向：斜冲、竖冲、贴地冲都在碰到的那一帧结束。
3. 后摇：**水平停住、以 `riseSpeed` 匀速上浮**一段随机距离（`riseMin` ~ `riseMax`）。
   - 所以后摇时长不是配置项，而是 `随机距离 / riseSpeed` 除出来的。
   - 进后摇那一帧就把速度换成 `(0, riseSpeed)`，不等下一帧 —— 命中那一帧身体还带着冲刺速度，晚一帧会先滑进玩家身体里。

**白（子弹）**
1. 前摇 `whiteWindupTime`：朝玩家飞，进到 `whiteChaseRadius` 以内就刹住（速度 `whiteChaseSpeed`），同时跟着玩家转身。
2. 攻击：前摇结束那一帧锁定方向、发一颗子弹（`BulletLogic`，复用玩家那套）。
   - 出膛点 = 身体中心 + 控制器上的 `bulletMuzzle`（默认 `(0,0)`，也就是从身上冒出来，视觉上不拖穿模）；选中控制器时 Editor 会画一个黄色小圈。
   - 这一阶段只有**一帧**（开火即完成），所以白色 `Strike` 的颜色只会闪一下。
3. 后摇 `whiteRecoverTime`：继续朝同一个半径靠——玩家跑远了就追上去，已经在圈里就不动。

- 走位**只进不退**：距离大于半径才给速度，小于等于半径一律刹停，所以玩家贴脸时它不会把人往外顶。
- 三个距离参数别混：`chaseStopDistance` 是追击态悬停距离（再近会推人），`whiteChaseRadius` 是白攻击想靠到多近，`wallOuterRadius` 是撞墙圆环的判定半径。默认 `chaseStopDistance(3) < whiteChaseRadius(5)`，所以正常追击到位后白攻击不会再挪。

两段后摇结束时都做同一件事：看得见 → 重进前摇；看不见 → 结束攻击。

### View 的 z 轴旋转（纯表现）

**黑（冲刺）**
- **前摇结束时锁定角度**：`E_SecondController.TickAimLock` 在「阶段刚变 `Strike` 的那一物理帧」（`DashAiming` 的上升沿）把当时的「自身 → 玩家受击盒中心」夹角算出来存下，之后玩家再换位也不追。
- **冲刺起手才开始转**：目标角一直是那个锁定的角，按 `dashTurnSpeed` 度/秒逼近。前摇期间 `View` 一动不动。
- 后摇一开始目标角变回初始角（装配时记下的 `localEulerAngles.z`，通常是 0），同样按 `dashTurnSpeed` 平滑转回，不会瞬间弹回。

**白（子弹）**
- 攻击三段（前摇 / 攻击 / 后摇）**全程实时转向玩家**：每帧重算夹角，玩家横移就跟着转，开火方向与朝向始终一致。

**通用**
- 角度一律**钳在初始角 ±90° 之内**（`MaxViewAngle`），再大就是头朝下，读起来像在飞而不是在扑。
- 这一步放在**渲染帧**（`IRenderTicked`）里，物理帧 50Hz 会把转向卡成跳格。
- 根节点的 `localScale.x` 会随朝向翻号（`MoveMotor.Face` 写的是根），`View` 因此是镜像的：镜像时本地角要按 `180° − 目标角` 反算（`AimAngle` 里按 `viewRoot.lossyScale.x < 0` 分叉），否则朝左时往下扑会朝上仰。

### 接触伤害（持续）

身体上的 `AttackBox` 碰到玩家受击盒就结算一次，之后**按间隔反复结算**（与 `Enemy` 同一套：`AttackBox.OnTriggerStay2D` 问 `IIntervalAttacker.ReadyToHit`）：

| 参数 | 含义 | 默认 |
| --- | --- | --- |
| `contactDamage` | 每次结算的伤害点 | 1 |
| `contactInterval` | 两次之间的最小间隔（秒） | 1 |
| `contactKnockback` | 每次的击退初速度 | 8 |

- 只写进出结算的话，玩家站在接触盒里不动就只能掉一次血；持续型攻击者才走 `Stay` 那条路。
- 被无敌窗挡掉的那一次**不消耗间隔**（`AttackBox` 只在 `TakeDamage` 返回 true 后才 `MarkHit`），所以把 `contactInterval` 调到玩家 `invulnerableTime`（0.4s）以下也不会白吃冷却。
- `contactInterval = 0` 表示不设间隔：每个物理帧都试一次，实际节奏由玩家的无敌窗决定，不建议。
- 伤害源是 `DamageSource.Contact`，`E_SecondLogic.TakeDamage` 直接丢弃这类伤害 —— 敌人之间挤在一起不会互相掉血。

## 八、颜色（只按极性取反）

不做状态配色：`View` 里摆的就是最终色，敌人是黑形态时 `rgb = 1 - rgb` 取反（alpha 不动），回白形态还原。初始色由 `EnemyTint` 在 Awake 缓存一次，缓存的是「设计态」颜色，反复反转不会越取越偏。

`Apply` 只在极性变化时写一次，不会每帧刷材质；`View` 下所有 `SpriteRenderer`（含未激活的）都会跟着取反。巡逻、追击、返回、攻击前后摇都不再单独配色，唯一还能改色的只有受击闪白（材质 `_FlashAmount`，与这里无关）。

## 九、预制体接线（本次只写了脚本）

- 根节点：`Rigidbody2D`（Dynamic）+ `E_SecondMotor` + `E_SecondController` + 身体碰撞体 + `HurtBox` + `AttackBox`。
- `config` 只挂在 `E_SecondController` 上（`E_SecondConfig` 资产需要自己在 `Resources/Data/Enemy` 下新建），装配时注入给 `E_SecondMotor`。
- **`E_SecondMotor.wallMask` 必须勾上墙所在的层**（现有预制体勾的是 `Ground`，墙与地面一起算，所以贴地飞时脚下那层也算撞到），留空等于永远探不到墙，返回态就撞不了墙。
- `View` 子物体下放 Sprite，所有 `SpriteRenderer` 都会跟着换色。
- 冲刺转向的节点是控制器上的 `viewRoot`：留空自动按名字找 `View`；它当前的 `localEulerAngles.z` 会被记成「初始角」，所以想让怪物平时就有个倾角，直接摆 `View` 的旋转即可，不用改代码。
- `bulletPrefab` 留空 = 白形态不开火（仍然走完前摇与后摇）；出膛点 `bulletMuzzle` 在同一个组件上（局部坐标，x 随朝向镜像），不进 `E_SecondConfig`。
- **必须有 `DetectCollider` 子物体**：名字必须是这个（`EnemyColliderConfig.detectChildName`）；Is Trigger 与层不用手勾，`EnemyColliderSetup` 会在 Awake 里按资产设成「触发器 + 只认 Player(7) 层」；`E_SecondController` 按名字找它并补挂转发器，找不到只警告，这只敌人会永远看不见玩家。
- 三只敌人共用的碰撞体资产要在 `Assets/Resources/Data/Enemy/` 下建（右键 Create ▸ Refraction ▸ Settings ▸ EnemyCollider），文件名必须正好是 `EnemyColliderConfig`：代码按 Resources 路径加载，改名或换目录就退回脚本默认值并打 warning。
- 两个框都在 controller 的 Inspector 上画（`EnemyGizmos` 统一绘制）：`patrolOffset / patrolSize` 是绿框，`chaseOffset / chaseSize` 是橙红框，`Reset()` 会给默认值（12×4 与 20×6）；另有一圈青色的 `DetectCollider` 视线范围框。
  - 运行中 gizmo 画的是逻辑里那份快照（`E_SecondLogic.PatrolArea` 与 `E_SecondLogic.ChaseArea`），钉在世界坐标上、不跟敌人走；只有 `Recenter` 成对平移两个框之后才会一起挪。没运行时画的才是 Inspector 参数框。
- `E_SecondMotor` 的撞墙探针画出两个同心圆（`wallInnerRadius` / `wallOuterRadius`），两圈之间那圈就是撞墙判定区，见下一节。

## 十、撞墙探针

`E_SecondMotor.SampleWall` 判的是**身体周围两圈之间的圆环里有没有墙**，不再是「朝前打一条胶囊」：

| 参数 | 含义 | 默认 |
| --- | --- | --- |
| `wallInnerRadius` | 圆环内径，取身体碰撞体半径；比这更近的只可能是自己或已嵌进墙里，不算探到墙 | 0.45 |
| `wallOuterRadius` | 圆环外径，两圈之间是判定区 | 0.75 |
| `wallMask` | 墙所在的层 | 空（必须勾） |

- 用圆环而不是朝前的胶囊，是因为黑冲刺的方向由玩家位置决定，可能是斜的甚至竖的；只探 ±x 会漏掉撞天花板、撞斜墙。
- 一次 `Physics2D.OverlapCircleNonAlloc` 取外圈，再按最近点把内圈里的剔掉（那一段要么是自己的身位、要么已经嵌进墙里），`IsTrigger` 与自己的子物体跳过；命中任何一个就算撞到，不分方向。
- 圆环只回一个布尔 `TouchingWall`：撞墙、撞地、撞天花板对这只怪是同一件事——巡逻掉头、返回就地安家、冲刺进后摇，都不需要知道墙在哪一侧。

### 初始摆放：不能触墙（含地面）

圆环不分方向，所以"贴着地面出生"等价于"出生就在撞墙"。这是本实现**已知的漏洞，不打算修**，用摆放约定规避：

- **出生点必须让圆环（`wallOuterRadius`，默认 0.75）里没有任何实体碰撞体**，地面与平台顶面也算。默认值意味着身体中心离最近的墙或地至少 0.75 单位。
- 不满足时的表现：巡逻每帧掉头，在原地震；黑冲刺一出招就被脚下的地面判成撞墙，当场进后摇。
- 贴着地面飞的效果不能靠调 `wallOuterRadius` 做小：内圈里本来就会剔除，两圈一旦重叠就等于永远探不到墙，返回态也就永远不会就地安家。

## 十一、已知取舍

- 视线只认触发器：玩家走出 `DetectCollider` 就不再进攻击（`Visible=false`），但只要还在追击框里就继续追，位置一直是最新的 —— 这只敌人不会像 `Enemy` 那样记住“最后看到的位置”再摸过去。反过来说，第一次发现玩家必须靠视线（`Known` 由 `DetectCollider` 首次进入置位），不会凭空去追框里的陌生人。
- 两个框都是进游戏那一刻的快照，之后敌人飞多远都不改；只有撞墙重新安家（`Recenter`）那一刻，巡逻框与追击框会一起平移同样距离。
- 白色攻击的"攻击"阶段只有一帧，颜色是闪一下而不是持续一段。
- 白子弹默认从**身体中心**出膛（控制器 `bulletMuzzle` 默认 `(0,0)`）：触发盒一出生就压在发射者自己的受击盒里，靠 `AttackBox.SetOwnerRoot` 跳过开火者层级，所以不会自伤；把 `bulletMuzzle` 调大只影响观感。
- 黑冲刺不因为距离远而放弃：前摇结束就冲，撞不到就冲满 `blackDashMaxTime` 再进后摇，冲不到就再来一轮。
- 冲刺结束靠命中回调，回调发生在物理步里（`FixedTick` 之后），所以最多晚一帧读到标记；那一帧身体已经换成上浮速度，不会继续往前扎。
- 没有死亡演出：血打空直接销毁。
- 圆环探墙不分方向，贴着地面或墙面出生会让巡逻原地反复转身、冲刺一出招就结束；这是已知漏洞，靠"初始摆放不触墙"规避，见第十节末。

## 十二、调试面板

场景里的 `DebugPanel` 右栏（`TxtEnemyInfo`）在 `Enemy` 之后接着列这只敌人，每只两行，由 `E_SecondController.AppendDebug` 拼出来（装配完成才出现）：

```
E_Second 白 追击
视有 领无 巡-3.20,0.00 追-3.20,0.00
```

- 第一段：名字、极性（黑 / 白）、当前状态（`EnemyStateTag`）。
- `视`：玩家在 `DetectCollider` 里；`领`：玩家还在追击框里（`Alert`，x、y 都要在框内）。
- `巡` / `追`：两个框**中心的世界坐标 x,y**。撞墙安家后这两列会一起跳，用来确认 `Recenter` 有没有漏搬追击框；没启用框的那一栏显示「无」。
- 巡逻框的高度不参与判定，那栏的 y 只是画框位置；追击框的 y 是真的会判出框。
- 面板只读：`E_SecondController` 只多暴露了 `Attached` 与 `Logic` 两个只读属性，面板不写回任何状态。
