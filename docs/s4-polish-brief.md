# S4-Polish 规划简报

> 用途：给另一个会话的 pro 模型做 **S4-Polish 规划**时使用，避免其全仓扫描。
> 状态：2026-08-28，仓库在 `0cfc248`，verify-local 13 门全绿。
> 本文件是规划入口，不是权威记忆；权威入口仍为 `HANDOFF.md` / `TODO.md` / `MEMORY.md`。

## 1. 项目现状

- Universal Squeaker 是 RimWorld 1.6 语音 mod，本地 fork，无 remote，不发布。
- UI 已完全收敛到 Ferrite 路径：Legacy UI 已删除，`UseFerriteUi` 开关已不存在。
- 运行时已实现 `(race, xeno)` 双键 context；调音数据单一权威为 `actionTuning` / `moodTuning`。
- 6-way review 已完成，报告在 `docs/review/`；Phase 0–6 修复已全部落地。
- 下一步大块是 **S4-Polish（纯视觉）**。

## 2. S4-Polish 范围（来自 TODO / HANDOFF）

1. **过滤**
   - 作者 / 种族 / 冲突过滤
   - 下拉快速过滤（只看已启用 / 只看冲突 / 只看 orphan 等）
2. **组件化帮助**
   - widget-attached help content
   - 就地呈现帮助，不点穿
3. **窄屏响应式**
   - 现有 mood 因子簇有 86px 最小宽度守卫，可作基线
   - 需要覆盖整个 Ferrite 页面
4. **视觉现代化**
   - 先出 `docs/ui-visual-modernization-zh.md` 评估稿
   - 再决定具体美化
5. **Footer build identity**
   - 左版本 / 右保存状态
6. **距离预览折线图**
   - 距离预设/距离范围的可视化预览

## 3. 相关架构触点

- `Source/UniversalSqueaker/UI/`：
  - `FerriteVoicePacksPage.cs`：页面主入口、view state 注入、命令收集
  - `VoicePacksPage.cs`：现在只是 Ferrite 薄入口
  - `Layout.xml`：Ferrite 布局清单
  - `UsWidgetRegistrar.cs`：widget 注册
  - `VoicePacksLayout.cs`：共享布局常量/测量
  - `Model/VoicePacksPageModel.cs`：唯一业务投影/命令执行
  - `Model/VoicePacksPageState.cs`：页面临时状态（含复合 `(race,xeno)` 选择）
  - `Model/VoicePacksViewState.cs`：只读视图 DTO
  - `Widgets/*`：ScopeTreeWidget、PresetListWidget、RaceLayerWidget、XenotypeLayerWidget、VoicePackChecklistWidget、PageTitleWidget、BasicTuningWidget、CameraIndicatorWidget 等
- `Source/FerriteLib.UiKit/`：通用 Ferrite 布局引擎/组件库，保持中性，不出现 US/SR 产品字面量。
- `Settings/UniversalSqueakerSettings.cs`：写桥、状态查询；S4-Polish 可能只读不改。

## 4. 与 S4-Polish 最相关的 review 发现

- `docs/review/review-05-ui-ferrite.md`：
  - UI 的 Xenotype 域选择/高亮已改为 `(race,xeno)` 复合键（Phase 1 已修），但仍需视觉上确认同 xeno 多 race 的可读性。
  - 空 catalog / 无 Biotech / 极窄窗口下的崩溃安全已有基础，但窄窗覆盖不完整。
  - `VoicePacksPageModel.BuildView` 有写回 state 的副作用，规划时可考虑是否纯化。
  - UI 逻辑缺少单元测试；S4-Polish 规划应包含“可测的纯投影/布局逻辑”提取。
  - Checklist 测量与绘制 metrics 不完全一致（`ITextMetrics` 混用），美化时建议统一。
- `docs/review/review-04-catalog-attach.md`：
  - 状态查询已按 `(race,xeno)` 过滤（Phase 1 已修），但 UI 对“无效 pack 被 catalog 丢弃”仍没有可视化提示；过滤功能可考虑把“无效/被拒 pack”作为一类展示。
- `docs/review/review-01-kernel-pure.md`：
  - 与 UI 关系不大，但距离预览图可能需要读取 `distanceRange` / `distancePreset`，确认数据来源为 settings 全局字段。

## 5. 已确定/不建议推翻的决策

- Ferrite 是唯一 UI 路径；不要重新引入 Legacy UI。
- `(race,xeno)` 是域身份唯一事实；UI 不得再退回 xenotype-only。
- `actionTuning` / `moodTuning` 是调音唯一权威；UI 不直接消费旧 `xenotypePresets.actionOverrides`。
- 不 ship 种子 Def；空 catalog 是合法状态，UI 必须优雅空态。
- HAR reflection 仍 deferred；UI 不投影 HAR hint 行。
- 无 remote、不 push、不发布；规划不涉及发布流程。
- 不修改 `MEMORY.md` / `HANDOFF.md` / `TODO.md` / `AGENTS.md` / `OBLIVIONIS.md`；pro 会话只输出规划稿。

## 6. 建议 pro 重点回答的问题

1. S4-Polish 六项的依赖顺序是什么？哪些可以并行，哪些必须串行？
2. 过滤系统应该放在 `VoicePacksPageModel` 投影层还是 widget 层？如何保持可测试？
3. 组件化帮助的数据模型怎么设计：帮助内容放 view state、widget 元数据，还是独立帮助表？
4. 窄屏响应式的统一策略：最小宽度、滚动、隐藏/折叠哪些区块？
5. 视觉现代化评估稿应该先覆盖哪些页面/组件？如何避免与 FerriteLib.UiKit 中性边界冲突？
6. Footer build identity 需要哪些数据（版本、保存状态、dirty 标记）？现有 settings 是否有这些状态？
7. 距离预览折线图的数据输入是什么？纯函数画图还是 Verse 绘制？是否需要 kernel/pure 测试？
8. 每个子任务的验收标准是什么？如何用现有 verify-local / 新增纯逻辑测试锁住？

## 7. 建议输出物

一份 `docs/s4-polish-plan-zh.md`（或直接更新 `docs/us-ui-migration-plan-zh.md` 的 S4 段落），包含：

- 目标与范围
- 拆解顺序（P1、P2…）
- 每个子任务的文件范围
- 验收标准
- 风险与 maintainer 决策项
- 预计改动面
- 测试策略

## 8. 给 pro 会话的阅读清单（按优先级）

1. `HANDOFF.md`
2. `TODO.md`
3. 本简报 `docs/s4-polish-brief.md`
4. `docs/review/review-05-ui-ferrite.md`
5. `docs/ui-phase3-implementation-notes-zh.md`
6. `docs/us-xeno-double-key-context-zh.md`
7. `docs/us-ui-migration-plan-zh.md` 中 S4-Polish 相关段落

不建议 pro 全仓扫描；如需要确认某个具体 UI 文件，再按路径定点阅读。
