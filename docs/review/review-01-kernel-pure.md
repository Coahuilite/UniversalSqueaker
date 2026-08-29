# Review 01 — Kernel / Pure 评审报告

> 评审范围：`Source/UniversalSqueaker/Kernel/*.cs`、`Source/UniversalSqueaker/Pure/*.cs`、`tools/UniversalSqueakerKernelTests/**`。
> 评审基准：最新提交 `4e15511`（double-key xeno tuning contexts）。
> 验证状态：`dotnet build tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj -c Release --no-restore` 成功；`dotnet run --project tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj -c Release --no-restore` 全绿（单测 + golden corpus 零 delta + 确定性重放）。

## 1. 范围

- Kernel 零 Verse/Unity/RimWorld 纯度
- `AudioDomain` / `RaceKey` / `XenotypeKey` / `AudioDomains` 键工具
- `SqueakPoolRegistry` 的 Off/Fallback/Remix、tier、pack fallback、remix 分布、orphan 语义
- `BuiltInFallbackTable` / `ActionKey` / `BuiltInActionKeys` 一致性
- Pure 层：`SqueakActionPlan`、`SqueakTimingModel`、`SqueakLayeredTuning`
- 新增 `SqueakTuningAggregator` / `SqueakContextSelector` 双键调音语义
- Kernel 测试 / golden corpus 的确定性与覆盖

## 2. 检查的文件

- `Source/UniversalSqueaker/Kernel/ActionKey.cs`
- `Source/UniversalSqueaker/Kernel/BuiltInActionKeys.cs`
- `Source/UniversalSqueaker/Kernel/BuiltInFallbackTable.cs`
- `Source/UniversalSqueaker/Kernel/Domain.cs`
- `Source/UniversalSqueaker/Kernel/Modulation.cs`
- `Source/UniversalSqueaker/Kernel/Pool.cs`
- `Source/UniversalSqueaker/Kernel/SelectionMode.cs`
- `Source/UniversalSqueaker/Kernel/SqueakActionDomain.cs`
- `Source/UniversalSqueaker/Kernel/SqueakPoolRegistry.cs`
- `Source/UniversalSqueaker/Pure/SqueakActionPlan.cs`
- `Source/UniversalSqueaker/Pure/SqueakLayeredTuning.cs`
- `Source/UniversalSqueaker/Pure/SqueakTimingModel.cs`
- `Source/UniversalSqueaker/Pure/SqueakTuningDomains.cs`
- `tools/UniversalSqueakerKernelTests/ActionKeyMirror.cs`
- `tools/UniversalSqueakerKernelTests/LcgRandom.cs`
- `tools/UniversalSqueakerKernelTests/Program.cs`
- `tools/UniversalSqueakerKernelTests/Scenarios.cs`
- `tools/UniversalSqueakerKernelTests/SimGate.cs`
- `tools/UniversalSqueakerKernelTests/UnitTests.cs`
- `tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj`
- `tools/UniversalSqueakerKernelTests/fixtures/corpus/us-corpus-0.1.0.txt`
- 相关文档：`docs/us-xeno-double-key-context-zh.md`、`docs/us-routing-table-baseline-design-zh.md`

## 3. 结论摘要

- **Blocker：无。**
- 当前代码纯度门成立：Kernel/Pure 未引用 Verse/Unity/RimWorld；测试工程以无 Verse 引用的方式编译这些文件，构建与测试均通过。
- 发现 3 个需要处理/拍板的主要问题：同 pack 多年龄变体不混抽、S4/S5 golden corpus 未真正覆盖 xeno 域、PackFallback 在 xeno context 下跳过同 race race-pool fallback 的语义歧义。
- 另有若干 minor / nit 级健壮性与覆盖缺口。

## 4. Findings

### 4.1 Major

#### M1. `SelectVariant` 把同 pack 同年龄多变体折叠为“第一个”，S6“同权混抽”语义未实现

- 位置：`Kernel/SqueakPoolRegistry.cs` `SelectVariant` / `SelectTier`。
- 问题：
  - `SelectVariant` 先返回第一个 `AgeTag == ctx.Age` 的变体，否则返回第一个 `AgeTag == null` 的变体。
  - 当同一个 pack 的同一个动作存在多个同年龄变体（例如 S6 中 `Call` 的普通变体 + 彩蛋变体，均为 `null` = 全年龄）时，`AllowEggs=true` 也永远只会选中第一个普通变体；彩蛋变体永远不会被抽到。
  - `ActionSoundSet.Weight` 字段存在但没有被任何选择逻辑读取，进一步表明“变体级带权混抽”是缺失实现而非设计意图。
- 证据：
  - `Scenarios.EggRaceEntry()` 注释明确写 `Call = 普通 + 彩蛋变体（同权混抽）`。
  - golden corpus 中 S6 没有任何 `US_EggPack` 结果行，也没有任何 `_Egg` 声音被抽出。
  - 现有 `EggFiltering` 单测只覆盖“单独 egg-only pack + 单独 normal pack”的 entry 级混抽，未覆盖“同一个 pack 内 normal + egg 变体”的变体级混抽。
- 建议修复：
  - 将 `SelectVariant` 改为返回“候选变体列表”：若存在 exact-age 变体则只收集 exact-age 变体；否则只收集 all-age 变体；**绝不跨年龄回退**。
  - `SelectTier` 按 entry 抽取后，再按 `ActionSoundSet.Weight`（缺省等权）从该 entry 的候选变体中抽取一个，再做 sound 级抽取。
  - 保留现有语义：exact-age 变体存在但全部因 egg 关闭或全 mute 不可用时，该 entry 不参与，不 fallback 到 all-age。
  - 增加单测：同 pack `Call` 含 normal+egg 两个 all-age 变体，`AllowEggs=true` 时多个 seed 应同时出现普通与彩蛋声音。
  - 修复后需用 `--update-corpus` 重建 golden corpus。

#### M2. S4/S5 golden corpus 没有真正覆盖 xeno 域，场景矩阵与注释不一致

- 位置：`tools/UniversalSqueakerKernelTests/Scenarios.cs` `DomainsFor`。
- 问题：
  - `S4-orphan-xeno` 和 `S5-dormant-xeno` 的 `BuildRegistry` 与 S2 完全相同，`DomainsFor` 也只返回 `RaceDomain`，没有返回 `XenoDomain`。
  - 因此 golden corpus 中 S4/S5 与 S2 的 612 行内容基本重复，只是场景名前缀不同；`S4-orphan-xeno|...|US_Race_A+US_Xeno_A` 行数为 0。
  - orphan/dormant 的 xeno 域行为只在 `SelectFallbackChain` 手写单测中覆盖了一部分，未进入 corpus 回归面。
- 建议：
  - 让 `DomainsFor("S4-orphan-xeno")` 至少返回 `{ RaceDomain, XenoDomain }`，使 corpus 真正覆盖“xeno 域选择存在但池空 → RacePack/BuiltIn 回退”。
  - 对 S5 决定是否把 dormant xeno 域纳入 corpus；如果 dormant 表示适配层不注入也不查询，则应在 corpus 中显式标注/跳过，而不是与 S2 重复。
  - 同时检查 S6：当前 corpus 因 seeds 恰好总让 entry 抽取选中 RacePackA，S6 没有任何 EggPack 行；即使修复 M1 后，也建议增加覆盖 EggPack/彩蛋变体的 corpus 场景或 seed。

#### M3. PackFallback 在 xeno context 下跳过同 race 的 race-pool fallback，与文档链语义存在歧义

- 位置：`Kernel/SqueakPoolRegistry.cs` `Select` / `SelectPackFallback`。
- 问题：
  - 文档/注释将 Fallback 链描述为 `XenotypePack → RacePack → PackFallback → BuiltInFallback`。
  - 当前实现中 `SelectPackFallback` 只查询 `ctx.Domain` 的精确池；对 `(Race, Xeno)` context，即使 race-pool 的 pack 声明了同一 action 的 fallback，也不会被使用。
  - 代码注释明确写“xeno ctx 不越域读取 race fallback”，但设计文档 `docs/us-xeno-double-key-context-zh.md` 与 `docs/us-ui-migration-plan-zh.md` 均未记录这一限制，也没有对应测试。
- 建议：
  - 这是语义拍板项：要么确认“PackFallback 永远只属于精确域”是有意设计，在文档中写明并增加 xeno 无 xeno-fallback 但有 race-fallback 的用例；要么将 xeno context 的 PackFallback 扩展为“先 xeno 精确池 fallback，再同 race race-pool fallback”，并更新 corpus。
  - 注意“绝不跨 race”仍然成立；这里讨论的是同 race 下的 xeno→race fallback，不是跨 race。

### 4.2 Minor

#### m1. 域键校验未处理空白字符串

- `AudioDomains.TryCreate` 使用 `string.IsNullOrEmpty`，`" "` 会被当作合法 race/xeno；`RaceKey.IsValid` 也只检查 null/empty 且未被使用。
- 建议改用 `string.IsNullOrWhiteSpace`，或在构造前 `Trim()`；`RaceOnly` 也建议做同样校验。

#### m2. `SqueakPoolRegistry.Select` 缺少 `ActionKey` 空值防御

- 若调用方传入 null `ActionKey`，`SelectBuiltIn` 安全返回 None，但进入 Fallback/Remix 后 `Dictionary.TryGetValue(null)` / `ContainsKey(null)` 会抛 `ArgumentNullException`。
- 建议在 `Select` 入口统一拒绝 null/whitespace action key（或返回 `ChainResult.None`）。

#### m3. 未知 `SelectionMode` 值会静默落入 Remix

- `Select` 只判断 `Off` 和 `Fallback`，其余值全部走 Remix 分支。虽然枚举当前只有 3 个值，但防御性建议对非法值返回 `Off`/`None` 或抛出明确异常。

#### m4. `SqueakTuningAggregator` 对 `overallMultiplier` 未做数值消毒

- NaN/Infinity/负数会原样写入 `LayerBehaviorAggregate.OverallIntervalMultiplier`，虽然 TimingModel 后续会消毒，但聚合器作为 Pure 边界建议统一规范化。
- 另外当前单测未覆盖“同域多个 multiplier 后写覆盖”和“同域 mood 同层 last-wins”；建议补齐。

#### m5. Kernel 测试 csproj 手工列举 Pure 文件

- `UniversalSqueakerKernelTests.csproj` 逐行 `<Compile Include>` 四个 Pure 文件；未来新增 Pure 文件时若忘记加入，会被排除在纯度门外。
- 建议改为 `Source/UniversalSqueaker/Pure/*.cs` 通配符，或增加一个断言“所有 Pure/*.cs 都在测试工程内”的检查。

#### m6. `FallbackProfile` / `FallbackDelta` 保存外部字典引用而非拷贝

- 构造后外部仍可修改原字典，绕过内置键/空值校验。
- 若这些类型按不可变数据处理，建议在构造时复制到新 `Dictionary`（或使用只读包装）。

### 4.3 Nit

- `ActionSoundSet.Weight` 当前完全未被读取；如果 M1 不实现变体权重，应删除该字段或明确标注“预留”。
- `SqueakPoolRegistry.Select` 无论模式都先计算 `vanilla`；Fallback/Remix 高 tier 命中时属于多余 gate 调用（纯函数无副作用，仅性能/可观测性 nit）。
- `PoolsFor` 返回单个精确池却用复数命名，容易误解为返回全部池。
- `SqueakTimingModel` 的 `GetActionIntervalSeconds` / `GetActionIntervalTicks` helper 与 `Evaluate` 的完整路径（population scale、realtime 分支）不完全等价；若未来用于诊断面板，需要明确它们只表示“基础间隔”并补注释。
- `SqueakTuningDomains.cs` 将 `SqueakContextSelector` 与聚合器放在同一文件，与设计稿中独立文件命名不一致，仅文档/组织 nit。

## 5. 维护者决策项

1. **变体混抽语义**：确认是否要实现同 pack 同年龄变体按 `ActionSoundSet.Weight` 混抽（M1）。当前 S6 注释和 `Weight` 字段都暗示“是”，但实现与测试均未体现。
2. **PackFallback 的域边界**：确认 xeno context 是否应使用同 race race-pool 的 pack fallback（M3），并在文档/测试中固化。
3. **S4/S5 corpus 覆盖**：确认 orphan/dormant xeno 域是否应进入 golden corpus（M2），以及 dormant 域在适配层是否完全不参与查询。

## 6. 建议验证命令

```bash
# 1) 纯度门 + 单测 + golden corpus 回归（推荐主验证）
dotnet build tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj -c Release --no-restore
dotnet run --project tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj -c Release --no-restore

# 2) 修改语义后重建 golden corpus（仅在确认行为变更时使用）
dotnet run --project tools/UniversalSqueakerKernelTests/UniversalSqueakerKernelTests.csproj -c Release --no-restore -- --update-corpus

# 3) 纯度静态检查（只查实际引用，不查注释）
rg -n "\busing (Verse|UnityEngine|RimWorld)\b|UnityEngine\.|Verse\.|RimWorld\." Source/UniversalSqueaker/Kernel Source/UniversalSqueaker/Pure || true

# 4) 主程序集编译（若环境无文件锁）
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release --no-restore
```

> 说明：本次实际已执行 1) 的 build 与 run，全部通过；未执行 `--update-corpus`，未修改任何 golden corpus。
