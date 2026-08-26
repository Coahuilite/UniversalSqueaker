# US UI 迁移与孤儿功能开发计划

> 状态：定案稿（2026-08-24 会话整理）。
> 范围：从 SR 设置 UI 迁移到 US 新 UI 调度器（FerriteLib.UiKit）的完整需求、决策与实现顺序。
> 本文件是开发计划正文；孤儿功能清单与其数据/运行时事实一并收录，与 `TODO.md` 的「下一步开发计划」节互指。

## 1. 产品基线与命名

- **产品定位**：US 是 pawn 身上的 addon（通过 `CompProperties_Squeaker` 挂载、拦截并重路由发声）。
- **模式四态**（非 1.0.0 允许破坏性改动，`voicePackMode` 枚举改名 + Scribe 迁移）：

| 枚举名 | 语义 |
|---|---|
| `Disabled` | 停止介入 = 真旁路：不拦截、不 `PlayOneShot`，pawn 走原版原生音；实现采用采样层短路，不改 `ThingDef.comps` |
| `Vanilla`（推荐名，待确认） | 旧 `Off` 改名：关池，VoicePack 池不参与，但 addon 仍在，vanilla fallback 仍响 |
| `Fallback` | XenotypePack → RacePack → PackFallback → BuiltInFallback 四级短路 |
| `Remix` | 四级等权折叠，固定序 [xeno, race, pack fallback, builtin] |

- **Off 改名映射（审查发现原文档矛盾，按维护者指示收敛为推荐方案，待最终确认）**：
  - 旧 `Off`（关池、vanilla fallback 仍响）→ 推荐改名 `Vanilla`（或 `FallbackOnly`），因为"Off"语义不诚实；
  - 新增 `Disabled`（真旁路，连 vanilla fallback 都不响）；
  - Scribe 迁移：旧枚举值 `Off(0)`→新 `Vanilla(0)`，`Fallback(1)`→`Fallback(1)`，`Remix(2)`→`Remix(2)`，新增 `Disabled(3)`。
- 「模组停止活动/静音」与 `Disabled` 合并，不单开第 5 态。

## 2. 决策总表

| # | 主题 | 定案 |
|---|---|---|
| 0 | 产品定位 | US = pawn addon；`Disabled` = 停止介入 = 真旁路 |
| 1 | 模式 | `Disabled / Vanilla(旧 Off 改名,推荐) / Fallback / Remix` 四态；非 1.0.0 允许破坏；映射见 §1 |
| 2 | 旁路实现 | 采样层短路（不动 Def）；"随时开回" = 翻转开关即恢复介入 |
| 3 | 旁路日志 | `Disabled` 下只打一条 `usdiag` 提示，之后不再因触发源反复写日志（§6） |
| 4 | 调音编辑器 | 行为 + 心情合并；baseline = 独立只读 XML Def；玩家分层 override；导入/导出 delta feedback |
| 5 | 动作作用域 | 方案 A 一张分层表（Global→Race→Xenotype，具体层覆盖宽层）；与动作门合并设计 |
| 6 | 彩蛋开关 | 设置主体、与模式卡同区、默认关 |
| 7 | `experimentalRaceAllowlist` | 删（SR 残留，项目早期清理） |
| 8 | `localizeDebugActions` | 保留 + 恢复 `Patch_DebugTabMenu_Actions` + `US_` 键 |
| 9 | `<description>`/`Name` 本地化 patch | 都删（本地化需重启才显示，观察点小，认定为无用） |
| 10 | Refresh Catalog | 不搬按钮（Def 需重启才变；保留内部 commit+flush 机制） |
| 11 | Developer 页 | 由 DebugAction 面板替代，不单独做 |
| 12 | 动作入口 wrapper 化 | `ActionEntry` + `TriggerBinding` 替代硬编码 `Notify_*`+patch；底层忠实路由原版通路；`Disabled` 旁路 = wrapper 短路让位原版（§13） |

## 3. 孤儿功能清单（数据 + 运行时活着、UI 死着）

判定标准：Scribe 序列化 + 运行时/数据面消费，但 `VoicePacksPage` 不渲染、不写、玩家无法改。

### A. 真孤儿（运行时活跃）—— 必须进入开发计划

| # | 字段/功能 | 运行时消费 | 计划归属 |
|---|---|---|---|
| A1 | `xenotypePresets`（异种行为/心情调制） | `SqueakRuntimeResolver.cs:149-155` 逐动作/心情调制 | 调音编辑器（§4） |
| A2 | `moodOverrides`（全局心情→音色覆盖） | `CompSqueaker.ResolveMoodMod`（`CompSqueaker.cs:561-573`） | 调音编辑器（§4） |
| A3 | `distanceRange`/`distancePreset` | `CompSqueaker.ApplyDistanceRange`（`CompSqueaker.cs:838-853`） | Basics 全局调音（§7） |
| A4 | 三个运行时缩放开关 | `CompSqueaker.cs:147-149` 静态字段 | Basics 全局调音（§7） |
| A5 | `globalCooldownMultiplier` | 纳入 `globalMinIntervalTicks` 计算 | Basics 全局调音（§7） |
| A6 | `globalActionEnabled`（动作作用域） | `SqueakGlobalActionPolicy.Publish` + `GetScope` | 动作作用域分层表（§5） |
| A7 | `allowEasterEggSounds` | `SqueakRuntimeSnapshot.AllowEggs` → pool 抽取过滤 | 彩蛋开关（§8） |
| A8 | `devLoggingMode` | `SqueakLog.Configure` + `EffectiveDevLogging` | DebugAction 面板（§9） |

### B. 半孤儿/死字段

| # | 字段 | 状态 | 计划 |
|---|---|---|---|
| B1 | `experimentalRaceAllowlist` | 纯兼容尸体 | 删 |
| B2 | `voicePackDefaultSeeded` | Scribe 形状兼容，仅作启动时 no-op 守卫写入，不产生业务动作 | 随 Scribe 清理评估 |
| B3 | `localizeDebugActions` | US 已删本地化 patch，字段纯死 | 恢复功能（§9） |
| B4 | `developerToolsEnabled` | 无消费者 | 随 Developer 页改 DebugAction 面板后评估 |

### C. 系统级机制（活着但无 UI 面）

| # | 机制 | 现状 |
|---|---|---|
| C1 | `SqueakActionDefinitions`/action 枚举 ABI | 运行时与内核在用；动作门 `SqueakActionDef`+`allowExternalActions` 未落 |
| C2 | `SqueakFallbackProfileStore`（fallback profile 编辑/merge） | 数据+运行时活着，无 UI |
| C3 | `SqueakXenotypeCatalog.Refresh()`/`RefreshCatalogAndRuntime()` | 运行时入口在，UI 按钮拆掉 |

## 4. 调音编辑器（行为 + 心情合并）

### 4.1 数据模型（三层）

```
作者 baseline（独立只读 XML Def）
   → 玩家 override（分层表：Global → Race → Xenotype，具体层覆盖宽层）
   → 运行时生效（SqueakRuntimeResolver 消费）
```

- **baseline 承载形态（区分两类，审查修正原矛盾）**：
  1. **玩家可调的调音 baseline**：新增独立只读 Def（建议 `UniversalSqueakerTuningBaselineDef`），DefDatabase 加载、模组不写回、不防玩家手改文件；**不进 `CompProperties_Squeaker`、不进 `SqueakVoicePackDef`**。
  2. **作者 per-pack 挂载与包内默认调音**：仍留在 `CompProperties_Squeaker`（收敛后的挂载面 + `actions` 触发配置 + `moodMods`，见 §11），这是作者 patch 的既有 ABI 面，不是玩家调音 baseline。
- **导入/导出**：玩家配置 delta（override）经独立契约（建议 `US.TuningFeedback.v1`）导出，作为分享与向作者反馈的途径；作者自行评估融入 baseline。

### 4.2 运行时解析顺序

`baseline Def → 玩家 override 逐字段覆盖 → 生效`；字段级 last-wins（沿用 `XenotypePresetRecord` 思想），`hasX=false` = 该层不做决定向下继承。

## 5. 动作作用域（方案 A：一张分层表）

- **替换** `globalActionEnabled`（单层）与 `xenotypePresets.actionOverrides`（仅异种层）。
- **统一记录**：`ActionTuningRecord` 带可选 `raceDefName` / `xenotypeDefName`；
  - 三者皆空 = 全局层；
  - 仅 `raceDefName` = Race 层；
  - `raceDefName` + `xenotypeDefName` = Xenotype 层。
- **规则**：`Xenotype > Race > Global > 动作默认(DefaultScope)`，具体层显式设置覆盖宽层；"全局关 + xenotype 独立开"兼容。
- **与动作门合并**：`SqueakActionDef` + `allowExternalActions` 总闸 + action 枚举→string 落地时，共用这张表的玩家开关面，避免两套动作开关。
- **注意**：删 `globalActionEnabled` 动 Scribe 形状，走迁移（非 1.0.0 允许破坏）。

## 6. `Disabled`（真旁路）实现规格

- **短路方案**：`SqueakRuntimeSnapshot`/`CompSqueaker` 各 `Notify_*` 触发链前置判断 `voicePackMode == Disabled` → 直接返回，不拦截、不 `PlayOneShot`，pawn 走原版原生音。**不改 `ThingDef.comps`**。
- **随时开回**：开关翻转即恢复介入，不重建 Def。
- **日志规则（硬要求）**：
  - `Disabled` 下只打一条 `usdiag` 提示（建议新事件 `audio.disabled`，或复用既有事件带 disabled 标记）；
  - 之后不因同一触发源反复写日志（复用 `SqueakLogOnce` once 门控）；
  - 短路必须发生在派发/fallback 日志之前，避免 `audio.route.selected`/`audio.dispatch.vanilla_fallback`/`TriggerOutcomeSummary` 在旁路态误打。
- **日志协议扩展**：新事件入 `usdiag` 协议需同步 `tools/UniversalSqueakerLogTests`（协议冻结契约）。

## 7. Basics 全局调音（纳入考量）

- **A 批·直接搬**：三个运行时缩放开关（A4，`CompSqueaker.cs:147-149`）、`globalCooldownMultiplier`（A5）、距离预设+范围（A3）、`globalMinIntervalTicks`（自 `CompProperties_Squeaker` 全局层迁出，新设置默认 216）。
- **B 批·重设计**：距离预览图表（保守/激进档 + 拖动条 + 折线图）、动作作用域 UI（与 §5 分层表一起做）。
- 这些字段运行时全活、UI 全死，不作为尸体删除。

## 8. 彩蛋开关

- `allowEasterEggSounds` 进设置主体，与模式卡同区，默认关。
- 语义：开则 egg 条目加性入池同权混抽，关则仅普通条目；走离散 resolver 重建（`SetAllowEasterEggSounds` 已就绪）。

## 9. Developer / Debug 面

- Developer 页由 DebugAction 面板替代（`Prefs.DevMode` 门控）。
- `localizeDebugActions` 保留：恢复 `Patch_DebugTabMenu_Actions` + `US_` 键 + 设置 toggle。
- `devLoggingMode`（A8）入口随 DebugAction 面板补。
- `<description>`/`Name` 本地化 patch 删除（`Patches/Patch_ModMetaData_LocalizedMetadata.cs` 整文件）。

## 10. UI 面其余项（FerriteLib.UiKit 承载）

- 过滤系统：作者 / 种族 / 冲突（dormant/target-unavailable/canonical-conflict/orphan）/ Legacy 四维必筛 + 下拉快速筛复选框（只看已启用 / 只看 Legacy / 只看某作者 / 只看冲突 / 只看 orphan）。
- 帮助组件化：帮助内容作为 UI 组件的对应部分（widget 自带 Help 元数据），帮助组件读取对应组件内容就地呈现；`ui` 工具层 keep separate help/action rect，防点穿。
- 窄屏响应式：Race 分配页 + 专门设置页结构，Ferrite `Measure→Draw` 两遍 + widget 内按 `ViewWidth` 选单列/双列。
- 美化：不用原版黑灰框，做 RimWorld 风格现代 UI（surface 层级、描边+内发光、金色语义点缀、圆角/栅格/字体层级、缩放安全），沉淀到 Ferrite core `Palette` + core widgets；先出 `ui-visual-modernization-zh.md` 评估稿再改码。
- build identity 进 footer 双槽（左版本 / 右保存状态）。

## 11. `CompProperties_Squeaker` 的 XML 默认与作者 ABI 收敛（事实基线 + 拆分决策）

### 11.1 当前承载（`CompSqueaker.cs:897-955`）

`CompProperties_Squeaker` 当下同时承载两类内容，**作者 patch 面与全局调音混在一起**：

| 字段 | 类型 | 默认 | 归属 | 语义 |
|---|---|---|---|---|
| `globalMinIntervalTicks` | int | 216 | **全局（每个作者都重复写）** | 全局最小间隔 |
| `scaleFrequencyWithTalking` | bool | true | **全局（每个作者都重复写）** | 交谈缩放默认 |
| `actions` | `List<SqueakActionConfig>` | 空（`CreateDefault()` 填 15 动作） | **作者 per-pack** | 动作→触发配置（mode/minInterval/probability/ignoreGlobalCooldown/cooldownClock） |
| `moodMods` | `List<SqueakMoodMod>` | 空（`CreateDefault()` 填 4 心情） | **作者 per-pack** | 心情→音调/音量/抖动默认 |
| `distancePresets` | `List<SqueakDistancePresetConfig>` | 空（`CreateDefault()` 填 3 档） | **全局/作者（现在混用）** | 距离衰减预设 |

**三层默认的区分**：
1. C# 字段默认值：`globalMinIntervalTicks=216`、`scaleFrequencyWithTalking=true`，列表全空。
2. `CreateDefault()`（`CompSqueaker.cs:916-954`）程序化默认：legacy 桥 auto-attach 用，镜像 15 动作 + 4 心情 + 3 距离，无 race/声音字面量。
3. 作者 comp patch XML：canonical 包**必须自己写** `CompProperties_Squeaker`（见 `us-voicepack-authoring/SKILL.md` 模板，含 `globalMinIntervalTicks`/`scaleFrequencyWithTalking`/`moodMods`/`distancePresets`）。

### 11.2 问题确认（维护者判断，已采纳）

- `globalMinIntervalTicks` 与 `scaleFrequencyWithTalking` 属**全局**参数，却被每个 canonical 包作者在 comp patch 里重复声明——改动它们就要动 skill + 作者指南，成本与语义都错。
- 作者 comp 应当**只负责两件事**：① 把音频挂到动作（actions 里的触发配置可保留作者可调部分）；② 写自己的 baseline（moodMods/distancePresets 中真正属于该包调音的部分）。

### 11.3 拆分决策

1. **全局参数**（`globalMinIntervalTicks`、`scaleFrequencyWithTalking` 及全局距离预设档）从作者 patch 面**移除**，迁到 US 全局设置（§7 Basics 全局调音），或由 US 自身提供默认 Def，作者不再重复声明。
2. **`CompProperties_Squeaker` 收敛**为：动作→音频挂载 + 作者 per-pack baseline（`actions` 触发配置 + `moodMods` 若作者要覆盖）。
3. **skill/作者指南同步更新**：模板删除全局字段；新增「作者只写音频挂载 + baseline」章节。此为 `us-voicepack-authoring/SKILL.md` 的一次破坏性修订（非 1.0.0 允许）。
4. **遗留兼容**：旧 pack 若仍写 `globalMinIntervalTicks`/`scaleFrequencyWithTalking`，US 加载时静默忽略（或告警一次），不报错、不破坏已有 ABI 存量——一次性迁移窗口。

### 11.4 结论

`CompProperties_Squeaker` 不再承载**玩家调音 baseline**；玩家调音 baseline = 独立只读 Def（§4.1）；作者 per-pack baseline = 收敛后的 comp 挂载面 + `actions` 触发配置 + `moodMods`（`distancePresets` 归全局，§11.3）；全局调音归 US 设置（§7）。

## 12. 实现顺序（S1–S4，每阶段独立提交 + 门禁）

- **S1 全局层拨离**：距离预设（保守/激进档 + 拖动条 + 折线图）、`globalMinIntervalTicks`、`scaleFrequencyWithTalking` 从作者 patch 面移除，归 US 全局设置（§7；`globalCooldownMultiplier` 已是全局设置，只需补 UI）；旧 pack 仍写这些字段时静默忽略（一次性告警）。skill 模板删全局字段。
- **S2 动作入口 wrapper 化**（原动作门 + 挂载/调音拆分合并，§13）：`ActionEntry`/`TriggerBinding` 替代硬编码 `Notify_*`+patch；`SqueakAction` 枚举→`ActionKey` 字符串；`allowExternalActions` 同批落地；`Disabled` 旁路 = wrapper 短路让位原版；`CompProperties_Squeaker` 收敛为挂载面 + 作者 baseline 指针。
- **S3 Sustainer 通路**：`TriggerBinding` 增加 `Sustainer` 型，底层忠实路由原版 `TrySpawnSustainer`/`Maintain`/`End`；当前 `SqueakTriggerMode.Sustained` 为死模式，S3 使其可被作者声明使用。
- **S4 调音编辑器 + 分层 override + 反馈**：baseline Def、玩家分层 override 表（方案 A）、调音编辑器 UI、导入/导出 delta feedback、彩蛋开关、过滤系统、帮助组件、窄屏、美化、build identity（UI 面可并行）。
- **S5 清理**：`experimentalRaceAllowlist`、`<description>`/`Name` patch、随 Developer 面板评估 B4。

## 13. 动作入口 wrapper 化（ActionEntry / TriggerBinding）

### 13.1 当前触发挂载事实（已核实）

- **配置面**：`CompSqueaker.Initialize` 从 `Props.actions` 填充 `actionPlans[]`；未配置的动作永不触发。
- **采样面分两类**：
  - 周期类：`CompTick` → `CurrentAction` 从 pawn 状态推导（Sleeping→Sleep / Eating→Eat / Socializing→Social / JoyJob→Joy / Moving→Move / Working→Work / 否则→Call），`EachTime`/`RandomOneShot` 走 `TryTrigger`；`External`/`Sustained` 在周期路径 break。
  - 外部类：`Notify_Wounded/Select/Death/Attack/Equip/MentalBreak/MentalFit` 各配一个 Harmony patch；`Draft` 与 `Undraft` 共用 `Patch_DraftGizmo_Toggle`，写死 `SqueakTriggerOrigin` 与 `SqueakInvocationSource`，统一走 `NotifyExternal` → `TryTrigger`。
- **当前扩展动作的代价**：改 `SqueakAction` 枚举 + `SqueakActionDefinitions` + 写 `Notify_*` + 写 patch + 内置表/日志/统计等多处同步。这就是 wrapper 化要消除的硬编码。

### 13.2 `Sustained` 现状（审查修正：周期路径死，外部路径不按 mode 过滤）

- `SqueakTriggerMode.Sustained` 在 `CompTick` switch 中与 `External` 一样 break（周期路径不触发）。
- 但 `NotifyExternal`/`TryTrigger` **不校验 mode**：若作者把某个外部动作（如 `Attack`）配成 `Sustained`，它仍会经 `Notify_Attack → NotifyExternal → TryTrigger → PlayOneShot` 一次性触发并参与池筛选/统计——即当前 `Sustained` 会**退化为一次性 PlayOneShot**，而非真正持续音。
- 内置/默认配置无 Sustained；真正的 Sustainer 播放路径（`TrySpawnSustainer`/`Maintain`/`End`）当前不存在，S3 才实现。

### 13.3 wrapper 化设计草图（S2 落地）

```
ActionEntry（动作入口注册表项，一个动作一项）
 ├─ ActionKey          : string（未来替代 SqueakAction 枚举；内置键 = 现枚举名）
 ├─ Binding            : TriggerBinding（原版通路绑定）
 ├─ DefaultPlan        : 默认触发协议（进 baseline，作者可调）
 └─ BuiltInAudioKey    : 内置 SoundDef 键（现 SqueakActionDefinitions.AudioKey）
```

```
TriggerBinding（原版通路绑定；底层忠实路由，US 只采样/决策/让位）
 ├─ BindingKind        : PeriodicState | VerseEvent | Sustainer
 ├─ StateProbe         : PeriodicState 用（Sleeping/Eating/… 谓词，替代 CurrentAction 硬编码）
 ├─ VersePatch         : VerseEvent 用（patch 目标 + origin/source 派生规则）
 ├─ InvocationSource   : 派生规则（StateEvent / PlayerSelection / ActiveCommand）
 └─ OnDisabled         : 让位行为（短路不介入 → 原版通路原样发声）
```

**原则**：
1. **忠实路由**：wrapper 只包装原版通路；US 决策通过 → 发 US 音；`Disabled`/未启用/门控拒绝 → wrapper 零介入，原版该响什么响什么。
2. **动作门同批落地**：`ActionEntry` 注册表即 `SqueakActionDef` 的运行时形态；`allowExternalActions` 总闸控制非内置 `ActionEntry` 是否生效。
3. **Scribe/日志同步**：`SqueakAction` 枚举→字符串键落地时，`usdiag` action 字段、`usdiag` 报告、动作作用域分层表全部改用 `ActionKey`。

### 13.4 三段式架构全景（从哪来 → 我是谁 → 到哪去）

```
从哪来（TriggerBinding）            → 我是谁（PawnIdentity）              → 到哪去（Registry.Select）
触发源识别 + 采样                  pawn → race/xenotype/age 域身份        域池 → 声音选择 → 播放
```

### 13.5 路由中立 vs 外置部分（评估定案）

判定标准：内核/Pure = 零 Verse、零产品字面量、只认字符串键与域类型；外置 = 一切 Verse/RimWorld 采样与副作用。

| 段 | 路由中立（Kernel/Pure） | 外置部分（Verse/RimWorld 适配层） |
|---|---|---|
| 从哪来 | `SqueakTriggerMode`（`Kernel/SqueakActionDomain.cs`）、`SqueakTriggerOrigin` / `SqueakInvocationSource` / `SqueakTriggerInvocation`（纯数据，`Pure/SqueakActionPlan.cs`） | `CompTick` 状态探针（`IsSleeping`/`IsEating`/`IsMoving`…）、Harmony patches（`Notify_*` 注入）、`CurrentAction` 推导、Sustainer 维护 |
| 我是谁 | `RaceKey` / `XenotypeKey` / `AudioDomain`（`Kernel/Domain.cs`）、`AgeBucket` 年龄桶（`Kernel/Pool.cs`）的类型与相等规则 | `pawn.def.defName` / `pawn.genes.Xenotype` / `CurLifeStage` 的采样（`SqueakRuntimeSnapshot.ResolveContext`/`Choose`，`Runtime/SqueakRuntimeResolver.cs`） |
| 到哪去 | `SqueakPoolRegistry.Select` 四级链、`SelectionMode`、`ChainTier`/`ChainResult`、`ISoundGate`、`IRollSource`（已在 `Kernel/`） | `SqueakKernelAdapter.GateFor`/`Rolls`/`ToChoice`、SoundDef 可用性判断、`PlayOneShot`（`TrySpawnSustainer` 为 S3 规划） |

结论：
1. 每段都是「中立决策核心 + 外置 Verse 采样」两半；内核只认 `ActionKey(string)` / `AudioDomain` / `SelectionMode` / `gate` / `rolls`，不认 `Pawn`/`ThingDef`/`XenotypeDef`/`SoundDef`/Harmony。
2. `ActionEntry` 注册表跨两层：元数据（ActionKey/BindingKind/DefaultPlan/BuiltInAudioKey）中立；绑定实现（StateProbe/VersePatch/Sustainer 维护）外置。
3. 新增动作 = 外置层注册绑定实现 + 内核业务逻辑零改动；如需内置回退支持，仅在 `Kernel/BuiltInActionKeys.cs` 清单增加一个数据字符串。
4. `Disabled` 旁路 = 外置层短路（wrapper 不采样、不投影），内核无感知。

## 14. 审查发现与待补设计（2026-08-24 双审查收敛）

### 14.1 高风险（进入 S2/S4 前必须补设计）

| # | 风险 | 缓解/待补 |
|---|---|---|
| H1 | 分层优先级「Xenotype > Race > Global」与现运行时冲突：`CompSqueaker.TryTrigger` 先查 `SqueakGlobalActionPolicy`（Global Disabled 直接 return），`SqueakRuntimeResolver.BuildContext` 用 `global.Enabled && item.Value.Enabled` 合并，全局关会压死 Xenotype 开 | 动作作用域改为 `ActionTuningRecord` 分层快照驱动；删除/降级 `SqueakGlobalActionPolicy` 早退；`TryTrigger`/`BuildContext` 按 X→R→G→Default 求值；补「全局关 + Xeno 开」内核/集成测试 |
| H2 | Scribe 迁移只有"走迁移"四字，无映射表/版本号/事务/fixture；且与 HANDOFF 旧红线「不得改动 voicePackSelections/voicePackMode/resolver 的 Scribe schema」存在文档冲突（该红线已按"非 1.0.0 允许破坏"更新） | S2 前补显式迁移设计：旧 `Off(0)`→`Vanilla(0)`（推荐名待确认）、`Fallback(1)`/`Remix(2)` 不变、新增 `Disabled(3)`；`globalActionEnabled`→全局层 `ActionTuningRecord`；`xenotypePresets.actionOverrides`→Xenotype 层记录；沿用"先克隆、成功原子发布、失败不落盘"事务；同步 ConfigCopy/Log 测试 |
| H3 | Race 层调音无运行时消费点：`ResolveContext` 只有 global/xenotype 两类 context，纯 Race pawn 或 Biotech 关时只得到 globalContext | resolver 引入 Race 维度 context 或分层 delta 合并；`ResolveContext` 纯 Race 返回 race context，Xenotype 做 Race→Xenotype 合并；`ResolveMoodMod` 走同一分层 |
| H4 | `ActionKey` 字符串化波及面被低估：`CompSqueaker` 5 个按 `SqueakActionDefinitions.Count` 固定长度数组、`SqueakRecentOutcome`/`SqueakActionPlan`/`SqueakGlobalActionPolicy` 全以枚举为键；`SqueakTriggerOrigin` 是闭合枚举，无法表示外部动作 | S2 前先做「枚举键消费点 inventory」；Comp 内部数组改 `Dictionary<ActionKey,...>` 或 adapter 统一投影；日志/诊断同步 string key；定义外部动作的 Origin/Source 表示 |
| H5 | S3 Sustainer 前置缺失：`SqueakVoicePackValidator.cs:105` 拒绝 `sound.sustain`，`SqueakSoundAvailabilityCache` 把 sustain 判为不可播 | S3 范围加：验证器允许 Sustained SoundDef、Sustainer playability 分支、`TrySpawnSustainer`/`Maintain`/`End` 外置通路、映射测试 |

### 14.2 中风险

| # | 风险 | 缓解/待补 |
|---|---|---|
| M1 | `Disabled` 可能从 `NormalizeMode`/`ToSelectionMode`/`BuildFallback` 漏成 `Off`，旁路态仍播 US 内置 fallback | `Disabled` 穿透 resolver 快照；`Choose` 前置返回 None；`BuildFallback` 保留原模式；UI NormalizeMode 识别 Disabled |
| M2 | 新 `usdiag` 事件（`audio.disabled`）协议版本未定，可能破坏 v1 冻结面 | 按 v2 扩展（或复用现有事件加 `disabled=true` 标记）；同步 `SqueakLogProtocol.cs` + `tools/UniversalSqueakerLogTests`；v1 字节面不变 |
| M3 | S2 与 S4 的动作作用域表存在 schema 重叠，若先 enum→string 再 record 化会产生两轮 Scribe 迁移 | 分层表 schema 定义与迁移提前到 S2；S2/S4 共用同一 `ActionTuningRecord` Scribe 形状 |
| M4 | Harmony hook 边界语义可能在 wrapper 化时丢失（Select 只认 playSound=true、Kill 前置、Equip 只认玩家 job、Attack 动态 ActiveCommand/StateEvent、Draft 前后值比较） | 每个内置 `TriggerBinding` 写"行为等价清单"，以现有 patch 行为为基准补回归测试；wrapper 只替换调用入口不改判断条件 |
| M5 | `globalMinIntervalTicks` 迁出需明确新设置字段名/默认值/Scribe 位置 | S1 明确：新设置字段、默认 216、`Scribe_Values.Look`、`ApplyToRuntime` 发布、旧 pack 字段静默忽略、SKILL 模板同步 |
| M6 | `Disabled` 短路位置应统一在 `CompTick`/`NotifyExternal` 进 `TryTrigger` 之前，避免漏过 `SqueakGlobalActionPolicy`/冷却/日志 | 在 `CompSqueaker` 触发入口统一前置判断 `VoicePackMode == Disabled`；`SqueakLogOnce` 单次提示 |

### 14.3 建议实施顺序（审查调整后）

1. 补「Scribe 迁移 + 数据模型」设计（voicePackMode 四态映射、`ActionTuningRecord`、`xenotypePresets`/`moodOverrides` 去向）。
2. S1 全局参数剥离 + 新设置字段（`globalMinIntervalTicks` 默认 216）。
3. S2 一次性完成 ActionKey 字符串化 + 分层表 + 动作门迁移（避免二次 Scribe）。
4. S3 同步放开 Sustained 验证与播放层。
5. S4 调音编辑器 + 分层 override + 反馈 + UI 面。
6. S5 清理（可在 S2 的 string key 化后并行）。
