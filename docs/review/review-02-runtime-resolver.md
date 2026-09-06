# Review 02 — Runtime Resolver / Adapter（双键 xeno context）

> 审查对象：`ca71d96` 后的运行时解析与适配层。
> 结论：未发现 blocker；kernel 验证门通过。发现 1 个 major（缺失/不可用 canonical XenotypeDef 时 xeno 音频域被错误降级为 race 域），若干 minor/nit。

## 范围

- `Source/UniversalSqueaker/Runtime/SqueakRuntimeResolver.cs`
  - `SqueakRuntimeSnapshot` / `ResolvedSqueakContext` / `RuntimeActionDelta` / `RuntimeMoodDelta`
  - `BuildBehavior` / `BuildRaceBehavior` / `BuildSnapshot` / `CollectXenoDomains` / `ResolveContext` / `ChooseProductionSound*`
- `Source/UniversalSqueaker/Runtime/SqueakKernelAdapter.cs`
  - `BuildEntries` / `BuildBuiltIn` / `ToChoice` / selection/domain 投影
- `Source/UniversalSqueaker/Runtime/SqueakLifeStageResolver.cs`
- `Source/UniversalSqueaker/Runtime/SqueakSoundAvailability.cs`（只作相关性检查）
- `Source/UniversalSqueaker/Runtime/SqueakVocalCapability.cs`
- `Source/UniversalSqueaker/Runtime/SqueakAudioPoolNotificationService.cs`（仅发现单键残留时检查）
- 相关支撑：`Kernel/Domain.cs`、`Pure/SqueakTuningDomains.cs`、`Pure/SqueakLayeredTuning.cs`、`Catalog/SqueakXenotypeCatalog.cs`、`Models/*`、`Settings/SqueakSettingsMigration.cs`

## 检查过的文件

- `Source/UniversalSqueaker/Runtime/SqueakRuntimeResolver.cs`
- `Source/UniversalSqueaker/Runtime/SqueakKernelAdapter.cs`
- `Source/UniversalSqueaker/Runtime/SqueakLifeStageResolver.cs`
- `Source/UniversalSqueaker/Runtime/SqueakVocalCapability.cs`
- `Source/UniversalSqueaker/Runtime/SqueakAudioPoolNotificationService.cs`
- `Source/UniversalSqueaker/Runtime/SqueakSoundAvailability.cs`
- `Source/UniversalSqueaker/Kernel/Domain.cs`
- `Source/UniversalSqueaker/Pure/SqueakTuningDomains.cs`
- `Source/UniversalSqueaker/Pure/SqueakLayeredTuning.cs`
- `Source/UniversalSqueaker/Catalog/SqueakXenotypeCatalog.cs`
- `Source/UniversalSqueaker/Models/ActionTuningRecord.cs`
- `Source/UniversalSqueaker/Models/MoodTuningRecord.cs`
- `Source/UniversalSqueaker/Models/SqueakXenotypePresetModels.cs`
- `Source/UniversalSqueaker/Models/SqueakVoicePackModels.cs`
- `Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs`
- `Source/UniversalSqueaker/Settings/SqueakSettingsMigration.cs`
- `tools/UniversalSqueakerKernelTests/UnitTests.cs`
- `docs/us-xeno-double-key-context-zh.md`

## 验证执行

- 已运行：`dotnet run --project tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj --no-restore`
  - 结果：全部单元断言通过，golden corpus replay 零 delta，determinism 通过。
- 未运行完整 RimWorld 游戏内验证；若环境有游戏引用，建议补充下面的命令。

## Findings

### Blocker

无。

### Major

#### M1. `DiscoveryAvailable` 未被使用：canonical XenotypeDef 缺失/发现失败时，Xeno context 被选中但音频路由回 race 域

- 位置：`SqueakRuntimeResolver.BuildSnapshot`（约 L176-189）、`SqueakRuntimeSnapshot.ResolveContext`（约 L421-437）、`SqueakRuntimeSnapshot.ChooseByKey`（约 L450-461）。
- 现象：
  - `SqueakXenotypeCatalogSnapshot.DiscoveryAvailable` 在 `SqueakXenotypeCatalog.cs` 中写入后从未被读取。
  - 当 Biotech 激活但 xenotype discovery 失败（异常）时，`XenotypeByDefName` 可能为空；`CollectXenoDomains` 仍会从 pack 声明/选择/调音记录收集 `(race, xeno)` 域。
  - `BuildSnapshot` 对这些域构造 `ResolvedSqueakContext` 时 `catalog.XenotypeByDefName.TryGetValue` 失败，`context.Xenotype == null`，但仍把域加入 `contexts`。
  - `ResolveContext` 经 `SqueakContextSelector` 命中该 `(race,xeno)` 域后返回这个 `Xenotype == null` 的 context。
  - `ChooseByKey` 只用 `context.Xenotype` 重建音频 `AudioDomain`；`Xenotype == null` 时构造的是 race-only 域，因此 xeno 专用 VoicePack 池被跳过，实际回退到 race/builtin。
- 影响：在“有 xeno pack/选择但 canonical `XenotypeDef` 不可用”的降级路径中，xeno 音频不会按预期路由；这正是 `DiscoveryAvailable` 想表达的失败面。现有代码既没有 fail-closed 到 race/global，也没有保留域键供音频路由。
- 建议修复（三选一，需 maintainer 定夺）：
  1. 最简：`BuildSnapshot` 仅在 `ModsConfig.BiotechActive && catalog.DiscoveryAvailable` 时构建 `contexts`；发现失败则 `contexts` 为空，`ResolveContext` 自然回退 race/global。
  2. 或在 `ResolveContext` 中：若 `selection.Kind == Xeno` 且 `context.Xenotype == null`，按“目标不可用”记日志并回退 race/global，避免 `ChooseByKey` 拿到空 canonical。
  3. 或在 `ResolvedSqueakContext` 中保存其 `AudioDomain`（或至少 `XenotypeKey`），使 `ChooseByKey` 不依赖 canonical `XenotypeDef` 是否存在；这是较大的公开 API 调整，需评估兼容性。

### Minor

#### m1. `CollectXenoDomains` 接受空 `xenotypeDefName` 的 Xenotype 选择记录，向 `contexts` 注入 race-only 域

- 位置：`SqueakRuntimeResolver.CollectXenoDomains`，约 L211-215。
- 现象：`settings.voicePackSelections` 中 `scope == Xenotype` 但 `xenotypeDefName` 为空的非法/旧记录会被加入 sources；`AudioDomains.Collect` 会把 `(race, null)` 变成 race-only `AudioDomain`，随后 `BuildSnapshot` 把它放入本应只装 xeno context 的 `contexts`。
- 影响：`BuildSelections` 已过滤同类记录，`SqueakContextSelector` 也只匹配 `Xenotype != null`，所以当前不影响音频路由；但违反“`contexts` 仅含 xeno 域”的不变量，可能污染诊断/测试/未来遍历。
- 建议：在 selection 循环中加 `if (string.IsNullOrEmpty(record.xenotypeDefName)) continue;`，与 `BuildSelections` 和旧实现保持一致。

#### m2. Ambiguous xeno 回退不再记录日志（相对旧行为回归）

- 位置：`SqueakRuntimeSnapshot.ResolveContext` / `SqueakContextSelector.Select`。
- 现象：旧实现在 `ResolveContext` 中遇到 `ambiguousCanonicalNames` 会调用 `WarnAndFallback` 记日志；新实现把 ambiguous 判断下沉到 Pure `SqueakContextSelector`，直接返回 `Global`，`ResolveContext` 不再触发 `WarnAndFallback`。
- 影响：音频行为正确（fail-closed 到 global），但设计稿 `docs/us-xeno-double-key-context-zh.md` §8 明确写了“ambiguous → globalContext + 日志”，诊断可观测性丢失。
- 建议：让 `SqueakContextSelector` 返回一个可区分原因的选择结果（如增加 `IsAmbiguous`/`Reason`），或在 `ResolveContext` 中先检查 `defName != null && ambiguousCanonicalNames.Contains(defName)` 并调用 `WarnAndFallback`。

#### m3. `SqueakAudioPoolNotificationService.GetDomainStatuses` 仍是单键 xeno 枚举

- 位置：`SqueakAudioPoolNotificationService.cs`，约 L20-22。
- 现象：Xenotype 状态只按 `targetDefName` 枚举，调用单参 `GetVoicePackSelectionStatus(SqueakVoicePackScope.Xenotype, target)`；多 race 共享同一 xeno 时，兼容桥 `ResolveLegacyRace` 在 `RaceDefNames.Count != 1` 时返回空 race，导致状态查询找不到实际 `(race, xeno)` 选择记录。
- 影响：该方法当前未被主 UI/运行时调用，但属于公开 runtime notification API，仍是双键改造后的单键残留。
- 建议：仿照 UI `VoicePacksPageModel.CollectXenotypeDomains` 枚举 `(raceDefName, targetDefName)` 对，并调用三参 `GetVoicePackSelectionStatus(scope, raceDefName, targetDefName)`；若该方法不再需要，可标记 obsolete 或删除。

#### m4. `SqueakKernelAdapter.ToChoice` 把 `PackFallback` 一律映射为 `RacePack`

- 位置：`SqueakKernelAdapter.cs`，约 L145-151。
- 现象：`ChainTier.PackFallback => SqueakSoundSource.RacePack`。`PackFallback` 来自“精确域池”的 entry fallback；当域是 xeno 域时，它实际是 xeno pack 的 fallback，却会被标记为 `RacePack`。
- 影响：不影响实际声音选择，但 diagnostics/UI 的 source 标识不准确。
- 建议：根据选择上下文域决定映射（xeno 域 fallback -> `XenotypePack`，race 域 fallback -> `RacePack`），或扩展 `SqueakSoundSource` 增加 `PackFallback`。后者属于公开 API 变更，需 maintainer 决策。

### Nit

#### n1. `ResolveContext` 重复构造 `RaceKey`

- 位置：`SqueakRuntimeSnapshot.ResolveContext`，约 L425-426。
- 建议：先 `RaceKey raceKey = new(raceDefName);` 再用于 `TryGetValue` 和索引。

#### n2. `BuildBuiltInSource` 中存在未使用的 `byRace` 局部字典

- 位置：`SqueakKernelAdapter.BuildBuiltInSource`，约 L65、L76。
- 建议：删除 `byRace`；`BuiltInFallbackTable` 构造器已按 race last-wins。

#### n3. `BuildGlobalActionLayers` 注释与代码不一致

- 位置：`SqueakRuntimeResolver.BuildGlobalActionLayers`，约 L112-125。
- 现象：注释写“外部动作键不进入本表”，但代码对所有非空 `actionKey` 都写入 layers；只是后续解析只遍历内置键，外部键从未被消费。
- 建议：要么像 Race/Xeno 层一样用 `ActionKey.TryParseBuiltIn` 过滤，要么修正注释为“外部键保留但当前不被解析消费”。

## Maintainer decisions needed

1. **缺失/不可用 canonical XenotypeDef 的语义**：应 fail-closed（跳过 xeno context / 回退 race-global），还是保留“tuning-only xeno context + race 音频路由”？是否值得给 `ResolvedSqueakContext` 增加 `AudioDomain` 字段以彻底解耦音频路由与 canonical `XenotypeDef`？
2. **`SqueakSoundSource` 枚举**：是否允许扩展 `PackFallback` source，还是维持现状仅修映射逻辑。
3. **`SqueakAudioPoolNotificationService.GetDomainStatuses`**：是否仍属公开 API 需要双键化，还是可以标记 obsolete。

## 建议验证命令

```bash
# Kernel 纯逻辑验证门（已在本机通过）
dotnet run --project tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj --no-restore

# 完整 mod 编译（需要 RimWorld/Unity 引用，若环境具备）
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj --no-restore
```

建议补充的游戏内/未来测试：

- 同一 xenotype 同时被两个 race 的 pack 声明：分别选择后确认两个 `(race, xeno)` 域互不串音。
- 构造 `DiscoveryAvailable=false` 或 `XenotypeByDefName` 缺失的场景，确认不会出现“选中 xeno context 但音频走 race 池”的静默降级。
- 开启诊断日志，确认 ambiguous xeno 命中时能观察到 `TargetRejected` 日志。
- `SqueakAudioPoolNotificationService.GetDomainStatuses` 在多 race 目录下手动/单测检查返回的 race 维度。
