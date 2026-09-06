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
