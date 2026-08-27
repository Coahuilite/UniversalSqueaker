# US 路由表 + baseline 机制设计文档

> 状态：设计定案整理（2026-08-25 会话收敛）。
> 范围：US 以「路由表（声明式 comp 挂载）」替代「语音包自带 XML patch」，与「baseline（调音基线 Def）」机制的关系、现状差距、端态音频包形态与架构心智模型。
> 配套文档：`us-ui-migration-plan-zh.md`（迁移计划）、`us-ui-migration-k3-review-zh.md`（K3 评估）、`mod-structure-reference-zh.md`。
> 前提：K3 评估已确认路由表方向与计划 §5 分层表同源、数据面大体就绪，两大硬前置是种族层数据缺失 + 覆盖语义冲突（见 `us-ui-migration-k3-review-zh.md` §四）。

## 1. 两个被混为一谈的「路由」

US 的路由实际是两件事，本文档只讨论第二件：

| 概念 | 职责 | 现状 |
|---|---|---|
| **声音路由** | 选哪段声音放（动作 → 域池 → 选择器 → 门控 → 播放） | ✅ 已 100% 数据驱动、功能完备，无任何 patch |
| **comp 挂载** | 种族 pawn 身上先有发声触发器（`CompProperties_Squeaker`），否则整条链不被调用 | ⚠️ 目前仍需作者自带 XML patch（唯一的必写 patch） |

「路由表机制」= 第二件事的自动化：语音包已声明 `raceDefName`，这份声明本身就是路由表；缺的只是一个「读表人」——启动时收集所有已装包声明的种族，逐个补挂默认 comp。

## 2. 现状：读表人已存在，但被 legacy 门控

`Legacy/LegacyAutoCompAttach.cs` 已实现完整逻辑，但只服务旧 SR 包：

```
遍历 catalog.PackByKey.Values
  → 仅收集 IsLegacy(pack) 且 raceDefName 非空的声明（:24 门控）
  → 对每个种族：DefDatabase<ThingDef>.GetNamedSilentFail
  → def.race == null → 静默跳过（:30，无诊断）
  → comps.Any(c => c is CompProperties_Squeaker) → 跳过（尊重已有配置）
  → 否则 Add(CompProperties_Squeaker.CreateDefault()) + usdiag 日志
```

- 调用点：`Mod.cs:69`，在 `LongEventHandler.ExecuteWhenFinished` 内首拍执行——Defs 全量加载、XML patch 已应用之后，pawn 生成之前。时序已被 legacy 路径实机验证。
- 默认计划：`CreateDefault()`（`CompSqueaker.cs:1036-1066`）= 15 个生产动作 + 4 档心情，无种族/声音字面量。
- 日志事件为 legacy 语义：`voicepack.comp.legacy_auto_attached` / `..._failed`（`SqueakLogProtocol.cs:134-135`），usdiag v1 协议逐字节冻结于 `tools/UniversalSqueakerLogTests`（`Program.cs:224-228` + `CaptureV2Coverage()`）。

## 3. 端态设计：开放读表人

### 3.1 机制变化（行为增量）

1. **放开门控**：遍历全部已准入包的 `raceDefName`（Race 包与 Xenotype 包都声明此字段），不再限制 `IsLegacy`。
2. **补诊断**：声明未命中（种族不存在 / `def.race == null`）时打一条 usdiag 告警（如 `voicepack.comp.attach_skipped` + 原因），替代静默 continue。
3. **中性化观测**：新增通用成功/失败事件（如 `voicepack.comp.auto_attached` / `..._failed`），legacy 包继续走旧事件保兼容；新事件必须同步 `SqueakLogProtocol.cs` + LogTests（协议只加不改）。
4. **归属更名**：类移出 `Legacy/`、更名（如 `VoicePackCompAttach`），注释同步。
5. **保留逃生舱**：`Any()` 去重 = 作者自带 patch 的自定义配置优先，auto-attach 让位。patch 从「必写」降级为「可选高级用法」。

### 3.2 边界规则（固化为契约）

- 挂载集合 = 所有已准入包声明的 race 并集（含 Xenotype 包声明的 race；含玩家未勾选的包——comp 存在 ≠ 有声音，池空即无声，与 patch 行为一致，非回归）。
- 异种声明（`targetDefName`）**不参与挂载**：XenotypeDef 不是可挂组件的 ThingDef；异种在运行时作为身份第二维（pawn 基因 → 域键）进入查询。
- 卸载安全不变：运行时改 `def.comps` 不落盘，重启自然消失。

### 3.3 与现状实现的具体差距

| # | 差距 | 规模 |
|---|---|---|
| 1 | 门控放开（`:24` 删 `IsLegacy` 条件） | ~1 行 |
| 2 | 未命中诊断（`:30` 补告警） | ~5 行 |
| 3 | 新日志事件 ×2（协议 + facade） | ~15 行 |
| 4 | LogTests 同步（协议冻结约束） | 数条断言 |
| 5 | 类迁移改名 + 注释 | 移动一个文件 |
| 6 | skill 正本同步（§0/§3.5/§9/§10/§11） | 文档改稿 |

净效果：canonical 作者删掉唯一必写 patch；行为面新增仅「canonical 包声明即挂载 + 未命中告警」。

## 4. baseline 机制（已落地，与路由表分层不冲突）

### 4.1 现状（代码证据）

- **Def 形态**：`Runtime/UniversalSqueakerTuningBaselineDef.cs` —— 只读 `Def`，含 `actions`（actionKey + scope + intervalMultiplier + probabilityMultiplier）与 `moods`（mood + pitchFactor + volumeFactor + pitchJitter）。不写回、不进存档；空源数据合法（回退内置默认）；无产品字面量、无种族种子，**第三方可发布自己的 baseline Def 覆盖默认**。
- **合并表**：`Runtime/BaselineTuningTable.cs` —— `Rebuild()` 全量重扫 `DefDatabase<UniversalSqueakerTuningBaselineDef>.AllDefs`，last-wins（后加载 Def 覆盖先加载，Def 内后条目覆盖前条目）；每次查找重扫，DefDatabase 生命周期（重载、第三方注入）不会留下陈旧状态。
- **与 ActionTuningRecord 的关系**：`ActionTuningRecord`（`Models/ActionTuningRecord.cs`，S2 已落地）是**玩家 override** 的 Scribe 记录面（Global/Race/Xenotype 三态分层，`hasX=false` 向下继承）；baseline Def 是**作者/第三方声明**的只读基线。运行时叠加顺序：`baseline Def → 玩家 override 逐字段覆盖 → 生效`。

### 4.2 与路由表的分层（互不读写对方数据）

| 层 | 机制 | 管什么 | 数据位置 |
|---|---|---|---|
| 第 1 层 触发器存在性 | 路由表自动挂载 | 能不能响 | 种族 ThingDef.comps（内存态，不落盘） |
| 第 2 层 触发计划 | comp 自带默认（CreateDefault / 作者 patch 可选自定义） | 哪些动作触发、基础参数 | 同上 |
| 第 3 层 运行时调音 | baseline Def + 玩家分层 override（ActionTuningRecord） | 基础节奏上的乘数与开关 | DefDatabase + 设置存档 |

- 路由表只碰第 1 层，塞的是无调音字面量的默认计划；
- baseline 只碰第 3 层，在解析器做字段级叠加，从不改 comp；
- 唯一交互是良性设计：作者要高度自定义触发配置可继续自带 patch，读表人见到已有 comp 即跳过。

比喻：路由表负责「给每个房间装上门铃」，baseline 负责「按铃的音量规则」。装门铃不关心音量规则，调音量不关心门铃是谁装的。

## 5. 端态语音包形态：纯数据模组，不是空模组

### 5.1 关键区分：patch ≠ Def ≠ 资产

- **patch** = 命令式修改别人的定义 → 路由表机制消灭；
- **Def** = 声明式数据，游戏原生加载 → 必须保留（这是包的「身体」）；
- **资产** = 音频文件（.ogg），非 Def、文件夹约定寻址 → 本来就在 Def 体系之外，不变。

### 5.2 端态包构成

| 构成 | 端态 | 原因 |
|---|---|---|
| comp 挂载 patch | ❌ 删除 | 由路由表自动挂载替代 |
| About.xml / LoadFolders.xml | ✅ 保留 | 元数据；Biotech 门控靠 `IfModActive` |
| SqueakVoicePackDef（PackDef） | ✅ 保留 | 路由表本身：raceDefName / scope / 动作→声音映射 |
| SoundDef XML | ✅ 保留 | 播放走 Verse SoundDef 系统（键背后必须是真实注册的 SoundDef） |
| 音频文件（.ogg） | ✅ 保留 | 裸资产，文件夹寻址 |
| UniversalSqueakerTuningBaselineDef | ➕ 可选 | baseline 机制本身是 Def，作者想自定义调音基线就带一份 |

端态包对游戏是**最规矩的模组：只声明、不修改别人**（零 patch 的纯数据模组），不是空模组。

### 5.3 为什么「内容由 US 自己读取」是坏方向

- 重造原版加载管线：继承合并、红字报错、IfModActive 门控、版本目录、loadAfter 排序——全部要重写；
- 脱离诊断生态：现双保险（原版红字 + usdiag）会退化为「作者成瞎子」；
- 违反项目红线（AGENTS.md）：内核零产品字面量、内容与内核分离——US 的 C# 去认包目录结构，等于把内容契约从「Def 类型」降级成「文件夹约定」；
- 兼容面收窄：第三方工具/翻译/冲突检测只认标准 Def。

US 对包内容的「获取」已实现完毕，通道就是 DefDatabase（catalog 刷新遍历已加载 Def），不需要也不会去自己读文件。

## 6. 架构心智模型（从哪来 → 我是谁 → 到哪去）

```
从哪来                我是谁                  到哪去
TriggerBinding   →   PawnIdentity       →   Registry.Select
触发源识别+采样       pawn→race/xeno域身份    域池→声音选择→播放
(代码绑定,与路由表     (运行时从pawn采样:     (路由表填候选集,
 无关,不可被包改变)     defName+异种基因)       选择器+门控定最终)
```

- **路由表**回答「这个种族有什么可响的」——决定每个身份域的池子内容（候选集），不负责从哪来；
- **身份采样**回答「这个 pawn 是谁」——`pawn.def.defName` + `genes.Xenotype` 构成域键；
- **门控**（ISoundGate：冷却/距离/可见性）不处理身份，是「到哪去」的末道放行闸；
- **自动挂载**= 种族级一次挂载（ThingDef），pawn 生成时由 RimWorld 组件系统自动继承，US 不碰单个 pawn。

## 7. 开放问题与决策待办

- [ ] 新日志事件名与 message 定稿（`voicepack.comp.auto_attached` / `attach_skipped` 待确认），同步 LogTests。
- [ ] 类迁移目标位置与命名（建议 `Source/UniversalSqueaker/Catalog/` 或 `Runtime/` 下 `VoicePackCompAttach`）。
- [ ] skill 正本 §0/§3.5/§9/§10/§11 同步改稿（实施时一并落地）。
- [ ] 是否提取「收集目标种族集合」为可测纯函数（现状 attach 逻辑无测试、耦合 Verse）。
- [ ] 与 K3 评估的两大硬前置（种族层数据缺失 H3、覆盖语义冲突 H1）的关系：本机制不引入新冲突，但落地顺序应遵守 `us-ui-migration-k3-review-zh.md` §四 B5「先数据后 UI」。

## 8. 决策记录

| 日期 | 决策 | 依据 |
|---|---|---|
| 2026-08-25 | 路由表机制 = 开放现有 LegacyAutoCompAttach 给全部包 + 补诊断 + 中性化事件，不新写机制 | §2/§3 |
| 2026-08-25 | 与 baseline 分层不冲突：触发器存在性 / 触发计划 / 运行时调音三层，零数据交集 | §4.2 |
| 2026-08-25 | 端态包为纯数据模组（零 patch），不采纳「US 自读包内容」 | §5.3 |
