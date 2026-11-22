# 敌人 Spliter（飞行怪 2）

> 依据代码：`Logic/Actor/Spliter/`（`SpliterLogic`、`SpliterMoveGroup`、四个状态）、
> `Presentation/Move/SpliterMotor`、`Presentation/Actor/Spliter/`、`Data/SpliterConfig`。
> 与其它敌人零共用，只共用 `EnemyLogicBase` / `MoveMotor` / `StateGroup` / `BulletLogic` 这些框架件。

## 一、三个范围

| 范围 | 来源 | 判定 |
| --- | --- | --- |
| 视线 | `DetectCollider` 子物体（同其他敌人） | 进 / 出触发器决定 `Visible`，只用来**开始**追击 |
| 巡逻 | 控制器上的 `patrolOffset` + `patrolSize`（编辑器画框，`Start` 里快照成世界坐标） | 只用 x：到左右边界掉头 |
| 追击 | 控制器上的 `chaseOffset` + `chaseSize` | x、y 都算，**玩家出框即脱战** |

- 没画框（`patrolSize.x ≤ 0`）= 巡逻无界，只靠墙掉头；追击框宽 0 同理。
- 框一旦快照就不再跟着敌人动：巡逻 / 返回时读到的是同一条边界。
- 触发攻击的距离是 `SpliterConfig.attackRange`（两中心之间的欧氏距离），Scene 里以自身为圆心画一个圆。

## 二、每物理帧顺序

1. 采样世界：`SpliterMotor` 探身体周围那圈圆环里有没有墙（不探地、不贴地）。
2. `EnemyLogicBase` 的帧序：BeginStep → 状态 Tick → 重力（`enableGravity = false`，空转）→ Commit。
3. `SpliterLogic.FixedTick` 先写入本帧玩家信息（视线、位置、受击盒中心）。
4. 状态组推进：抢占 → `IsDone` → Fallback → 当前态 Tick。
5. 表现层：转 z 轴、刷极性色、蓄力吸入、推进子弹。

## 三、四态与切换

| 机制 | 条件 | 下一态 |
| --- | --- | --- |
| 抢占 | 在追击框内 · 冷却已好 · 距离 ≤ `attackRange` | 攻击 |
| 抢占 | 玩家在视线内（且不是已在追击） | 追击 |
| Fallback | 玩家还在追击框里 | 追击 |
| Fallback | 不在追击框里 · 身体中心在巡逻框 x 内 | 巡逻 |
| Fallback | 不在追击框里 · 身体中心在巡逻框外 | 返回 |

- **进入追击看视线，退出追击只看追击框**（玩家离开框才结束）。追击途中撞墙、触地都**不打断**。
- 攻击不被抢占，起手后一定走完前摇 / 攻击 / 后摇。
- 攻击起手不看视线，只看「在领地内 + 够近 + 冷却好」：玩家躲在掩体后面不该让它收手。

## 四、巡逻

- 只动 x 轴：竖直速度每帧清零，巡逻高度 = 进状态时的高度。
- 方向：到 `[MinX, MaxX]` 边界、贴墙，任一就掉头。

## 五、追击

- 朝玩家直线飞（二维），贴到 `chaseStopDistance` 就悬停，始终把水平朝向对准玩家。
- 结束条件只有「离开追击框」；撞墙不结束，玩家离开框后由 Fallback 决定回巡逻还是先返回。

## 六、返回

- 朝巡逻框中心直线飞，进到 0.2 单位内停住、结束返回。
- **撞墙就地重定义中心**：`Recenter` 把当前站位同时设成巡逻框与追击框的中心，两个框尺寸都不变。
- 返回途中玩家进入视线 → 抢占进追击。

## 七、攻击（按自身形态二选一）

形态来自控制器 Inspector 的 `blackForm`，世界每换一次极性它会翻到另一态。

**黑 · 冲撞**
1. 前摇 `chargeWindupTime`：立定不动，水平朝向跟着玩家。
2. 前摇结束那一帧锁方向（朝玩家的单位向量），同时把**冲撞距离**定为「此刻两者连线距离 × 2」。
3. 攻击：按 `chargeSpeed` 沿锁定方向平移，累计位移走满冲撞距离就进后摇。**没有时间上限**。
   - 撞墙也当场收手（顶在墙上会一直推），这条是对需求的补充。
4. 后摇：随机方向浮动（见第八节）。

**白 · 散射弹幕**
1. 前摇 `barrageWindupTime`：立定不动，**只把身体转向玩家**（见第十节）。
2. 前摇结束那一帧锁方向，**不再转动**。
3. 攻击：一次性打 `waveCount` 波，第一波在起手当帧出，之后每 `waveInterval` 秒一波。
   - 每波 `bulletsPerWave` 颗，以锁定方向为中心对称张开，相邻两发夹角 `spreadAngle`（总张角 = (发数 − 1) × 夹角）。
   - 最后一波打完立刻进后摇，不等待。
4. 后摇：与黑形态完全相同。

**两段后摇结束**时做同一件事：还在追击框内、距离够近、冷却又好了 → 原地重进前摇；否则结束攻击，交给状态组。

- 冷却从前摇起手那一帧算，整套动作都在间隔内：黑用 `chargeCooldown`，白用 `barrageCooldown`。

## 八、后摇浮动

- 方向随机（0 ~ 2π 均匀），距离在 `[floatDistanceMin, floatDistanceMax]` 里随机。
- 速度恒为 `floatSpeed`，所以**后摇时长 = 随机距离 / floatSpeed**，没有单独的后摇时长时间参数。
- 浮动期间不锁定朝向、不看玩家位置：它只是把身体甩出去一段。

## 九、弹反（只有黑形态）

- 范围是预制体子物体 `ReboundCollider` 的触发器；`SpliterRebound` 在 Awake 补挂，只转发进入事件。
- 只认玩家阵营的飞行子弹；冷却 `reboundCooldown` 之内只能弹一发，冷却从触发那一刻算。
- 弹反本身走 `Bullet.TryReflect`：原弹反向、原数值、归敌方。
- **额外两发**：方向与被弹反那发完全相同（平行），位置沿弹道法线各偏 `reboundSpread`（一边一颗）。
  这两颗用 `SpliterConfig` 的 `bulletSpeed / bulletDamage / bulletKnockback`，不吃原弹的数值。
- 实现顺序很重要：`ReflectDirection` 与 `Position` **必须在 `TryReflect` 之前取** —— 它内部会把子弹回池，`Reset` 会清零速度并把位置复位。

## 十、View 的 z 轴旋转（纯表现）

- 只有**白形态攻击**才参与转向：前摇期间每帧实时对准玩家（自身 → 玩家受击盒中心），前摇结束那一帧定格，攻击与后摇期间**不再转动**。
- 锁角与逻辑层锁方向用的是同一份玩家位置、同一物理帧，所以朝向与弹道一致。
- 出攻击态后按 `viewTurnSpeed` 平滑转回初始角（`View` 装配时记下的 `localEulerAngles.z`）。
- 黑形态全程不转。
- 步进放在**渲染帧**（`IRenderTicked`），物理帧 50Hz 会卡成跳格。
- 角度钳在初始角 ±90°；根节点 `localScale.x` 会随朝向翻号，`View` 因此是镜像的，镜像时本地角按 `180° − 角` 反算（`AimAngle` 里按 `viewRoot.lossyScale.x < 0` 分叉）。

## 十一、接触伤害（持续）

与其它飞行怪同一套：身体上的 `AttackBox` 碰到玩家受击盒结算一次，之后按 `contactInterval` 反复结算。
被无敌窗挡掉的那次不消耗间隔（`AttackBox` 只在 `TakeDamage` 返回 true 后才 `MarkHit`）。
伤害源是 `DamageSource.Contact` / `EnemyBullet`，`EnemyLogicBase.TakeDamage` 直接丢弃，敌人之间不会互相掉血。

## 十二、预制体与资产（本次只写了脚本）

- 根节点：`Rigidbody2D`（Dynamic）+ `SpliterMotor` + `SpliterController` + 身体碰撞体 + `HurtBox` + `AttackBox`。
- 子物体按名字找：`View`、`BodyCollider` 类的身体盒、`DetectCollider`（触发器，视线）、**`ReboundCollider`（触发器，弹反范围）**。
- `config` 挂控制器上，指向新建的 `SpliterConfig` 资产；装配时注入给 `SpliterMotor`。
- **`SpliterMotor.wallMask` 必须勾上墙所在的层**，留空等于永远探不到墙：返回态撞不了墙、中心永远不会重设。
- `bulletPrefab` 留空 → 白形态不开火、也弹不了反（动作照走）。
- 视线 `EnemySight(false)`：出视线只关可见性，位置仍然有效。

## 十三、尚未实现

- 冲撞不做落点预测：撞墙才收手，撞到别的敌人只靠物理推挤。
- 弹幕没有瞄准预判：打的是锁定那一帧玩家所在的方向（轨迹直线）。
- 受击与死亡没有演出：血打空直接 `Destroy`。
- 调试面板只列 `EnemyController` 与 `E_SecondController`，这只暂时不显示。
