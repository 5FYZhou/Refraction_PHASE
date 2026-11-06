<#
项目评审包：把「外部 AI 做方案 / 评审」需要的文件平铺到一个目录里，附带 MANIFEST.txt。

为什么需要它：
  评审方拿不到仓库上下文（打不开团结引擎、看不到场景与预制体），也没法按目录结构翻代码。
  所以这里按"读得懂的顺序"分组、平铺、附清单与随包说明；不参与编码执行的东西
  （Library/、Temp/、obj/、场景、预制体、贴图）一律不打包。

用法：
  pwsh Tools/pack-toagent.ps1                       # 输出到仓库同级的 toAgent/（默认）
  pwsh Tools/pack-toagent.ps1 -Out D:\some\dir      # 指定输出目录
  pwsh Tools/pack-toagent.ps1 -List                 # 只打印分组清单，不写文件

注意：
  · 每次运行都会先清空输出目录，再整包重建；清空前会挡一道（不许是盘根、不许是仓库本身或其祖先）。
  · 平铺必然有同名文件，后者覆盖前者 —— 清单里会把每一处同名冲突列出来，自己确认要不要改名。
#>
[CmdletBinding()]
param(
    [string]$Out = '',
    [switch]$List
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $Out) { $Out = Join-Path (Split-Path -Parent $root) 'toAgent' }

# 这些后缀原样保留；其余（.asset/.inputactions/.prefab/.unity/.shader/.ps1 …）在文件名后补 .txt，
# 内容一字不改，真实格式留在文件名里。
$uploadableExt = @('.txt', '.md', '.cs', '.json', '.xml', '.yml', '.yaml', '.csv')

# 分组 → 相对仓库根的路径（目录整棵复制，仍然平铺）。
# 顺序就是建议阅读顺序：先纪律与需求，再自下而上读代码，最后是门禁脚本。
$groups = [ordered]@{
    '00-入口与纪律'   = @(
        'AGENTS.md'
        'README.md'
        # 平铺后会和根 README 撞名，改一个名（内容不动）
        @{ From = 'Docs/README.md'; As = 'README_Docs文档地图.md' }
        'Docs/工程/注释规范.md'
    )
    '01-需求与说明'   = @(
        'Docs/场景块需求.txt'
        'Docs/玩家相关需求.txt'
        'Docs/怪物通用属性接口需求.txt'
        'Docs/说明/玩家状态机.md'
    )
    '02-地基与数据'   = @(
        'Assets/Scripts/Foundation'
        'Assets/Scripts/Data'
        'Assets/Resources/PlayerConfig.asset'
    )
    '03-Logic'        = @(
        'Assets/Scripts/Logic'
    )
    '04-表现层'       = @(
        'Assets/Scripts/Presentation'
    )
    '05-门禁脚本'     = @(
        'Tools/comment-lint.ps1'
        'Tools/comment-budget.json'
        'Tools/compile-gate.ps1'
        'Tools/compile-gate.json'
    )
}

if ($List) {
    foreach ($g in $groups.Keys) {
        Write-Host "[$g]" -ForegroundColor Cyan
        foreach ($e in $groups[$g]) { Write-Host "  $e" }
    }
    exit 0
}

# 输出目录只允许这一处：清空前挡一道，免得变量被改坏时误删别处
$full = [System.IO.Path]::GetFullPath($Out).TrimEnd('\', '/')
if ($full -match '^[A-Za-z]:$' -or $full -eq '' -or $full -eq [System.IO.Path]::GetPathRoot($full)) {
    throw "拒绝使用盘根作为输出目录：$full"
}
$rootTrim = $root.TrimEnd('\', '/')
if ($full -eq $rootTrim -or $rootTrim.StartsWith($full + '\') -or $rootTrim -eq $full) {
    throw "拒绝清空：$full 是仓库本身或它的祖先目录。"
}

if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
New-Item -ItemType Directory -Path $full -Force | Out-Null

$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add('折光·PHASE · 项目评审包（给外部 AI 的方案 / 评审用）')
$manifest.Add("打包时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$manifest.Add("来源仓库: $root")
$manifest.Add('')

$missing = New-Object System.Collections.Generic.List[string]
$clashes = New-Object System.Collections.Generic.List[string]
$seen = New-Object System.Collections.Generic.HashSet[string]
$fileCount = 0

function Get-OutName([string]$name) {
    $ext = [System.IO.Path]::GetExtension($name)
    if ($ext -and ($uploadableExt -contains $ext.ToLowerInvariant())) { return $name }
    return "$name.txt"
}

foreach ($g in $groups.Keys) {
    $manifest.Add("[$g]")

    foreach ($rel in $groups[$g]) {
        $asName = $null
        if ($rel -is [hashtable]) { $asName = $rel.As; $rel = $rel.From }

        $src = Join-Path $root ($rel -replace '/', '\')
        if (-not (Test-Path -LiteralPath $src)) {
            $missing.Add($rel)
            $manifest.Add("  缺失  $rel")
            continue
        }

        $item = Get-Item -LiteralPath $src -Force
        if ($item.PSIsContainer) {
            # 目录整棵复制，但 .meta 不进包（Unity 的 GUID 侧车文件，评审方用不上）
            $files = Get-ChildItem -LiteralPath $src -Recurse -File |
                Where-Object { $_.Extension -ne '.meta' } | Sort-Object FullName

            foreach ($f in $files) {
                $outName = Get-OutName $f.Name
                if (-not $seen.Add($outName)) { $clashes.Add("$outName  ←  $rel/$($f.Name)") }
                Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $full $outName) -Force
                $fileCount++
                $note = if ($outName -ne $f.Name) { "  (补 .txt，原 $($f.Name))" } else { '' }
                $manifest.Add(("  文件  {0}  ←  {1}/{2}{3}  ({4} KB)" -f $outName, $rel, $f.Name, $note, [math]::Round($f.Length / 1KB, 1)))
            }
        } else {
            $name = if ($asName) { $asName } else { $item.Name }
            $outName = Get-OutName $name
            if (-not $seen.Add($outName)) { $clashes.Add("$outName  ←  $rel") }
            Copy-Item -LiteralPath $src -Destination (Join-Path $full $outName) -Force
            $fileCount++
            $renamed = if ($asName) { "  (改名自 $($item.Name))" } else { '' }
            $suffixed = if ($outName -ne $name) { "  (补 .txt，原 $name)" } else { '' }
            $manifest.Add(("  文件  {0}  ←  {1}{2}{3}  ({4} KB)" -f $outName, $rel, $renamed, $suffixed, [math]::Round($item.Length / 1KB, 1)))
        }
    }
    $manifest.Add('')
}

# 随包说明：评审方没有仓库上下文，这几条必须随包给（改工程事实时同步这里）
$manifest.Add('[随包说明]')
$manifest.Add('0. 用途：供外部 AI 做方案 / 评审，不参与编码执行。不含场景 / 预制体 / 贴图 / Library / obj。')
$manifest.Add('1. 工程：团结引擎 2022.3.62t17（Unity 2022.3 LTS 分支），2D + URP 14.2.0-t1，Input System 1.14.4-t1，')
$manifest.Add('   Cinemachine 2.10.7，TextMeshPro 3.0.9。物理固定步长 0.02s（50Hz），逻辑层时间来自 Time.fixedTime。')
$manifest.Add('   构建场景：SampleScene + Levels/Level_1..3。')
$manifest.Add('2. 单程序集：**没有 asmdef**，全部代码在 Assembly-CSharp。四层单向依赖')
$manifest.Add('   Presentation → Logic → Data → Foundation 只靠命名空间与目录自觉，编译器不拦（AGENTS.md §2）。')
$manifest.Add('   Logic 是纯 C#：不许 MonoBehaviour / GameObject / Transform / Component / Rigidbody2D，')
$manifest.Add('   不许直接读 Time.time / Time.deltaTime（走 LogicContext.GameTime），表现层能力一律通过接口消费。')
$manifest.Add('3. 驱动模型：GameRoot 是全工程唯一真单例，也是唯一有 Update / FixedUpdate 的类；')
$manifest.Add('   场景系统实现 ISceneRoot 并声明 Order，由 GameRoot 按 Order 推进；Attach 必须幂等，且注册后下一帧才执行。')
$manifest.Add('4. 热路径（Tick / FixedTick / State.Tick / BeginStep / Commit）0 GC：禁 LINQ、禁闭包与 lambda、')
$manifest.Add('   禁每帧 new List/Array、禁 GetComponent；物理检测用 NonAlloc 版本。')
$manifest.Add('5. 手感真源：Assets/Resources/PlayerConfig.asset + Assets/Scripts/Data/PlayerConfig.cs（SO 字段即契约，')
$manifest.Add('   单位与边界写在 [Tooltip] 里）。逻辑层只读不写；改手感不许散成代码里的魔法常量。')
$manifest.Add('6. 生成物禁区：Assets/Scripts/Generated/InputSys.cs 由 Input System 从')
$manifest.Add('   Assets/Scripts/Presentation/Input/InputSys.inputactions 生成，手改必丢；改键位改 .inputactions。')
$manifest.Add('7. 本工程没有测试工程、没有配表（Luban / Excel / StreamingAssets 全部不存在）；')
$manifest.Add('   "编译门 + 注释守卫"两把脚本在 [05-门禁脚本]，评审时不必执行，但它们是本工程的验收口径：')
$manifest.Add('   pwsh Tools/compile-gate.ps1（0 error、警告不超基线、无越层引用）与 pwsh Tools/comment-lint.ps1（注释预算 / 标签白名单）。')
$manifest.Add('8. 上传兼容：.txt 结尾且前面还有一层扩展名的都是改名件，例如 Player.prefab.txt / PlayerConfig.asset.txt；')
$manifest.Add('   前面那层扩展名才是真实格式，文件按字节原样，去掉末尾 .txt 即可还原。')
$manifest.Add('9. 文档与代码冲突时以代码为准（Docs/README.md 的定义：文档是快照，不是契约），并回头把文档改回来。')
$manifest.Add('')
$manifest.Add('[建议阅读顺序]')
$manifest.Add('  ① AGENTS.md（行为宪章）→ 注释规范.md（注释预算与工具口径）')
$manifest.Add('  ② 玩家相关需求.txt / 场景块需求.txt / 怪物通用属性接口需求.txt（设计意图）')
$manifest.Add('  ③ 基础：Foundation（Singleton / StateMachine / Cooldown / Require）→ Data（PlayerConfig）')
$manifest.Add('  ④ Logic：Context（入参快照）→ Actor（StateGroup / MoveGroup / PostureGroup）→ PlayerLogic 与九个状态')
$manifest.Add('  ⑤ 表现：GameRoot（装配与驱动）→ PlayerController → MoveMotor（探针、贴地、蹲起）')
$manifest.Add('  ⑥ 玩家状态机.md 收尾（每帧顺序、抢占规则、参数表）')
$manifest.Add('')

if ($missing.Count -gt 0) {
    $manifest.Add("缺失 $($missing.Count) 项：")
    foreach ($m in $missing) { $manifest.Add("  $m") }
} else {
    $manifest.Add('缺失 0 项。')
}

if ($clashes.Count -gt 0) {
    $manifest.Add('')
    $manifest.Add("同名覆盖 $($clashes.Count) 项（平铺的代价，后者覆盖前者）：")
    foreach ($c in $clashes) { $manifest.Add("  $c") }
}

$manifestPath = Join-Path $full 'MANIFEST.txt'
[System.IO.File]::WriteAllLines($manifestPath, $manifest, (New-Object System.Text.UTF8Encoding $true))

Write-Host ''
Write-Host ("打包完成: {0}" -f $full) -ForegroundColor Green
Write-Host ("文件总数: {0}" -f $fileCount)
if ($missing.Count -gt 0) { Write-Host ("缺失: {0} 项（见 MANIFEST.txt）" -f $missing.Count) -ForegroundColor Yellow } else { Write-Host '缺失: 0' }
if ($clashes.Count -gt 0) { Write-Host ("同名覆盖: {0} 项（见 MANIFEST.txt）" -f $clashes.Count) -ForegroundColor Yellow } else { Write-Host '同名覆盖: 0' }
exit 0
