# US UI 迁移计划与树形作用域开关 — Kimi-K3 评估总结

> 日期:2026-08-25
> 评估模型:CommandCode Kimi-K3(`commandcode/moonshotai/Kimi-K3`),经临时 subagent 派发,实际运行 10m2s
> 输入:`docs/us-ui-migration-plan-zh.md` + 仓库勘察归档 + 玩家提出的"树形动作作用域开关"特征构思
> 依据:K3 逐条对照仓库代码(含行号)独立复验;本文件忠实转述其结论,证据标点保留

---

## 一、迁移计划归纳(来源:us-ui-migration-plan-zh.md)

- **目标**:US 从 SR 设置 UI 迁到 FerriteLib.UiKit 新调度器;含四态模式重整、孤儿功能接回 UI、动作入口 wrapper 化、CompProperties 作者 ABI 与全局调音拆分。
- **核心方案**:
  - 四态 `Disabled / Vanilla / Fallback / Remix` + Scribe 迁移;
  - 调音三层:作者 baseline(独立只读 Def)→ 玩家 override(G→R→X 分层表)→ 运行时;
  - 动作作用域方案 A:`ActionTuningRecord` 分层表替换 `globalActionEnabled` + `xenotypePresets.actionOverrides`,`X > R > G > Default`;
  - wrapper 化:`ActionEntry` + `TriggerBinding`,`SqueakAction` 枚举 → `ActionKey` 字符串,`Disabled` = 采样层短路;
  - `globalMinIntervalTicks` / `scaleFrequencyWithTalking` / 距离预设迁出作者 patch 面到 US 全局设置;
  - 实施顺序 S1–S5。
- **隐式依赖**:非 1.0.0 允许破坏 Scribe;Kernel/Pure 与外置适配层分界可容 wrapper 化;UiKit two-pass + 注册表可承载新 UI;`SqueakSettingsMigration` 原子事务模式可复用;usdiag 协议 + LogTests 冻结契约可扩展 `audio.disabled`。

## 二、合规判定(K3 逐条复验,证据为代码行号)

| 约束 | 判定 | 证据 |
|---|---|---|
| Kernel 零 Verse/Unity/RimWorld | ✅ 符合 | Kernel/Pure grep `using (Verse\|UnityEngine\|RimWorld)` 零匹配;计划 §13.5 外置分配正确 |
| 中性 UI 库无 US/SR 字面量 | ✅ 符合 | FerriteLib.UiKit grep `US`/`SR_`/`usdiag` 零匹配;ui-shared-library-design §3 红线 |
| 卸载安全(不写存档) | ✅ 符合 | 无 MapComponent/GameComponent;SqueakFallbackProfileStore.cs:44 注 no save-game data;Disabled 不改 ThingDef.comps |
| 内容/内核分离 | ✅ 符合 | §4.1 baseline 独立 Def,不进 CompProperties/VoicePackDef |
| 可选依赖仅反射 | ✅ 符合 | BiotechActive 运行时判定 SqueakRuntimeResolver.cs:228;HAR 反射发现 |
| A4 三缩放开关行号 | ✅ 符合 | CompSqueaker.cs:147-149 |
| A2 moodOverrides 消费点 | ✅ 符合 | CompSqueaker.cs:561-573 `ResolveMoodMod` 三层合并 |
| A6 GlobalActionPolicy | ✅ 符合 | SqueakGlobalActionPolicy.cs:19/30;`TryTrigger:396` 早退 |
| §11 CompProperties 五字段 | ✅ 符合 | CompSqueaker.cs:898-903;`CreateDefault()`:916-954 |
| **H1 G 早退压死 X 开** | ⚠️ **冲突(计划已自识别,未落地)** | `TryTrigger:396-397` 全局 Disabled 直接 return;BuildContext `global.Enabled && xeno.Enabled`(SqueakRuntimeResolver.cs:181)是"与"非"覆盖" |
| **H3 Race 层无消费点** | ⚠️ **冲突(计划已自识别)** | ResolveContext:226-239 仅 xenotype/global,无 race context(声音池 Choose 有 race,行为/心情 context 无) |
| H4 定长数组 + 闭合枚举 | ✅ 符合(风险属实) | CompSqueaker.cs:158-160,171-172 五个定长数组;SqueakTriggerOrigin 11 值闭合(Pure/SqueakActionPlan.cs:44) |
| H5 Sustained 死模式 | ✅ 符合 | CompTick:289-290 break;validator SqueakVoicePackModels.cs:105 拒 sustain;无 TrySpawnSustainer |
| M2 usdiag 协议冻结 | ✅ 符合 | SqueakLogProtocol.cs + tools/LogTests |
| C2 FallbackProfileStore 活无 UI | ✅ 符合 | store 完整;ui-phase3 §3 UI 不引用 |
| globalMinIntervalTicks 默认 216 | ✅ 符合 | CompSqueaker.cs:900 |
| catalog Race 域已支持 | ✅ 符合 | RaceDefNames/RacePacks SqueakXenotypeCatalog.cs:59-63,134,197 |
| Disabled 短路在派发日志前 | ⚠️ 缺失信息 | CompSqueaker 无 `voicePackMode==Disabled` 前置;NormalizeMode:207 会归一为 Off;M1 已识别但无代码级锚点[推断可实现] |
| US.TuningFeedback.v1 导入导出 | ⚠️ 缺失信息 | 全仓库无该契约,§4.1 仅建议[缺失] |
| TuningBaselineDef | ⚠️ 缺失信息 | 仓库无此 Def,§4.1 建议新增,字段级合并规则未定[缺失] |

## 三、总体评价(A)

- **强项**:事实基线扎实(引用行号逐条核验属实,是勘察后写的非凭空设计);自我审查到位(§14 H1-H5/M1-M6 条条对应真实代码冲突,K3 独立复验 H1/H3/H4/H5 一致);合规方向全对,无新增违规项;原子迁移事务可直接复用,降 H2 风险。
- **风险点**:
  - **H1 是根本性语义冲突**(与合并 vs 覆盖),须改 `TryTrigger` + `BuildContext` + resolver,非加 UI 能解;
  - H4 字符串化波及面大,排期耦合 S2 高风险;
  - 多个关键契约仅占位名词(TuningFeedback.v1 / TuningBaselineDef / audio.disabled),S4 硬前置缺 schema。
- **缺口**:Scribe 迁移无映射表/版本号/fixture 清单;TuningBaselineDef 与 XenotypePresetRecord 字段级合并规则未定义;Race 层 context 数据结构未设计。
- **可执行性**:中等偏上。S1/S5 可直接做;S2 高耦合高风险须先补迁移 + inventory;约 30% 关键数据契约仍是占位,需先补设计。

## 四、树形动作作用域开关评估(B)

**特征构思**:最顶为全局树(统一开关);展开每个动作后按种族列出;每个种族展开后有"种族级全局开关"(可覆盖全局);每个种族展开后按该种族异种排列;每个异种展开后为最底层开关(可覆盖种族与全局)。

### B1 数据模型对应

| 层级 | 现状 |
|---|---|
| 全局层 | ✅ 有对应:`globalActionEnabled` + `GlobalActionPolicy` |
| 种族层 | ❌ **缺失**:`globalActionEnabled` 无 race 键,`xenotypePresets` 无 race 键 |
| 异种层 | ✅ 有对应:`xenotypePresets.actionOverrides`(仅 enabled,无 scope) |
| 覆盖语义 X>R>G | ⚠️ **冲突**:当前 `global && xeno` 是"与"关系,且 Global Disabled 早退先于一切 = **H1** |
| globalMinIntervalTicks 等 | 正交(管"响不响"vs"怎么响"),计划 §11 已迁出 |

### B2 Scribe 与覆盖

- **落点**:`ActionTuningRecord` 可承载三级;`hasX` 字段级 presence 表继承,避免不可逆覆盖;`Scribe_Collections.Look(Deep)`。
- **冲突 1**:`SqueakGlobalActionPolicy` 全局 Disabled 早退先于分层求值,须降级/删除,改由分层快照统一驱动(H1)。
- **冲突 2**:删 `globalActionEnabled` + `actionOverrides` 会动 Scribe 形状,须一次性迁入避免两轮迁移(M3);原子事务 + schema 升级。
- **不可逆**:单向迁移不可逆(写后旧版读不回),计划已接受;`hasX=false` = 继承可回落,无锁住回不去;[推断] UI 需显式提供"重置为继承"。

### B3 预留结构

- **已预留**:数据模型(计划 §5 分层表)+ 运行时改造方向(H1 求值 / H3 补 Race context)。
- **未预留**:树形 UI 具体结构(现有是扁平行,无树形缩进/展开/三态 widget);Race 层 context 数据结构设计。
- **缺口改动文件**:
  1. Models/ 新增 `ActionTuningRecord` + Settings 字段 + ExposeData 序列化迁移;
  2. `SqueakSettingsMigration.cs` 迁移两处到分层表;
  3. `SqueakRuntimeResolver.cs` 引入 Race context + X→R→G 求值;
  4. `SqueakGlobalActionPolicy.cs` + `CompSqueaker.TryTrigger:396` 降级早退;
  5. FerriteLib.UiKit/Widgets 新 `TreeRowWidget` + US 侧 PageModel 投影三级域。

### B4 UI 契合度

- **契合**:IWidget Measure 返高 + Draw 进 rect,树形 = 一个 `TreeRowWidget` 按展开态返"行高 × 可见行数",契合 two-pass;命令走中立 UiCommand 不污染库;单向数据流已建。
- **性能**:全展开约 `17 × (1 + R + R·X)` 行(R=3/X=5 约 323 行)[推断];IMGUI 可承受,但须折叠懒渲染 + 滚动(LayoutEngine 无虚拟化)。
- **复杂度**:三态勾选 + 缩进 + 展开态持久化,比扁平 checklist 高一档。
- **取舍**:默认折叠只显 17 动作 + 全局开关,按需展开;展开态存 pageState,不写 Scribe。

### B5 明确结论

- **是否值得做**:**值得**,但应作为 §5 分层表的 UI 呈现层随 S4 一起做,不作独立前置。
- **落地路径(先数据后 UI)**:
  1. 先落 `ActionTuningRecord` + Race context + 改 `TryTrigger` 求值(解 H1/H3),再谈树形;
  2. 迁移一次性:`globalActionEnabled` + `actionOverrides` 一次迁入(遵 M3)+ 原子事务 + schema 升级;
  3. 树形 = Ferrite 新 `TreeRowWidget`(三态 + 缩进 + 折叠),默认折叠 + 滚动;
  4. 提供"重置为继承",避免不可逆覆盖体感。
- **风险**:
  - 高:H1 语义改造是运行时核心,须配"全局关 + Xeno 开"测试;
  - 中:字符串化 H4 与分层表同批改动面叠加,遵 §14.3 顺序一次成型;
  - 中:Scribe 单向迁移不可逆,靠 fixture + 备份;
  - 低:UI 行数性能,折叠懒渲染可控。
- **一句话**:方向正确、与计划 §5 同源、数据面大体就绪;但**种族层数据缺失 + 覆盖语义冲突是两大硬前置**——先改对分层数据模型和 resolver 求值,树形 UI 顺 S4 自然落地,而非提前单做。

---

## 附:本次评估派发方式(备查)

- 临时 agent 定义经 frontmatter `model:` 显式指定,未走 slow 角色别名:
  - 仓库勘察:`commandcode/deepseek-v4-flash:low`(只读,产出归档,4m30s);
  - 评估:`commandcode/moonshotai/Kimi-K3`(10m2s)。
- 两次派发均只用 commandcode provider;任务结束后两个临时 agent 文件已删除。
- 本总结对应的一次性评估;后续若计划迭代,建议重跑 K3 复核或更新本文档。
