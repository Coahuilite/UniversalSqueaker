# MEMORY

## Current durable state

- This repository is the **Universal Squeaker (US)** local fork of Squeaky Ratkin (SR), created 2026-08-23. It has **no remote** and is **not published**.
- Fork source: SR branch `0.3.x` tip `b19d68a` (post-0.3.2-pre1, including the `.slim` cleanup commits C34–C42).
- Confirmed identity: repo `coahuilite/UniversalSqueaker`, packageId `coahuilite.universalsqueaker`, namespace/prefix/log = `UniversalSqueaker` / `US_` / `usdiag`. Workshop display name and license are pending maintainer confirmation.
- 0.4 co-existence policy (decided): SR owns Ratkin exclusively; US serves other races only; both mods may be enabled together; no `incompatibleWith`. Maintainer-authorized legacy bridge exception (2026-08-24): US ships one thin empty `SqueakyRatkin.SqueakVoicePackDef` shim so old SR VoicePack XML loads; legacy packs are explicitly marked in logs (`usdiag voicepack.pack.legacy_admitted`) and UI (`Legacy SR` tag + banner). Legacy packs omitting `raceDefName` default to `Ratkin` (SR-dependency semantics; explicit declarations are kept). US still ships no Ratkin audio/content/profiles/attachments.
- SR 1.0.0 will shrink SR into a pure audio pack with US as prerequisite. The legacy bridge offline prototype lives in the SR repository (`tools/LegacyBridgePrototype` / `tools/LegacyBridgeHarness`) and will be activated only in the US takeover version.

## Authoritative entries

- Fork handoff and UI adaptation evaluation: `HANDOFF.md` (single entry point).
- Mod structure reference: `docs/mod-structure-reference-zh.md` (RimWorld Wiki + SR-minus-Extras/audio baseline).
- Release flow: `docs/release-runbook-zh.md` (inherited from SR and adapted for US: no Extras/audio/OGG mirror/codemap checks; `version.txt` first line is `UniversalSqueaker <version>`). Local scripts: `scripts/verify-local.ps1` (six gates), `scripts/build-dev.ps1`, `scripts/pack-dev.ps1`, `scripts/stage-package.ps1`. Dev packaging flow verified 2026-08-23 on commit `8c2f056`: six gates green, package content/exclusions/version.txt/DLL identity pass the runbook Phase 0 checks.
- Kernel compile set (rebuilt, zero-Verse, de-SR-ized): `Source/UniversalSqueaker/Kernel/`. Legacy root `Kernel/` was deleted in the Phase 4 cleanup (git history retains it).
- Pure funnel logic (rebuilt, zero-Verse): `Source/UniversalSqueaker/Pure/SqueakActionPlan.cs`, `Source/UniversalSqueaker/Pure/SqueakTimingModel.cs`. Legacy root `Pure/` was deleted in the Phase 4 cleanup.
- Kernel harness (rebuilt): `tools/UniversalSqueakerKernelTests/` links `Source/UniversalSqueaker/Kernel/`+`Pure/`; US 0.1.0 golden corpus committed and replay-green. Legacy `tools/KernelCharacterization/`, `fixtures/`, and `sr_reference/` were deleted in the Phase 4 cleanup (see `OBLIVIONIS.md`).
- Runtime assembly (rebuilt): `Source/UniversalSqueaker/` builds Dev/Release clean (0 warnings). Catalog/resolver/settings/logging/comp/production patches all US-namespaced; `usdiag` protocol re-frozen; no `SqueakProductDomainFilter`, no HAR Ratkin special-casing (HAR discovery deferred TODO).
- Data surface: `UniversalSqueakerFallbackProfileDef` injects fallback profiles from DefDatabase (no shipped seed Defs; empty = valid); `1.6/Languages/*/Keyed/UniversalSqueaker.xml` committed.
- Tool gates: `tools/UniversalSqueakerConfigCopyTests/` and `tools/UniversalSqueakerLogTests/` both pass; they link Source kernel/fallback/logging files.
- VoicePack authoring skill: `.github/skills/us-voicepack-authoring/SKILL.md` (migrated from SR; canonical `US_` authoring only — legacy `SR_` packs need no author action because the bridge auto-loads, defaults to Ratkin, auto-attaches the comp, and marks them `Legacy SR`).
- UI migration & orphan-feature development plan (locked 2026-08-24): `docs/us-ui-migration-plan-zh.md` (decision table §2, orphan inventory §3, tuning editor §4, layered action table §5, Disabled bypass §6, implementation order S1–S5 §12, action-entry wrapper + three-segment architecture + neutral/external split §13, review findings §14). `TODO.md` "Next development plan" mirrors it.
- Phase A FerriteLib UiKit (2026-08-24): standalone DLL `Source/FerriteLib.UiKit/` (neutrality = no US/SR product literals — the DLL still references `Krafs.Rimworld.Ref` and `using UnityEngine/Verse` IMGUI types by design, per `docs/ui-shared-library-design-zh.md` "production uses Verse, tests use stubs") (assembly/DLL `FerriteLib.UiKit`, packageId/XML scope `coahuilite.ferritelib.uikit`, C# namespace `FerriteLib.UiKit`), core two-pass XML layout engine + core widgets, tests at `tools/FerriteLib.UiKit.Tests/`. US integration now uses Ferrite UI by default (`VoicePacksPage.UseFerriteUi = true`) with embedded `Source/UniversalSqueaker/UI/Layout.xml`, US-registered widgets include `us/page-title`, `us/race-layer`, `us/xenotype-layer`, `us/voice-pack-checklist`; old UI is retained as `UseFerriteUi = false` fallback pending in-game matrix. `scripts/verify-local.ps1` includes UiKit tests/builds/neutrality grep and both-DLL presence. Phase B (split into private dependency mod) is deferred.
- SR upstream (read-only evidence source): sibling repository at `../squeaky_ratkin` relative to this repo root. Do not write there and do not infer its external state from this repo.

## Engineering decisions and handoff

- **Local fork only**: no remote is configured; local commits are the only permitted git operations until the maintainer authorizes remote/push. Reachable history still contains the pre-fix personal absolute SR path in `MEMORY.md` (commits `eb2ac90..dc8c598`); it is recorded in TODO and must be handled by the maintainer before any first push.
- **Migration inventory (2026-08-23, historical)**: `Kernel/*.cs` (9 files), `Pure/` (2 files), `tools/KernelCharacterization/*` plus `fixtures/` and `sr_reference/` were migrated, consumed as references during the rebuild, and deleted in Phase 4; they remain in git history (`dc8c598` baseline).
- **Inherited technical debt (from the SR snapshot) — resolved by the rebuild**: namespaces migrated to `UniversalSqueaker*`, Ratkin seed and `SR_*` keys removed, `DomainFilter`/`SqueakProductDomainFilter` deleted, five-way sync replaced by three-way sync plus tool gates.
- **Target baseline**: kernel with zero product literals (race/sound-key/prefix all injected as data); UI only needs VoicePack assignment to work, all other pages may be removed but must not crash; every race routes equally with no Ratkin special-casing.
- **UI adaptation decision (per HANDOFF.md)**: Plan A (vertical cut) is recommended — keep settings shell, Off/Fallback/Remix mode cards, Race layer, and VoicePack domain checkboxes; remove SoundMood workbench, Diagnostics, audio browser, statistics/overlay/mote diagnostics, and the Xenotype behavior editor without changing any Scribe schema.

## Rebuild plan decisions (approved 2026-08-23)

- D1 Version: csproj `<Version>0.1.0-dev` is primary; `About.xml <modVersion>` follows it.
- D2 Rebuilt pure code lives under `Source/UniversalSqueaker/Kernel/` (zero-Verse compile set) and `Source/UniversalSqueaker/Pure/` (funnel pure logic); the old root `Kernel/`+`Pure/` were reference-only and are now deleted.
- D3 SR 0.2.4 settings-migration fixtures are not replicated now: US is a new mod with no legacy config; the legacy bridge is deferred to the takeover version.
- D4 Verification project: `tools/UniversalSqueakerKernelTests/` with the US 0.1.0 golden corpus; old `tools/KernelCharacterization/` was reference-only and is now deleted.
- D5 Kernel product literals: `BuiltInFallbackCatalog` (Ratkin seed + `SR_*` keys) and `DomainFilter` whitelist semantics removed; `BuiltInFallbackTable.Empty` plus data injection remain.
- D5b Comp attach: VoicePacks may attach `CompProperties_Squeaker` via their own XML patch; the legacy bridge additionally auto-attaches a default comp to every race declared by an admitted legacy pack when none exists (Ratkin in practice), so old SR packs work with no pack-side patch.
- D6 UI: reactive view-model + declarative immediate-mode components over Verse widgets (evaluation: `docs/ui-componentization-evaluation-zh.md`; implementation notes: `docs/ui-phase3-implementation-notes-zh.md`).
- D7 Cleanup executed in an atomic commit: `Kernel/`, `Pure/`, `fixtures/`, `sr_reference/`, old `tools/KernelCharacterization/` deleted; `OBLIVIONIS.md` records the pre-rebuild baseline commit `dc8c598`.

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
- Prior session state still relevant: local commit chain ends at `19ba732` (working tree now additionally has the new plan doc + TODO edit + MEMORY checkpoint edit, uncommitted). Test pack inventory under `dist/` unchanged: `Kiiro-US-EXP`, `SqueakyRatkinExampleVoices`, `SqueakyRatkinLegacyVoices`, `Nivarian-US-EXP`, `KiiroSiamese-XenoRoutingTest-US-EXP`, `RatkinOA-XenoRoutingTest-US-EXP`. Install by copying `dist/dev/UniversalSqueaker` over the game Mods folder; do NOT enable original SR together with the US legacy bridge (first-wins type conflict). `dist/` is gitignored build/test output.
- Next action on resume: read `docs/us-ui-migration-plan-zh.md` §14, then either start the Scribe+data-model design or S1 global-layer removal; collect maintainer answers to the three pending confirmations first if possible.


## Session resume checkpoint (2026-08-25c — 硬债清理 + 路由表评估会话；跨 harness 压缩锚点)

- This session executed **双源统一 + voicePackDefaultSeeded 删除 + dist/ 残留清理**（Task 3 仅工作区清理，dist/ 被 .gitignore 排除）and committed as 2 LOCAL commits: `1c64146`(双源统一) → `55b6990`(voicePackDefaultSeeded), on top of `a4db6f4` + `801792e`. Design doc `docs/us-routing-table-baseline-design-zh.md` tracked at `50bb2ed`. All green: verify-local 12 gates + Dev/Release 0 warnings + kernel golden-corpus replay zero-delta. Working tree clean.
- Durable facts established this session (do not re-derive):
  - **双源统一完成**：`globalActionEnabled` / `GlobalActionEnabledRecord` / `GetActionGlobalScope` / `SetActionGlobalScope` / `IsActionGloballyEnabled` 全部删除。`BuildGlobalActions` 现在是 `C# DefaultScope < baseline Def < actionTuning Global 层`，不再有中间旧 globalActionEnabled 层。`xenotypePresets.actionOverrides` 保留（仍是运行时消费源）。`IsActionAllowedByKey`（CompSqueaker.cs:422）是 external-action 门，与 globalActionEnabled 无关，未动。
  - **voicePackDefaultSeeded** 哨兵字段已删除（字段 + Scribe 行）。
  - **dist/ 残留清理**：6 个测试包 XML 中 `globalMinIntervalTicks` / `scaleFrequencyWithTalking` / `distancePresets` 已删除（工作区生效，dist/ 被 .gitignore 排除无法提交）。
  - **路由表机制评估**：四维度分析完成。`IsLegacy` 不能删，只能从"门控"降级为"日志事件选择器 + UI 标记驱动 + 缺省 race 填充"。日志事件采用方案 A（legacy→旧事件，canonical→新事件，不双发不统一）。**baseline 是全局的**（不是 per-pack），叠加是**字段级覆盖**（不是叠乘），baseline 不适合 per-pack 音色修正。最大风险：canonical 包从"声明即静默"变成"声明即挂默认 comp"——设计目标，需发布说明。
  - **subagent 配置**：后续使用 `deepseek-official` / `deepseek-v4-flash`；scout `low`，审阅 `high`，开发 `max`。`commandcode` provider 已弃用。
  - **RimWorld enum Scribe is by-name**（前会话已定案，保留供参考）：Scribe_Values writes `value.ToString()`，ParseHelper parses `Enum.Parse`，`Off→Vanilla` ABI-safe。
  - **Ferrite widget 扩展模板（五步）**（前会话已定案，保留供参考）：implement `IWidget` → `UsWidgetCommandAdapter.For` 映射 → `FerriteVoicePacksPage` viewState 键 → `UsWidgetRegistrar` 注册 → `UI/Layout.xml` 行。
  - **Diagnostics panel**（前会话已定案，保留供参考）：`CompSqueaker.PostDraw()` 公开钩子，非模态 Window，17 门禁链三态。
- Remaining: (1) **路由表开放**（~20 行 C# + LogTests `AssertEqual(9→12)`，独立小块）；(2) **专项测试** "global off + xenotype on" 需提取 Pure 函数或 Runtime harness；(3) **S4-Polish**（过滤/帮助/窄屏/美化/footer/距离预览/A1-A2 调音编辑器 UI/Race-Xenotype 层 scope UI）；(4) **`docs/workdocs/` 移除**（所有剩余块落地后）；(5) **Ferrite UI 游戏内稳定化**（maintainer step）。

