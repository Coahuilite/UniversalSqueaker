# TODO

## Rebuild plan (approved 2026-08-23) — completed

All phases completed. See `OBLIVIONIS.md` for the pre-rebuild baseline.

## Next development plan — UI migration + orphan features (decided 2026-08-24)

Full plan: `docs/us-ui-migration-plan-zh.md`.

### Completed (2026-08-24 through 2026-08-25c)

- S0 — 5 design docs
- S1 — global-layer removal
- S2a — mode enum Off→Vanilla + Disabled
- S2b — action scope field-level last-wins + ActionTuningRecord layered table + H3 Race-layer contexts
- S3 — Sustainer path + external-action end-to-end
- S4-Tuning-Backend — baseline Def + consumer wiring
- S4-Orphan-Sync — Ferrite path for A7/A3/A4
- S4-Scope-Tree — A6 per-action scope switch (Global layer)
- S4-Diag-Foundation — camera indicator + DebugAction entry
- S4-Diag-Panel — diagnostics panel (overlay + draggable window + PostDraw)
- S5 — delete dead code (SqueakGlobalActionPolicy, experimentalRaceAllowlist, localization patch, developerToolsEnabled)
- Goal A — ActionEntry/TriggerBinding wrapper + external-action firing
- **双源统一** — delete globalActionEnabled/GlobalActionEnabledRecord/GetActionGlobalScope/SetActionGlobalScope
- **voicePackDefaultSeeded** — delete Scribe sentinel
- **dist/ 残留** — delete stale comp fields from test pack XMLs (working-tree only, dist/ is gitignored)
- **Legacy 兼容桥删除** — delete entire Legacy/ directory + 11 file references + 3 legacy log events + UI Legacy SR tags
- **Baseline 预设系统** — tree-shaped Def + BaselinePresetImporter + sourcePresetDefName + PresetListWidget
- **路由表开放** — VoicePackCompAttach canonical auto-attach + 3 new v2 events + LogTests registry 6→9 + SKILL sync (456b2a4)
- **分层心情调音（B1）** — MoodTuningRecord three-layer table + schema 5 transactional migration + runtime fold + importer decision flip (81e7bb9)
- **三层调音编辑器 UI（B3，选项①）** — layer segment + domain picker + scope ring + mood rows; SetTuningLayer/SetTuningDomain/SetMoodTuning (2ce5dd9)
- **专项测试（B4）** — Pure/SqueakLayeredTuning + 13 kernel assertions (43835ea)
- **三 reviewer 审查修复** — 8 fixes across UI/data/fold (f4d8448)
- **双键 context（2026-08-28）** — `AudioDomains` 域键工具 + Pure 聚合器/选择器 + resolver 薄适配层 + kernel 测试（设计稿 `docs/us-xeno-double-key-context-zh.md`）
- **S4-Polish（2026-08-30）** — S4-Vol/S4-Nav/P1–P7 全部完成并验收
- **FerriteLib.UiKit B1–B4（2026-08-30）** — `UiInteract` 分层输入路由、`UiValueStore`、`input/number-slider`
- **US UI 大修 U0–U6（2026-08-30）** — `UiGuard` 公开/删 `UsGuard`/`FerriteGuard`、交互迁移、值控件、布局、Fallback、Skin、测试门禁；见 `docs/workdocs/us-ui-overhaul-*`
- **dist voicepack 整理（2026-08-30）** — `dist/voicepacks/final-test/` 三包独立，`archive/` 归档；Ratkin 包重命名/清 legacy
- **全屏自绘设置窗口 + 单页分区 + 响应式多栏（2026-08-30）** — `UniversalSqueakerSettingsWindow` 自绘背景（`doWindowBackground=false`），窗口改为居中 60% 并保留边缘安全距离；`WindowStack.Add` 重定向 US 的 `Dialog_ModSettings` 入口；**三页签改为单页分区 + 左侧分组导航，子项点击滚动定位到对应区块**；宽屏下全页响应式多栏（Layout.xml `Column` + 多 LayoutEngine 并排）；启动提示改 `SilentInput` 消除 `Message_NeutralEvent` 红字；**修复根因：`VoicePacksPage.BeginSession` 每帧调用 `VanillaVoicePacksPage.ResetSession()` 导致 `State` 每帧重置，导航切页永远被清回 Basic**
- **现代 UI 重构（2026-08-30，`514efb2`）** — UiKit 新增 `UiPanel`/`UiText` 中性原语与 `Palette` 深色+金色 token；US 新增 `UsCard` 卡片外壳并覆盖主要设置区块；设置窗口按维护者安全区在 60%～75% 之间浮动（默认 72%×66%）；左侧导航改现代侧边栏（品牌区/hover/active/金色 accent）；verify-local 14 门全绿
- **Camera+ 参考右侧帮助面板（2026-08-30，`b86fc97`）** — 参考 Camera+ 设置界面，宽屏时在内容右侧显示当前区块帮助面板；`UsHelpPanel` 纯展示；窄屏自动隐藏；verify-local 14 门全绿

### Review-fix phases (2026-08-28) — completed

- **Phase 0** — Legacy UI 删除（`UseFerriteUi` 开关与旧绘制路径移除）
- **Phase 1** — 全链路统一 `(race,xeno)` 域身份（Settings/UI/Baseline/Notification/resolver）
- **Phase 2** — 调音单一权威 + 统一 upsert/clear（停用 `xenotypePresets.actionOverrides`）
- **Phase 3** — Settings 迁移事务化 + `tools/UniversalSqueakerSettingsMigrationTests`
- **Phase 4** — YAGNI 删除 ActionEntry/TriggerBinding；Sustained 触发/播放管线统一
- **Phase 5** — Kernel 变体混抽/corpus/PackFallback 边界 + 健壮性
- **Phase 6** — 诊断/日志/fail-closed 修复

### Remaining (next goal)

- **UI 实施计划 Phase 0–2（2026-08-30 完成）** — 内联 `?` 已移除；右侧帮助始终保留（800×600 三栏）；功能块独立滚动 + 包过滤器归位；Tuning Editor 下拉 + Mood stepper-slider；衰减图改为 UiKit `chart/line`；UiKit 容器化/控件基础设施已落地；`docs/workdocs/` 已移除。
- **独立 review agent 审查** — ✅ 已完成并修复（B1/M1–M4）。
- **反馈汇总与全局风险** — ✅ `docs/ui-feedback-consolidated-zh.md`。
- **修复前排查工序** — ✅ `docs/ui-fix-triage-report.md` 已生成；批次 A–D 已调度完成。
- **UI 反馈修复批次 A–D（2026-08-30 调度完成）** — G2/G3（交互通路+下拉定位）、G1（文本高度）、G4（原版/SR 视觉统一）、M1/M2+低风险全部落地；`scripts/verify-local.ps1` 14 门全绿。任务书：`docs/ui-fix-task-books-zh.md`；行业调研：`docs/ui-ux-industry-review-zh.md`。
- **Camera+ 分层帮助 + 标题去重（2026-08-30 完成）** — 右侧帮助升级为分区总览+单项列表+悬停/选中联动高亮；左侧导航品牌区隐藏；Global Volume 隐藏体内重复标签；`UsCard` 新增 `TitleHidden` 能力。计划：`docs/ui-camera-help-and-title-hide-plan-zh.md`；`verify-local.ps1` 14 门全绿。
- **UI 首次打开故障排查（已闭环）** — null 来源防御、完整堆栈日志、`ResetSessionLog` 刷屏修复、首次失败自动重试已落地；Gate R 实机确认新 Kernel Settings Host 首开与关闭重开无异常。
- **Gate U 游戏内稳定化（当前 maintainer step）** — 集中验证 800×600/1280×720/1920×1080、Tuning/Packs/Camera Indicator、popup/chart/hotControl、真实数据/翻译与受控 fallback。
- **Backlog** — OB-02 `UiLayoutTier.ClampWidth` 接入、OB-03 加载 clamp 测试、OB-04 `VoicePacksLayout` 测试（可在游戏内验证后处理）。
- **（可选尾部）Runtime harness** — adapter fold/converters/BuildFallback mode pass-through untested by kernel gate (ReviewResolverFold P3 residual).

## UiKit / US 设置 UI 重建计划（2026-08-31，Gate U 自动部分成立）

- 已完成调查：UiKit 源码、测试 harness、US UI 调用链与依赖切割审计。
- 已冻结行为不变量：IMGUI 原生事件唯一权威；XML 只负责结构/布局/静态属性/翻译键/有限响应式；typed binding；Window/Overlay 独立 session；Dark Gold theme-ready。
- 权威入口：`docs/uikit-rebuild/README.md`、`07-rebuild-reset-and-execution-contract-zh.md`、`tasks/MAIN-ORCHESTRATOR.md`。
- Gate R：`PASS`。自动证据与维护者实机验证均完成；首开、Kernel banner、global-volume 操作、关闭/重开及其余场景无异常。
- Gate U 自动证据：`PASS（现有 harness/源码范围）`。真实嵌入 Schema2 Host、17 个 US Kind、typed binding/action、Host session 隔离、五工作区×三视口、scope cleanup、disposed session 和创建期失败均通过；主代理独立复现 `verify-local.ps1 -NoRestore` 15 门全绿与 `build-dev.ps1` 0 warning/0 error。
  - **修正（2026-09-04）**：当时确为 15 门，此数已随 UI 库拆出而失效。库的门禁迁到 `../ferritelib` 从内部自证，US 侧现为 **13 门**；旧"门 14 本地化契约""门 15 文本适配"不再是独立门，现为门 12 / 门 13 内部的断言。历史条目按原样保留，口径以 `HANDOFF.md` §5 为准。
- DeepSeek UI 收口：`PASS（自动/源码范围）`。800 宽 Mood 已改为 stacked compact 布局，Pitch/Volume/Jitter 各保留 minus/slider/number/plus，Auto 独立；`MoodLayoutFocusedTests` 验证 2 行共 26 个控件在 card 内且不重叠，并覆盖全部 typed `set-mood-tuning` 交互。
- Gate U Basic 实机：`PASS`。设置页正常开启、global volume 正常修改、attenuation graph 可拖动、关闭重开正常、无红字。
- Gate U 整体：`LIMITED/未通过`。下一步仅为维护者实机验证 Tuning、Packs、Camera Indicator、真实数据/翻译、popup/chart/hotControl、fallback 与 800×600/1280×720/1920×1080；证据齐全后再执行旧路径 clean-cutover inventory 审查。
- 最新候选包由 `scripts/build-dev.ps1` 生成；旧 Settings/Overlay fallback 在实机 Gate U 闭环前必须保留，不得宣称 clean cutover。

## Review tracking (2026-08-28 — 6-way isolated review, except S4-Polish)

- [x] review-01 kernel-pure — `docs/review/review-01-kernel-pure.md` — note: no blocker; M1 same-pack variant mixing, M2 S4/S5 corpus xeno coverage, M3 PackFallback domain boundary
- [x] review-02 runtime-resolver — `docs/review/review-02-runtime-resolver.md`
  - note: major DiscoveryAvailable unused / null-Xenotype xeno context misroutes audio; kernel tests pass.
- [x] review-03 settings-migration — `docs/review/review-03-settings-migration.md`
  - note: top findings = pre-v4 globalActionEnabled migration gap, v5+stale-voice clobber risk, Baseline upsert first-match inconsistency.
- [x] review-04 catalog-attach — `docs/review/review-04-catalog-attach.md`
  - note: no blockers; M1 xeno status race-dimension gap + minor attach/validation/log findings.
- [x] review-05 ui-ferrite — `docs/review/review-05-ui-ferrite.md`
  - note: top findings = xeno domain selection/status race-blind, empty-domain writes Global, actionTuning clear removes one only, legacy actionOverrides still runtime-consumed.
- [x] review-06 comp-events-diag — `docs/review/review-06-comp-events-diag.md`
  - note: blocker = Sustained 周期路径绕过触发门；M1 sustainer 丢弃 SoundInfo 调制；M2 ActionEntry.DefaultPlan/TriggerBinding 未接入；M3 external sustained 可重叠；M4 Visible 音频列被状态覆盖。

## Pending decisions / follow-ups

- [ ] Workshop display name and license (maintainer only; do not invent).
- [x] Camera+ 帮助机制调研与实施（2026-08-30 维护者反馈）— 已实现：分区总览 + 单项列表 + 悬停/选中联动高亮；见 `docs/ui-camera-help-and-title-hide-plan-zh.md`。
- [x] 全局行高/文字截断修复（2026-08-30 维护者反馈）— 当时由 legacy `VoicePacksLayout` 动态测量实现；该文件已在 2026-09-02 cutover 中删除。现由内核 measured bands 承担，最终闭环见 "Text fit and localization (2026-09-02b)"。
- [x] 包管理筛选联动（2026-08-30 维护者反馈）— 已修复：race/xenotype 并列筛选 + 自动收窄 xeno。
- [x] 未列出 xenotype 黯淡显示（2026-08-30 维护者反馈）— 已修复：`CandidateCount == 0` 显示 “No available packs” 并黯淡。
- [x] 包管理作者筛选下拉（2026-08-30 维护者反馈）— 已修复：FilterBar 作者下拉。
- [x] 下拉弹层定位修复（2026-08-30 维护者反馈）— 已修复：`UiInteract.ToPageSpace` + popup 页面坐标。
- [x] Tuning Editor 高度协调（2026-08-30 维护者反馈）— 已修复：统一行高/控件尺寸。
- [x] Action Scope 分组与过滤（2026-08-30 维护者反馈）— 已修复：`ActionScopeRules` 分组；隐藏 Crying/Giggling。
- [x] 恢复 SR 按钮/导航高亮样式（2026-08-30 维护者反馈）— 已修复：`SelectionButton` 底部/左侧高亮条；替换模式卡/Tuning layer/FilterBar/预设等。
- [x] 衰减图可拖节点高亮提示（2026-08-30 维护者反馈）— 已修复：LineChart hover 高亮。
- [x] 切换/选取按钮改原版设计语言（2026-08-30 维护者反馈）— 已修复：UiKit `SelectionButton`。
- [x] 不可用控件通路排查（2026-08-30 维护者反馈）— 已修复：Forget/Import/Auto 可见可点；LineChart 滚动拖拽修复。
- [ ] HAR reflection discovery generalization (currently a catalog-side TODO; assembled-only).
- [ ] In-game crash/assignment matrix per `docs/ui-phase3-implementation-notes-zh.md` (maintainer step).
- [ ] First release prep: About icon/preview, final description, runbook US copy adaptation.
- [ ] Before any first push: maintainer decides how to handle the reachable-history personal absolute path (pre-fix `MEMORY.md` line in commits `eb2ac90..dc8c598`); no remote exists so there is no external exposure today.
- [ ] scripts/CI migration: GitHub/Steam build-pack scripts and CI workflows remain deferred until first release prep.
- [ ] Baseline preset system: unit tests now in `UniversalSqueakerSettingsMigrationTests`; still no in-game validation (PresetListWidget) and no shipped example preset Def XML.
- [ ] Builtin fallback table maintenance review (2026-08-27): per-race Defs already support race-level independent maintenance; same-race multi-Def last-wins = single-owner contract (no Def-level field merge); BuiltInActionKeys whitelist closes external keys out of the builtin table; decide whether to publish example fallback/baseline Def XML as doc fixture (currently zero shipped Defs, empty = valid).
- [x] **xeno 层调音 race 身份（2026-08-28 已拍板）** — 采用 ② 运行时双键 context；开发项见 Remaining。调研结论：HAR 通过 race 侧 `raceRestriction` 白/黑名单绑定 xenotype，`XenotypeDef` 无 race 字段，同一 xenotype 可被多 race 白名单；兼容补丁将多 gene mod 的 xenotype union 到同一 race，因此按运行时 pawn race+xeno 路由对玩家最友好。

## Old-UI removal — three knives (maintainer ruling 2026-09-02)

### Knife 1 — cutover (DONE 2026-09-02)

- Deleted 84 files outright and relocated 6 more (commit `e25c686`: 137 paths, +1,546/−12,653); `Source/` C# fell from 205 files / 28,513 lines to 144 files / 19,493 lines. Scope: legacy page chain, legacy widget/component/visual families, `Layout.xml` (Schema=1), the string command bridge (`UiCommand`/`UsCommandPayload`/`UsWidgetCommandAdapter`, `VoicePacksPageModel.Execute*`), and the caller-less final-preview surface (`PreviewFinal`, `SqueakFinalPreview*`, `SqueakSettingsGameContext`). Also removed the unreferenced `UsRangeWrite` payload.
- Whole-page fallback is gone. The window ends in a `pageUnavailable` notice (switch deferred one frame so it never happens inside the IMGUI pass that threw) and per-widget recovery lives in `UiSessionGuard`. No second UI exists.
- Relocated into `FerriteLib.UiKit.Kernel`: `UiElementSpec`, `ITextMetrics`, `UiFont`, `UiValueState`, `UiKitFonts` (now public, single `UiFont`→`GameFont` mapping). The root namespace `FerriteLib.UiKit` no longer exists in any file, so a stale `using FerriteLib.UiKit;` is now a compile error rather than a silent pull toward the old design. `VerseFerriteTextMetrics` moved to `UI/Kernel/`.
- Settings-window chrome and the diagnostics panel now draw only through `UiTheme`/`UiThemeDraw`; `Palette`/`UiText`/`SurfaceFrame`/`UiPanel`/`UiKitGui` and the six self-described compatibility shims are deleted, which closes the dual-palette (G4) root cause.
- Added with the cutover: `SqueakVoicePackModes.All`/`IsKnown` plus a reported `NormalizeMode` (unknown value → one `Log.Error`, degrade to Vanilla); five en/zh Keyed strings for the unavailable notice and the vanilla-shell path; `Mod.DoSettingsWindowContents` now offers the in-house window instead of hosting a second implementation.
- Measured: 15/15 gates green, Dev/Release 0 warning, golden-corpus replay byte-identical (no audio semantics moved).

### Knife 2 — verification closure (DONE 2026-09-02)

- Gate 13 repointed from "old XML well-formed" to "both Schema=2 manifests exist, parse, are rooted at `UiPage` with `Schema=2` and the US package id, and declare at least one Widget".
- `FerriteLib.UiKit.Tests` reduced to 8 kernel lanes (160 assertions) plus two new files: registry/manifest contract tests and core widget behaviour tests. `KernelCoreWidgetTests` turned out never to be wired into `RunAll` and is now wired. `UiGuard` coverage survives as `KernelSessionTests` (fallback height, per-session dedupe, trip id/kind/path/stack, GUI-state restore, relog in a new session).
- `UiSourceInvariantTests` rewritten into six failure-sensitive guards over the surviving Schema2 path: no legacy page symbols may return, no `UiInteract`/`Palette`/`SurfaceFrame`/`UiText`/`UiValueStore` anywhere in the UI tree, registrar Kind set must equal the manifest Kind set (both directions, cardinality 16), embedded resources must be exactly the two Schema2 manifests, help catalog/manifest may not drift, and the help panel height formula must stay keyed to the same item list it draws.
- `UniversalSqueakerUiLogicTests` gained the mode-drift assertion (`SqueakVoicePackModes.All` must equal `Enum.GetValues(typeof(SqueakVoicePackMode))` elementwise, and a non-member value must be unknown).
- `UniversalSqueakerKernelHostTests` gained a step asserting against the shipped assembly that no `Execute*` remains on the model/source surface, the typed facade is the sole write authority, a disposed Host refuses `DrawFrame`, and the deleted page/command/preview types are absent by name.
- The narrow-screen evidence was preserved untouched: `MoodLayoutFocusedTests` still drives the real production Host and asserts 26 Mood controls inside the card without overlap.

### Knife 3 — optional, maintainer call (OPEN)

- Re-implement natively in `UI/Kernel/`, each with a failure-sensitive geometry plus interaction assertion: sticky Tuning layer row, ~~reverse help linkage when hovering mid-column controls~~ (**closed 2026-09-05 by the C+A landing, `72cff33`** - see "Help presentation redesign" below); xenotype-row dimming at zero candidate packs; minor `HideBodyLabel` and an in-list `All` entry for the author dropdown.
- Also decide: delete the remaining pure-Verse camera-readout fallback and make the overlay kernel-only.
- ~~Constant row heights / text clipping unasserted (G1)~~ — **closed 2026-09-02b** by the `UiFitAudit` measurement seam plus gate 15's both-language sweep; see the new section below.
- Alternative for Knife 3: drop those behaviours as legacy-anchored. If dropped, delete the matching help entries and backlog lines in the same commit instead of leaving them dangling.

## Text fit and localization (2026-09-02b, commit `55b6edf`)

- Landed: `ITextMetrics.MeasureWidth`; `UiFitAudit` on the single label outlet; Dev-only `usdiag evt=ui.text.overflow`; manifests `TitleKey`-only; 53 new Keyed strings in both languages (152 keys each); gate 14 localization contract; gate 15 both-language fit sweep with an in-process positive control. Bands fixed: page-title caption, scope-tree inherited hint (was 14px), preset summary (was 11px), checklist banners (one band had been allocated for up to three), basic-tuning rows, nav rows, chrome banner, empty state.
- [x] **Help catalog localization** — closed 2026-09-05 in `72cff33`: all 41 entries (now 40 after the dead `basic-tuning/distance` removal) are Keyed, Chinese authoritative manual-style copy, English translated from it (58 new keys per table + 24 reuses). The historical measurement above stands; the "largest untranslated surface" sentence no longer applies.
- [ ] **Review the proposed Chinese wordings** before any publication: routing modes (原版/回退/混音/禁用), distance presets (保守/均衡/强烈/自定义), filter labels (全部/仅启用/冲突/孤立/种族/异型/作者), card titles. Product vocabulary, not mechanical translation.
- [ ] **Container-level auto-width** deliberately not done: `UiLayoutEngine.ResolveColumnWidths` honours only static `Width=` plus equal split, so `nav-column 192` and `help-scroll 232` stay fixed; "widen for Chinese" currently happens by growing bands, not columns. Revisit only if the in-game log shows a column genuinely too narrow after the band fixes — measured data says Chinese is narrower than English for 146 of 152 keys, so it may never be needed.
- [ ] **In-game half of the evidence**: open the settings window with dev logging in both languages and confirm `evt=ui.text.overflow` stays silent. The harness model is a half-width advance approximation; only the real font engine confirms. Any line it prints is a fix target with an exact need/have pair.

## VoicePack routing table review (2026-09-02; evidence gap closed, content gaps open)

- Maintainer concern: the routing-table mechanism may be incomplete in the same way the built-in fallback table is — `Vanilla` currently yields silence for every pawn because no built-in fallback profile content exists, so routing is the only thing that can make any pawn sound.
- Mechanism verdict: the routing table itself is sound. A pack's declared `raceDefName` is the whole routing surface; `VoicePackCompAttach.Apply` mounts `CreateDefault()` on the union of declared races, skips when the race Def is missing / has no race props / already carries an author comp, and mutates only in-memory `ThingDef.comps` (uninstall-safe). Pack identity is `packageId:defName` (`SqueakVoicePackModels.cs:21-28`), so cross-mod defName collisions cannot occur; a duplicated pack key drops the whole group rather than resolving last-wins.
- Evidence gap **closed**: all three `final-test` packs had their `*_AddSqueakComp.xml` deleted (each was a verbatim copy of `CreateDefault()`, so every race was taking the `author_patch` skip branch and auto-attach had never been exercised). The packs are now canonical per `SKILL.md` §3.5, and `dist/voicepacks/README.md` documents the `usdiag` observation method (`voicepack.comp.auto_attached` vs `attach_skipped reason=race_not_found|no_race_props|author_patch`).
  - New consequence to close later: the escape hatch now has **no fixture at all** — nothing in the corpus proves that an author's custom comp patch wins over the default mount. Add one fixture pack whose patch deliberately differs (e.g. a longer `Work` interval or a `Sustained` action) and assert `attach_skipped reason=author_patch` plus the custom timing.
- Content gap found: no `UniversalSqueakerFallbackProfileDef` and no `UniversalSqueakerTuningBaselineDef` exists anywhere in the repository or in `dist/`. `BuildBuiltInSource()` therefore always returns `BuiltInFallbackTable.Empty`.
  - Consequences to stop treating as designed behaviour: `Vanilla` is silent for every pawn; the last tier of `Fallback` is always empty; `Remix`'s built-in ticket is always `None`; the Presets workspace can only ever show its empty state; and the per-race Config-copy self-heal machinery (`SqueakFallbackProfileStore`, `profileVersion`, merge/rebuild, the A-F lifecycle tests) manages zero data. The four modes currently produce three observable behaviours, two of which are silence.
  - Open decision (maintainer, content work): author built-in fallback profiles per race, or explicitly de-scope the built-in tier and rename the mode accordingly. Do not infer intent from current silence.
- Authoring coverage gap: no pack in `dist/` declares `ageTag`, `isEgg`, or `fallbacks`. Kernel tests cover all three (`UnitTests.cs` 14 sites, `Scenarios.cs` 1), so these are kernel-verified but never author-exercised; the `Remix` four-tier branch is unreachable with real content because it keys off a declared pack fallback.
  - Action: add one fixture pack exercising age variants + egg clips + a per-action pack fallback, and assert it end to end.
- First-run behaviour to confirm by decision rather than accident: pool entries are built only from explicit selection records (`SqueakKernelAdapter.BuildEntries` skips domains with no record or an empty key set), so installing a VoicePack makes nothing audible until the player enables it per domain in the settings UI. Combined with the empty built-in table, a fresh install is completely silent even in `Remix`.
- Brand-boundary item to rule on (still open): the US fixture `Ratkin-US-EXP` routes to race `Ratkin`, and `Kiiro-US-EXP` keeps packageId `coahuilite.squeakyratkin.meowingkiiroexp` with clip roots under `coahuilite.squeakyratkin.*`. `dist/` is gitignored test content, but `AGENTS.md` reserves Ratkin and the SR namespace for SR; either re-target these fixtures to non-Ratkin third-party races or record the exception explicitly. Related risk noted while de-patching: if an external Ratkin or SR-side mod ships its own squeak comp, the log will show `attach_skipped reason=author_patch race=Ratkin` and auto-attach remains untested for that race — that is an external-carrier signal, not a reason to re-add a patch.
- Stale fixture text: fixed 2026-09-02b — `Ratkin-US-EXP/About/About.xml` no longer claims it attaches `CompProperties_Squeaker`, and the empty `1.6/Race/Patches/` directories are deleted. Remaining: the Nivarian pack README still references the old `SR_MeowingKiiroExp_` naming.

## In-game UI defects from the 2026-09-03 acceptance round (fixed, awaiting re-test)

- Landed in `eba5336` + `e75ec28`. Three maintainer-reported defects — the help header overprinting its own index rows on Distance, Packs sections overdrawing each other after the filter is cleared, and a dropdown option click being stolen by the row beneath it — plus five more constant-band defects the now-honest sweep produced (filter chips, preset header title, mood label), plus the gate hole that had been hiding all of them: a harness `MeasureText` that returned a constant, and a production host that hard-coded a Verse metrics object which yields nothing outside the game, so layout and audit were measuring with two different models.
  - **2026-09-04 correction**: the dropdown item above was only half fixed on 09-03 — that fix covered the manifest widget, while every US dropdown is the composite `UsKernelDraw.Dropdown`, which published no popup rect and never clamped or flipped. Closed in US `170e172` + library `44b00c5` through the `UiPopup` primitive; the in-game re-test now covers the composite path (Tuning scope dropdown: picking Auto must select and close, never open the neighbour; a dropdown near the window bottom must flip upward).
- [ ] Maintainer re-test: HANDOFF §5 items 12-14, Chinese, 800×600.
- [ ] No harness can assert real glyph advance. With detailed logging on, walk all five workspaces and confirm `usdiag evt=ui.text.overflow` stays silent; any `height` record is a live defect of exactly this family.
- [ ] Same family, deliberately left out of this block: `SqueakDiagnosticsPanel.DrawVisible` pairs `Widgets.BeginScrollView`/`EndScrollView` and restores `Text.Anchor`/`Text.Font`/`GUI.color` **outside** `try/finally`, and `UiSessionGuard` restores GUI state only in its `catch` and then keeps drawing siblings. Both can leave the IMGUI group stack unbalanced after a single throw — crash-shape risks, so they belong with the heap-corruption triage, not with layout.
- [ ] Knife 3 remainder (maintainer decision): sticky tuning layer row, xenotype-row dimming at zero candidate packs, `HideBodyLabel` in the global volume widget, explicit `All` row in the author dropdown. (Reverse help linkage was closed 2026-09-05 with C+A.)

## FerriteLib extraction (ruled and executed 2026-09-03)

- **Phase 0 — library-side prep inside this tree: DONE (`368297e`).** Language term in the layout cache key; `VerseFerriteTextMetrics` relocated into the library and made public; unread theme tokens deleted and `AccentGoldAlpha20` made derived; `UiTheme.DarkGold` turned into a per-call template; three new harness lanes, all mutation-checked.
- **Phase 1 — physical extraction: DONE.** `../ferritelib` initialised as its own repo (`8e32620`, main) with `About/About.xml` (`coahuilite.ferritelib`, display name FerriteLib, `modVersion` 0.1.0), its own LoadFolders/AGENTS/MEMORY/TODO and a six-gate `verify-local.ps1`. Library and harness moved by file-for-file diff check. `FerriteLibVersion` landed there with `Require()` and the duplicate-carrier enumeration.
- **Step 4 of the original plan (local NuGet feed) was dropped on measurement, not on taste.** NuGet strips build metadata from the cache identity, so a `0.1.0-dev+<sha>` republish still lands in one stale `lib/0.1.0-dev`; only a `0.1.0-dev.<sha>` prerelease label creates a distinct package, which needs a floating consumer version, which these gates defeat by running with restore disabled (18 hard-coded `--no-restore`). US therefore uses a sibling relative `HintPath` with `Private=false`. `dotnet pack` still works in the library repo; the upgrade path is written into its TODO.
- **US unbound:** `ProjectReference` gone; `About/About.xml` declares `modDependencies` + `loadAfter`; `UniversalSqueakerMod`'s constructor calls `FerriteLibVersion.Require(0.1.0, 0.2.0, PackageId)` and logs rather than throws. `verify-local` went 15 → 12 gates: the four library gates moved to their own repo, and the old "FerriteLib.dll must be present" check **inverted** into a single-carrier red line, now asserted in three places (gate 9, `check-pack-readiness`, `stage-package`). `dist/dev` verified to contain exactly one DLL.
- The neutrality guard that used to scan the library from over here (`VerifyUiKitTreesNeutral`) was **deleted, not moved**: its `Directory.Exists(tree) { continue }` meant it would have passed vacuously the moment the trees left. The library now asserts its own neutrality from inside, with a positive control and a path-pinned self-exemption. The old note that a library "cannot assert its own neutrality" was wrong.
- **Blocking next step: nothing in either repo has run inside the game since the split.** Cross-mod assembly binding, the duplicate-carrier report path, and the mid-session language-switch fix are all unverified in the real build — see `../ferritelib/TODO.md` §1.
- **Do not freeze the public API in phase 1.** Freeze only after NivarianGrandStructure is wired: it is a full-screen world-overlay consumer, not a page consumer, and its shape is what will tell us whether the widget contract and `UiTheme` surface are right.
- **Phase 2 — FerriteLib's own retained-mode roadmap** (after extraction, in the new repo): element/identity layer with per-node dirty flags; a third operation on the bindings (announce, not just get/set); an owned hit stack. Each is additive and each should delete a workaround, not add a shim.

## Workspace cleanup, integration check and the composite popup fix (2026-09-04)

- Cleanup done (`fd51304` plus untracked deletions): removed the two split shells (`tools/FerriteLib.UiKit.Tests` stub residue, `Source/FerriteLib.UiKit/obj`), the stale zero-byte dirty package label in `dist/dev`, and corrected three investigation-phase status claims (`AGENTS.md` HAR sentence, `docs/uikit-rebuild/README.md` current-state line, `07-…md` §1) with dated correction banners that keep the original text.
- Integration re-verified in source with **no gap found**: csproj `HintPath` + `<Private>false`, `About.xml` `modDependencies` + `loadAfter`, `Mod.cs` → `FerriteLibVersion.Require`, carrier payload present at `../ferritelib/1.6/Assemblies/`, and gates 6/9/10 plus `check-pack-readiness`/`stage-package` enforce the single-carrier rule. The in-game proof remains the 2026-09-04 session recorded in HANDOFF §7.
- Composite popup fix landed and mutation-checked both ways (MEMORY "Composite popup path closed"); library 7 gates and US 13 gates green after restore.
- [ ] Maintainer in-game re-test of the composite path (see the corrected item in the 2026-09-03 section above).
- [x] Refactor assessment: answered — no broader refactor warranted; reasoning in MEMORY. Reopen only if a second geometry/input duplication appears.
- Open decisions unchanged: Knife 3 behaviours, overlay kernel-only cut, help-catalogue localization tone, built-in fallback/baseline content, brand-boundary fixtures, Workshop naming/description.

## Desync incident follow-ups (2026-09-04b, US `a2ade99` + library `b2006a0`)

- [ ] Maintainer: replace BOTH installed mods from the new package pair (US zip labelled `a2ade99…` and the FerriteLib zip from library `b2006a0`); lockstep is release policy and the Api pin now enforces it at build time.
- [ ] Maintainer in-game: the settings window opens with no notice region; the Tuning scope dropdown selects Auto and closes without opening the neighbour; with detailed logging, both languages walked through all five workspaces keep `ui.text.overflow` silent (the 2026-09-04 height-axis records should all be closed by the band calibration).
- [ ] If a future in-game round reports `ui.text.overflow` again, re-measure the stub line heights from the log's need values before touching any band.
- [ ] Maintainer: replace **only the FerriteLib mod** from the new `FerriteLib-dev-v0.1.0-dev.zip` (library `9a197a2`); the US package `b56aa08` stays as installed — the fix shipped in the carrier DLL. Proof the new library is live: ptrace lines now carry `pointerLocal=` and `pointerWindow=` separately. Then re-run the Tuning scope-dropdown click (Auto over the next row's trigger) and the wheel-scroll workaround should be unnecessary.
- [x] Filter misalignment diagnosed and fixed: view cache and layout cache shared clock (US `87e3cd6`); package `UniversalSqueaker-dev-v0.1.0-dev-87e3cd6.zip` (US-only change, FerriteLib stays).
- [ ] Maintainer re-test: apply/clear race and xenotype filters on the Packs workspace - content and layout must agree in the same frame, no workspace-switch refresh needed.
- [x] Filter dropdown labels localized + 异型→异种 (US `d23ad22`); package `UniversalSqueaker-dev-v0.1.0-dev-d23ad22.zip` (US-only).
- [ ] Maintainer re-test: race/xenotype/author dropdowns show translated labels (鼠族 etc.) while writes stay machine tokens; xenotype row's race context matches the race layer's title.
- (superseded by the ruling below - direction is C+A, plan delivered 2026-09-05).

## Help presentation redesign (CLOSED 2026-09-05: C+A implemented + catalog keyed, US `72cff33`)

- Camera+ settled from SOURCE (GitHub tag `v3.4.7.0` = installed version, `Source/Settings.cs`): the right panel is hover-driven with topic fallback (`hoveredHelp*` cleared every frame → no pin state); every row type wires `DrawControlHover` (+3.5% white row highlight); tiers topic→group→control each with a `SettingsHelp_*` key; `helpExtra` is an amber dynamic-note channel; inline `SettingsNote_*` are runtime state notes, not help; no `?` buttons, no tooltips; help column 220px non-scrolling. Both earlier claims (pass-1 ungrounded "hover swaps panel", pass-2 DLL-string "topic-driven only") are superseded. Camera+'s shape = C+A combined.
- Options for the maintainer, re-labelled against the source fact: A topic-driven panel only / B index+collapse / C per-control hover swap / **C+A (Camera+'s actual shape, and what US's pre-cutover reverse linkage already was)** / D focus + persistent index. `SettingsNote_*`-style inline state notes are a separate channel Camera+ uses for rule-override status, not a help format. Translating the 41 catalog entries (tone: manual-style vs colloquial) is a prerequisite for any direction in a Chinese client.
- DONE: C+A implemented with zero UiKit change (library stayed read-only through the GitHub release prep; Api untouched, US-only package `UniversalSqueaker-dev-v0.1.0-dev-72cff33.zip`).
- DONE: catalog fully localized - Chinese authoritative manual-style copy (58 new keys/table, 24 reuse), English translated from it; camera-indicator copy corrected from the false "marks pawns" claim to the real bottom-left readout.
- [ ] Maintainer re-test (C+A walkthrough, Chinese client): hover switches instantly per control, moving away falls back to the section overview, panel height never jumps, nav/footer cross-section claims work, help bodies read fluently in 232px with `ui.text.overflow` silent.
- Open for the copy owner: the maintainer should skim the 58 Chinese entries for tone (they follow the 说明书式 ruling but were drafted by an agent); 复用键 mean panel titles equal card titles including the "1./2./3." step prefixes - accepted unless ruled otherwise.

## Help key naming tightening (decision open; batch rename, zero behavior change)

- Measured 2026-09-05: 152/211 keys sit at 4+ dot-segments; the help keys' role suffixes (.Title/.Overview/.Label/.Text) carry no information the parent name does not already fix. Proposal: key = concept, body on the bare name, label keeps .Label - all help keys become ≤4 segments. Constraint: every reference stays a source literal (concatenated/derived keys are prohibited; gate 14's existence scan only sees literals).
- [ ] Maintainer decision: rename now or after the C+A in-game walkthrough (renaming changes shipped bytes and forces a re-test of the very build being walked).
