# Tools · 工程门禁

三个脚本、两份基线。**都在仓库外运行，不参与编译**（不在 `Assets/` 下，不会进 Assembly-CSharp）。

| 文件                    | 作用                                              | 什么时候跑                       |
| --------------------- | ----------------------------------------------- | --------------------------- |
| `comment-lint.ps1`    | 注释守卫：预算（注释字符/文件）+ XML 标签白名单 + `verify`          | 动过 `.cs`（哪怕只动注释）            |
| `comment-budget.json` | 上者的预算表 + `tagAllow` 存量豁免                        | **只由 `-Mode seal` 生成**，不要手改 |
| `compile-gate.ps1`    | 编译门：现场重扫文件表 → 生成临时工程 → 0 error + 警告基线 + 越层/驱动守卫 | 每次交付前                       |
| `compile-gate.json`   | 上者的警告基线与守卫放行项                                   | **只由 `-Seal` 生成**，不要手改      |
| `pack-toagent.ps1`    | 评审包：按阅读顺序平铺 + `MANIFEST.txt`                    | 需要外部 AI 评审时                 |

## 常用命令

```bash
pwsh Tools/comment-lint.ps1                      # 判定（违规 exit 1）
pwsh Tools/comment-lint.ps1 -Mode report -Top 30 # 逐文件 预算/现值/余量/密度
pwsh Tools/comment-lint.ps1 -Mode tags           # 只查标签（含存量对比）
pwsh Tools/comment-lint.ps1 -Mode verify -Base <rev>   # 证明只改了注释
pwsh Tools/comment-lint.ps1 -Mode check -StagedOnly    # 只看本次改动的 .cs
pwsh Tools/comment-lint.ps1 -Mode seal           # 清理验收后重新封印预算

pwsh Tools/compile-gate.ps1                      # 守卫 + 生成 + 编译
pwsh Tools/compile-gate.ps1 -NoBuild             # 只跑守卫 + 生成工程
pwsh Tools/compile-gate.ps1 -Seal                # 修好存量偏差后重新封印基线

pwsh Tools/pack-toagent.ps1                      # 打包到仓库同级 toAgent/
pwsh Tools/pack-toagent.ps1 -List                # 只看分组与路径，不写文件
pwsh Tools/pack-toagent.ps1 -Out <dir>           # 换输出目录（清空前会挡一道）
```

退出码统一：0 = 通过，1 = 有违规或前置检查失败。

## 三条约定

1. **基线只许封印，不许手改**。`comment-budget.json` / `compile-gate.json` 是"当前欠账"的凭证；  
   手工调数字等于把自己的超标改成合法，正是这两个脚本要防的事。真要上调，跑 `seal` 让 diff 上看得见。
2. **只降不升**。修好一处就重新封印，让额度跟着变小；日常开发加功能不许顺手抬线。
3. **生成物不碰**。`Assets/Scripts/Generated/InputSys.cs` 由 Input System 生成（见 `AGENTS.md` §3），  
   注释守卫按目录排除它，不要为它调预算。

## 环境

需要 PowerShell 7（`pwsh`）。Windows PowerShell 5.1 未验证——脚本用了  
`[System.IO.Path]::GetRelativePath` 与 `$env:DOTNET_CLI_UI_LANGUAGE` 这类 7.x 上更稳的行为。  
`compile-gate.ps1` 需要本机 `dotnet`（当前 9.0.201）与团结引擎的 DLL（由 csproj 里的 HintPath 指向）。
