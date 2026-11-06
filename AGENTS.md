# AGENTS.md

> 本文件是所有参与「折光·PHASE」工程的 AI Agent 的**最高行为宪章**。
> 任何代码生成、修改、重构必须严格无条件遵守以下纪律。

工程底盘：Unity（团结引擎 2022.3.62t17）、2D + URP、Input System、**无 asmdef**（全部代码在 `Assembly-CSharp`）。

---

## 1. 唯一机器判据：编译门 (Compile Gate)

你在本工程的任何代码改动，最终的唯一验收标准是执行并全绿通过：

```bash
dotnet build Assembly-CSharp.csproj
```

- **通过判据**：**0 error**。
- **警告基线**：记在 `Tools/compile-gate.json`（当前 2 条 `CS0649`：`GameRoot._uiMgr` / `_resMgr`）。
  - **新增任何 warning 视为不合规**，不许用 `#pragma warning disable` 掩盖。
  - `MSB3277`（`System.Net.Http` 版本冲突）来自团结引擎自带的 netstandard shim，与代码无关，不作为判据。
- 推荐用 `pwsh Tools/compile-gate.ps1` 代替裸 build：它现场重扫文件表（避免 csproj 清单过期漏编），
  并顺带验上面 §2 的越层引用与 §5 的"只有 GameRoot 有 Update / FixedUpdate"。
- 编辑器内编译（团结引擎）同样必须 0 error；两者不一致时以编辑器为准并回头查出原因。

---

## 2. 四层分层与边界铁律

层次依赖具有严格单向性：`Presentation ➔ Logic ➔ Data ➔ Foundation`。

```text
  [Refraction.Presentation]  ← 表现层/组合根/MonoBehaviour/物理探针/UI/驱动
              │
              ▼
    [Refraction.Logic]       ← 纯业务逻辑/状态机/输入缓冲/关卡推进（严禁碰场景）
              │
              ▼
    [Refraction.Data]        ← 调参 SO（PlayerConfig）与纯数值定义
              │
              ▼
  [Refraction.Foundation]    ← 纯地基：Singleton/Require/Cooldown/StateMachine，谁都能引它
```

物理目录与命名空间严格对齐：`Assets/Scripts/{Foundation,Data,Logic,Presentation}/**` ↔ `Refraction.{Foundation,Data,Logic,Presentation}.**`。

### 🔴 致命雷区：单程序集里没人替你拦边界

全工程仍是**一个** `Assembly-CSharp`，分层只靠命名空间与目录自觉——**编译器不会报错、测试不会变红**。
所以：`Foundation` / `Data` / `Logic` 的代码里**严禁** `using Refraction.Presentation.*`，也不许反向引用同层之外的实现类型。破一次就再没人守。

### 🔴 目录与 `.meta` 归 Unity 管，AI 只碰 `.cs`

- 目录已统一为正确拼写：`Assets/Scripts/Presentation/`、`Assets/Scripts/Generated/`。**不许再出现** `Presentaion` / `Genereted` 这类历史拼写。
- **严禁 AI 手写、编辑、删除任何 `.meta`**：`.meta` 一律由 Unity 导入时自动生成（新 `.cs` 只要落到磁盘，编辑器刷新就会补上）。
  guid 是 56 字符 base64，手写错一位会被 `AssetDatabase` **静默忽略**，表现为引用丢失、预制体字段变 `None`，编译门完全查不出来。
- 重命名 / 移动 / 删除资源请在 Unity 的 Project 窗口里做（或用 Unity 的 API），不要用文件管理器或脚本批量搬目录——`.meta` 不跟着走，等于把引用全掐断。

### 🔴 逻辑层纯净度纪律 (`Logic/`)
- **严禁**继承 `MonoBehaviour`。
- **严禁**触碰场景对象（`GameObject`、`Transform`、`Component`、`Rigidbody2D`、`Tilemap`）。
- **严禁**直接读 `Time.time` / `Time.deltaTime`：时间与 Δt 必须走 `LogicContext.GameTime`（`now` / `deltaTime`）注入。
- 只允许 `Vector2`、`Vector3Int`、`Mathf` 等纯值类型，以及 `PlayerConfig` 这类纯数据 SO；表现层能力一律通过接口（`IMoveMotor`、`IDoorReader`、`IStateMove`）消费。

---

## 3. 生成物与修改禁区 (Forbidden Zones)

以下内容由工具生成，**严禁手动修改**（改了必丢）：

- `Assets/Scripts/Generated/InputSys.cs` —— InputActions 生成物。
  改键位请改 `Assets/Scripts/Presentation/Input/InputSys.inputactions`，让生成器重写。
- **所有 `*.meta`** —— 一律由 Unity 自动生成，**AI 不许创建、编辑、删除**（连"顺手补一个新文件的 .meta"都不行）。
- `Docs/**/*.txt` —— 用户手写的需求文档（`场景块需求.txt`、`玩家相关需求.txt`、`怪物通用属性接口需求.txt` 等）。
  **只读**：不许改内容、不许改文件名、不许删、不许替换成 `.md`。要记录实现取舍就另写 `.md`，别去动 `.txt`。
- `Library/`、`Temp/`、`obj/`、`Logs/` —— 编辑器缓存与构建产物。
- `*.csproj`、`*.sln`（`Assembly-CSharp.csproj` 等）—— 编辑器生成，切分支/换版本会被整份重写；需要加程序集依赖时改 Unity 里的设置，别改 csproj。

---

## 4. 数据、调参与表现权责边界 (Data vs SO vs Art)

1. **调参 SO（`Assets/Scripts/Data/*.cs` + `Assets/Resources/PlayerConfig.asset`）**：
   - 归程序管辖：移动/跳跃/冲刺/重力的运动学参数、输入缓冲窗口、姿态倍率与时长都在 `PlayerConfig`。
   - 编辑器内实时生效，无需重编译；**所有手感数值只许落在这里，不许散成代码里的魔法常量**。
   - 新增参数：先加字段 + 中文 `Tooltip` + `[Min]`/`[Range]` 约束，再在 asset 上调；`Tooltip` 里写清**单位与量纲**（速度 / 加速度 / 秒 / 倍率），这是逻辑层唯一能读到的契约。
2. **逻辑层只读不写 SO**：`Logic` 消费 `PlayerConfig` 的字段，**严禁**在运行时改它（会污染编辑器里的资产）。
3. **资源按需懒加载**：
   - 预制体在 `Assets/Resources/Prefabs/`（`Player`、`Door`），由表现层按需取用并经局部字典缓存。
   - **严禁**在启动时全量预加载所有 Prefab / Sprite / Tile。
4. **接口防腐**：上层只依赖 `IMoveMotor` / `IDoorReader` / `IStateMove` 等接口，底层实现换签名时**由实现侧吸收**，不许把改动推到调用方。
5. **需求文档（`Docs/*.txt`）只读**：那是用户手写的设计意图，**严禁修改**（见 §3）。
   与代码冲突时**以代码为准**；实现差异、取舍、待办一律写进 `.md`（`Docs/说明/`），不要回写 `.txt`。

---

## 5. 运行时驱动模型与生命周期 (Runtime Model)

- **唯一真单例**：全工程仅 `GameRoot` 为真单例（`Singleton<T>` + `DontDestroyOnLoad`）。任何需要全局访问的服务挂在 `GameRoot` 上，不许再开第二个单例。
- **驱动入口唯二**：
  - **严禁**在业务组件中私自编写 `Update()` / `FixedUpdate()`（当前全工程只有 `GameRoot` 有这两个方法，保持这个状态）。
  - 所有驱动收敛在 `GameRoot.Update`（渲染帧，`Tick`）与 `GameRoot.FixedUpdate`（物理帧，`FixedTick`）。
  - 场景系统实现 `ISceneRoot` 并声明 `Order`（`SceneOrder.Entry` / `Player` / `World`），由 `GameRoot` 按 Order 统一拓扑推进；`Attach` **必须幂等**，注册后**下一帧**才 Attach（别在注册当场就跑装配）。
  - `Attach` 里再注册的根，下一帧继续；单个根抛异常不许拖垮其余根。
- **UI 与服务**：
  - 场景内**严禁**放置 `Canvas`、`EventSystem`、第二个 `AudioListener`、额外 UI 摄像机。由 `UIMgr` 统一持有常驻四层（`Bottom` / `Middle` / `Top` / `System`）。
  - UI 开关只走 `UIMgr.ShowPanel<T>()` / `HidePanel<T>()`；**面板预制体名必须与面板类名一致**（`UIMgr` 用 `typeof(T).Name` 找资源）。

---

## 6. 高性能与 2D 避坑铁律 (0 GC & Guardrails)

1. **热路径 0 GC (Hot-Path 0 GC)**：
   在 `Tick`、`FixedTick`、`State.Tick`、`BeginStep` / `Commit`、`OnTriggerEnter2D`、`TakeDamage` 等每帧或高频调用链中：
   - **严禁** LINQ。
   - **严禁**闭包分配与匿名 lambda（改用静态委托或成员方法）。
   - **严禁**每帧 `new List` / `new T[]` / `ToArray()`（用类字段复用或 Scratch 缓冲；仅在"确实有新增项"这种一次性分支里允许）。
   - **严禁**在热路径 `GetComponent`（在 `Awake`/`Attach` 里缓存引用）。
2. **2D 渲染与物理**：
   - 刚体必须：`freezeRotation = true`、`interpolation = Interpolate`。
   - **重力只有一套**：`PlayerConfig.enableGravity = true` 时刚体 `gravityScale` 必须为 `0`，否则逻辑重力与物理重力叠加，跳跃高度直接失控。
   - 物理检测优先复用 `Physics2D.CircleCast` / `CapsuleCast`（自带结果结构体，无分配），避免 `OverlapCircle` 之类的数组版本。
   - 排序强制用 `Sorting Layer` + 渲染顺序，**严禁**靠 Z 轴粗暴排序（2D URP 下 Z 只影响裁剪，不影响先后）。

---

## 7. 注释纪律 (Comment Discipline)

> 完整规范：`Docs/工程/注释规范.md`（唯一权威）。
> 机器判据已落地：`pwsh Tools/comment-lint.ps1`（退出码 0 = 通过；`-Mode report / tags / verify / seal`）。
> 基线在 `Tools/comment-budget.json`，**只许由 `-Mode seal` 改写**，不许手调数字。

注释是本工程唯一**没有编译期兜底**的产物——写多了不会编译失败、不会测试变红。
因此**注释量受预算约束**，不是"随手补点说明"的自由区。

1. **唯一判据**（每次想写注释先问这一句）：
   这条注释是不是在防止某个人做出一个「**编译器不会拦、测试不会红、只表现为手感 / 观感 / 性能错**」的改动？
   是 → 写，且压成**一行**；否 → **不写**。
2. **只写这 9 类**：跨层时序约定 / 配表与 SO 字段映射 / 单位与量纲 / 魔法值来源 / 非直觉算法依据 /
   接口契约（null 与 false 的语义）/ 已知坑与硬约束 / 静默失败 / 性能约束。
3. **禁止这 4 类**：`重复复述`（命名已说清的事）、`背景论证`（变更史、"曾经…"、"实测…"、未采纳方案）、
   `排版标签`、`冗余修饰`。进度叙事（`TODO` / 待办）不许进代码。
4. **XML 标签白名单只有 `<summary>` 和 `<remarks>`**：`<c>` `<para>` `<b>` `<i>` `<em>` `<strong>`
   `<paramref>` `<param>` `<returns>` `<list>` `<item>` `<code>` `<br>` `<p>` `<inheritdoc>`
   一律物理删除、只留其中文字；`<see cref="X"/>` 降级为纯文本 `X`。**`<summary>` / `<remarks>`
   允许在同一连续注释块内跨行开闭**（块内必须配平），其余注释仍一律单行。
5. **预算只降不升**：动过的文件，注释字符数不得超过改动前（新增文件按 ≤5 条文档注释起步）。
   确需突破必须在交付说明里写明理由，不许"顺手把红线改成合法"。
6. **改注释 == 改代码**：同样要过编译门。清理注释时不许顺手改代码 token、缩进、
   字符串字面量（`[Tooltip("…")]` 里的中文**是代码**）、行尾风格或 BOM。

---

## 8. Agent 执行改动工作流 (DoD Checklist)

当你接受任务并准备交付代码时，必须按顺序自检：
- [ ] 1. 物理目录与命名空间严格对齐（`Assets/Scripts/**` ↔ `Refraction.**`）。
- [ ] 2. 没有越层引用：`Foundation` / `Data` / `Logic` 里搜不到 `Refraction.Presentation`。
- [ ] 3. **没有手写 / 手改 / 删除任何 `.meta`**（新文件交给 Unity 生成）；没有改 `Docs/**/*.txt`。
- [ ] 4. 没有触碰任何生成物禁区（`Generated/InputSys.cs`、`Library/`、`Temp/`、`obj/`、`*.csproj`）。
- [ ] 5. 热路径无堆内存分配（0 GC），没有新增 `Update` / `FixedUpdate`。
- [ ] 6. `pwsh Tools/comment-lint.ps1` → `LINT OK`（预算不超、无新增白名单外标签）；改了注释再加
      `-Mode verify -Base <上一个已验收提交>` → `VERIFY PASS`。
- [ ] 7. `pwsh Tools/compile-gate.ps1` → `GATE OK`（0 error、警告不超基线、无新增越层 / 驱动违规）。
      裸 `dotnet build Assembly-CSharp.csproj` 只能算等价下限：它不重扫文件表，新文件可能被漏编。

---

## 9. 工程速查

| 项 | 位置 |
|---|---|
| 单例 / 驱动入口 | `Assets/Scripts/Presentation/Root/GameRoot.cs` |
| 场景根契约与 Order | `Assets/Scripts/Presentation/ISceneRoot.cs` |
| 逻辑入参快照（输入/世界/时间） | `Assets/Scripts/Logic/Context.cs` |
| 全部手感参数 | `Assets/Resources/PlayerConfig.asset` |
| 输入绑定 | `Assets/Scripts/Presentation/Input/InputSys.inputactions` |
| 输入生成物（禁改） | `Assets/Scripts/Generated/InputSys.cs` |
| 状态机说明 | `Docs/说明/玩家状态机.md` |
| 注释规范 | `Docs/工程/注释规范.md` |
| 门禁脚本（注释 / 编译） | `Tools/comment-lint.ps1`、`Tools/compile-gate.ps1`（说明见 `Tools/README.md`） |
