# US Xeno 调音双键 Context 设计稿

> 状态：已实现（2026-08-28），verify-local 全绿。
> 目标：消除运行时 xeno 调音 context 单键（`xenotypeDefName`）与数据面/UI/音频域 `(race, xeno)` 双键不一致的问题。

## 1. 背景

- 数据面：`ActionTuningRecord` / `MoodTuningRecord` 的 layer-2 记录强制 `(raceDefName, xenotypeDefName)` 双键（`IsValidLayer`）。
- UI 面：调音编辑器 layer-2 域选择、scope 行、mood 行均按 `(race, xeno)` 展示。
- 音频面：`SqueakPoolRegistry` 已使用 `AudioDomain(race, xeno?)` 作为池键。
- 运行时调音面：`SqueakRuntimeResolver` 仍以 `xenotypeDefName` 单键构建 `contexts`，layer-2 的 race 被忽略；`SqueakRaceForXenotype` 还试图“猜”一个 race。

调研确认：RimWorld `XenotypeDef` 无 race 字段；HAR 通过 `ThingDef_AlienRace.alienRace.raceRestriction` 白/黑名单按 race 过滤 xenotype，同一 xenotype 可被多个 race 白名单。因此双键不是冗余，而是与 HAR/内容生态对齐的运行时身份。

## 2. 分层归属

| 层 | 内容 | 依赖 |
| --- | --- | --- |
| Kernel | `AudioDomain` / `RaceKey` / `XenotypeKey`，`AudioDomains` 工具 | 零 Verse |
| Pure | 域聚合器（按 `AudioDomain` 合并调音源）、context 选择器（fallback/ambiguous 规则） | 零 Verse |
| External | `SqueakRuntimeResolver` / `SqueakKernelAdapter` / `ResolvedSqueakContext` | Verse/RimWorld |

边界规则：

- Kernel 只提供键和通用域工具，不包含调音语义。
- Pure 只放“有规则、值得测试、不依赖 Verse 类型”的逻辑。
- `ResolvedSqueakContext` / `RuntimeActionDelta` / `RuntimeMoodDelta` 因依赖 `XenotypeDef` / `FloatRange`，留在 External。

## 3. Kernel：`AudioDomains`

在 `Kernel/Domain.cs` 增加静态工具：

- `TryCreate(raceDefName, xenotypeDefName?)`：校验并构造 `AudioDomain`；空 race 返回 false。
- `Collect(IEnumerable<(string race, string? xeno)> sources)`：去重、排序，返回 `IReadOnlyList<AudioDomain>`。
- `RaceOnly(raceDefName)`：构造 race-only 域。

不引入产品字面量，不判断“音频归属”，只做键的构造/校验/集合。

## 4. Pure：域聚合器

新增 `Pure/SqueakTuningDomains.cs`（或独立文件），提供：

```csharp
public sealed class LayerBehaviorAggregate
{
    public float OverallIntervalMultiplier;
    public Dictionary<string, LayerActionDelta> Actions;
    public Dictionary<SqueakMood, LayerMoodDelta> Moods;
}

public static class SqueakTuningAggregator
{
    public static IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate> Aggregate(
        IEnumerable<(AudioDomain domain, string actionKey, LayerActionDelta delta)> actions,
        IEnumerable<(AudioDomain domain, SqueakMood mood, LayerMoodDelta delta)> moods,
        IEnumerable<(AudioDomain domain, float overallMultiplier)> multipliers);
}
```

语义：

- 同 `AudioDomain` 分组；
- 同域同 action/mood 字段级 `Merge`（后写覆盖先写，`HasX` 取并集）；
- `overallIntervalMultiplier` 后写覆盖；
- 空输入返回空表。

## 5. Pure：context 选择器

新增 `Pure/SqueakContextSelector.cs`：

```csharp
public enum ContextSelectionKind { Global, Race, Xeno }

public readonly struct ContextSelection
{
    public ContextSelectionKind Kind;
    public AudioDomain? XenoDomain; // Kind == Xeno 时有效
}

public static class SqueakContextSelector
{
    public static ContextSelection Select(
        string raceDefName,
        string? xenoDefName,
        IEnumerable<AudioDomain> availableXenoDomains,
        IReadOnlyCollection<string> ambiguousXenoNames,
        bool hasRaceContext);
}
```

规则：

1. race 为空 → Global。
2. 无 Biotech 或 xeno 为空 → Race（有 raceContext）否则 Global。
3. `(race, xeno)` 不在 `availableXenoDomains` → Race / Global，绝不跨 race。
4. xeno 在 `ambiguousXenoNames` → Global（fail-closed）。
5. 命中 `(race, xeno)` → Xeno。

## 6. External：resolver 薄适配

`SqueakRuntimeResolver` 改造：

- `BuildBehavior` 改为投影 + 调用 `SqueakTuningAggregator.Aggregate`，返回 `IReadOnlyDictionary<AudioDomain, LayerBehaviorAggregate>`。
- `BuildRaceBehavior` 保留 race-only 聚合，也可复用同一聚合器（race-only 域）。
- `BuildSnapshot`：
  - `raceContexts` 键改为 `RaceKey`；
  - `contexts` 键改为 `AudioDomain`；
  - 域枚举来自 catalog pack 声明、selections、presets、layer-2 records；
  - 每个 `(race, xeno)` 域构建一个 `ResolvedSqueakContext`；
  - 删除 `SqueakRaceForXenotype` 主路径。
- `SqueakRuntimeSnapshot`：
  - 内部字典类型改为 `Dictionary<AudioDomain, ResolvedSqueakContext>` 与 `Dictionary<RaceKey, ResolvedSqueakContext>`；
  - `ResolveContext(Pawn)` 只做：取 race/xeno 字符串 → 调 `SqueakContextSelector.Select` → 查表；
  - 保留 `Xenotype` 引用一致性检查（命中 Xeno 后校验 `context.Xenotype` 与 pawn 的 `Xenotype` 引用一致）。
- 公开 API 不变：`ResolvedSqueakContext`、`RuntimeActionDelta`、`RuntimeMoodDelta`、`ChooseProductionSound*` 均不变。

## 7. 域枚举源

从以下来源收集 `(race, xeno)`：

1. `catalog.XenotypePacksByDefName` 中每个 pack 的 `raceDefName + targetDefName`；
2. `settings.voicePackSelections` 中 `scope == Xenotype` 的记录；
3. `settings.xenotypePresets` 中非空 `raceDefName + xenotypeDefName`；
4. `settings.actionTuning` / `moodTuning` 中 layer-2 记录。

不扫描 HAR `whiteXenotypeList`；HAR reflection 保持 deferred，本次维持 assembled-only。

## 8. 回退与歧义语义

- 缺失 `(race, xeno)` → raceContext；无 raceContext → globalContext。
- 同 xeno 多 race：各自独立 context，互不串音。
- `ambiguousCanonicalDefNames` 包含该 xeno → globalContext + 日志。
- 命中 xeno 后 `context.Xenotype` 与 pawn 的 `Xenotype` 引用不一致 → globalContext + 日志（沿用现状）。
- 空 race 或非法 layer-2 记录 → 忽略，不参与聚合。
- **PackFallback 是精确域边界（intentional）**：`SqueakPoolRegistry.SelectPackFallback` 只查询 `ctx.Domain` 的精确池。
  xeno context 即使同 race 的 race-pool 声明了同一 action 的 pack fallback，也不会读取该 race-pool fallback；
  race context 只读取 race 精确池的 pack fallback。这是设计定案，不是缺口。

## 9. 测试矩阵

Kernel harness 新增：

- 同 xeno 多 race：`(Ratkin, X)` 与 `(Feline, X)` 各自独立，不串音。
- 缺失域：`(Ratkin, X)` 不在表内 → Race 回退。
- Ambiguous：xeno 在 ambiguous 集合 → Global。
- 空 race：非法记录被忽略。
- 聚合：同域 action/mood 字段级 last-wins；`overallIntervalMultiplier` 按域独立。
- 选择器：无 Biotech / 无 xeno / 无 raceContext 各分支。

## 10. Out of scope

- HAR reflection discovery（保持 deferred）。
- 完整 Runtime harness（适配层 Verse 胶水仍无单测；本次通过 Pure 提取覆盖核心规则）。
- Settings schema / UI 改动（数据面已双键，UI 已按双键展示）。
- `docs/workdocs/` 清理。
