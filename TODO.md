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
- **Ferrite UI 游戏内稳定化** — maintainer step（尤其 800×600 三栏、Tuning Editor、下拉、Mood stepper、衰减图拖拽、sticky layer、分层帮助 hover/选中联动）。
- **Backlog** — OB-02 `UiLayoutTier.ClampWidth` 接入、OB-03 加载 clamp 测试、OB-04 `VoicePacksLayout` 测试（可在游戏内验证后处理）。
- **（可选尾部）Runtime harness** — adapter fold/converters/BuildFallback mode pass-through untested by kernel gate (ReviewResolverFold P3 residual).

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
- [x] 全局行高/文字截断修复（2026-08-30 维护者反馈）— 已修复：`VoicePacksLayout` 动态测量行高；Basic toggles/VoicePackRow/Race/Xeno 行均按文本测量。
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
