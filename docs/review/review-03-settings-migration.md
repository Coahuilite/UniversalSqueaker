# Review 03 — Settings / Migration / 数据面审查

## 范围

本次审查聚焦设置持久化、schema 4→5 事务迁移、分层调音写桥、Baseline 预设导入，以及 Config copy 生命周期测试。审查的是已合入的 `4e15511` 之后的数据面：该提交本身未改 settings schema，但运行时已按 `(race, xeno)` 双键消费 layer-2 记录，因此需要确认 settings 数据面与运行时一致。

## 检查文件

- `Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.cs`
- `Source/UniversalSqueaker/Settings/UniversalSqueakerSettings.ExposeData.cs`
- `Source/UniversalSqueaker/Settings/SqueakSettingsMigration.cs`
- `Source/UniversalSqueaker/Settings/BaselinePresetImporter.cs`
- `Source/UniversalSqueaker/Settings/SqueakSettingsGameContext.cs`
- `Source/UniversalSqueaker/Models/ActionTuningRecord.cs`
- `Source/UniversalSqueaker/Models/MoodTuningRecord.cs`
- `Source/UniversalSqueaker/Models/SqueakXenotypePresetModels.cs`
- `Source/UniversalSqueaker/Models/SqueakVoicePackModels.cs`（settings 相关部分）
- `Source/UniversalSqueaker/Runtime/UniversalSqueakerTuningBaselineDef.cs`
- `Source/UniversalSqueaker/Runtime/UniversalSqueakerFallbackProfileDef.cs`
- `Source/UniversalSqueaker/Fallback/SqueakFallbackProfileStore.cs`
- `tools/UniversalSqueakerConfigCopyTests/**`
- 另参阅：`Source/UniversalSqueaker/Pure/SqueakLayeredTuning.cs`、`Source/UniversalSqueaker/Pure/SqueakTuningDomains.cs`、`Source/UniversalSqueaker/Runtime/SqueakRuntimeResolver.cs`、`Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`、`Source/UniversalSqueaker/Mod.cs`

## 已执行验证

- `dotnet run --project tools/UniversalSqueakerConfigCopyTests/UniversalSqueakerConfigCopyTests.csproj --no-restore`：**通过**（A–F 全部场景）。
- `dotnet run --project tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj --no-restore`：**通过**（含分层调音与双键域测试）。

未执行主程序集构建；建议在后续验证中补跑（见文末）。

## 总体结论

核心数据面（`actionTuning` / `moodTuning` 的 layer 0/1/2、`(race,xeno)` layer-2 记录、运行时折叠）与最新双键 runtime 一致，未发现当前主路径会直接破坏新写入的记录。主要风险集中在**旧档迁移的完整性**、**重复记录下的 last-wins 一致性**，以及**缺少设置迁移自动化测试**。

## Findings

### Major

#### M1. 旧 `globalActionEnabled` 数据源已不可读，pre-v4 全局动作调音可能静默丢失

当前 `UniversalSqueakerSettings.ExposeData` 不再 `Scribe_Collections.Look` `globalActionEnabled`，且 `SqueakSettingsMigration.TryCreateActionTuningRecords` 只接收 `xenotypePresets`，不再接收 `globalActionEnabled`。

- 影响：若存在 `settingsSchemaVersion < 4` 且仍带 `globalActionEnabled` 的旧配置，Scribe 会忽略该未知节点，迁移时无法把它转为 `actionTuning` layer-0 记录。即使 `MigrateV3RecordsTransactionally` 成功，全局动作开关/作用域也会静默丢失。
- 判断：该 mod 当前未发布，若维护者明确不支持 pre-S2 存档，可作为“不支持旧档”记录；否则这是数据丢失级缺陷。

**建议**：若需支持，保留一个仅加载期读取的 legacy `globalActionEnabled` 列表（可把旧 DTO 恢复为 `[NonSerialized]`/load-only 兼容类型），在 `TryCreateActionTuningRecords` 中一并生成 layer-0 记录，并与 v4/v5 迁移同一事务提交。

#### M2. `MigrateV3RecordsTransactionally` 在 settings 已为 v5、仅 voicePackSchemaVersion 落后时会用旧字段覆盖 `moodTuning`

`MigrateV3RecordsTransactionally` 的入口条件是两个 schema 任一落后。若 `settingsSchemaVersion == 5` 但 `voicePackSchemaVersion == 1`，它仍会调用 `TryCreateMoodTuningRecords(moodOverrides, xenotypePresets, ...)` 并整体替换 `moodTuning`。

- 影响：v5 之后用户在 `moodTuning` 中的新编辑会被旧 `moodOverrides` / `XenotypePresetRecord.moodOverrides` 重新生成的记录覆盖。正常升级路径不会触发，但手工编辑或异常半迁移状态可造成数据丢失。

**建议**：把心情迁移限定为真正的 settings schema 迁移：

```csharp
bool needSettingsMigration = settingsSchemaVersion < CurrentSettingsSchemaVersion;
if (needSettingsMigration) {
    if (!TryCreateMoodTuningRecords(...)) return false;
    moodTuning = migratedMoods;
}
```

voicePackSchema 单独落后时只走 `TryCreateV4Records` 的 voice/preset 规范化，不重建 `moodTuning`。

#### M3. `BaselinePresetImporter.UpsertAction` 只替换第一个同身份记录，违反 last-wins

- `UpsertAction` 找到第一个匹配就原地替换；`SetActionTuningScope` 和运行时同层合并都是“同身份多条记录时列表末位胜出”。
- 若 `actionTuning` 已存在陈旧重复行（迁移、手编或历史版本可能产生），导入 preset 后旧重复行仍可能位于列表尾部并覆盖刚导入的值。
- 对比：`UpsertMood` 已采用“清除全部同身份行后追加”，因此心情侧无此问题。

**建议**：将 `UpsertAction` 改为与 `UpsertMood` 一致——先 `RemoveAll` 同 `(actionKey, raceDefName, xenotypeDefName)` 的行再追加；或至少扫描到最后一个匹配并原地替换。

#### M4. Settings 迁移与 Baseline 导入缺少自动化测试覆盖

- `tools/UniversalSqueakerConfigCopyTests` 只覆盖 fallback profile Config 副本生命周期，未覆盖：
  - `SqueakSettingsMigration.TryCreateV4Records` / `TryCreateMoodTuningRecords`
  - `MigrateV3RecordsTransactionally` 成功与失败路径
  - `SetMoodTuning` / `SetActionTuningScope` 写桥
  - `BaselinePresetImporter` 的 race/xeno 导入、`inheritFromRace`、upsert/last-wins、`sourcePresetDefName`
- 当前这些逻辑只能靠人工/游戏内验证，正是本 review 发现 M3 这类问题未被门禁拦住的原因。

**建议**：新增一个 settings-migration 测试项目（可仿 ConfigCopyTests 链接相关 Settings/Pure 文件，或用 Verse stub 跑 Scribe fixture），至少覆盖：v3→v5 无损迁移、失败不落盘、v5+voice-stale 不覆盖 `moodTuning`、重复记录 last-wins、Baseline 导入幂等与 source 标记。

### Minor

#### m1. `SetActionTuningScope(scope: null)` 只移除最后一个匹配，不清理陈旧重复

与 M3 同源。`SetMoodTuning` 的 `clear` 已 `RemoveAll`，动作侧只 `Remove(record)` 最后一个。若存在重复记录，清空后仍有旧行存活。

**建议**：`scope == null` 时改为 `RemoveAll` 同身份行。

#### m2. `SetMoodTuning` 遇到未知 factor 且无现有记录时会先插入空行

代码在检查 `factor` 合法性前已 `new MoodTuningRecord` 并 `Add`，未知 factor 走 `else return` 后会留下一个全 `hasX=false` 的空记录。

**建议**：先校验 `factor`，合法后再创建/更新记录。

#### m3. `TryCreateMoodTuningRecords` 使用原始 `xenotypePresets` 而非已规范化的 `migratedPresets`

`MigrateV3RecordsTransactionally` 成功取得 `migratedPresets` 后，心情迁移仍读原始 `xenotypePresets`。当前 `LegacyDefaultRaceDefName` 为空时两者等价；但若未来为非空默认 race，原始空 race 预设会被心情迁移跳过，而 v4 记录迁移会补上默认 race，造成不一致。

**建议**：心情迁移传入 `migratedPresets`。

#### m4. `TryCreateActionTuningRecords` 失败发生在 schema 已提交之后，下次启动不会重试

`schema` 先由 `MigrateV3RecordsTransactionally` 推进到 v5，之后才执行 `if (migratedFromPreV4 && actionTuning.Count == 0)` 的动作迁移。若动作迁移异常失败，`migratedFromPreV4` 下一启动为 false，遗留 action overrides 不再有机会迁移。

**建议**：把动作迁移并入事务，或失败时不要推进 schema / 置 `migrationPersistenceBlocked`。

#### m5. 死字段与过期注释

- `moodOverrides` 与 `XenotypePresetRecord.moodOverrides` 仍在序列化，但运行时已完全不消费；作为一次性迁移源保留可以，但建议在文档/注释中明确“仅迁移源，不再消费”。
- `UniversalSqueakerSettings.cs` 第 272 行 `ImportBaselinePreset` 注释仍写“写入 actionTuning 与 moodOverrides”，实际写的是 `moodTuning`。
- `UniversalSqueakerSettings.ExposeData` 的 `voicePackModeWasLoaded` 已无任何读取方，属死状态。

### Nit

- `BaselinePresetImporter.Selection.XenotypeDefNames` 是扁平集合，未携带 race。若同一 xenotype 出现在多个 race 的 baseline 块中，勾选一次会导入到所有 race。需要维护者确认这是期望语义，还是应改为 `(race, xeno)` 选择。
- `BaselinePresetImporter` 将 baseline 行的三个 action 字段 / 三个 mood 因子全部写成 `hasX=true`，即使值为默认。这让导入行“钉住”默认值并覆盖更低层；若希望只覆盖非默认字段，需要引入字段级 presence 语义。当前更像“整行预设覆盖”，可作为设计决策记录。
- `MergeRaceMoods` / `MergeXenotypeMoods` 的计数按遍历次数累加，preset 内重复 mood 行会高报导入数量；不影响最终数据。

## 修复建议汇总

1. 明确旧 `globalActionEnabled` 支持策略；若支持，恢复 load-only 兼容读取并纳入事务迁移。
2. `MigrateV3RecordsTransactionally` 仅在 `settingsSchemaVersion < 5` 时重建 `moodTuning`。
3. 统一动作/心情写入的重复行策略：清除全部同身份行后追加，或更新最后匹配行。
4. 为 settings 迁移与 Baseline importer 增加自动化测试。
5. 清理死字段/注释，或将它们标记为“兼容迁移源”。

## 维护者决策

- pre-v4 / `globalActionEnabled` 旧档是否仍在支持范围？若否，建议在迁移文档中显式声明不支持，避免未来误判为 bug。
- baseline 的 xenotype 选择按 `xenotypeDefName` 全局勾选，还是需要按 `(race, xeno)` 精确勾选？
- baseline 导入行是否应保持“整行全字段显式”语义，还是改为只导入非默认字段？
- `moodOverrides` / `XenotypePresetRecord.moodOverrides` 是否在下一个可破坏 schema 版本中移除，或长期保留为兼容迁移源？

## 建议验证命令

```bash
dotnet run --project tools/UniversalSqueakerConfigCopyTests/UniversalSqueakerConfigCopyTests.csproj --no-restore
dotnet run --project tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj --no-restore
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release --no-restore
```

新增测试后建议追加：

```bash
dotnet run --project tools/UniversalSqueakerSettingsMigrationTests/UniversalSqueakerSettingsMigrationTests.csproj --no-restore
```

（项目名仅为建议，可按仓库现有测试风格命名。）
