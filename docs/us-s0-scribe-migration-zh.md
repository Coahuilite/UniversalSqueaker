# S0-a Scribe 四态迁移设计

> 状态：定稿（2026-08-25，主会话产出）。
> 前置：本设计依赖 `docs/us-s0-data-model-zh.md`（ActionTuningRecord 形状）与 `docs/us-s0-key-and-race-context-zh.md`（枚举键消费点）尚未落稿；本稿先锁定 voicePackMode 四态迁移 + globalActionEnabled/xenotypePresets.actionOverrides → ActionTuningRecord 的迁移方向，record 字段细节以数据模型稿为准。

## 1. 决定性事实（已用 RimWorld 源码核验，不重验）

1. **枚举按名称序列化**：Scribe_Values.Look 对非 float 值调用 value.ToString()（Verse/Scribe_Values.cs:82），写入 XML 的是枚举名（如 Off/Fallback/Remix），不是序数。
2. **枚举按名称解析**：ParseHelper.FromString 对 itemType.IsEnum 先查 BackCompatibility.BackCompatibleEnum，再 Enum.Parse(itemType, str)（按名称）；无法解析抛 ArgumentException（Verse/ParseHelper.cs:FromString）。
3. **加载失败回落默认**：ScribeExtractor.ValueFromNode catch 解析异常 → Log.Error → 返回 default(T)（Verse/ScribeExtractor.cs:22-32）。对枚举，default = 序数 0。
4. **US 现有枚举**：SqueakVoicePackMode { Off, Fallback, Remix }（SqueakVoicePackDomain.cs:14-19）。voicePackMode Scribe Look 默认 Fallback（UniversalSqueakerSettings.ExposeData.cs:61），voicePackModeWasLoaded 判定缺失=未显式选择（:88）。

## 2. 四态映射（维护者定案：允许破坏，Off 直接改名 Vanilla）

| 新枚举名 | 序数 | 语义 | 来源 |
|---|---|---|---|
| Vanilla | 0 | 关池，vanilla fallback 仍响（旧 Off 改名） | 旧 Off(0) |
| Fallback | 1 | 四级短路（不变） | 不变 |
| Remix | 2 | 四级等权（不变） | 不变 |
| Disabled | 3 | 真旁路：不拦截、不 PlayOneShot（新增） | append |

枚举定义（SqueakVoicePackDomain.cs:14-19）改为：

    public enum SqueakVoicePackMode { Vanilla, Fallback, Remix, Disabled }

## 3. 旧档加载行为（已推导，编码期加 fixture 验证）

- 旧档 voicePackMode = "Off" → 改名后 Enum.Parse 失败 → ValueFromNode 返回 default = Vanilla(0)。
- **语义正确**：旧 Off 的语义就是「关池、vanilla fallback 仍响」，正好等于 Vanilla。
- **唯一副作用**：加载时打一条 Log.Error("Exception parsing node ... into a UniversalSqueaker.SqueakVoicePackMode") 噪音，不崩溃、不破坏存档、不写坏文件（catch 吞掉）。
- 旧档 Fallback/Remix 名称未变，正常解析。
- 无显式 voicePackMode 节点的新档 → 默认 Fallback（不变，ExposeData.cs:61）。

### 消除 Log.Error 噪音（可选，S2 顺带）

方案 A（推荐）：在 UniversalSqueakerSettings.ExposeData 的 voicePackMode Look 前，先读原始 XML 字符串做 Off→Vanilla 重写，避免 Enum.Parse 抛异常。代价：需手写一次原始节点读取（Scribe.loader.curXmlParent["voicePackMode"]?.InnerText）。
方案 B：接受噪音（一次性、无害），不改 ExposeData，靠 fixture 证明无破坏。**默认选 B**（开发早期、允许破坏、噪音仅一条且发生在非致命路径），如需静默再切 A。

## 4. 归一化链更新（NormalizeMode / ToSelectionMode / BuildFallback）

- SqueakRuntimeResolver.NormalizeMode（SqueakRuntimeResolver.cs:207）当前 Fallback/Remix → 原样，其他 → Off。需改为：
  - Disabled 穿透（不得归一成 Vanilla/Off）——见 §5 旁路。
  - Vanilla/Fallback/Remix 原样；非法值回落 Vanilla（原回落 Off 语义不变，名称随枚举改）。
- SqueakKernelAdapter.ToSelectionMode（Runtime/SqueakKernelAdapter.cs，需查证当前实现）同步识别四态。
- BuildFallback（SqueakRuntimeResolver.cs:189-205）构造 SqueakRuntimeSnapshot 时传 SqueakVoicePackMode.Off（:202），改为 Vanilla（枚举改名后编译器强制）。
- 全仓 SqueakVoicePackMode.Off 引用点改 Vanilla（grep 清单：UI/VoicePacksViewState.cs、VoicePacksPageModel.cs、VoicePacksPage.cs、UiCommand.cs、FerriteVoicePacksPage.cs、ModeCard.cs、UsCommandPayload.cs、SqueakKernelAdapter.cs、UniversalSqueakerSettings.ExposeData.cs、SqueakRuntimeResolver.cs、UniversalSqueakerSettings.cs）。

## 5. Disabled 旁路（M1/M6，S2 落地，本稿只定契约）

- NormalizeMode 必须让 Disabled 原样穿透到 SqueakRuntimeSnapshot.VoicePackMode。
- 触发入口 TryTrigger（CompSqueaker.cs:391）与 NotifyExternal（:339）在进 TryTrigger 前统一前置判断 VoicePackMode == Disabled → 直接 return，**不采样、不投影、不写派发日志**（audio.route.selected / audio.dispatch.vanilla_fallback / TriggerOutcomeSummary 均不得在旁路态出现）。
- 只打一条 usdiag 提示（新事件 audio.disabled，见 docs/us-s0-log-protocol-zh.md），复用 SqueakLogOnce 门控，不因同一触发源反复写。

## 6. globalActionEnabled + xenotypePresets.actionOverrides → ActionTuningRecord（迁移方向，M3）

> record 字段细节以 docs/us-s0-data-model-zh.md 为准；本稿定迁移方向与 schema 版本。

- **一次迁移**（避免两轮 Scribe）：settingsSchemaVersion 4 → 5（或按数据模型稿定版本号）。
- globalActionEnabled（List<GlobalActionEnabledRecord>，ExposeData.cs:94 Deep）→ 全局层 ActionTuningRecord（raceDefName=""/xenotypeDefName=""）。
- xenotypePresets[].actionOverrides（List<XenotypeActionBehaviorOverride>）→ Xenotype 层 ActionTuningRecord（携带 raceDefName + xenotypeDefName）。
- 沿用 SqueakSettingsMigration 的原子事务模式（TryCreateXxxRecords 先克隆 → 成功原子发布 → 失败不落盘，SqueakSettingsMigration.cs 范本），新方法 TryCreateV5Records。
- 迁移后 globalActionEnabled 与 xenotypePresets.actionOverrides 字段删除/不再 Scribe；旧字段读入后仅用于迁移，不双写。
- **Race 层**：无旧数据源（现状无 Race 层键），迁移只产生 G/X 两层记录，Race 层由未来 UI 新增。

## 7. 测试 fixture 清单（S2 编码期落地）

1. 旧档 voicePackMode="Off" → 加载后 Vanilla（且语义 = 关池 vanilla 响）。
2. 旧档 voicePackMode="Fallback"/"Remix" → 原样保留。
3. 旧档 globalActionEnabled（含 scope/enabled 组合）→ 全局层 ActionTuningRecord，逐字段 hasX 正确。
4. 旧档 xenotypePresets.actionOverrides → Xenotype 层记录，raceDefName/xenotypeDefName 携带正确。
5. 迁移失败（坏记录）→ 旧列表保留、migrationPersistenceBlocked=true、下次启动幂等重试。
6. Disabled 旁路：不采样、不打派发日志、仅一条 audio.disabled。
7. schema 版本 4→5 双向 replay（旧档 → 新档再读回一致）。

## 8. 依赖与排期

- 本稿的 §2-§4（四态枚举 + 归一化）独立于数据模型稿，S1 即可动（枚举改名属 S1 全局层拨离的伴生改动）。
- §6（ActionTuningRecord 迁移）依赖 docs/us-s0-data-model-zh.md 的 record 字段定稿，随 S2 落地。
- §5（Disabled 旁路）依赖 docs/us-s0-log-protocol-zh.md 的 audio.disabled 事件，随 S2 落地。
