# Review 04 — Catalog / Comp Auto-Attach

审查日期：2026-08-28（工作区当前 HEAD `ca71d96`）
审查类型：独立代码审查（只读 + 报告 + TODO 勾选）
结论：**无 blocker**；有 1 个需要维护者确认的 major 级域身份一致性问题，其余为 minor/nit。

## Scope

- `Source/UniversalSqueaker/Catalog/*.cs`
- `Source/UniversalSqueaker/Catalog/VoicePackCompAttach.cs`
- `Source/UniversalSqueaker/Models/SqueakVoicePackModels.cs`
- `Source/UniversalSqueaker/Models/SqueakVoicePackDomain.cs`
- `Source/UniversalSqueaker/Logging/SqueakLog.cs` 与 `SqueakLogProtocol.cs` 中 catalog/attach 相关事件
- `CompSqueaker.cs` 中 `CompProperties_Squeaker.CreateDefault()` / comp attach 相关部分
- 为核对 `AudioDomain/RaceKey/XenotypeKey` 一致性，另读取了 `Kernel/Domain.cs`、`Runtime/SqueakKernelAdapter.cs`、`Runtime/SqueakRuntimeResolver.cs`、`Settings/UniversalSqueakerSettings.cs`、`Runtime/SqueakAudioPoolNotificationService.cs`、`UI/Model/VoicePacksPageModel.cs` 的域投影/状态消费路径。

## Files checked

- `Source/UniversalSqueaker/Catalog/SqueakXenotypeCatalog.cs`
- `Source/UniversalSqueaker/Catalog/VoicePackCompAttach.cs`
- `Source/UniversalSqueaker/Models/SqueakVoicePackModels.cs`
- `Source/UniversalSqueaker/Models/SqueakVoicePackDomain.cs`
- `Source/UniversalSqueaker/Logging/SqueakLog.cs`
- `Source/UniversalSqueaker/Logging/SqueakLogProtocol.cs`
- `Source/UniversalSqueaker/CompSqueaker.cs`（`CompProperties_Squeaker` / `CreateDefault()` 附近）
- 相关消费者：`Source/UniversalSqueaker/Kernel/Domain.cs`、`Runtime/SqueakKernelAdapter.cs`、`Runtime/SqueakRuntimeResolver.cs`、`Settings/UniversalSqueakerSettings.cs`、`Runtime/SqueakAudioPoolNotificationService.cs`、`UI/Model/VoicePacksPageModel.cs`
- 测试：`tools/UniversalSqueakerLogTests/Program.cs`、`tools/UniversalSqueakerKernelTests/UnitTests.cs`

## Findings

### Blocker

无。

### Major

#### M1. Xenotype 域状态接口在多 race 目录下丢失 race 维度

- 位置：`UniversalSqueakerSettings.GetVoicePackSelectionStatus(SqueakVoicePackScope, string raceDefName, string targetDefName)` 第 371 行；`SqueakAudioPoolNotificationService.GetDomainStatuses` 第 20-22 行。
- 问题：`GetVoicePackSelectionStatus` 对 Xenotype scope 调用 `catalog.GetVoicePackDomainPacks(SqueakVoicePackScope.Xenotype, target)` 时没有按 `raceDefName` 过滤。该快照方法返回“同一 xenotype 目标的所有 race 的 pack”。因此 `domainKeys` 是跨 race 的并集，而选择的域身份是 `(race, target)`。
- 实际影响：
  - UI 主路径 `VoicePacksPageModel` 已先按 race 过滤，因此当前 UI 不触发此问题。
  - `SqueakAudioPoolNotificationService.GetDomainStatuses` 对 Xenotype 使用旧的重载 `GetVoicePackSelectionStatus(scope, target)`，`ResolveLegacyRace` 在多 race 目录下返回空 race，随后 `GetVoicePackDomainPacks` 返回该 target 的全部 pack；即使没有任何选择，也会因 `enabledKeys` 为空且 `domainKeys` 非空而返回 `Available`，而不是 `Orphan`。
- 建议：`GetVoicePackSelectionStatus` 的 Xenotype 分支先按 `raceDefName` 过滤：
  ```csharp
  IEnumerable<SqueakVoicePackDef> domainPacks = catalog.GetVoicePackDomainPacks(scope, target)
      .Where(pack => string.Equals(pack.raceDefName, raceDefName, StringComparison.Ordinal));
  ```
  同时将 `SqueakAudioPoolNotificationService.GetDomainStatuses` 改为按 `(race, target)` 聚合，或弃用无 race 的 Xenotype 重载。

### Minor

#### m1. 作者补丁命中时没有 `attach_skipped` 诊断

- 位置：`VoicePackCompAttach.Apply` 第 53 行。
- 问题：`def.comps.Any(comp => comp is CompProperties_Squeaker)` 时直接 `continue`，既不上报 `auto_attached` 也不上报 `attach_skipped`。任务说明中提到 `attach_skipped` 诊断；当前只有 `race_not_found` / `no_race_props` 两种 reason。
- 建议：补充 `SqueakLog.CompAttachSkipped(raceDefName, "author_patch")` 或 `"already_attached"`，让“逃生舱生效”可观测；同时更新 LogTests 的 v2 覆盖。

#### m2. 无效 VoicePack 被 catalog 静默丢弃

- 位置：`SqueakXenotypeCatalog.Refresh` 第 26 行。
- 问题：`!SqueakVoicePackValidator.IsValid(pack)` 时直接 `continue`，没有 catalog 级诊断。`ConfigErrors()` 只在开发/校验路径可见，普通玩家日志看不到某个第三方 pack 为什么没有进入目录。
- 建议：收集 `GetErrors(pack)` 的首个/有限条错误，通过 `SqueakLog.PackRejected(key, count, reason)` 或新增 `voicepack.pack.invalid` 事件输出；避免把无效 pack 静默吞掉。

#### m3. `raceDefName` / `targetDefName` 允许首尾空白，可能产生幽灵域

- 位置：`SqueakVoicePackValidator.GetErrors`（`SqueakVoicePackModels.cs` 第 66、69 行）。
- 问题：校验只做 `string.IsNullOrWhiteSpace`，不拒绝 `" Human "` 这类带首尾空白的“非精确 defName”。目录按 Ordinal 精确字符串建域，空白串会形成独立域，且 `VoicePackCompAttach` 必然 `race_not_found`。
- 建议：对 `raceDefName` 与 Xenotype `targetDefName` 增加“与自身 Trim 后相同”或“不含空白字符”的校验，因为 defName 不允许空白。

#### m4. Biotech 装配异常时 canonical 可能部分发布

- 位置：`SqueakXenotypeCatalog.Refresh` 第 70-88 行。
- 问题：`canonical` / `ambiguousCanonicalNames` 在 try 内逐步填充；若 `DefDatabase<XenotypeDef>.AllDefs` 迭代中途抛异常，catch 只设 `discoveryAvailable = false`，已填充的部分 canonical 仍会进入快照，破坏“fail-closed”语义。
- 建议：catch 中清空 `canonical` 与 `ambiguousCanonicalNames`，或把装配字典放到 try 内并在异常时丢弃。

#### m5. `VoicePackCompAttach` 整轮 try/catch，单个 race 异常会中断后续装配

- 位置：`VoicePackCompAttach.Apply` 第 34-61 行。
- 问题：一个 race 的 `GetNamedSilentFail` / `comps.Add` 异常会跳出整个循环，剩余 race 不再处理。
- 建议：把 per-race 处理包成内部 try/catch，记录 `attach_failed` 或 `attach_skipped(reason="exception")` 后继续；外层 catch 仅作最后兜底。

#### m6. 日志可见度：`auto_attached` / `attach_skipped` 均为 Daily

- 位置：`SqueakLogProtocol.cs` 第 80-82 行。
- 问题：`VoicePackCompAutoAttached` 是 Daily Info，`VoicePackCompAttachSkipped` 是 Daily Warning；当目录包含大量 race 时启动日志会逐条输出。作为“诊断”事件，`auto_attached` 更适合 DevOnly；`attach_skipped` 保持 Daily Warning 也可接受。
- 建议：维护者确认日志噪声策略；若改为 DevOnly，同步 LogTests。

#### m7. `SqueakXenotypeCatalogSnapshot` 构造器会排序并修改调用方传入的 List

- 位置：`SqueakXenotypeCatalogSnapshot` 第 139、145 行。
- 问题：`racePacks.Sort(...)` 与 `entry.Value.Sort(...)` 直接改传入列表。当前调用方传本地新建列表，无实际危害，但构造器有隐藏副作用。
- 建议：先复制再排序，保持构造器无副作用。

### Nit

- `SqueakXenotypeCatalog.Refresh(Settings)` 的 `settings` 参数未使用，可移除或说明未来用途。
- `SqueakXenotypeCatalog.discoveryAvailable` 在 assembled-only 阶段恒为 `false` 且未被任何消费者读取；建议注释明确“仅表示 HAR 反射 discovery”，或删除该字段。
- `SqueakVoicePackValidator` 中 `prefix = "US_"` 是硬编码字面量，建议提取常量，避免与项目前缀约定漂移。
- `SqueakVoicePackValidator` 对 Race scope 的 `targetDefName` 用 `!string.IsNullOrEmpty`，而 Xenotype 用 `!string.IsNullOrWhiteSpace`，两者不一致；建议统一为空白即视为非法。

## 域身份一致性核对结果

- `RaceKey` / `XenotypeKey` / `AudioDomain` 使用 Ordinal、区分大小写，与 catalog 的 `StringComparer.Ordinal`、`string.Equals(..., Ordinal)` 一致。
- `SqueakKernelAdapter.BuildEntries` 对 Race 与 Xenotype 都按 `domain.Race.DefName` 精确过滤，跨 race 池隔离正确。
- `SqueakRuntimeResolver.CollectXenoDomains` 使用 `(pack.raceDefName, pack.targetDefName)` 生成双键域，符合 `AudioDomain(race,xeno)`。
- `VoicePackSelectionRecord.SameDomain` 使用 `(scope, raceDefName, xenotypeDefName)` 三字段 Ordinal 比较，与 `AudioDomain` 语义一致。
- 主要不一致点见 **M1**：状态查询路径没有完整保留 race 维度。

## 隐私与安全

- 日志事件只记录 race defName、pack key、target defName、异常类型/脱敏消息；未见本地绝对路径、凭据、API key 或 PublishedFileId。
- 异常消息经 `SqueakLogText.SanitizeExceptionMessage` 做 `<path>` 脱敏与截断，符合仓库隐私要求。
- 未发现新的隐私/安全风险。

## Maintainer decisions needed

1. **M1 的修复范围**：是否立即把 `GetVoicePackSelectionStatus` / `GetDomainStatuses` 改为严格 `(race,xeno)` 域？还是先弃用无 race 的 Xenotype 重载，等 `SqueakAudioPoolNotificationService` 实际接入 UI 时再修？
2. **无效 pack 诊断**：是否新增 `voicepack.pack.invalid` 事件，还是复用 `PackRejected` 的 `reason` 字段输出校验错误？
3. **attach 日志可见度**：`auto_attached` 是否降为 DevOnly？作者补丁命中是否要输出 `attach_skipped(author_patch)`？
4. **`discoveryAvailable` 语义**：保持“仅 HAR discovery”恒 false，还是改为表示“Biotech 装配成功”？

## Suggested verification commands

在仓库根目录执行（若遇到 obj/文件锁竞争，不要重试过多次，记录为待验证）：

```bash
dotnet run --project tools/UniversalSqueakerKernelTests
dotnet run --project tools/UniversalSqueakerLogTests
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release
```

人工/未来回归建议：

- 构造一个多 race、同 xenotype 的 catalog，验证 `GetVoicePackSelectionStatus(Xenotype, raceA, X)` 与 `(raceB, X)` 的 `domainKeys` 互不串扰。
- 构造重复 `XenotypeDef.defName` 的 Biotech 环境，确认 `AmbiguousCanonicalDefNames` 使运行时 fail-closed 到 Global。
- 构造 `raceDefName` 带首尾空白的 pack，确认 validator 拒绝或 attach 有明确诊断。
