# OBLIVIONIS

> Cold archive. Read only for a historical conflict or an explicit request; it cannot override current sources.

## Entries

- 2026-08-24 legacy bridge activation (maintainer authorization): the 0.4 co-existence rule "US must not define SqueakyRatkin.* types" was explicitly overridden for one thin empty `SqueakyRatkin.SqueakVoicePackDef` compatibility shim. Old SR VoicePacks now load through the bridge and are explicitly marked as old SR content in logs (`voicepack.pack.legacy_admitted`) and UI (`Legacy SR` tag/banner). No Ratkin audio/content ships with US.
- 2026-08-23 US rebuild cleanup: the SR-derived reference trees were consumed and deleted from the working tree — root `Kernel/` (9 cs files), `Pure/` (2 cs files), `fixtures/` (SR corpora + 0.2.4-shaped settings fixtures), `sr_reference/` (SR SoundDefs/localization snapshot), and `tools/KernelCharacterization/` (legacy SR harness). They remain retrievable from git history. Pre-rebuild baseline commit: `abae59c`; last pre-cleanup commit: `a43b731`. Rebuild commits: `edfba6b` (de-SR-ized kernel/pure + US test gate), `1ea6856` (runtime assembly + data surface + tool gates), `a43b731` (componentized minimal UI).

## Archived session checkpoints (moved out of `MEMORY.md` on 2026-09-02b; verbatim, byte-copied)

- Reason: `AGENTS.md` forbids session narratives in the active memory files. Every block below was live working state at its date and has since been superseded by the cutover (commit `1a4e904`) and text-fit/localization (commit `bba6da2`) sections that remain in `MEMORY.md`. Line references and commit hashes are as written at the time.
- Most superseded specifics: gate counts grew 12 -> 13 -> 14 -> 15; `Palette`/`UiText`/`UiPanel`/`SurfaceFrame`/`UiInteract`/`UiValueStore`/`UiGuard`/`VoicePacksLayout`/`Layout.xml` (Schema=1) and the legacy page chain no longer exist; Gate U no longer requires a retained fallback.

## UiKit / US settings rebuild checkpoint (2026-08-31)

- Architecture audit completed for `Source/FerriteLib.UiKit/**`, its runtime-stub harness, and US UI consumers. The current implementation is a flat rect/deferred-interaction brownfield, not the target kernel.
- Maintainer-approved behavior baseline is `docs/uikit-rebuild/05-p0-contract-baseline-zh.md`: native IMGUI is the sole event authority; XML owns structure/static props/translation keys/limited responsive constraints; typed bindings; per-Window/Overlay session; injectable Dark Gold theme; session fallback.
- P0 freezes invariants and offers a recommended API baseline without prescribing one implementation. The DeepSeek implementation lead may choose API shape, file decomposition, algorithms, focused vertical slices, and local migration order, provided it records tradeoffs and preserves invariants.
- Rebuild execution is documented in `docs/uikit-rebuild/README.md`, 01–07, `tasks/MAIN-ORCHESTRATOR.md`, and the DeepSeek task books. The old P0 implementation plan is historical only.
- Gate R 窄切片已于 2026-08-31 接入真实生产窗口：`Layout.Schema2.xml`（banner + global-volume）→ `UsKernelSettingsHost` → `UniversalSqueakerSettingsWindow` 持有的 `UiHost`/`UiSession`；自动 build、嵌入资源、focused harness 与完整本地门禁均通过。
- Gate R 实机结果为 `PASS`：维护者确认首开、Kernel banner、global-volume 交互、关闭/重开及其余场景无异常。数字框按 Enter 关闭窗口与其他 Mod Settings 一致，是 RimWorld 默认行为，不做 US 特殊拦截。证据记录：`docs/uikit-rebuild/gates/GATE-R-2026-08-31.md`。
- Gate U 已获准启动，但完整 Basic/Tuning/Packs Settings Host 尚未迁移；旧完整 Settings 路径继续作为明确过渡 fallback，直至 Gate U 验收和 clean cutover。后续代码实现交给 DeepSeek，主代理只负责任务书、调度、契约裁决与验收。
- SR upstream (read-only evidence source): sibling repository (public, read-only; local checkout path withheld). Do not write there and do not infer its external state from this repo.

## Engineering decisions and handoff

- **Local fork only**: no remote is configured; local commits are the only permitted git operations until the maintainer authorizes remote/push. Reachable history still contains the pre-fix personal absolute SR path in `MEMORY.md` (commits `eb2ac90..dc8c598`); it is recorded in TODO and must be handled by the maintainer before any first push.
- **Migration inventory (2026-08-23, historical)**: `Kernel/*.cs` (9 files), `Pure/` (2 files), `tools/KernelCharacterization/*` plus `fixtures/` and `sr_reference/` were migrated, consumed as references during the rebuild, and deleted in Phase 4; they remain in git history (`abae59c` baseline).
- **Inherited technical debt (from the SR snapshot) — resolved by the rebuild**: namespaces migrated to `UniversalSqueaker*`, Ratkin seed and `SR_*` keys removed, `DomainFilter`/`SqueakProductDomainFilter` deleted, five-way sync replaced by three-way sync plus tool gates.
- **Target baseline**: kernel with zero product literals (race/sound-key/prefix all injected as data); UI only needs VoicePack assignment to work, all other pages may be removed but must not crash; every race routes equally with no Ratkin special-casing.
- **UI adaptation decision (per HANDOFF.md)**: Plan A (vertical cut) is recommended — keep settings shell, Off/Fallback/Remix mode cards, Race layer, and VoicePack domain checkboxes; remove SoundMood workbench, Diagnostics, audio browser, statistics/overlay/mote diagnostics, and the Xenotype behavior editor without changing any Scribe schema.
- **Layer boundary (2026-08-28, durable)**: Kernel owns zero-Verse domain keys/pools/selection; Pure owns zero-Verse logic (timing/action-plan/layered tuning/aggregation/context selection); External owns Verse-coupled adapters/UI/settings. Only extract into Pure logic that has rules worth testing AND does not depend on Verse types. `ResolvedSqueakContext`/`RuntimeActionDelta`/`RuntimeMoodDelta` remain External because they reference `XenotypeDef`/`FloatRange`. Avoid over-abstraction: don't move Verse-coupled containers into Pure just to make the layering look clean.

## Rebuild plan decisions (approved 2026-08-23)

- D1 Version: csproj `<Version>0.1.0-dev` is primary; `About.xml <modVersion>` follows it.
- D2 Rebuilt pure code lives under `Source/UniversalSqueaker/Kernel/` (zero-Verse compile set) and `Source/UniversalSqueaker/Pure/` (funnel pure logic); the old root `Kernel/`+`Pure/` were reference-only and are now deleted.
- D3 SR 0.2.4 settings-migration fixtures are not replicated now: US is a new mod with no legacy config; the legacy bridge is deferred to the takeover version.
- D4 Verification project: `tools/UniversalSqueakerKernelTests/` with the US 0.1.0 golden corpus; old `tools/KernelCharacterization/` was reference-only and is now deleted.
- D5 Kernel product literals: `BuiltInFallbackCatalog` (Ratkin seed + `SR_*` keys) and `DomainFilter` whitelist semantics removed; `BuiltInFallbackTable.Empty` plus data injection remain.
- D5b Comp attach: VoicePacks may attach `CompProperties_Squeaker` via their own XML patch.
- D6 UI: reactive view-model + declarative immediate-mode components over Verse widgets (evaluation: `docs/ui-componentization-evaluation-zh.md`; implementation notes: `docs/ui-phase3-implementation-notes-zh.md`).
- D7 Cleanup executed in an atomic commit: `Kernel/`, `Pure/`, `fixtures/`, `sr_reference/`, old `tools/KernelCharacterization/` deleted; `OBLIVIONIS.md` records the pre-rebuild baseline commit `abae59c`.

## Session resume checkpoint (2026-08-24 — UI migration planning session; compression anchor)

- This session produced the locked UI migration + orphan-feature plan: `docs/us-ui-migration-plan-zh.md`. It supersedes the earlier "UI adaptation Plan A" as the implementation entry for the next work. `TODO.md` "Next development plan" section mirrors the locked decisions.
- Locked decisions (durable):
  - Mode set becomes `Disabled / Vanilla(旧 Off 改名，推荐名待确认) / Fallback / Remix`; `Disabled` = true bypass (sampling-layer short-circuit, no `ThingDef.comps` mutation; single `usdiag` notice only, then no repeated trigger logging); Scribe migration mapping in plan §1.
  - Tuning editor merges behavior + mood: player-tuning baseline = standalone read-only XML Def (NOT `CompProperties_Squeaker`, NOT `SqueakVoicePackDef`); author per-pack baseline stays in the converged comp (`actions` trigger config + `moodMods`); `distancePresets`/`globalMinIntervalTicks`/`scaleFrequencyWithTalking` are global and leave the author patch face.
  - Player overrides = Plan A single layered table (`ActionTuningRecord`, Global→Race→Xenotype, `Xenotype > Race > Global > DefaultScope`); replaces `globalActionEnabled` + `xenotypePresets.actionOverrides`.
  - Action entry wrapper: `ActionEntry`+`TriggerBinding` replace hardcoded `Notify_*`+Harmony patches; three-segment architecture = TriggerBinding(从哪来) → PawnIdentity/ResolveContext(我是谁) → Registry.Select(到哪去); neutral-kernel vs external-adapter split locked in plan §13.4–13.5.
  - Easter-egg toggle in main settings next to mode cards, default off; keep+restore `localizeDebugActions`; delete `experimentalRaceAllowlist`, `voicePackDefaultSeeded`(evaluate), `<description>`+`Name` localization patch; Developer page replaced by DebugAction panel.
- Two independent reviewer reports completed this session and folded into plan §14. Top open items before S2/S4: H1 layered-priority conflict with `SqueakGlobalActionPolicy` early-return; H2 explicit Scribe migration design needed; H3 Race-layer runtime consumption missing; H4 ActionKey stringification inventory; H5 Sustained validator/playback prerequisites. Recommended execution order: Scribe+data-model design → S1 → S2 (one-shot ActionKey+layered table+action gate) → S3 → S4 → S5.
- Key code facts verified this session (do not re-derive): `CompProperties_Squeaker` = `CompSqueaker.cs:897-955` (globalMinIntervalTicks/scaleFrequencyWithTalking/actions/moodMods/distancePresets; `CreateDefault()` 916-954); `SqueakVoicePackDef` never carries behavior/mood data (`Models/SqueakVoicePackModels.cs:9`); `xenotypePresets` consumed at `SqueakRuntimeResolver.cs:149-155`; `moodOverrides` at `CompSqueaker.cs:561-573`; distance applied at `CompSqueaker.cs:838-853`; three runtime scale switches at `CompSqueaker.cs:147-149`; `Sustained` period path dead but external `NotifyExternal` does not filter by mode; pawn identity = `SqueakRuntimeSnapshot.ResolveContext` + `Choose` (`SqueakRuntimeResolver.cs:224-252`); `localizeDebugActions` dead field (patch removed); `Patch_ModMetaData_LocalizedMetadata` still present and to be deleted.
- Pending maintainer confirmations: (1) old `Off` rename target — recommended `Vanilla` (or `FallbackOnly`); (2) tuning baseline Def final name/shape; (3) `ActionEntry`/`TriggerBinding` C# interface detail before S2.
- Prior session state still relevant: local commit chain ends at `d3c94b3` (working tree now additionally has the new plan doc + TODO edit + MEMORY checkpoint edit, uncommitted). Test pack inventory under `dist/` unchanged: `Kiiro-US-EXP`, `Nivarian-US-EXP`, `KiiroSiamese-XenoRoutingTest-US-EXP`, `RatkinOA-XenoRoutingTest-US-EXP`. Install by copying `dist/dev/UniversalSqueaker` over the game Mods folder. `dist/` is gitignored build/test output.
- Next action on resume: read `docs/us-ui-migration-plan-zh.md` §14, then either start the Scribe+data-model design or S1 global-layer removal; collect maintainer answers to the three pending confirmations first if possible.


## Session resume checkpoint (2026-08-25c — 第三次会话；跨 harness 压缩锚点)

- This session executed **双源统一 + voicePackDefaultSeeded 删除 + dist/ 残留 + Legacy 兼容桥删除 + Baseline 预设系统 + 路由表评估**，committed as 7 LOCAL commits: `5068548` → `5518827` → `a49b06c` → `8279444` → `a5bcff3` → `5c405f4` → `bec1db2` → `9812c02`, on top of `e279de1`. All green: verify-local 12 gates + Dev/Release 0 warnings + kernel golden-corpus replay zero-delta. Working tree clean.
- Durable facts established this session (do not re-derive):
  - **双源统一完成**：`globalActionEnabled` 全链路删除。`BuildGlobalActions` = `C# DefaultScope < actionTuning Global 层`。
  - **Legacy SR 兼容桥已删除**（维护者裁决）。`Legacy/` 目录全删，`SqueakyRatkin.*` 类型不再存在，3 个 legacy 日志事件删除，UI Legacy SR 标记删除。LogTests V2 事件计数 9→6。
  - **Baseline 预设系统（方案 C）**：`UniversalSqueakerTuningBaselineDef` 重构为 race/xeno 树形结构 + `inheritFromRace`。`BaselineTuningTable` 运行时消费删除。新增 `BaselinePresetImporter`（增量导入 + 合并）。`ActionTuningRecord` 新增 `sourcePresetDefName` 来源标记。新增 `PresetListWidget`（五步模板）。mood 预设走现有 `moodOverrides`/`XenotypePresetRecord.moodOverrides` 路径。
  - **路由表机制评估**：四维度分析完成。路由表不是新机制，是开放现有 `CreateDefault()` 给所有包。`IsLegacy` 已随 legacy 桥删除。baseline 是全局的，叠加是字段级覆盖（不是叠乘），不适合 per-pack 音色修正。
  - **subagent 配置**：代码开发用 `dispatch_subagents`（不要用 `subagent`——它复用主 agent 的 pro model，太贵）。provider `qwen-token-plan-cn`，model `deepseek-v4-flash`。scout `low` / 审阅 `high` / 开发 `max`。
  - **RimWorld enum Scribe is by-name**（前会话）：Scribe_Values writes `value.ToString()`，`Off→Vanilla` ABI-safe。
  - **Ferrite widget 扩展模板（五步）**（前会话）：`IWidget` → `UsWidgetCommandAdapter.For` → `FerriteVoicePacksPage` viewState → `UsWidgetRegistrar` → `UI/Layout.xml`。
  - **Diagnostics panel**（前会话）：`CompSqueaker.PostDraw()` 公开钩子，非模态 Window，17 门禁链三态。
- Remaining: (1) **路由表开放**（~20 行 C# + LogTests `AssertEqual(6→9)`）；(2) **专项测试** "global off + xenotype on" Pure 提取或 Runtime harness；(3) **S4-Polish**（过滤/帮助/窄屏/美化/footer/距离预览/A1-A2 调音编辑器/Race-Xenotype scope UI）；(4) **`docs/workdocs/` 移除**（所有剩余块落地后）；(5) **Ferrite UI 游戏内稳定化**（maintainer step）。
- Known residuals: BaselinePresetImporter 无单测；PresetListWidget 无游戏内验证；未 ship 示例预设 Def XML；`sourcePresetDefName` 为增量 Scribe 字段（旧存档可直接加载）。

## Session resume checkpoint (2026-08-27 — route-table open + audit fixes + plan update)

- This session landed **路由表开放** and closed audit findings A1–A3 on top of `9812c02`:
  `5819089` (route table open) → `eccca79` (escape-hatch rename) → `7320e9e` (A1) → `b8de71a` (A2) → `f6be81b` (A3). All green: Dev/Release 0 warnings + verify-local 12 gates + kernel golden-corpus replay. Working tree clean.
- Durable facts established this session (do not re-derive):
  - **路由表开放 (456b2a4)**: new `Catalog/VoicePackCompAttach.cs` mounts `CompProperties_Squeaker.CreateDefault()` on every race of the admitted-pack union (`catalog.RaceDefNames`), with `auto_attached`/`attach_skipped` diagnostics; author-patch escape hatch preserved via `Any()` skip (docs term now **作者自定义优先 (escape hatch)** — renamed in `eccca79` to stop colliding with RimWorld pod naming). 3 new v2 events (`voicepack.comp.auto_attached`/`attach_skipped`/`attach_failed`); LogTests v2 registry 6→9. SKILL.md rewritten: canonical packs need no comp patch (patch = optional advanced use only).
  - **A1 (24f958c)**: actionTuning layered table serves built-in action keys only — the Global-layer comment previously claimed external keys apply (they never did). External-key tuning is YAGNI; revisit if an external-action ecosystem appears.
  - **A2 (23785a3)**: `BuildFallback` preserves `NormalizeMode(settings.voicePackMode)` instead of hardcoding Vanilla — Disabled true bypass now survives resolver rebuild exceptions (M1 compliance; before the fix the bypass gates stopped firing and data-driven builtin fallback could play while Disabled).
  - **A3 (11904d5)**: `CompTick` Disabled gate moved ahead of `MaintainSustainer`, mirroring the `NotifyExternalByKey` head gate. An active Sustainer is no longer maintained in bypass; vanilla's unmaintained lifecycle ends it in ~1–2 s. No special-case End branch.
  - **Builtin fallback table architecture confirmed (D5 continuation)**: one `UniversalSqueakerFallbackProfileDef` per race (`raceDefName` + `profileVersion` + `entries`); DefDatabase may hold one Def per race → race-level independent maintenance already supported. Kernel `BuiltInFallbackTable` is only the runtime aggregation view (by `RaceKey`). Per-race Config work copies (`UniversalSqueaker_Profile_<race>.xml`) + per-race `profileVersion` drive self-heal/merge/rebuild. US ships no seed Defs (`1.6/Defs` empty); `Empty` = valid release posture. Boundaries: same-race multi-Def is last-wins (single-owner contract, no Def-level field merge); `BuiltInActionKeys` whitelist closes the builtin table to built-in keys (external actions only via the ActionEntry gate); no per-action default-inheritance chain (missing entry = silent, by design).
- Next blocks (mirrors TODO.md):
  1. **分层心情调音 + Race/Xeno scope UI（B1+B3 并块，方案 A）**: unified `MoodTuningRecord` three-layer table (mirror of `ActionTuningRecord`); `race.moods` → Race layer (layer 1) — **decision flip superseding the 2026-08-25 方案 C global-dict record**; xenotype overlays race (also fixes the xeno-does-not-inherit-race-moods gap). Migration: `moodOverrides` → layer 0, `XenotypePresetRecord.moodOverrides` → layer 2 (1:1 lossless). Mood resolution folds into snapshot contexts — `ResolveMoodMod` drops its live `settings.moodOverrides` read (completes plan H3). Importer rewrite; UI three-layer scope tree + mood editor rows (A1/A2 editor v1). Commit split: 1 data+migration+runtime+importer, 2 UI, 3 docs (incl. decision-flip record).
  2. **专项测试 (B4)**: layered merge semantics as pure functions — 'global off + xeno on' (actions) and race→xeno mood inheritance; extract merge logic to Kernel/Pure alongside the layered-mood block; Runtime harness for the Verse-coupled residue. Folds in BaselinePresetImporter merge extraction (B2).
  3. Then S4-Polish remainder (filtering/help/responsive/visual/footer/distance chart), `docs/workdocs/` removal, Ferrite UI in-game stabilization (maintainer step).

## Session resume checkpoint (2026-08-27c — S5 layered tuning + tuning editor + kernel gate landed)

- This session completed the layered-mood + tuning-editor + kernel-gate plan on top of `f6be81b`:
  `c2aaeae` (layered mood data) → `d7dc7f7` (three-layer tuning editor UI, option 1) → `76b8115` (pure fold + kernel gate) → `14f86b6` (three-reviewer fixes). All green: Dev/Release 0 warnings, verify-local 12 gates, kernel golden-corpus zero delta, 13 layered-fold assertions. Working tree clean.
- Durable facts (do not re-derive):
  - **分层心情调音 (81e7bb9)**: `MoodTuningRecord` three-layer table (mirror of `ActionTuningRecord`); settings schema 4→5 transactional migration (`moodOverrides`→layer 0, `XenotypePresetRecord.moodOverrides`→layer 2, lossless); runtime three-layer fold in snapshot contexts; `ResolveMoodMod` no longer live-reads settings (H3 completed); importer decision flip — race.moods → layer 1 (supersedes the 2026-08-25c global-dict record), xeno.moods → layer 2.
  - **三层调音编辑器 (2ce5dd9)**: `ScopeTreeWidget` rewritten as the S5 tuning editor — layer segment (Global/Race/Xenotype), domain picker (race list / xeno domain union), scope rows with inherit ring (Auto/Off/Any/Command filtered by supported scopes; inherit clears the layer record), four mood rows with −/+ step factor controls (field-level hasX) + Auto clear. New commands `SetTuningLayer`/`SetTuningDomain`/`SetMoodTuning`; `SetMoodTuning` write bridge (last-wins, clear-all, continuous resolver rebuild).
  - **纯折叠 (43835ea)**: `Pure/SqueakLayeredTuning.cs` (zero-Verse; LayerActionDelta/LayerMoodDelta + Resolved* + Merge/Resolve folds, jitter as (min,max) pairs); resolver builder accumulation replaced by layer-source dictionaries + pure fold in `BuildContext`; public `RuntimeActionDelta`/`RuntimeMoodDelta`/`ResolvedSqueakContext` unchanged; GetMoodDelta null semantics preserved. Kernel gate: 13 assertions (H1 global off + xeno on; xeno > race > global > default; field-level last-wins; empty-layer identity; same-layer merge; probability/jitter inheritance; race→xeno mood inheritance; explicit ActiveCommand preserved — intentional deviation from old flatten-to-Any).
  - **三 reviewer 审查 (f4d8448)**: parallel `task` + `reviewer` agents (inherit session model config) found 14 items; fixed 8 real defects (UI effective-scope display + layer-priority projection folds; mood label column + narrow-window guard; SetMoodTuning/UpsertMood last-wins + clear-all; FromMoodRecord defensive sanitize; S2 actionTuning resurrection gated to pre-v4). Accepted/documented: migration-failure path (legacy moods inactive for the session, retried next startup, exceptions only); Runtime harness residual (adapter fold/converters/fallback untested by kernel gate).
  - **待拍板（下一个会话第一件事，历史项）**: xeno-layer tuning race identity — data records are (race,xeno) two-key but runtime xeno contexts are `xenotypeDefName`-keyed (pre-existing since S2), so the same xenotype on different races shares layer-2 tuning while the UI suggests race-specific. Option ① accept as documented limitation; option ② re-key runtime contexts by (race,xeno). **Superseded by the 2026-08-28 decision: option ② adopted.**
- Remaining: **xeno double-key context dev block (decided 2026-08-28)** → S4-Polish (pure visual: filtering/help/responsive/beautification/footer/distance chart) → `docs/workdocs/` removal → Ferrite UI in-game stabilization (maintainer) → optional Runtime harness tail.

## Session resume checkpoint (2026-08-28 — HAR xenotype/race investigation + double-key decision)

- Investigated HAR xenotype↔race binding from local workshop mods and decompiled HAR 1.6 `AlienRace.dll`:
  - `XenotypeDef` has **no race field** in RimWorld source.
  - HAR binds xenotypes on the race side: `ThingDef_AlienRace.alienRace.raceRestriction` with `onlyUseRaceRestrictedXenotypes`, `xenotypeList` (compat alias), `whiteXenotypeList`, `blackXenotypeList`; `ResolveReferences` copies `xenotypeList` into `whiteXenotypeList`.
  - Runtime filtering: `RaceRestrictionSettings.CanUseXenotype` / `FilterXenotypes`, patched into `PawnGenerator.XenotypesAvailableFor`, `GetXenotypeForGeneratedPawn`, `Pawn_GeneTracker.SetXenotype`, `CharacterCardUtility.LifestageAndXenotypeOptions`.
  - A xenotype can be whitelisted on **multiple races**; there is no engine-enforced one-to-one owner.
  - Ratkin gene compatibility patch (`Ratkin Gene Patch`) unions `Ratkin_*` (EoralMilk) and `OAGene_*` (OA) into the same `Ratkin` `whiteXenotypeList` and generation sets, confirming “ignore mod source, route by xenotype identity”.
- **Decision (maintainer agreed)**: adopt option ② — runtime xeno tuning contexts become `(raceDefName, xenotypeDefName)` double-keyed. This aligns runtime with the existing data/UI/audio-domain identity and HAR’s race-restriction model.
- **Implementation (2026-08-28, landed)**: `AudioDomains` (Kernel), `SqueakTuningAggregator`/`SqueakContextSelector` (Pure), resolver thin adapter; `contexts` keyed by `AudioDomain`, `raceContexts` by `RaceKey`; `SqueakRaceForXenotype` removed from main path; public runtime API unchanged. Design doc: `docs/us-xeno-double-key-context-zh.md`.
- **Review-fix phases (2026-08-28, landed)**: Phase 0–6 completed after 6-way review. Legacy UI removed; full `(race,xeno)` domain identity across Settings/UI/Baseline/Notification/resolver; single tuning authority + unified upsert/clear; transactional settings migration + new `tools/UniversalSqueakerSettingsMigrationTests`; YAGNI removed `ActionEntry`/`TriggerBinding`; Sustained trigger/playback pipeline unified; kernel variant mixing/corpus/PackFallback boundary; diagnostics/logging/fail-closed fixes. `scripts/verify-local.ps1` now has 13 gates, all green.
- Next: S4-Polish, `docs/workdocs/` removal, Ferrite in-game validation, optional Runtime harness.

## Session resume checkpoint (2026-08-30 — US UI overhaul + in-game trial hotfix)

- **S4-Polish + US UI 大修 U0–U6 全部完成**：verify-local 14 门全绿，Dev/Release 0 警告；任务书/评估/最终 review 在 `docs/workdocs/us-ui-overhaul-*`。
- **UiKit B1–B4 已落地**：`UiInteract` 分层输入路由、`UiValueStore`、`input/number-slider`；`UiGuard` 公开，`UsGuard`/`FerriteGuard` 已删除。
- **游戏内试用发现**：左导航不可点击、帮助按钮被内容覆盖、部分控件错位。已做 **US 侧 hotfix**（nav 改 `ButtonInvisible` + 手绘 label；help 在 US widget 内移到 surface 之后绘制），提交 `1369869`。
- **架构警告**：hotfix 是绕过 UiKit 的 workaround；正确方案应回到 UiKit 层（顶层绘制阶段 + 导航纳入统一交互帧），记为 **U7**，尚未实现。
- **dist voicepack 已整理**：`dist/voicepacks/final-test/` 只保留三个独立当前 US 包（`Ratkin-US-EXP`、`Kiiro-US-EXP`、`Nivarian-US-EXP`），`archive/` 放其它测试/legacy 包；包间不互相引用。Ratkin 包已删除 legacy `SqueakyRatkin_ExampleTemplate_Race.xml` 并重命名为 `US_RatkinExp`；Nivarian packageId 修复为 `coahuilite.nivarianusexp`。
- **日志红字**：旧 SR ABI `SqueakyRatkin.SqueakVoicePackDef` 红字来自旧 Ratkin/legacy 包，当前 US 不再存在该类型；final-test 包已清理。
- **最新 dev 包**：`dist/dev/UniversalSqueaker-dev-v0.1.0-dev-1ae7dd6.zip`。
- **Backlog**：`docs/workdocs/us-ui-overhaul-backlog.md`（OB-01..OB-04）+ U7。

## Session resume checkpoint (2026-08-30b — 现代 UI 重构落地)

- **现代 UI 重构（`a4d5070`）**：UiKit 新增 `UiPanel`/`UiText` 中性原语 + `Palette` 深色/金色 token；US 新增 `UsCard` 卡片外壳覆盖主要设置区块；设置窗口按维护者安全区在 60%～75% 之间浮动（默认 72%×66%），不盖满全屏；左侧导航现代侧边栏（品牌区/hover/active/金色 accent）。verify-local 14 门全绿，Dev/Release 0 警告。
- **Camera+ 参考（`4619062`）**：参考 Workshop Camera+ 设置界面，宽屏时增加右侧帮助面板（`UsHelpPanel`，≥1200px 显示，窄屏隐藏）。
- **UI 审阅结束（2026-08-30）**：本次审阅需求已整理到 `docs/us-ui-review-requirements-zh.md`（反馈原文 `docs/ui-feedback.md`，设计调研 `docs/ui-ux-research-zh.md`，实施计划 `docs/us-ui-implementation-plan-zh.md`）；**UiKit 容器化/控件基础设施列为最高优先**，衰减图作为可复用 UiKit 控件，帮助入口已确定移除内联 `?`、右侧帮助始终保留，待维护者确认后实施。
- **UI 帮助方案反馈（2026-08-30）**：维护者认为 Camera+ 右侧帮助面板优于内联 `?` 按钮 + banner；已记录到 `docs/ui-feedback.md`，待拍板后实施。
- **800×600 三栏帮助（2026-08-30）**：硬下限 800×600，右侧帮助始终保留，不使用帮助按钮；三栏宽度响应式收缩，方案见 `docs/us-ui-review-requirements-zh.md` R18。
- **UI 功能块分区反馈（2026-08-30）**：维护者认为三个功能块应各自独立连续滚动，包过滤器应属于包管理块而非全局顶部；已记录到 `docs/ui-feedback.md`，待拍板后实施。
- **Tuning Editor 交互反馈（2026-08-30）**：维护者希望 domain/action scope 改下拉菜单，mood 控件升级为 滑条 + 数值输入 + −/+ 微调复合控件；已记录到 `docs/ui-feedback.md`，待拍板后实施。
- **衰减图/功能块标题/组合式功能块反馈（2026-08-30）**：衰减折线图需重做；每个功能块要有标题作为导航锚点；维护者提出组合式功能块思路，需评估 UiKit 容器化以支持 XML 层级；已记录到 `docs/ui-feedback.md`，待拍板后实施。
- **未实机审阅**：`a4d5070`/`4619062` 仅通过构建/测试门禁，需维护者在游戏内确认视觉效果与交互。
- **后续 backlog 不变**：OB-02/03/04 + `docs/workdocs/` 移除 + Ferrite 游戏内稳定化 + 可选 Runtime harness。
- **UI 实施计划 Phase 0–2 完成（2026-08-30）**：内联 `?` 已移除（`faca4cd`）；右侧帮助始终保留、800×600 三栏响应式（`d8ecf09`）；功能块独立滚动 + 包过滤器归位（`5f09c39`）；Tuning Editor 下拉 + Mood stepper-slider（`b04aa53`，含 UiKit `OptionsBind` 与紧凑宽度支持）；衰减图改为 UiKit `chart/line`（`6121138`）；`docs/workdocs/` 已移除；verify-local 14 门全绿。待独立 review agent 审查与游戏内实机验证。

## Session resume checkpoint (2026-08-30c — UI 反馈修复批次 A–D 调度完成)

- **修复前排查 + 批次调度全部完成**：任务书 `docs/ui-fix-task-books-zh.md`；排查报告 `docs/ui-fix-triage-report.md`；行业调研 `docs/ui-ux-industry-review-zh.md`。`scripts/verify-local.ps1` 14 门全绿，Dev/Release 0 警告。
- **T2（G2/G3）**：`UiInteract.ToPageSpace` 改 internal；`DropdownWidget` popup 注册前转页面坐标，滚动视图内下拉正下方展开；`LineChartWidget` 滚动内拖拽命中用页面坐标；Forget Unavailable / Preset Import / Mood Auto 窄屏补可见按钮；新增滚动内 dropdown/line chart/ToPageSpace 测试。
- **T3（G1）**：`VoicePacksLayout` 新增动态行高基线（`MeasuredRowHeight`/`TwoLineRowHeight`/`VoicePackRowHeightFor`/`LayerRowHeightFor`）；BasicTuningWidget、VoicePackRow、Race/Xenotype 行、Scope/Preset 行均按 `ITextMetrics` 测量；新增纯逻辑高度测试。
- **T4（G4）**：新增中性 `SelectionButton`（底部/左侧高亮条，Palette token）；`ModeCardRenderer`/`DropdownWidget`/`UsSurface`/`ScopeTreeWidget`/`FilterBarWidget`/`AttenuationEditorWidget`/`PresetListWidget`/`VoicePackChecklist` 全部收敛到统一视觉；新增 SelectionButtonTests 与源码不变式。
- **T5（中低风险）**：Tuning Layer sticky；`ActionScopeRules` 动作分组（Autonomous/Operable）并默认隐藏 Crying/Giggling；race/xenotype 并列筛选 + 自动收窄；作者筛选下拉；无可用包 xenotype 黯淡显示；LineChart 可拖节点 hover 高亮；Tuning Editor 高度协调。新增纯逻辑与交互测试。
- **待办**：维护者游戏内实机验证（800×600 三栏、Tuning Editor sticky/dropdown、Mood stepper、衰减图拖拽、FilterBar 下拉）；Camera+ 分层帮助（M3）仍未实现，行业调研已给出建议；`docs/workdocs/` 已移除。

## Session resume checkpoint (2026-08-30d — Camera+ 分层帮助 + 标题去重落地)

- **维护者确认**：隐藏整个导航品牌区；Global Volume 隐藏卡片体内重复标签；分层帮助做完整版（总览+单项列表+悬停/选中联动高亮）。
- **隐藏**：`FerriteVoicePacksPage.DrawNav` 移除品牌区；`GlobalVolumeWidget` 支持 `HideBodyLabel`，`Layout.xml` 已声明；`UsCard` 新增 `TitleHidden` 能力（`UsCardLayout.MeasureBody` 零 Verse 高度计算）。
- **分层帮助**：`UsHelpCatalog` 结构化（`HelpSection`/`HelpItem`，保留 `Get` 兼容）；`UiPageState` 新增 `HelpHoverKey`/`HelpSelectionKey`；`UsHelpPanel` 显示分区标题、Overview、item 列表，支持 hover/selection 切换；新增 `UsHelpHighlight` 控件高亮；`VoicePacksPageState` 持久化 `HelpSelectionKey`。
- **覆盖**：模式卡、Global Volume、Attenuation、Basic toggles、Camera indicator、ScopeTree、PresetList、FilterBar、Race/Xenotype 行、VoicePack 搜索/行/Forget 均已接入 help key 与高亮。
- **测试**：`TestUsCardLayoutHeight`、`TestHelpCatalog`、`TestHelpPanelLogic`、`UiPageState` help keys reset、源码不变式（导航品牌区不绘制、GlobalVolume 隐藏标签、UsCard TitleHidden、InputModeRowWidget help hover 高亮）。
- **验证**：`scripts/verify-local.ps1` 14 门全绿，Dev/Release 0 警告。
- **待办**：维护者游戏内实机验证新增分层帮助 hover/选中联动与隐藏效果；`docs/workdocs/` 已移除。

## Session resume checkpoint (2026-08-30e — UI 首次打开故障排查与自动恢复)

- **现象**：打开设置页时 `us/ferrite-page` 与 `us/vanilla-fallback` 每帧 fallback，异常 `Value cannot be null. Parameter name: source`；关闭再打开可恢复（瞬态首次打开问题）。
- **处理（本地提交）**：
  - `5976b0e`：`UiGuard` 记录完整异常堆栈；对 `voicePackSelections`、`domain.Packs`、`GetVoicePackDomainPacks` 等可能 null 的 LINQ 来源加防御保护；修复 `UiGuard.ResetSessionLog()` 每帧调用导致的日志刷屏。
  - `790a993`：`FerriteVoicePacksPage` 首次绘制失败自动重置 `State` 并下一帧重试（等价“关闭再打开”），第二帧仍失败才 fallback。
- **验证**：`verify-local.ps1` 14 门全绿，Dev/Release 0 警告。
- **Gate R 后续事实**：新 Kernel Settings Host 首开、Basic 操作和关闭重开已由维护者实机确认无异常；若未来回归，现有完整堆栈日志仍可直接定位。

## Gate U migration checkpoint (2026-08-31)

- 首轮 Gate U 源码审查判定 `LIMITED/未通过`：完整 US Kernel Host 已接入，但暴露 section card 外框测量缺失、800px ScopeTree/FilterBar 窄屏几何缺陷、Kernel 异常帧可能与 legacy 同帧输出、保存 tick/legacy 生命周期耦合及自动/实机证据缺口。
- 已修复并验证：`UsSectionWidgetBase` 统一 card outer-height 与 body width，`UsScopeTreeWidget` 窄屏 layer stack 测量/绘制一致，`UsFilterBarLayout` + `UsFilterBarWidget` 在窄屏纵向堆叠 dropdown，custom Settings Window 保存 tick，异常后隔帧切换 fallback，设置窗口入口单实例化，Kernel-only close 不重置 legacy session。
- 新增 `tools/UniversalSqueakerKernelHostTests`：直接使用生产 `UsKernelSettingsHost.Create`、真实嵌入 `Layout.Schema2.xml`、真实 US Kind 注册与 typed binding 表；覆盖真实 Host 创建、创建期未知 Kind/属性拒绝、双 Host session 隔离、typed 业务路由、800/1280/1920 三视口 Measure/Draw、scope 清理和 disposed session。
- 证据：`pwsh -File scripts/verify-local.ps1` 15 门全绿；main Release 构建 0 warning/0 error；`tools/UniversalSqueakerKernelHostTests` ALL PASS；`tools/FerriteLib.UiKit.Tests` ALL PASS；`tools/UniversalSqueakerUiLogicTests` ALL GREEN。
- Gate U 自动证据判定 `PASS（stub/源码范围）`。维护者实机已通过 Basic 最小门：设置页正常开启、global volume 正常修改、attenuation graph 可拖动、关闭重开正常、无红字；这证明真实 Settings Host、Basic typed 写入、chart 原生拖拽和 session 重开在当前 dev 包成立。
- 外部 DeepSeek 收口与主代理复查记录位于 `docs/uikit-rebuild/reports/DEEPSEEK-US-UI-REBUILD-COMPLETION-REPORT.md`。首次复查发现 800 宽 Mood 控件被省略；返工后 `UsScopeTreeWidget` 改为统一的 stacked narrow Mood 几何：标题/Auto + Pitch/Volume/Jitter 三行，每行保留 minus/slider/number/plus，Measure/Draw 共用 `UsesStackedMoodRows` / `MoodRowHeightFor`。
- `MoodLayoutFocusedTests` 使用真实生产 Host 捕获原生控件 rect：800×600、2 个 rich Mood 行共 26 个控件，全部位于 scope-tree card 内且互不重叠；minus、plus、slider、number commit、Auto 均验证 typed `set-mood-tuning` 写入。主代理独立复现 Host `ALL PASS`、`verify-local.ps1 -NoRestore` 15 门全绿、`build-dev.ps1` Dev/Release 0 warning/0 error。
- Gate U 自动/源码范围当前 `PASS`；整体仍 `LIMITED/未通过`。Tuning/Packs/Camera Indicator 的真实 RimWorld 交互、真实 popup/chart/hotControl 坐标、catalog/翻译/数据路径、800×600/1280×720/1920×1080 实机记录和受控 fallback 恢复尚未验证。旧 Settings/Overlay fallback 继续保留，禁止 clean cutover；下一动作是维护者集中实机验收。


## Architecture audit and old-UI removal decision (2026-09-02)

- Maintainer ruling (durable): cut the legacy UI chain **before** in-game Gate U validation. Rationale accepted after audit: the legacy page is the weaker implementation (it still ships the unfixed narrow-width control-omission branch) and it anchors design debates. The Gate U in-game checklist still applies, but now against a single kernel path instead of two.
- Legacy UI measured surface: 72 files / 8,785 LOC. 58 files / 8,236 LOC are reachable only through `UniversalSqueakerSettingsWindow.DrawLegacyPage`. Business layer loss is zero: `VoicePacksPageModel`/`VoicePacksPageState`/`VoicePacksViewState` are the shared boundary the kernel already delegates every write through.
- Root structural finding: `fead57e` landed the greenfield kernel as 95 added / 22 modified (+16,811/-115) — a **parallel tree**, not the in-place rewrite `docs/uikit-rebuild/02-brownfield-cutover-matrix-zh.md` §2.2/§3.1 planned. Consequence still live today: two widget families register the same `us/*` Kind names, and `UniversalSqueaker.csproj` embeds Schema=1 plus both Schema=2 manifests despite the matrix rule "no half migration".
- The §8 clean-cutover inventory mandated by `docs/uikit-rebuild/tasks/EXTERNAL-DEEPSEEK-FINAL-RECOVERY.md` was never produced; the measurements above replace it.
- Relocation set (kernel compiles against these while they sit in legacy directories — move, do not delete): `FerriteLib.UiKit/Layout/UiElementSpec.cs`, `Metrics/ITextMetrics.cs`, `Metrics/UiFont.cs`, `Interaction/UiValueState.cs`, `Widgets/{UiKitFonts,Palette,UiText,SurfaceFrame}.cs`. The settings-window chrome depends on the last three (`UniversalSqueakerSettingsWindow.cs:135-174`); `UiKitFonts` is a kernel dependency and was misclassified as deletable in an earlier review pass.
- Shim set kept alive only by the diagnostics panel (migrate `SqueakDiagnosticsPanel` to `UiTheme`/`UiThemeDraw`, then delete): `UI/Components/{SectionFrame,UiPalette,EmptyState,StatusBanner}.cs`, `UI/Visuals/{UsSurface,UsVisualTokens}.cs`. All six self-describe as compatibility forwarding.
- Capability gaps present only in the legacy path (re-implement natively or drop by maintainer decision): sticky Tuning layer row, per-control help hover highlight, xenotype-row dimming at zero candidate packs; minor: `HideBodyLabel` unread by the kernel volume widget, author dropdown lacks an explicit `All` entry.

## FL 0.3.0 migration round record + closed pointer lines (archived verbatim from `HANDOFF.md` §1-§6 and `TODO.md`, 2026-09-09)

- Reason: the migration round and FL→US round 2 are fully landed (round 2 CLOSED by FL; durable facts live in `MEMORY.md` - gate 14, packaging construction, carrier lockstep - and in git log `7777cbe`/`1a8dd51`/`445e138`). `HANDOFF.md` was rewritten as a session-to-session buffer; the superseded state record and the closed TODO pointer lines are byte-copied here per the archive convention. Watch one internal pointer: the archived HANDOFF text references "§5 记逐条判定" style section numbers of the OLD file layout, and its §6.4 round-2 closure narrative is now history (FL closed it).

### HANDOFF.md sections 1-6, verbatim

# Universal Squeaker — Handoff（FL 0.3.0 迁移轮 · 状态记录）

> 本文件原本是 **FL 0.3.0 迁移轮的执行面**（M1–M6 + §2 边界 + §3 验收）。迁移包已于 2026-09-07c 全部落地、§3 逐条验收通过，按 §3 尾条改写回**状态记录**：下面不再是待办指令，而是这一轮做了什么、留下什么。
> 权威顺序不变：代码 > `AGENTS.md`/`MEMORY.md`/`TODO.md` > 本文件。对侧 `../ferritelib/HANDOFF.md`：US→FL round 1 已 CLOSED；**FL→US round 2（打包脚本 S1–S6）由 FL 同日开出，US 已全量处置**（§4 记原委，§5 记逐条判定与实测）。

## 1. §0 裁决前提：仍然有效的部分

- 术语「renderer backend」= Unity IMGUI / Verse Widgets；全仓无 Dear ImGui。**继续有效。**
- **豁免一（dev 诊断面板）**：白名单条目暂存，但角色裁定已两轮翻新（09-08 逃逸面 → **09-09 撤销**）：面板全量迁入 UiKit 后该条目**退役**（白名单 2→1，only-shrink 棘轮兑现）。迁移前它仍是常驻豁免；细节与执行项见 §7。金丝雀位 = mod settings（每个玩家必经；其 UiKit 故障原样暴露，不许加固）——该条 09-08 裁定继续有效。
- **豁免二（相机指示器 legacy fallback 段，冻结）**：继续有效；本轮只按 M5 改了它的 `EventType.Layout` 帧门行（§0 明文允许，不算增内容）。段的去留仍是 Knife 3 的维护者决定。
- **裁定三（窗口 chrome）**：**已闭合**——chrome 归库壳 `UiWindowHost`，消费者侧不再有 chrome 豁免资格，窗口文件本身已从白名单消失。
- 边界门只减不增、两侧白名单逐条一致：**继续有效**，且本轮就是它把白名单从 5 条收缩到 2 条的落点。

## 2. M1–M6 落地形状

| Canonical | 结果 |
|---|---|
| **M1 / P1** | `UI/UsHoverProbe.cs` 删除；四处 hover 直读 `UiNative.IsMouseOver`（窗口那处随 M2 一并消失）。gate 14 的 `hoverCalls` 期望改为 **0**，探针存在性断言删除，白名单删 `UI/UsHoverProbe.cs` 与 `CompSqueaker.cs` 两条 |
| **M2 / P2** | `UniversalSqueakerSettingsWindow : UiWindowHost`（251 行 → 148 行）。`Theme`/`Title`/`Subtitle`/`CloseText`/`CreateHost`（含 `UsTextFitAudit.Begin()`）/`DrawNotice`（两通知合并、按 `UiWindowNotice` 分支）/`PrerequisiteVerified`/`BeforeDraw`（只剩 `TickSettingsSaveForWindow`）/`OnDrawFailure`（`SqueakLog.SettingsOpenFailed`）/`InitialSizePolicy`（60–75%、800×600 clamp 原样留在 US，条件 a）。**已删**：`DrawBackground`/`DrawTitleBar`/`DrawCloseButton`、`pageUnavailable`/`noticeDueNextFrame` 状态机、prerequisite 早退分支、`Margin`/`InitialSize`/`DoWindowContents` 覆写、`kernelHost`/`kernelSource` 字段；`PreClose` 只留 `UsTextFitAudit.End()` + 基类链。`Mod.cs` 注册/flush 链未动（按 `Window` 工作） |
| **M3 / P3** | grace 机入 session：`UsKernelSettingsHost` 建 host 后设 `host.Session.HoverGraceFrames = HelpHoverGracePasses(15)`；`UsKernelDraw.HelpHover` 改 `ctx.Session.ClaimHover`；读取侧（`UsHelpPanelWidget.ResolveFrameDisplay`、`UsSectionWidgetBase.IsHelpSelected`）读 `ctx.Session.HoverClaim`，`"<section>/..."` 前缀与 StartsWith 规则不变。**已删**：`help-hover`/`set-help-hover` 两条 binding、`VoicePacksPageState` 五个 hover 字段（含 `HelpHoverKey`）与 `Reset()` 对应行、`VoicePacksPageModel.SetHelpHover`/`BeginHelpHoverFrame`/`HoverGraceFrames`、`IUsKernelSettingsSource`/`UsKernelSettingsSource` 两条转发、窗口的 `BeginHelpHoverFrame()`。`SectionHelpKey` 业务解析留在 source |
| **M4 / P4** | `UiBindings.ActiveTabKey` 替换 6 处 `"active-tab"` 字面量（`UsKernelSettingsHost:128`、`UsSectionWidgetBase:68`、`UsNavWidget:68/92`、`UsPageTitleWidget:39/108`）+ 三处 doc 提法。`set-tab` 是 action 名，未动 |
| **M5 / P6** | `UsKernelOverlayController:69` 与相机 patch `:31` 的帧门 → `UiNative.IsLayoutEvent()`；overlay controller 退出白名单 |
| **M6** | `PrerequisiteApiMin/Max` → `[0.3.0, 0.4.0)`，M1–M5 全部落地后作为本轮最后一步写入；开工红灯（`prerequisite range tracks the compiled FerriteLib Api`）随该车道改判而转绿 |

M6 顺带修掉的一处 gate 形状：该车道原先把 `0.2.0` **复写**在测试里，门禁其实是在和自己的常量一致。现改为从 `Mod.cs` 解析区间并断言三件事——carrier Api 落在区间内、窗口恰为一个 minor 宽、floor 等于所链 Api（stub 无 `Verse.Mod`，所以类型反射不可行，源码是唯一可读面）。

gate 14 终态实测：`scan: 101 files; 2 file(s) hold backend calls; raw Mouse.IsOver = 0; whitelist 2 entries`，两条各 exempt（24 / 1），**无 NOTE**；自测仍在每次运行前置（看不见 code hit 就直接红）。三种失败面各自验过：白名单外命中、豁免文件消失、raw `Mouse.IsOver` 非 0。

## 3. §3 验收结论

- `pwsh -NoProfile -File scripts/verify-local.ps1` → **14/14 OK**；Dev/Release 各 **0 警告 0 错误**。
- kernel-host harness **34 道 Step 全绿**，本轮新增/重写的四条：`the window shell carries the next-frame trip and the two notices`（M2：抛错 pass 内不出通知、下一 pass 才红、此后不重试、两通知可分、`PreClose` 由壳 dispose session）、`hover claims release to the overview only after the D10 grace window`（M3：改跑 session 时钟，pin 15 与「一次 `DrawFrame` 恰推进一 pass」）、`control hover claims help through real pointer passes` 与 `distance card draws its bands filled and disjoint`（改读 `HoverClaim`）。
- §3 点名的「在 harness 强制 `CreateHost` 抛错验证 next-frame trip 由壳完成」已做：真窗口在 harness 里构造不出来（stub 无 `Verse.Mod`），故用一个最小 `UiWindowHost` 探针驱动壳本身，页面用真实 overlay host；消费者侧「不得重建 chrome/状态机」由 `UiSourceInvariantTests` 反向钉住。
- 文档与计数同轮修正：`README.md`、`README.zh-CN.md`、`CONTRIBUTING.md`（中英）、`MEMORY.md`（gate 14 事实改写为 2 条白名单 + 零直读、D10→`UiSession`、carrier 区间与「pin 从源码读」纪律、源码集重测 101/15,566）、`verify-local.ps1` 头注释。日期化历史记录未改写。
- 源码集：101 files / 15,566 lines（迁移前 102/15,751）。
- **实机冒烟未做（本机无 RimWorld）**：已并进 `TODO.md` 的合并冒烟项。两处要看的真实差异：(1) chrome 标题/副标题现由壳以 `singleLine: true` 绘制，贴合审计对这两条走宽度轴——若某语言标题报 `ui.text.overflow(width)`，那是壳的带子规格而非 US 回退；(2) 切换工作区不再立即清空帮助悬停解释（旧 `ApplyActiveTab` 里那句 `state.HelpHoverKey = ""` 随字段一并删除），改由 session 的 15 pass grace 释放——这正是 D10 裁定的机制本身，但 `scroll-to` 引发的切页若发生在非导航区，面板会多留住上一条解释 ≤0.25 s。

## 4. 库侧往来：迁移零缺陷，round 2 由 FL 主动开给 US（打包脚本）

**迁移本身未开 round 2**：只用库的既有公开表面，生产代码零绕行。唯一需要改的是**测试注入口**——FL item C（`0cf397d`，per-widget 恢复归引擎）使「widget 抛错」不再升到 `UiHost.DrawFrame` 之外，overlay「整帧失败不得双扣行高」车道的旧注入因此失效。这是 FL 明文设计且有自家正反车道证明（`KernelWindowHostTests.VerifyWidgetFailureIsRecoveredBelowTheShell`、`KernelLayoutTests` 的 Clip 恢复 + group 深度归零），属契约变更而非缺陷；处理是把失败上移到引擎仍未包裹的帧级路径（`UiHost.Draw` 末尾的 session popup pass），断言目标一字未松。

**FL→US round 2（同日，打包脚本 S1–S6）已由 FL 开出**，起因是 FL 在给自己收打包证据时发现 S1 那条规则先坏在 FL（`f2f4dd0` 已修），于是把抓出问题的检查交给消费者。逐条处置见 §5。

## 5. FL→US round 2 处置（S1–S6，同日执行）

| 项 | 判定 | US 落地与实测 |
---|---|---|
| **S1** `build=` 是声明不是测量 | **成立**，且我今晚刚被它咬过一次（第一次 stage 到的 md5 与随后 `--no-incremental` 强制重建不同） | 复现：`verify-local` → 旧 `pack-dev` ⇒ `version.txt: build=dev`，包内 DLL `AssemblyConfiguration=Release`。修：新增 `scripts/read-assembly-stamp.ps1`（子进程读 stamp；不用 `MetadataReader`——Store 版 PowerShell 的 `PEReader` 无 `GetMetadataReader`），stager 与 gate 6 共用**同一实现**；`stage-package` 按通道拒收（负控实测：Release 字节走 dev 通道被 `throw` 拒）；`pack-dev` 自己 `-c Dev --no-incremental` 建它承诺的 flavor（不再靠调用顺序）；`build-dev` 建载主 `-c Release --no-incremental` |
| **S2** dev 归档形状错 | **成立** | 旧 dev zip 条目实测以 `1.6/`、`About/`、`LoadFolders.xml` 开头（解到 `Mods/` 会摊一个散 `LoadFolders.xml`）——正是 `release.yml` 长注释解释并绕行那个形状的地方。修：dev 默认**不出归档**（`pack-dev -Zip` 才出），形状规则移进引擎 |
| **S3** 四处 strip 做一件构造就能免的事 | **成立** | `About.xml` 按**单文件**复制（`PublishedFileId.txt` 从任何通道都无入口，strip 步删除）；内容根 `1.6/` 保持开放（不 allowlist），构建碎屑在复制时排除、事后**断言不存在**；稳定部分（根 + `About/` + 那一个 DLL）做**封闭集合**断言，入侵者判红而非删除。实测：仓库里放 `About/PublishedFileId.txt` + `1.6/Assemblies/stray.pdb` 后 stage ⇒ 包内两者皆无、7 文件；另一次故意把内容根摊到包根 ⇒ 封闭集合当场点出三个多余文件 |
| **S4** 两个写者两个名字一个没人能复现的摘要 | **成立** | 归档写者进引擎，唯一实现：条目名归一 `/`、排序、每条 mtime 钉到被打包 commit 的 author date、根目录恰一层 `UniversalSqueaker/`；`-ArchiveName` 让资产名仍由知道 tag 的人定（`UniversalSqueaker-<tag>.zip` 是对账目标）。实测：同一 payload 相隔 3 秒两次出包 sha256 相同（`52D80B57…`），而修之前根条目用「现在」时间戳，两次不同。`release.yml` 的内联 `Compress-Archive` 与那条绕行注释删除 |
| **S5** 载主门只看存在不看字节 | **成立**（它原先连 label 都在撒谎：`Invoke-Check` 第二参只是显示用的 retry 提示） | gate 6 改为存在 + stamp==Release，失败信息给出可执行的重建命令。选择**在 US 自己实现 reader**，不要求库新开公开面（reader 是打包侧工具，不是库契约） |
| **S6** `US_STEAM` 有码轴无构建轴 | **成立**，无需改码 | 已记 `TODO.md`：不可达分支，别当 bug 排；真要接 Workshop 通道时走同一引擎加一个 `-BuildFlavor` 值，不再长一个 packer |
| FL 附带的 cross-repo 提醒 | 部分已过时 | (1) `UiPanel` 幻影禁名：在 **gate 侧补齐**而非从 MEMORY 删——`UiPanel` 确是旧链里真存在过的类型（`Widgets/UiPanel.cs`），只是从没进过扫描表；现在 MEMORY 与 gate 说的是同一件事（六名）。(2) workflow 大小写：实测两处已是 `Coahuilite/FerriteLib`。(4) 0.3.0 迁移：本文件 §2 已完成。(5) `dependency-reality.ps1` 规则 (c)：留作 gate 14 的候选，不在本轮引入 |
| 顺手修掉的一处自伤 | — | `pack-dev` 旧流程在 stage 旁留一个空 `.txt` 标记（与 `version.txt` 重复，且诱导人从文件名读身份）；改为清掉历史遗留，身份只从包内 `version.txt` 读 |

## 6. 遗留（不属于本文件）

1. 实机冒烟（两轮合并，维护者本地清单）。**打包流程变了**：dev 包是一个目录，直接放 `Mods/`，不要再找 zip；包内 `version.txt` 现在多一行 `carrier=<stamp> <informational>`，进游戏前先核它是不是 `Release 0.3.0-dev+…`（对不上就先 `dotnet build ../ferritelib/… -c Release --no-incremental`），而包内 DLL 的 flavor 已由引擎测量，`build=dev` 现在是真的。
2. 发布轴：US `<Version>`/`About.xml <modVersion>` 维持 0.2.0 冻结；carrier 0.3.0 标签与 US 下一个 rc 的配对发版是维护者决定（FL 的标签在等本轮）。
3. Knife 3（相机 fallback 去留）、dev 诊断面板迁入 UiKit 后豁免一退役（裁定链与执行项见 §7）、`UiSessionGuard` 的 IMGUI group 栈血统风险（归 FL 自家收口）。
4. FL→US round 2 的**关闭动作在 FL 侧**（缓冲区生命周期：裁决追加在提出方的文件里）。2026-09-08 US 会话对 S1–S6 做了**全量复核**（不看旧记录、每项重跑复现：S1 dev 通道拒收 Release 字节当场测得；S3 埋 `PublishedFileId.txt`/`stray.pdb`/`codemap.md`，前两者被构造挡掉、第三者被封闭集合点名判红；S4 同 payload 两次出包 sha256 一致且条目 mtime 钉在 commit author date；S5 载主改 `-c Dev` 后 gate 6 当场变红、Release 重建后转绿；终态 `verify-local` 14/14），裁决已按生命周期追加进 `../ferritelib/HANDOFF.md` 的 `### US re-review (2026-09-08)`，并明确 FL 可标 CLOSED。`TODO.md` 挂指针直到 FL 完成 body 修剪。同轮编号对齐：US 自己开的 N1–N3 那轮被 FL 按全局计数器规则改名为 **round 3**（本文件旧提法与 `TODO.md` 的 "US→FL round 2" 已全部改标）。

### TODO.md closed/landed pointer lines, verbatim (prefix = line number at extraction time)

8: - **Cross-repo round CLOSED on both sides (2026-09-07) — US→FL round 1**: P1-P6 + A-E landed in `../ferritelib` (carrier Api 0.3.0), and the US consumer half (the FL 0.3.0 migration package, `HANDOFF.md` §2 M1-M6) landed the same day: probe deleted, chrome into `UiWindowHost`, the D10 claim machine into `UiSession`, `UiBindings.ActiveTabKey`, both frame gates on `UiNative.IsLayoutEvent`, pin written back to `[0.3.0, 0.4.0)` as the last step. Gate 14's US whitelist is now 2 entries with a zero raw-hover assertion. Nothing on the migration itself needed the library. What is left is not code: the carrier 0.3.0 release tag + US's next rc pairing is a maintainer decision (FL holds its tag until this commit; §2 forbids touching the US version axes).
9: - **Cross-repo round — FL→US round 2 (2026-09-07): packaging scripts. US DONE + RE-VERIFIED 2026-09-08.** Buffer section `## FL→US round 2` in `../ferritelib/HANDOFF.md`, items S1-S6; FL fixed S1 in its own tree first (`f2f4dd0`) and offered US the check that caught it. **S1-S5 executed in US** (measured reproduction and controls in `HANDOFF.md` §5), **S6 needs no code** (recorded below as a dead-until-wired axis). On 2026-09-08 US re-ran every item as a fresh reproduction on `2324d5f` (scripts identical to landing commit `1a8dd51`): S1 dev-channel refusal measured, S3 planted intruders caught by name, S4 two packs agree byte for byte, S5 gate 6 red against a Dev carrier, `verify-local` 14/14. **The verdict is appended in FL's buffer (`### US re-review (2026-09-08)`); the remaining action is FL's: trim the body to CLOSED per the lifecycle.** This pointer stays until that trim lands.
12: - [x] ~~US `HANDOFF.md` §1 items 1-6 (seam round)~~ **EXECUTED 2026-09-07b** (dev session): chrome close button `UiNative.Button`; hover behind a then-new `UI/UsHoverProbe.cs`; checkbox off `DrawBoxSolid` onto `UiThemeDraw.Surface`; `VoicePacksPageState` scroll fossils deleted; boundary gate landed as verify-local **gate 14** (self-testing scanner, append-only gate numbers). Its 5-file whitelist and the `[0.2.0, 0.3.0)` pin were both superseded the same day by the migration above - the only-shrink ratchet is what made that shrink an expected outcome rather than a renegotiation.
15: - [x] ~~CI first real run~~ **GREEN 2026-09-07** (run `34075213342`): carrier checkout + full-tree sibling staging + 13/13 gates + privacy audit CLEAN. Two runner-only defects found and fixed on the way (DLL-only staging broke gate 13's in-place stub build; missing `* text=auto eol=lf` would have broken gate 10's byte-exact licence compare on a CRLF checkout) - both proven by a clean worktree simulation before re-push.
16: - [x] ~~Branch protection / public flip~~ **DONE 2026-09-07**: repo flipped public on maintainer authorization (collaborator download need; full-history privacy audit CLEAN immediately before). Protection applied same session in the carrier-minimal shape (force-push off, deletion off; PUT requires explicit `"restrictions": null`). Anonymous download of the rc1 asset verified byte-exact against the server digest.
17: - [x] ~~First US rc tag~~ **CUT 2026-09-07**: version axis 0.2.0 (aligned with carrier), `v0.2.0-rc1` prerelease live, six-point reconciliation green after two re-cuts (asset name, then zip shape - both fixed in release.yml, 0 downloads each time so the collaborator never saw a bad artifact). Release.yml's first real execution proved the whole chain end to end.
19: - [x] ~~Cross-repo hash duty~~ **CLOSED by maintainer ruling 2026-09-07**: stop chasing hashes; existing history is never rewritten for them. Dangling citations are archaeology (MEMORY "Hash archaeology - CLOSED").
24: - [x] ~~Hash re-pointing of live docs~~ **CLOSED by the 2026-09-07 ruling**: no further hash maintenance anywhere; `OBLIVIONIS.md`/`docs/review/**`/live docs all keep whatever hashes they carry.
25: - [x] ~~Pre-existing hash rot in cold docs~~ **CLOSED by the same ruling**: archaeology, not damage; the mirror chain stays available but unused by default.
## Diagnostics round-9 audit + build + FL cross-repo submission (archived verbatim from HANDOFF.md sections 1-3, 2026-09-10)

- Reason: the round-9 migration is CODE-COMPLETE and merged to 0.3.x (e9e6a34). The nine ruling rounds, the over-design audit, the close-button evaluation, the round-9 build contract and the FL session's cross-repo submission are finished work - durable facts live in MEMORY.md (gate-14 one-entry whitelist, devpanel rules bullet) and the open action surface is in TODO.md (live walkthrough). HANDOFF.md is rewritten as the post-build walkthrough buffer; the superseded body is byte-copied below per the archive convention.
- Watch while reading: the text is era-faithful - its section numbers reference THAT HANDOFF layout; the "pending ruling" markers were answered by the later rounds that follow in the same body; the acceptance line's "two modes" predates the master-detail rework those very rounds ruled.

### HANDOFF.md sections 1-3, verbatim

## 1. 现行交接：诊断面板迁入 UiKit（devpanel；下个会话从设计讨论开始，逐项裁定后再动工）

**裁定终局（2026-09-09，历经三轮反转，完整链条已记 `MEMORY.md` gate 14 行；已作废立场勿复活——尤其"面板独立成纯 Verse 逃逸面"与"零库依赖棘轮"）。** 面板是与设置页同等的 UiKit 消费者：壳 `UiWindowHost` + 页面 `UiHost` + `<Scroll>` + 注册 kernel widget；故障显示走库自己的机制（`UiSessionGuard` 的 `RecoveryBand` 行 + 壳的 next-frame `pageUnavailable`），数据面（`usdiag` 文件日志）本就在 Kernel 纯度规则下不依赖 UiKit。金丝雀位 = mod settings，其故障原样暴露、不加固件。

**现状事实（已实测）**：`SqueakDiagnosticsPanel.cs` 703 行、gate 14 免检 24 处；`:4` `using FerriteLib.UiKit.Kernel`、`:37` 静态 `UiTheme.DarkGold`、13 处 `UiThemeDraw`/`UiFont`/`UiStatusTone`——迁移是在既有耦合方向上继续走，不是先拆后建。ctor（`:75-91`）自设非模态四件套 + `draggable=true` + `doCloseX=true` + `closeOnCancel/Accept=false` + `onlyOneOfTypeAllowed` + `focusWhenOpened=false` + `onlyDrawInDevMode=true`；`WindowOnGUI` 做拖出钳制（KeepGrabPx）；`OnCancelKeyPressed` 做双 Esc 武装（3 s）；`PreClose` 调 `SqueakDiagnosticsOverlay.NotifyPanelClosed()`。内容侧：Selected 模式 = 17 gate 三态行 + pawn/action/audio 文本，Visible 模式 = ≤16 pawn 行（点标 + 列），文本仅在 `Overlay.Revision` 变化时重建。

**壳的表面（FL `487d80a` 实测）**：sealed 仅 `Margin`/`InitialSize`/`DoWindowContents`；必写 virtual = `Theme`/`CreateHost`/`DrawNotice`，可选 virtual = `Title`/`Subtitle`/`CloseText`/`InitialSizePolicy`/`PrerequisiteVerified`/`BeforeDraw`/`OnDrawFailure` + chrome 几何（`TitleBarHeight`/`SidePadding`/`AccentBarHeight`/`CloseButtonSize`/三个 Font）。modality 与 `draggable` 壳不碰（ctor 注释明文归消费者）；`PreClose` 是 override，子类链调即可。**迁移不需要 FL 开任何新表面。**

**第一轮裁定（2026-09-09b，维护者；讨论产出，逐项照此执行）**：
0. **需求基线（早期既定，本轮复述为权威）**：Selected 模式 = 指定 pawn 的**全部门控 + 计时器 + 发配音频及其所属 VoicePack**；Visible 模式 = 屏幕可视范围内全部 pawn 的**简要诊断**。两模式是硬要求，内容审计以此为尺。
1. **控件落位**：`BeforeDraw` 标题栏手绘**被否决**。mode badge 与 s/t 秒位切换做成 **US 自有 widget**，进页面内容首行；实现中若证实某控件本该由 UiKit 提供，先讨论、再向 FL 提提案，不私改库。
2. **关闭钮**：维护者判断关闭控件"很应当是 lib 组件类型"→ 出评估：论证壳现给的 `CloseText` 文本钮之外，一个组件级关闭钮（独立公共组件形态）是否**足以立为 UiKit 公共组件**；不足以支撑则 US 自有实现。双 Esc 武装无论结果如何都留在 `OnCancelKeyPressed`。
3. **数据通道**：维持——窄 typed bindings，`Overlay.Revision` 映射 session `ContentRevision`（Cache clock rule 硬约束）。
4. **清单形态**：嵌入 XML manifest 判定为 UiKit 保留式时代之前的**疑似遗留**，面板**不复制**该仪式：程序构造 spec。依据：gate 11 只断言两份已发布 manifest（settings + overlay）的存在/良构/归属，不要求每页一份；键引用扫描走源码字面量，程序构造不脱检。设置页既有 manifest 面本轮不动。
5. **harness 车道**：按**审计后的定稿项目集**建满（假快照注入、两模式遍历、revision 重建断言、抛错→RecoveryBand、关窗→NotifyPanelClosed 联动）。
6. **过度设计审计（新增前置工序）**：SR 旧面板项目按裁定**多为过度设计、不恢复**（上轮"恢复 SR 诊断面"的理解作废，SR 仓无需读取）；现重建面板项目集为基线，逐项对照 §0 需求基线审计——含核实 `cachedAudioText`（"Dispatched audio"行）是否已携带**包归属**、计时器呈现是否完整——清单交维护者逐项裁定，然后才定 widget 集、spec 面与 §5 验收面。
7. **同 commit 收口**：不变——gate 14 白名单 2→1（删豁免一条目）、`MEMORY.md` gate 14 行与 `CONTRIBUTING.md` 第 6 条"两个豁免"表述同步、crash-lineage 面板半边销账。

**第二轮产物：过度设计审计清单（dev 会话备好 2026-09-09b，待维护者逐项批注）**。逐组结论：
- **A 窗口行为**（非模态四件套 / KeepGrabPx 拖出钳制 / 双 Esc 3s 武装 / onlyOneOfType / PreClose→NotifyPanelClosed / onlyDrawInDevMode）：全部保留；迁移落点 = `UiWindowHost` 子类的 `WindowOnGUI`/`OnCancelKeyPressed`（壳只封 `Margin`/`InitialSize`/`DoWindowContents`，钳制与双 Esc 放得下，零新面）。
- **B 标题与控件**：静态标题、mode badge、s/t 切换、Ready/Blocked 汇总徽章 → 保留（badge/s 按 §1.1 进 US 自有 widget 内容首行）；底部 CloseHint 独立 22px 提示行 → **删除**，语义并入 CloseText（§1.2）。
- **C Selected**：pawn 身份行、Current action 行 → 保留；**G5 Action gate = 死行**（外部动作永不成为 `CurrentTimingAction`，该行恒 Pass/N/A，代码注释自认 "kept as a distinguishable chain entry"）→ 建议删除、语义归 `usdiag` 日志；G3 Plan（恒 Pass、仅 N/A 态）、G4 Identity（仅 external 触发时活）→ 保留或并入他行待裁；"Dispatched audio" 行与 G16 值列在新派发时刻渲染同一 `pack : sound` 串（前者取 `LastSignificantOutcome`、后者取 `LastEvaluation`）→ 建议归一：G16 只留三态，音频归属单点在 C 行。补强①（计时器完整性）：G10/G11 值列 "remaining/total"（`SqueakTimingEvaluation.ActionIntervalTicks/Seconds` 已在快照，零数据成本）；补强②（音频所属包完整性）：vanilla/fallback 派发 `PoolStableKey` 为 null，现显裸 sound 名 → 加 `SqueakSoundSource`（XenotypePack/RacePack/Vanilla）tier 显示。
- **D Visible**：列集（dot|pawn|action|action+global 冷却|audio|ready）符合"简要诊断"基准，保留；**`MaxVisiblePawns=16` 与"可视范围全部 pawn"基准冲突** → 提额或保留截断+可见计数，待裁；增强候选：行点击 → 选中该 pawn（两模式联动的闭环）。
- **E 引擎层**：revision 驱动重建、0.25s/0.5s 刷新节奏、逐 pawn fail-closed → 保留，由 §1.5 harness 车道钉住。

**关闭钮评估（§1.2 返回结论：不向 FL 提案）**：库壳已持有关闭钮（`CloseText` 文本钮 + `CloseButtonSize` 几何），且 `doCloseX=false` 正是壳"双关闭钮是缺陷"的裁定——面板与设置页两消费者都已用壳钮，"壳外独立公共关闭钮组件"的消费者数 = 0，按 n≥2 校准纪律不足以立为 UiKit 公共组件。若面板日后要在内容层再加关闭入口，用现有 `UiNative.Button` 缝组 US 自有按钮即可（零新公共件）。"标题栏 × 图标"形态属 chrome action slot 议题，与 round 4 素材同池，本轮不开。

**第三轮（2026-09-10 维护者批注）**：
- **交互模型重构（裁定，覆盖 §1.1 与审计 Q7 原案）**：全局视图为默认页；游戏内选中 pawn → 面板自动展开该 pawn 详情视图；**mode badge 作废**（"主动切换"被 drill-down 取代，s/t 切换与锁钮为仅存的 US 自有控件）；面板支持**锁定**：锁定后摄像机移开该 pawn 仍持续跟踪。引擎改动点：① `RefreshSelected` 的 `view.Contains` 门对锁定 pawn 解除（快照 = comp 层纯读，离屏单 pawn 成本 = 0.25s 一次采样，可忽略）；② 锁定与"跟随选择"的交叠语义**待裁**（提案：锁定即停跟选择，直到解锁/pawn 死亡/离场）；③ G2 "On screen" 在锁定离屏时如实恒显 Block——生产语义不撒谎，保留；④ 入口 `SqueakDebug.OpenSelectedDiagnostics` 已天然兼容（无有效选择即 Visible），改为"开会话 + 视图状态机"微调；⑤ 行点击 → `Find.Selector.Select` 保留为列表视图的 drill-in 输入方式；⑥ §1.5 harness 车道增：锁定跟踪（离屏不断更）、解锁回跟随、drill-in 联动。
- **Q4 素材已查（"总时长是否可变"）**：**可变，且可变本身是生产行为**。有效 interval = 配置 ticks × 分层表动作乘数 × 异种 overall × 全局 master（逐级取整）× 时间速度（tick 钟且 `ScaleCooldownWithTimeSpeed` 开）× 可闻人口 scale（≥1）；其中时间速度与人口两轴**随运行时变、玩家不可见**——3x 速度或人口多时总时长被拉大正是"为何迟迟不发声"的直接证据，故"剩余/有效总时长"有诊断价值非装饰。数据零成本：`Timing.ActionIntervalTicks/Seconds` 已在快照。**命名警告（实测）**：`BaseTiming` 不是"调音前基线"——同一 context/delta 照乘全部调音乘数，仅 `periodicScale=1f`，其真实语义是"未含人口拉伸的值"；UI 若展示第二对比串不得叫"基线"。建议：首批只显示"剩余/有效总"，`BaseTiming` 对比缓上。
- **Q1/Q2 重述后待裁**；Q3/Q5/Q6 继续待批注。

**第四轮（2026-09-10 维护者批注·续）**：
- **Q7 再重构（覆盖第三轮 ①②）**：主窗 = **左列表栏 + 右详情栏** 的 master-detail 单窗；选中 pawn（游戏选择或点击列表行）→ 右详情栏展开其详情；**锁定 = 详情独立化**——该 pawn 的详情脱离为一个独立详情窗，持续跟踪（含摄像机移开/离屏），直到解锁或直接关闭该详情窗（关闭即自动解锁）。会话语义：主窗关闭 → 整个诊断会话结束，连带关闭全部独立详情窗；独立详情窗 = 第二个 `UiWindowHost` 壳实例。开放子语义（提案默认，若无异议即照建）：(a) 允许多 pawn 同时锁定，overlay 以 locked 集合跟踪，单 pawn 成本 = 0.25s 一次纯读快照；(b) 存在锁定独立窗时，主窗右栏仍跟随实时选择（锁窗钉死自己的 pawn，主窗不失去浏览能力）。
- **G3 保留（裁定）**，且 N/A 态分配**白色**——审计顺带查实：现实现把 N/A 文本挂在 `GateState.Pass` 上渲染成**绿色**（语义错误，"不适用"不该长得像"通过"），裁定即修法：新增第四态 `GateState.NA` → `UiStatusTone.Neutral`/白。
- **G4 现实现内容（供裁定）**：激活条件 = 当前动作的计划为 External 触发模式（`s.CurrentTriggerMode == SqueakTriggerMode.External`，数据源 `plan.Mode`），否则整行 N/A；激活时检查三条件聚合：`pawn.IsPlayerControlled && !pawn.Downed && pawn.Awake()`，值列仅显 "Pass"/"Blocked"，**不细分被哪条挡住**。增强提案（待裁）：Blocked 时值列列出失败条件（如 `Blocked: Downed`）。
- **G5 删除（裁定）**：死行确认成立，从面板移除，语义保留在 `usdiag` 日志。
- **Q4 通过（裁定）**：G10/G11 值列 = "剩余/有效总时长"，`BaseTiming` 对比缓上、命名陷阱记录在案。
- 仍待批注：Q3（"Dispatched audio" 行与 G16 双写归一）、Q5（无包键派发的 source tier 标注）、Q6（Visible 16 上限三选一）。

**第五轮（2026-09-10 维护者批注·续 2）**：
- **折叠横条（Q7 模型扩展）**：主窗与每个独立锁定详情窗都支持**收缩成横条**状态。主窗横条 = 实时显示**一条正在更新的 pawn 状态行**（语义提案：最近一次快照 revision 变化的那个 pawn，行格式同左列摘要行；若维护者另有意图此处待纠）；锁定窗横条 = 只显示自己那个 pawn 的摘要行（同主窗左列行格式：dot|pawn|action|冷却|audio|ready）。展开交互：点击横条（或横条上的展开件）恢复全窗；折叠态随会话生命周期（主窗横条关闭仍 = 会话结束连坐锁窗）。
- **实现经济**：摘要行做成一个 US 自有 widget，一物三用——主窗左列表行、主窗横条行、锁定窗横条行同组件；每窗折叠状态为窗口私有 UI 态，overlay 不感知。
- **库边界备忘**：`UiWindowHost` 的壳 chrome（标题栏）无"隐藏"表面，折叠态若保留壳即"带标题栏的小横条"。**先按带壳实现**；若实机走查判定横条必须无壳（纯浮条形态），那才是壳级新面诉求 → 攒 round 4 素材走独立提案，不预先开面。
- **G3/G4 批准（含 G4 增强）**：G3 保留 + `GateState.NA` 第四态白色；G4 保留 + Blocked 值列细分失败条件（`Blocked: Downed` 式）。
- **G5/Q4 复确认**：G5 删行（门链 16 行）；G10/G11 = "剩余/有效总时长"，BaseTiming 缓上。
- 仍待批注：Q3（"Dispatched audio" 与 G16 双写归一）、Q5（无包键派发加 source tier）、Q6（列表 16 上限三选一）。

**第六轮（2026-09-10 维护者批注·续 3）**：
- **快照监视器语义确认**：主窗横条 = 最近一次 revision 变化的 pawn 摘要行，按第五轮提案照建。
- **Q6 裁定（覆盖原三选一）**：可视 pawn **不被上限拦截**——追踪集扩为视口内全部带 `CompSqueaker` 的 pawn（删 `MaxVisiblePawns=16` 追踪闸门；快照 = 纯读，0.5s 一轮，规模 50-80 只无压力）；**列表每页 8 条翻页**（页指示 "n/N · 共 M"，上/下页钮）；**pawn 名称搜索框**过滤当前追踪集（子串、不区分大小写，过滤后分页在子集上进行，无命中走空态）。为防翻页抖动，追踪集排序改**按 `thingIDNumber` 稳定升序**。搜索框/s/t 钮/页钮/锁钮全部为 US 自有 widget 家族（内容首行带）。快照数据链（overlay 状态机）不感知分页与搜索——纯视图层。
- **Q3 示例已呈维护者**（双写时刻两条一模一样的 `pool : sound`；分离时刻 "Dispatched audio"= 最后一次真发声、G16 = 本链末梢当前判定），建议 G16 退纯三态、归属单点在 Current state 区。**待裁**。
- **Q5 方案已呈维护者**：推荐显示**四层 ChainTier**（异种包/种族包/包回退/内置），代价 = `ChainResult.Tier` 顺 plumbing 进 `SqueakSoundChoice`/`SqueakRecentOutcome`（三处 runtime-only 结构加一个字段，零 Scribe、零 usdiag 词表变更——协议日志维持三层折叠不动）； blanket "vanilla" 方案已论证为劣（混淆包回退与内置、且 Vanilla 一词在 US 词汇里已是模式名）。**待裁**。

**第七轮（2026-09-10 事实核查与提案，待维护者确认）**：
- **Q6 乙案落定 + 地图边界天然成立**：搜索数据源 = `map.mapPawns.AllPawnsSpawned`（游戏按图自管的集合），扫描范围即"本地图"，零自制地图过滤代码；跨图会话拆毁已有 `MaintainLifecycle`（`cachedMap` 比对）兜底——**切换地图 = 会话与全部锁定窗自然终结**，为"仅限本地图"的既定推论。
- **C 行语义核查（维护者记忆 = 期望语义，现实现有三处偏差）**：现行 = "当前动作的、最近一次显著结果恰为派发的音频"。偏差①按动作过滤：最近显著结果属别的动作 → 显 "—"；偏差②被覆盖：`lastSignificantOutcome` 被任何显著失败（NoSoundFallback/EligibilityRejected 等，仅冷却×3 与概率拒绝四类不算显著）顶掉——成功喊过一次后任何一次失败尝试即清空该行的"最后一次发声"；偏差③换动作即清空。**提案**：CompSqueaker 增设 `lastDispatched` 专用槽（仅 `Outcome == Dispatched` 时写入，不被失败覆盖、不按动作过滤），快照加一字段（runtime-only，零 Scribe，usdiag 词表不动），C 行与 Visible audio 列同用；G14/G15/G16 维持按动作过滤保证链一致性。**待批**。
- **G16 为何在三态位置显示派发音频（代码史实）**：`Add("Dispatch", Pass, FormatDispatched(...))`——值列 Pass 态被塞归属串，属建面板时的顺手复用；归一为纯三态后信息零损失（归属单点 = 修好语义的 C 行）。
- **Q5 最终显示方案（维护者映射经代码验证成立）**：四层 = 命中层即状态，无需独立"被 fallback"标记——`[异种包·<包键>]`（直接命中）、`[种族包·<包键>]`（直接命中）、`[包回退·<包键>]`（命中该包自带回退音组，`SqueakPoolRegistry.cs:198-199` 实证 PoolStableKey 保留回退来源包键）、`[原版]`（内置层，`PoolStableKey` 恒 null，`SqueakKernelAdapter.cs:148` 本就把 BuiltInFallback 折成 Vanilla source、usdiag 协议同款折叠——"内置=原版"不是 UI 私设，是既有生产语义的直读）。注意：内置层数据未 ship（Vanilla silence = 未完成内容），今天 `[原版]` 标签实际不可观察，G14 先红；标签就位是为内容落地那天面板不说谎。plumbing 仍为 Tier 沿 `ChainResult → SqueakSoundChoice → SqueakRecentOutcome` 各 +1 字段。**待批**。

**第八轮（2026-09-10 回退路径实测，澄清 Q5 混淆）**：
- **回退梯子的实况**（`SqueakPoolRegistry.cs:73-79`，Fallback 模式）：第二级 = **跨层回退**（异种层无此动作音 → 种族层直取）；第三级 = **包内回退**（两层皆无 → 某包声明的自带兜底音组 `PackFallback[actionKey]`）；第四级 = 内置表（=原版）。Remix 模式无梯子：非空层（含包兜底，若声明）3/4 路抽签（:89-128）。Off 模式直返内置表（:65-66）。
- **标签语义更正**：维护者记忆的"回退到种族层"落地时标签就是 `[种族包·<key>]`——链结果记录的是**供音频的层**，跨层"路过"事实不在结果内（要显示须扩 `ChainResult` 加 passedThrough；提案：**不扩**——"为何没发声"由 G14/G0 回答、"音频来自哪层"由供层回答，路过是过程非状态，且 Remix 抽签根本无路过分层可谈；扩字段还将触发 kernel 黄金语料再生成轮）。`[包回退·<key>]` 仅当音频真来自包兜底音组（tier=PackFallback，:198-199 实证 key=兜底音所属包的 PackKey，非种族层变体写法）。
- **四层标签定稿提案**：`[异种包·key]` / `[种族包·key]` / `[包回退·key]` / `[原版]`。协议折叠旁注：适配层按域折 PackFallback → source（有异种域折 xenotype_pack，`SqueakKernelAdapter.cs:147`），`SqueakDebug.cs:35` 的"折进 RacePack"旧注释与代码有出入，属文档债，与面板标签无关。
- **lastDispatched 槽已批准** → 进实现清单。

**第九轮·收口定稿（2026-09-10，审计关闭，本清单即动工合同）**：
- **窗口拓扑**：主窗（master-detail：左列 = 搜索框 + 8/页分页列表 + 页指示，右列 = 当前选中 pawn 详情）；锁定向导出一枚独立详情窗（可多枚，overlay 以 locked 集合跟踪，离屏持续快照）；主窗与各锁定窗均支持折叠横条（主条 = 最近 revision 变化者的摘要行，锁条 = 本 pawn 摘要行）；双 Esc/KeepGrabPx 钳制/非模态在子类侧；CloseText 吸收 Esc 提示语义；主窗关闭 = 会话终结连坐全部锁窗与跨图既有守卫。
- **US 自有 widget 家族**：摘要行（一物三用：列表行/主条/锁条）、s/t 单位切换、锁钮、搜索框、分页钮+页指示、字段行、门链行（四态：Pass 绿/Block 琥珀/Pending 蓝/**NA 白**）。全部注册进程序构造 spec（无新 manifest）。
- **数据/引擎改动**：① `CompSqueaker.lastDispatched` 槽（仅 Dispatched 写入、失败不覆、跨动作存活）+ 快照字段；② `Tier` plumbing（`ChainResult→SqueakSoundChoice→SqueakRecentOutcome`，runtime-only）；③ overlay：删 16 追踪闸、`thingIDNumber` 稳定排序、locked 集合解除视口门、搜索源 = `AllPawnsSpawned`+comp 过滤（本地图天然）；④ 门链 16 行（G5 删）、G4 Blocked 细分失败条件、G10/G11 "剩余/有效总"、G16 纯三态、音频归属单点 = `[异种包·键]/[种族包·键]/[包回退·键]/[原版]` 四层标签（走 Keyed）。不扩内核 ChainResult（"路过"不入标签），不向 FL 开面。
- **harness 车道（建满，定稿项目集）**：假快照注入、两视图遍历、revision 驱动重建断言、锁定离屏不断更、解锁回跟随、行点击 drill-in/搜索结果直锁联动、lastDispatched 失败不清语义、四层标签渲染用例、抛错→RecoveryBand、关窗→NotifyPanelClosed。
- **同 commit 收口**：gate 14 白名单 2→1 + `MEMORY.md`/`CONTRIBUTING.md` 第 6 条"两个豁免"文案同步 + crash-lineage 面板半边销账。
- **实机走查清单（并入维护者验收）**：拖拽/非模态/下钻/多锁/折叠条/翻页/搜索（含搜离屏 pawn 直接锁定）/双 Esc/关窗停会话/**PackFallback 端到端首验**——你的"没测过"记忆已实证为"链路全存在、游戏内从未观察"（kernel 有 `PackFallbackTier`/`PackFallbackExactDomainOnly` 钉，adapter `:187-195` 接通作者 XML 投影）；验收时用一份缺动作音 + `<fallbacks>` 的包实测 `[包回退·键]` 显形。

**不做**：不给 settings 加防炸 fallback；本迁移不向 FL 开轮——§1.2 的关闭钮评估与 §1.1 的"控件本属库"论证若成立，走**独立提案**通道（讨论后向 FL 提项），迁移本体按现壳表面继续，不私改库、不以库改动为面板动工前提；迁移中撞出的壳级缺陷照旧攒 round 4 素材。
**验收**：面板文件 raw backend 命中 0；whitelist 1 条目无 NOTE；verify-local 14/14；新 harness 车道绿；维护者实机清单加一条面板走查（拖拽/非模态/两模式/双 Esc/关窗停 session）。

## 2. 活口指针（详情都在 `TODO.md`，此处只留一行索引）

- round 3 已 CLOSED 且 FL 已实现合入 `main`（US 侧剩采纳：`Width="Auto"`/`MinWidth`/`MaxWidth` + 容器 `Breakpoint` 已在 CI 所检出的 carrier 默认分支上）→ `TODO.md` 账本行；D7 区域形状开放分叉 → TODO D7 节；Knife 3 → TODO；rc1 试用循环与发布轴配对 → TODO；`US_STEAM` 死轴记录 → TODO。
- 分支线（2026-09-10 三级定稿）：**功能分支**（短命，自版本线尖端切出，随积累推送留存，合回线后本地+远端删除）→ **版本线** `0.3.x`（集成与积累面，14/14 绿；纯文档/裁定提交可直接落线）→ **`main`**（发布信号面，仅切 rc/release 时合入线，现停 rc1 面 `d9b1d24`）。首个适用对象 = 面板迁移，实际分支名 `diagnostics`。FL 侧 merge/tag 归库会话与维护者，本侧只读。
- 分支命名（09-10 裁定补充）：功能分支名带版本面与用途前缀（例：`0.3.x/feat-diagnostics`）；本轮已开的 `diagnostics` 不改名，合回即死。

- **面板迁移状态（2026-09-10）：已合回 `0.3.x`（ff 至 `e9e6a34`，27 文件 +2613/−845），`diagnostics` 分支本地+远端已删**。代码面 14/14 全绿（纯车道+kernel-host 车道+verify-local 实测，白名单 2→1 落地）。**实机走查清单待维护者**：拖拽/非模态/下钻/多锁/折叠条/翻页/搜索（含离屏直锁）/双 Esc（重点：**未消费 Esc**——若泄漏进游戏取消/关设置窗，即 FL round-4 的 UiNative 事件缝素材）/关窗连坐/跨图终结/**PackFallback 首验**。

## 3. FL 递交（2026-09-10，跨仓核对；本节由库会话代写，供 US 会话核对后并入或删去）

**为什么有这一节**：FL 侧 2026-09-09 的跨仓核对发现 US 文档里关于 FL 状态与包体版本的陈述已滞后，而 FL 缓冲区里"等对侧下次打开时自行 re-point"的假设已被证伪一次（US 在 refile 当天重写过自己的 `TODO.md`/`HANDOFF.md`，那几行没动）。维护者本次授权库会话跨目录只读检查并直接改对侧文件，因此下列改动**已经落盘**，不是待办：

- `TODO.md:8`（Cross-repo ledger 行）：把"等 FL 把 `feat/round-1-0.3.0` 合入其 main"改为已完成——round 1 + round 3 均在 FL `main`（PR #1），lib 侧只剩 `v0.3.0` 切tag，那归维护者试用裁决，US 的 rc 配对跟的是那个 tag 而不是 merge。
- `TODO.md:9`（round 3 行）：状态由 "REVIEWED by FL, pending scheduling" 改为 **CLOSED by FL，2026-09-09 实现并经 PR #1 合入 `main`**；并在行内记三处过时断言的更正：① 包体 0.4.0 → **0.3.0**（首发前加入不动契约轴，US pin `[0.3.0,0.4.0)` 本就覆盖，"0.3.0 stays frozen / N ships on 0.4.0" 作废，阈值禁令现在就在 0.3.0 解除）；② 宽度钳位实为 **`MinWidth`/`MaxWidth`**，不是 verdict 原文的 `Min`/`Max`（后者被 StepperSlider 的值域占用）；③ FL 缓冲区里的 `## US→FL round 3` 正文按生命周期已修剪，永久记录改指 FL `MEMORY.md` round-3 条与 US `MEMORY.md` 新增的 responsive-vocabulary 条。
- `TODO.md:46`、`TODO.md:60`：这两行原来以"0.4.0 还没落地"为前提，把声明式尺寸排到一个不会存在的版本之后。更正后写明：`Width="Auto"` + `MinWidth`/`MaxWidth` + 容器 `Breakpoint`（`Narrow`/`Cols`/`NarrowCols`/`NarrowHidden`）**已在 CI 所检出的 carrier 上可用**——FL 远端默认分支是 `main`，而 US 两条 workflow 检出 carrier 时都不带 `ref:`，所以采纳不再被 FL 发布阻塞；R14 的手写阈值禁令不变，本轮手测几何仍是过渡实现。原 `:60` 关于 `ResolveColumnWidths` 只有"静态 `Width=`/等分"的描述是 round 3 之前的引擎，现已是 Auto 测量列。
- `MEMORY.md`（Carrier lockstep 条之后新增一条）：把上述事实作为持久能力面记入，附复推命令 `git -C ../ferritelib show origin/main:Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs` 后 grep `IsAutoWidth` / `"Breakpoint"`，并带上 FL 侧自己的证据学告诫——harness 的宽度是字符数线性模型（`StubTextWidth`），Auto 列在真实字形下是否合身仍是实机问题。
- `HANDOFF.md:5`、`:39`：对侧状态摘要与活口指针索引同步为 CLOSED。

**US 侧需要复核的两点**（库会话不改判，只提示）：其一，`:46`/`:60` 的裁定主体（维护者 2026-09-08 三点、区域形状分叉）原样保留，只替换了版本与可用性断言；其二，本轮没有开 FL round——§1.2 关闭钮评估得出 n=0 不立案、§1 声明零新面诉求，因此 FL 侧 round 计数仍停在 3，**round 4 号位空置**，攒素材的触发条件是迁移中撞出的壳级缺陷。若 US 会话认为上述任何改动越界，直接 `git checkout -- TODO.md MEMORY.md`（`HANDOFF.md` 不入库，手工回退本节即可）。
