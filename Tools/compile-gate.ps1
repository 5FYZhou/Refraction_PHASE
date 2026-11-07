<#
编译门（不依赖 Unity，只依赖本机 dotnet + 团结引擎安装目录里的 DLL）。

为什么需要它：
  *.csproj 只由 Unity 在编辑器里重新生成。改代码时一旦新增 / 删除 / 改名文件，
  仓库里那份 Assembly-CSharp.csproj 的 <Compile Include> 清单就是过期的 ——
  直接 build 它会漏编译新文件（于是"门是绿的，编辑器里却一片红"），
  或因为清单里的旧文件不存在而报错。
  本脚本因此**现场 glob** 出临时工程（落在 Temp/compile-gate/，不进 Unity、不进构建产物）。

它验三件事：
  ① 编译：0 error；警告按 Tools/compile-gate.json 的基线判定，**新增任何警告都算不合规**。
  ② 边界：本工程没有 asmdef（全在 Assembly-CSharp，编译器不会替人拦层边界，见 AGENTS.md §2），
     所以这里用文本判据补上：Foundation / Data / Logic 里不许出现指向上层的 using。
  ③ 驱动入口：全工程只许 GameRoot 有 Update / FixedUpdate（AGENTS.md §5）。
  ①②③ 的存量偏差登记在 compile-gate.json 的 guardAllow 里，只减不增：
  修好一处就跑一次 -Seal，让表跟着变干净。

为什么要现场重算工程而不是直接 build 仓库里的 csproj：
  仓库的 csproj 是 Unity 的产物（改了也是白改，切分支会被整份重写）。
  本脚本读它只当**模板**：保留全部 <Reference>/<HintPath>/<DefineConstants>，
  把 <Compile> 清单换成现场扫出来的文件表，输出到 Temp/compile-gate/Gate.csproj。
  模板里的相对 HintPath（Library\ScriptAssemblies\…）会一并改成绝对路径，
  否则工程搬到 Temp/ 下就解析不到 DLL。

诊断计数口径：**去重后的逐条诊断**（路径(行,列) + 级别 + 编号）。
  坑：一个工程被别的工程 ProjectReference 时会重复编译，同一条诊断打印多遍；
  "匹配到几行" ≠ "有几条警告"。另外中文诊断在本机控制台会按 GBK 解成乱码，
  所以脚本对子进程设 DOTNET_CLI_UI_LANGUAGE=en —— 解析不依赖措辞，输出也不会花。

用法：
  pwsh Tools/compile-gate.ps1              # 守卫 + 生成 + 编译
  pwsh Tools/compile-gate.ps1 -NoBuild     # 只跑守卫 + 生成工程
  pwsh Tools/compile-gate.ps1 -Seal        # 把当前警告口径与守卫放行项封印进 json（只在"确实修好了"后跑）

退出码：0 = 0 error 且没有新增警告 / 新增守卫违规；1 = 否则。
#>
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$Seal
)

$ErrorActionPreference = 'Stop'

# 子进程输出固定英文：既避开控制台 GBK 乱码，也让诊断正则不依赖中文措辞
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:DOTNET_NOLOGO = '1'

$root      = Split-Path -Parent $PSScriptRoot
$outDir    = Join-Path $root 'Temp/compile-gate'
$gateProj  = Join-Path $outDir 'Gate.csproj'
$gateEditorProj = Join-Path $outDir 'GateEditor.csproj'
$cfgPath   = Join-Path $PSScriptRoot 'compile-gate.json'
$utf8      = [System.Text.UTF8Encoding]::new($false)

$template = Join-Path $root 'Assembly-CSharp.csproj'
if (-not (Test-Path -LiteralPath $template)) {
    throw '找不到 Assembly-CSharp.csproj —— 先让 Unity / 团结引擎在编辑器里重新生成工程文件'
}
if (-not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

# ======================================================================
# 1 · 守卫（文本判据：编译门兜不住的那几条）
# ======================================================================

# 层次只能单向：Presentation → Logic → Data → Foundation（AGENTS.md §2）
$layerOf = {
    param([string]$rel)
    if ($rel -match '^Assets/Scripts/Foundation/') { return 'Foundation' }
    if ($rel -match '^Assets/Scripts/Data/') { return 'Data' }
    if ($rel -match '^Assets/Scripts/Logic/') { return 'Logic' }
    if ($rel -match '^Assets/Scripts/Presentation/') { return 'Presentation' }
    if ($rel -match '^Assets/Scripts/(Generated|Genereted)/') { return 'Generated' }
    return $null
}

$forbiddenTargets = @{
    'Foundation' = @('Refraction.Data', 'Refraction.Logic', 'Refraction.Presentation')
    'Data'       = @('Refraction.Logic', 'Refraction.Presentation')
    'Logic'      = @('Refraction.Presentation')
    'Generated'  = @('Refraction.Logic', 'Refraction.Presentation', 'Refraction.Data', 'Refraction.Foundation')
}

$issues = New-Object System.Collections.ArrayList

$csFiles = @(Get-ChildItem (Join-Path $root 'Assets') -Recurse -File -Filter *.cs | Sort-Object FullName)

foreach ($f in $csFiles) {
    $rel = ([System.IO.Path]::GetRelativePath($root, $f.FullName) -replace '\\', '/')
    $layer = & $layerOf $rel
    if (-not $layer) { continue }

    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $ln = $lines[$i]
        $trim = $ln.TrimStart()
        # 只看真正的 using 指令：注释里的 "using …" 是说明文字，不算引用
        if ($trim.StartsWith('//') -or $trim.StartsWith('*') -or $trim.StartsWith('/*')) { continue }
        if ($trim -notmatch '^using\s+(static\s+)?([A-Za-z_][\w.]*)') { continue }

        $target = $Matches[2]
        foreach ($bad in $forbiddenTargets[$layer]) {
            if ($target -eq $bad -or $target.StartsWith($bad + '.')) {
                [void]$issues.Add([pscustomobject]@{
                        Key = "layer:$rel"
                        Msg = "$rel`: 越层引用 $layer → $target（第 $($i + 1) 行）"
                    })
                break
            }
        }
    }

    # 驱动入口：只许 GameRoot（AGENTS.md §5）
    if ($rel -notmatch '/Root/GameRoot\.cs$') {
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^\s*(private|public|protected|internal)?\s*void\s+(Update|FixedUpdate)\s*\(\s*\)') {
                [void]$issues.Add([pscustomobject]@{
                        Key = "driver:$rel"
                        Msg = "$rel`: 私自声明 $($Matches[2])（第 $($i + 1) 行）—— 驱动只许走 GameRoot"
                    })
            }
        }
    }
}

# ======================================================================
# 2 · 生成临时工程（模板 = 仓库里的 Unity csproj，清单 = 现场扫描）
# ======================================================================

$templateText = Get-Content -Raw -LiteralPath $template

# 模板里的相对 HintPath 改成绝对：工程要落在 Temp/ 下，相对路径会指错地方
$templateText = [regex]::Replace($templateText, '<HintPath>([^<]+)</HintPath>', {
        param($m)
        $p = $m.Groups[1].Value
        if ([System.IO.Path]::IsPathRooted($p)) { return $m.Value }
        return '<HintPath>' + (Join-Path $root $p) + '</HintPath>'
    })

function New-GateProject {
    param(
        [string]$OutPath,
        [string]$AssemblyName,
        [System.IO.FileInfo[]]$Files,
        [string]$ExtraProjectRef,
        [string]$ExtraReferences
    )

    if ($Files.Count -eq 0) { return $null }

    $text = $templateText
    $text = [regex]::Replace($text, '(?m)^\s*<Compile Include="[^"]+"\s*/>\r?\n', '')
    $text = [regex]::Replace($text, '<AssemblyName>[^<]*</AssemblyName>', "<AssemblyName>$AssemblyName</AssemblyName>")
    $text = [regex]::Replace($text, '<OutputPath>[^<]*</OutputPath>', "<OutputPath>$outDir\bin\</OutputPath>")

    $compileXml = ($Files | ForEach-Object { '    <Compile Include="' + $_.FullName + '" />' }) -join "`n"
    $block = "  <ItemGroup>`n$compileXml`n  </ItemGroup>`n"
    if ($ExtraProjectRef) {
        $block += "  <ItemGroup>`n    <ProjectReference Include=""$ExtraProjectRef"" />`n  </ItemGroup>`n"
    }
    if ($ExtraReferences) {
        $block += "  <ItemGroup>`n$ExtraReferences  </ItemGroup>`n"
    }

    $anchor = '<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />'
    if ($text -notmatch [regex]::Escape($anchor)) { throw "工程模板结构变了（找不到 Microsoft.CSharp.targets 导入）：$template" }
    $text = $text.Replace($anchor, $block + $anchor)

    [System.IO.File]::WriteAllText($OutPath, $text, $utf8)
    return $OutPath
}

# 路径里含名为 Editor 的目录 → Unity 会把它编进 Assembly-CSharp-Editor
$runtimeFiles = @($csFiles | Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]' })
$editorFiles  = @($csFiles | Where-Object { $_.FullName -match '[\\/]Editor[\\/]' })

# 测试程序集要额外的 nunit / TestRunner 引用：模板 csproj 里没有，Unity 生成时才注入。
# 包缓存目录名带版本号，按前缀找；找不到就不加，交给编译直接报错（别静默跳过整块测试）
$testReferences = ''
$nunitPkg = Get-ChildItem (Join-Path $root 'Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit*' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($nunitPkg) {
    $nunitDll = Get-ChildItem $nunitPkg.FullName -Recurse -File -Filter 'nunit.framework.dll' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($nunitDll) {
        $testReferences += '    <Reference Include="nunit.framework"><HintPath>' + $nunitDll.FullName + '</HintPath></Reference>' + "`n"
    }
}
foreach ($asm in @('UnityEngine.TestRunner', 'UnityEditor.TestRunner')) {
    $dll = Join-Path $root "Library/ScriptAssemblies/$asm.dll"
    if (Test-Path -LiteralPath $dll) {
        $testReferences += '    <Reference Include="' + $asm + '"><HintPath>' + $dll + '</HintPath></Reference>' + "`n"
    }
}

$projects = @()
$null = New-GateProject -OutPath $gateProj -AssemblyName 'CompileGate' -Files $runtimeFiles
$projects += $gateProj

if ($editorFiles.Count -gt 0) {
    $null = New-GateProject -OutPath $gateEditorProj -AssemblyName 'CompileGateEditor' -Files $editorFiles -ExtraProjectRef $gateProj -ExtraReferences $testReferences
    $projects += $gateEditorProj
}

Write-Host ("工程：{0} 个手写/生成 .cs 进 Gate，{1} 个进 GateEditor；模板 {2}" -f `
        $runtimeFiles.Count, $editorFiles.Count, (Split-Path -Leaf $template)) -ForegroundColor DarkGray

# ======================================================================
# 3 · 编译 + 诊断计数
# ======================================================================

$log = @()
if (-not $NoBuild) {
    Write-Host ''
    Write-Host '编译：' -ForegroundColor Cyan
    foreach ($p in $projects) {
        $log += & dotnet build $p -v minimal -nologo 2>&1
    }
    # 只打有信息量的行：MSB3277（引擎 shim 噪音）与 restore 寒暄一律不刷屏；
    # 同一条诊断 dotnet 会打印两遍（编译阶段 + 末尾汇总），这里去重。
    $printed = @{}
    foreach ($l in $log) {
        $s = [string]$l
        if ($s -match 'warning MSB3277') { continue }
        if ($s -match '^\s*(Determining projects to restore|Nothing to do\.|All projects are up-to-date|Restored )') { continue }
        if ($s -match '^\s*Time Elapsed') { continue }
        if ([string]::IsNullOrWhiteSpace($s)) { continue }
        if ($printed.ContainsKey($s)) { continue }
        $printed[$s] = $true
        Write-Host $s
    }
}

function Get-Diagnostics {
    param([object[]]$Log)

    $seen = @{}
    foreach ($line in $Log) {
        $m = [regex]::Match([string]$line, '^(.*?)\((\d+),(\d+)\):\s+(error|warning)\s+([A-Z]+\d+)\s*:')
        if (-not $m.Success) { continue }
        $key = $m.Groups[1].Value + '|' + $m.Groups[2].Value + '|' + $m.Groups[5].Value
        if (-not $seen.ContainsKey($key)) {
            $seen[$key] = [pscustomobject]@{
                File  = $m.Groups[1].Value
                Line  = [int]$m.Groups[2].Value
                Level = $m.Groups[4].Value
                Code  = $m.Groups[5].Value
            }
        }
    }
    return @($seen.Values)
}

$diags = Get-Diagnostics -Log $log
$errors = @($diags | Where-Object { $_.Level -eq 'error' })
$warns  = @($diags | Where-Object { $_.Level -eq 'warning' })

# ======================================================================
# 4 · 基线（Tools/compile-gate.json）
# ======================================================================

$cfg = $null
if (Test-Path -LiteralPath $cfgPath) { $cfg = Get-Content -Raw -LiteralPath $cfgPath | ConvertFrom-Json }

# 引擎自带的 netstandard shim 版本冲突：与代码无关（AGENTS.md §1）
$ignoredCodes = @('MSB3277')

$warnCount = @{}
foreach ($w in $warns) { if ($ignoredCodes -contains $w.Code) { continue }; $warnCount[$w.Code] = 1 + [int]$warnCount[$w.Code] }

$guardAllow = @()
$warnBaseline = @{}
if ($cfg) {
    if ($cfg.guardAllow) { $guardAllow = @($cfg.guardAllow) }
    if ($cfg.warningBaseline) { foreach ($p in $cfg.warningBaseline.PSObject.Properties) { $warnBaseline[$p.Name] = [int]$p.Value } }
    if ($cfg.ignoredCodes) { $ignoredCodes = @($cfg.ignoredCodes) }
}

$newGuard = @($issues | Where-Object { $guardAllow -notcontains $_.Key })

$warnFailures = @()
foreach ($code in $warnCount.Keys) {
    $allowed = 0
    if ($warnBaseline.ContainsKey($code)) { $allowed = $warnBaseline[$code] }
    if ($warnCount[$code] -gt $allowed) {
        $warnFailures += "$code：$($warnCount[$code]) 条 > 基线 $allowed 条"
    }
}

if ($Seal) {
    $head = & git -C $root rev-parse --short HEAD 2>$null
    if (-not $head) { $head = 'nogit' }
    $obj = [ordered]@{
        note         = '编译门基线：warningBaseline 是逐警告编号的存量条数（只降不升）；guardAllow 是边界/驱动守卫的存量放行项（Key 形如 layer:路径 或 driver:路径）；ignoredCodes 是与代码无关的构建噪音。修好一处就跑 -Seal 让它从表里消失。口径与用法见 Tools/compile-gate.ps1 文件头。'
        sealedAt     = [string]$head
        sealedOn     = (Get-Date -Format 'yyyy-MM-dd')
        warningBaseline = $warnCount
        ignoredCodes = $ignoredCodes
        guardAllow   = @($issues | ForEach-Object { $_.Key } | Sort-Object -Unique)
    }
    $obj | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $cfgPath -Encoding utf8
    Write-Host ''
    Write-Host ("封印完成：警告 {0} 类（{1} 条），守卫放行 {2} 项  (sealedAt={3})" -f `
            $warnCount.Count, (($warnCount.Values | Measure-Object -Sum).Sum), @($obj.guardAllow).Count, $head) -ForegroundColor Green
    exit 0
}

# ======================================================================
# 5 · 报告
# ======================================================================

Write-Host ''
Write-Host ("错误 {0} 条 / 警告 {1} 条（已按编号去重，忽略 {2}）" -f `
        $errors.Count, (($warnCount.Values | Measure-Object -Sum).Sum), ($ignoredCodes -join ',')) `
    -ForegroundColor $(if ($errors.Count -eq 0) { 'Green' } else { 'Red' })

if ($errors.Count -gt 0) {
    $errors | Select-Object -First 20 | ForEach-Object {
        Write-Host ("  {0}({1}): {2}" -f $_.File, $_.Line, $_.Code) -ForegroundColor Red
    }
}

if ($warnFailures.Count -gt 0) {
    Write-Host "`n新增警告（基线外的编号，或条数超基线）：" -ForegroundColor Red
    $warnFailures | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    Write-Host '  新警告不许用 #pragma warning disable 掩盖（AGENTS.md §1）。' -ForegroundColor Red
}

if ($newGuard.Count -gt 0) {
    Write-Host "`n新增守卫违规（编译门兜不住的那几条）：" -ForegroundColor Red
    $newGuard | ForEach-Object { Write-Host "  $($_.Msg)" -ForegroundColor Red }
}

if (-not $cfg) {
    Write-Host "`n没有基线文件（$cfgPath）：本次只做体检，不做拦截。确认无误后跑 pwsh Tools/compile-gate.ps1 -Seal。" -ForegroundColor Yellow
    exit 0
}

if ($errors.Count -gt 0 -or $warnFailures.Count -gt 0 -or $newGuard.Count -gt 0) {
    Write-Host "`nGATE FAIL" -ForegroundColor Red
    exit 1
}

Write-Host 'GATE OK：0 error，警告与基线一致，没有新增越层 / 驱动违规。' -ForegroundColor Green
exit 0
