# S4-Polish P纯 — UI 纯逻辑脚手架（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker，第一棒）。可与 P0 并行（无文件冲突）。
> 目标：为过滤与距离预览图先落地零 Verse 纯逻辑 + 测试项目，后续 UI 块直接消费。

## 先读

- 本任务书全部内容。
- `docs/s4-polish-plan-zh.md` §4 P3、§4 P4、§6.2（只读这几节，不用读全文）。
- `tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj`（测试项目范式）
- `scripts/verify-local.ps1`（第 13 门附近的写法，用于加第 14 门）
- 不要读其它 workdocs、不要读其它 docs。

## 任务

### 1. 新增 `Source/UniversalSqueaker/UI/Layout/VoicePacksFilters.cs`（零 Verse）

- namespace `UniversalSqueaker.UI`。
- 只允许依赖 `System` / `System.Collections.Generic`。禁止 `UnityEngine` / `Verse` / `RimWorld`。
- 内容：
  - `public readonly struct UiDomainFilter { bool EnabledOnly; bool ConflictOnly; bool OrphanOnly; string Author; }`（Author 空 = 全部作者）。
  - `public readonly struct UiPackFilter { bool EnabledOnly; string Author; }`。
  - `public static class VoicePacksFilters`：
    - `public static bool DomainMatches(bool hasConflict, bool isDormant, bool isTargetUnavailable, bool isOrphan, bool isEnabled, string author, in UiDomainFilter filter)`
      - `ConflictOnly` 命中：`hasConflict || isDormant || isTargetUnavailable || isOrphan`。
      - `OrphanOnly` 命中：`isOrphan`。
      - `EnabledOnly` 命中：`isEnabled`。
      - `Author` 非空时：`author` 与 filter.Author OrdinalIgnoreCase 包含。
      - 空 filter（全 false、Author 空）恒 true。
    - `public static bool PackMatches(string author, string modName, bool isSelected, in UiPackFilter filter)`
      - `EnabledOnly` 命中：`isSelected`。
      - `Author` 非空时：`author` 或 `modName` OrdinalIgnoreCase 包含 filter.Author。
      - 空 filter 恒 true。
  - 不实现 UI、不实现列表投影。

### 2. 新增 `Source/UniversalSqueaker/UI/Layout/DistancePreview.cs`（零 Verse）

- namespace `UniversalSqueaker.UI`。只允许 `System` / `System.Collections.Generic`。
- 内容：
  - `public readonly struct DistanceSample { public float Distance; public float Audibility; }`（Audibility 0..1）。
  - `public static class DistancePreview`：
    - `public static IReadOnlyList<DistanceSample> SampleAudibilityCurve(float min, float max, int sampleCount, float graphMin, float graphMax)`
    - 语义：`d ≤ min` → 1；`min < d < max` → 线性 1→0；`d ≥ max` → 0。
    - `graphMin`/`graphMax` 是采样显示范围；采样点等距覆盖 `[graphMin, graphMax]`，`sampleCount` 至少 2。
    - 参数防御：`min`/`max` 为 NaN/Infinity 时按 `min=15,max=50` 处理；`min > max` 时交换；`min == max` 时返回全 1 曲线；`sampleCount < 2` 按 2；`sampleCount > 512` 按 512；`graphMin >= graphMax` 时按 `graphMin=15, graphMax=65`。
    - 纯函数、确定性、无随机、无 IO。

### 3. 新增 `tools/UniversalSqueakerUiLogicTests/`（控制台测试项目）

- `tools/UniversalSqueakerUiLogicTests/UniversalSqueakerUiLogicTests.csproj`：
  - `OutputType=Exe`、`TargetFramework=net472`、`PlatformTarget=x64`、`LangVersion=latest`、`Nullable=enable`。
  - 仿 `UniversalSqueakerKernelTests`：`NuGetAudit=false`、`RestoreUseStaticGraphEvaluation=true`、`IncludeSourceRevisionInInformationalVersion=false`、`InformationalVersion=0.0.0+us-ui-logic`。
  - 不引用 Verse/RimWorld/Krafs.Rimworld.Ref。
  - `<Compile Include>` 链接两个源文件：`..\..\Source\UniversalSqueaker\UI\Layout\VoicePacksFilters.cs` 与 `..\..\Source\UniversalSqueaker\UI\Layout\DistancePreview.cs`（`Link` 到本目录）。
- `tools/UniversalSqueakerUiLogicTests/Program.cs`：
  - 自写断言（无外部测试框架），全绿输出 `ALL GREEN` 并返回 0；失败抛异常返回非 0。
  - 断言至少覆盖：
    - `DistancePreview`：端点值（graphMin 处 1、graphMax 处 0）、单调非增、`min==max` 退化全 1、`sampleCount=2` 与 `=200`、`min>max` 交换、NaN/Infinity 防御、越界 `graphMin>=graphMax` 防御。
    - `VoicePacksFilters`：空 filter 恒真；EnabledOnly 只留已启用；ConflictOnly 命中 hasConflict/isDormant/isTargetUnavailable/isOrphan 四者之一；OrphanOnly 只看 isOrphan；Author 对 author/modName 的 OrdinalIgnoreCase 包含；组合过滤（EnabledOnly+Author）同时成立。

### 4. `scripts/verify-local.ps1` 加第 14 门

- 在现有 13 门之后追加：构建并运行 `tools/UniversalSqueakerUiLogicTests`（`dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release`）。
- 保持现有门的顺序与输出格式一致；失败即整体失败。

## 验收（自己先跑，全绿再提交）

- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`（0 警告）
- `dotnet run --project tools/UniversalSqueakerKernelTests -c Release`（全绿）
- `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release`（ALL GREEN）
- `pwsh -File scripts/verify-local.ps1`（14 门全绿）
- 隐私预检：新增内容无 `PublishedFileId`/个人绝对路径/凭据。

## 提交

一条提交：

```text
test(S4): pure UI logic scaffold — filters + distance preview + UiLogicTests
```

只允许包含：两个新纯逻辑文件、`tools/UniversalSqueakerUiLogicTests/`、`scripts/verify-local.ps1` 第 14 门。不得改动其它文件。

## 禁止

- 不要为纯函数写任何 Verse/Unity 调用方（后续任务书会写）。
- 不要改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`。
- 不要 push / 配 remote。
