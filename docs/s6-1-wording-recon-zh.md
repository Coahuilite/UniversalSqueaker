# S6-1 侦察 + 首轮落地：发声规则 / 频率 / 间隔 / 衰减 的文案对照

> **基准来源**：**lead 从维护者截图逐条转录**（2026-09-22）。这是**可见样本，不是全量基准** ——
> SR 仓库路径尚未拿到，未在样本中出现的条目**一律未动**。
> 本文件状态：第 3 列已填（样本内）/ 标「样本中不可见」（样本外），并记录本轮改了什么、为什么。

## 0. 三层出处（本会话能核到的事实）

| 数 | 值 | 出处 |
|---|---|---|
| US 窗口自身地板 | `SettingsWidthFloor = 800f` | `Source/UniversalSqueaker/UI/Layout/WindowChromeLayout.cs:60`（由覆盖 vanilla `Dialog_Options.InitialSize = 650x600` 的最小整像素 4:3 盒推导） |
| 逻辑屏幕地板 | 1024 | 平台强制（`UIScaleSafeWithResolution`） |
| harness 最低压力档 | 320 | `tools/UniversalSqueakerKernelHostTests/FrameGeometryLaneTests.cs:72`（**UNMEASURED**：无证据表明真实屏幕能达到） |

## 1. SR 可见样本（来源：lead 转录维护者截图）

- 页面标题：**鼠辈啁啾**
- 页签三个（含副标题）：**发声规则**（频率、距离与动作） | **心情音色**（音高、音量与试听） | **语音来源与异种**（语音包与目标专属设置）
- 区块说明：发声规则 —— 「调整鼠族的发声频率、听取距离和可发声动作。改动会立即生效并自动保存。」
- 子标题：**频率**
- 行（含暗色注文）：
  1. 高倍速时保持叫声节奏稳定
  2. 按语言能力（Talking）调整普通叫声尝试
  3. 可听区域鼠族较多时减少周期叫声 ／ 注文：Talking 能力越低，普通叫声尝试通过的概率越低。
  4. 仅在真正进食（正在摄入营养）时触发 Eat 叫声 ／ 注文：默认关：整段进食行程（含食物路径）都算 Eat。
  5. 使用成瘾品
- 滑条行：**叫声间隔总控：0.11x** ／ 注文：1.0x 使用当前默认节奏。
- 区块标题（带计数）：**听取距离 · 激进 · 15–50**
- 行（选中态，带左金轨）：衰减预设：激进
- 页脚：版本 0.3.3+cd90a9e5 ／ 按钮：关闭

## 2. 对照表 · 卡片标签层

| # | US 键 | US 现译（EN） | US 现译（ZH） | SR 可见串 | 差异类型 | 本轮处置 |
|---|---|---|---|---|---|---|
| 1 | `US.Tuning.ScaleCooldown` | Keep squeak timing steady at high game speed | 高倍速时保持叫声节奏稳定 | 高倍速时保持叫声节奏稳定 | 用词不同 | **已同步**（原「按时间流速缩放冷却」） |
| 2 | `US.Tuning.ScaleTalking` | Adjust ordinary squeaks by the pawn's Talking ability | 按语言能力（Talking）调整普通叫声尝试 | 同左 | 用词不同（原把它描述成门控） | **已同步**（原「按交谈缩放频率」） |
| 3 | `US.Tuning.ScalePopulation` | Fewer periodic squeaks when many kin are in earshot | 可听区域鼠族较多时减少周期叫声 | 同左 | 用词不同 | **已同步**（原「按可闻人口缩放周期」） |
| 4 | `US.Tuning.EatPrecision` | Eat squeaks only while actually eating (ingesting nutrition) | 仅在真正进食（正在摄入营养）时触发 Eat 叫声 | 同左 | 用词不同 | **已同步**（原「仅在真正进食时发声」） |
| 5 | `US.Tuning.EatPrecision.IncludeDrugs` | Also for drugs | 使用成瘾品 | 同左 | 用词不同 + 与其他条目句形不一致 | **已同步**（原「服用成瘾品也算作进食」） |
| 6 | `US.Tuning.MinInterval` | Call interval master: {0}  ·  1.0x uses the default rhythm | 叫声间隔总控：{0}  ·  1.0x 使用当前默认节奏 | 叫声间隔总控：0.11x + 注文 | **缺注文 + 用词不同** | **已同步**（注文嵌入模板；`{0}` 位置不变） |
| 7 | `US.Tuning.CooldownMultiplier` | Cooldown multiplier | 冷却倍率 | 样本中不可见 | — | **已同步**（去掉冗余 Global；样本无对应行） |
| 8 | `US.Section.PlaybackBehaviour` | Squeak rules | 发声规则 | 发声规则 | **缺条目/用词不同** | **已改**（原「Playback behaviour / 播放行为」） |
| 9 | `US.Distance.Preset.Balanced` | Balanced | 均衡 | 激进（样本显示的是 Strong 档） | 样本只出现另一档 | **未动** |
| 10 | `US.Section.TriggerTiming` | Trigger timing | 触发节奏 | 样本中不可见 | — | **未动** |
| 11 | `US.Section.DistanceAttenuation` | Distance attenuation | 距离衰减 | 样本中不可见（区块标题是「听取距离 · …」） | 待判 | **未动** |
| 12 | `US.Nav.*` / `US.Page.*.Title` | Overview / VoicePack Routing … | 总览 / 语音包路由 … | 三个页签名与副标题 | SR 三页签 ≠ US 五工作区，不是同一层 | **未动**（属产品结构） |

## 2.5 ScaleTalking 的代码结论（lead 要求：UI 不得撒谎）

**结论：US 的实现是「按 Talking 能力缩放通过概率」，不是「正在交谈」的门控。样本措辞正确，本轮改文不变。**

代码证据（全部在 `Source/UniversalSqueaker/` 内）：

| 位置 | 代码 | 说明 |
|---|---|---|
| `CompSqueaker.cs:513-524` | `capability = SampleVocalCapability(); applyTalkingGate = ScaleFrequencyWithTalking && plan.Definition.VocalGatePolicy == ApplyTalkingGate; roll = capability.RequiresTalkingRoll(applyTalkingGate) ? Rand.Value : 0f; vocalDecision = capability.Decide(applyTalkingGate, roll);` 非 Allowed ⇒ 记 `VocalOrgansSilent` 或 `TalkingRejected` 并 **return（不播放）** | 决定性分支：一次**随机掷骰**决定这次尝试是否通过 |
| `Runtime/SqueakVocalCapability.cs:21-29` | `RequiresTalkingRoll = applyTalkingGate && VocalOrganEfficiency > 0.001f && TalkingChance < 0.999f`；`Decide: ... && !(roll < TalkingChance) ? TalkingRollRejected : Allowed` | 通过条件 = `roll < TalkingChance`；`TalkingChance` 就是能力值（0..1，被 clamp） |
| `CompSqueaker.cs:895-896` | `SampleVocalCapability() => new(GetVocalOrganEfficiency(), Pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Talking) ?? 1f)` | `TalkingChance` = **Talking 能力等级**，不是"是否正在说话" |

推论（可核）：能力 = 1.0（≥0.999）时**不掷骰、恒通过**；能力 0.5 时约一半通过；能力趋近 0 时几乎不通过。

**排除项**：全树没有任何"当前正在交谈"信号参与该判定——唯一相关的策略枚举是 `Pure/SqueakActionPlan.cs:7` 的
`SqueakVocalGatePolicy { ApplyTalkingGate, ExemptTalkingGate }`，它只决定**这条动作是否参与上面那次掷骰**，
不是"必须正在说话"。

**因此**：样本的「Talking 能力越低，普通叫声尝试通过的概率越低」与实现一致；而**旧 US 注文**把机制写成
「需在殖民者正在交谈时才可能通过」才是**对玩家的错误描述**（把概率机制说成了布尔门控）。本轮改文既是
对齐 SR，也是**纠正一处 UI 撒谎**。

**该需求从未存在**（维护者 2026-09-22 确认，lead 全工作区复核）：旧文案是**描述错误**——词组「交谈门控」
直接抄了代码枚举名（`SqueakVocalGatePolicy.ApplyTalkingGate` / `applyTalkingGate`），**把名字当成了行为**。
「需在殖民者正在交谈时才可能通过」全工作区只出现在 (a) 语言表及其 `dist` 副本、(b) 本侦察文档；
**设计文档 / spec / TODO / MEMORY / 任何需求记录里都没有**。⇒ **无待裁项，改文成立。**
若将来确有「仅交谈中发声」的需求，按**新功能**走（另开条目、另做行为设计），不是修文案。

> **教训（lead 记，写进本存档）**：**文档与代码冲突时：代码是事实、文档是缺陷 —— 除非维护者当场说不是。**
> 不许把一处文档缺陷升级成产品决策。（与「把外观差异读成能力差异」是同一类错：把**一个有缺陷的描述**
> 当成了**需求来源**。）

## 3. 对照表 · 帮助注文层（`US.Help.*`）

| # | 键 | 本轮是否跟随改 | 说明 |
|---|---|---|---|
| 13 | `US.Help.BasicTuning.ScaleCooldown.Label/.Text` | **是** | 标签 → 时间流速缩放；正文补「关闭则用原始刻冷却」，「squeak」统一为「叫声」 |
| 14 | `US.Help.BasicTuning.ScaleTalking.Label/.Text` | **是** | 标签 → 语言能力缩放；正文改为「按语言能力调整普通叫声，能力越低通过概率越低」（原描述的是门控） |
| 15 | `US.Help.BasicTuning.ScalePopulation.Label/.Text` | **是** | 标签 → 可听区域缩放；正文改为「可听区域内鼠族较多时减少」 |
| 16 | `US.Help.BasicTuning.EatPrecision.Label/.Text` | **是** | 首句改为样本的「默认关：整段进食行程都算 Eat 叫声」，交叉引用改为新标签「使用成瘾品」 |
| 17 | `US.Help.BasicTuning.EatPrecision.IncludeDrugs.Label` | **是**（标签） | 标签 → 使用成瘾品；正文未动（样本未给） |
| 18 | `US.Help.Timing.Interval.Label/.Text` | **是** | 标签 → 叫声间隔总控；正文补 1.0x 说明 |
| 19 | `US.Help.Timing.Multiplier.Label/.Text` | **未动** | 样本中不可见 |

## 4. 三个结构风险的处置

1. **标签/注文分键** ⇒ 本轮**成对改**：改标签的每一条，其 `US.Help.*` 注文同批更新（表 2/3 的 #13-#18）；`US.Help.Timing.Multiplier.*` 明确**豁免**（样本未给），不假装改过。
2. **中英形态不对位** ⇒ 本轮统一句形：5 条行标签中 4 条改「条件 + 动作」式句子（原 `Also for drugs` 是碎片）；`EatPrecision` 的中英都带「（正在摄入营养）」限定。
3. **`US.Distance.Status` 是模板 + host 两处** ⇒ 本轮**未动**（样本只显示另一档预设名），风险留在本节：说法若变要同时改**预设名键**、**状态模板**与 `AttenuationStatus` 的拼装顺序。

## 5. 本轮**未动**（等 SR 全量基准）

- `US.Page.*.Title/.Caption`、`US.Nav.*`（SR 三页签 ≠ US 五工作区，属产品结构不是文案）；
- `US.Section.TriggerTiming` / `US.Section.DistanceAttenuation`（样本不可见）；
- `US.Distance.Preset.*` 四档（样本只显示"激进"一档）；
- `US.Distance.Status` 模板、`US.Help.Attenuation.*`、`US.Help.Timing.Multiplier.*`；
- 页脚「版本 … / 关闭」（属 shell chrome）。

## 5.5 占位符同构确认（lead 要求一句确认）

`US.Tuning.MinInterval`：EN `Call interval master: {0}  ·  1.0x uses the default rhythm` 与 ZH
`叫声间隔总控：{0}  ·  1.0x 使用当前默认节奏` —— **各含且仅含一个 `{0}`，位置在句首冒号后**，与
`US.Tuning.GlobalVolume`（`{0}`）和 `US.Distance.Status`（`{0}` `{1}`）一样满足两表同构；
`UiSourceInvariantTests` 的本地化契约门（键集相同 + 占位符同构）**ALL GREEN** 已实测通过。

## 6. 本轮实测（玩家可见差异，全部【仍需实机】）

量测输出来自 `DeclarativeTimingLaneTests` / `DeclarativeOverviewLaneTests`：

| 卡 | 宽度 | 改前(EN) | 改后(EN) | 改后(ZH) |
|---|---|---|---|---|
| timing | 1024 | 154.67 | **176** | 154.67 |
| timing | 736 | 197.33 | 197.33 | 176 |
| timing | 480 | 197.33 | 197.33 | 176 |
| timing | 320 | 218.67 | **240** | 197.33 |
| basic-tuning | 1024 | 315.33 | 315.33 | 315.33 |
| basic-tuning | 736 | 315.33 | **336.67** | 315.33 |
| basic-tuning | 480 | 315.33 | **379.33** | 315.33 |
| basic-tuning | 320 | 400.67 | **422** | 379.33 |

（timing 的 caption 说明行在 1024 由 33.33 → 54.67，即多一行；英文比中文多一行处是「句子更长」，不是布局缺陷。）

## 7. 门链（本批）

`verify-local -NoRestore`：**15 门全部 OK，`[verify] all checks passed.`，exit 0**；载体
`0F95DF35…5A0B66`、mtime `2026-09-22 13:30:00`、249344 B、无 PDB，**跑前跑后 `sha moved=False mtime moved=False`**。
