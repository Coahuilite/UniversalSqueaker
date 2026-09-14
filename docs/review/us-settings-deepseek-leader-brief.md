# Universal Squeaker Settings UI — DeepSeek Leader Brief

Date: 2026-09-13
Audience: a new DeepSeek harness leader operating an agent team
Repository scope: `UniversalSqueaker/` first; inspect `ferritelib/` only when proving an FL capability or compatibility constraint.

You are the implementation leader. Start by reading this entire brief and the referenced design documents before assigning work. Treat the user-facing behavior and invariants below as the product contract.

## Mission

Bring the US Mod Settings window to the agreed release UI shape while preserving current settings semantics, diagnostics provenance, save behavior, and FerriteLib compatibility.

The work has four connected UI outcomes:

1. A retractable help drawer on the right side of the settings window.
2. Uniform geometry for diagnostic/support rows.
3. A readable, localized mood modulation editor showing Pitch, Volume, and Jitter.
4. Stable equal geometry for the left navigation cards.

Do not redesign unrelated screens or change runtime audio behavior.

## Required team decomposition

Assign separate workers with explicit file ownership. Keep ownership boundaries clear and merge in dependency order:

### Worker A — layout/state architecture
Own the settings page layout resource, settings host/session state, and help visibility plumbing. Implement the retractable help drawer contract below. Do not edit mood widget internals or localization files except when adding a narrowly required key reference.

### Worker B — mood modulation
Own the mood modulation widget/layout and its US localization keys. Implement the two-line parameter geometry and value round-trip behavior. Do not change help drawer state or generic FL theme tokens.

### Worker C — diagnostics and navigation geometry
Own diagnostic/support row layout and the `us/nav` geometry. Add focused assertions for equal bounds and fixed control columns. Do not alter diagnostic fact provenance or status semantics.

### Worker D — validation/reviewer
After the first three workers finish, review the combined diff for regressions, run tests, inspect screenshots/harness output, and report remaining in-game-only gaps. This worker must not silently rewrite architecture; report conflicts to the leader.

If the harness cannot provide four workers, combine C and D only after A and B are complete.

## Help drawer contract

The current page has left navigation, center content, and a fixed right help scroll. Change it to a retractable right-side drawer.

Wide layout:

```text
closed:
+------------------+-----------------------------------------------+
| navigation       | center content                                 |
+------------------+-----------------------------------------------+

open:
+------------------+-----------------------------+------------------+
| navigation       | center content             | help drawer      |
+------------------+-----------------------------+------------------+
```

Rules:

- Help visibility is independent state. Do not reuse `active-tab`.
- The closed state removes the help column from measured width; it must not leave a blank reserved strip.
- The open state reallocates width between center content and help drawer; it must not paint over center controls.
- Provide a discoverable Help toggle in the settings chrome or page header. Use existing US/FL button primitives.
- Preserve the currently selected help topic when the drawer is toggled.
- Preserve independent scroll positions for center content and help content.
- Toggling the drawer must invalidate layout and view caches through the existing revision mechanism.
- First implementation has no animation.
- Narrow layout must not force three columns. Use list/detail navigation or an in-flow help block according to the existing narrow-layout capability. The help content must remain readable and usable.
- Do not invent a recording switch. Existing diagnostic bar text is display state, not a writable control.

Required acceptance checks:

- open → close → open preserves selected help topic;
- closing removes the reserved help width;
- switching tabs while open updates help content without losing drawer state;
- narrow widths do not clip or horizontally overflow;
- no use of `active-tab` for help visibility.

## Diagnostic/support geometry contract

All diagnostic/support rows in the Overview page use one shared width contract:

```text
[ label/content fills remaining width ][ fixed-width control column ]
```

- Every row fills the same section content width.
- The right control column has one fixed width and one right edge.
- Auto/On/Off choices use one fixed-width segmented control or equal-width cells.
- Labels must never resize the control column because of translation length.
- Keep row heights stable.
- Preserve all existing diagnostic states and current-vs-previous evidence semantics.

## Mood modulation contract

Use real product moods and keyed localization:

- Good / 良好
- Neutral / 中性
- Bad / 低落
- Break / 崩溃

Do not use placeholder mood names. Do not hard-code language in widget code.

Each mood is a card with a readable repeated parameter template:

```text
mood header                                      reset / preset
Pitch:  [−] [numeric input] [+]
        [---------------- slider ----------------]
Volume: [−] [numeric input] [+]
        [---------------- slider ----------------]
Jitter: [−] [numeric input] [+]
        [---------------- slider ----------------]
```

Parameter labels must be fully visible and localized (`Pitch/Volume/Jitter`, `音高/音量/抖动`). Numeric input and +/- buttons share a row. The slider is on the following row. All four mood cards use identical widths and control geometry.

This is a presentation and localization correction. Preserve existing min/max/step, binding keys, reset behavior, and persistence semantics.

## Navigation geometry contract

The left navigation stack must be geometrically stable:

- one fixed column width;
- every card fills it exactly;
- selected and unselected cards have identical outer bounds;
- identical text inset, border thickness, and horizontal padding;
- fixed minimum height and fixed inter-card gap;
- reserve the same two-line subtitle band when subtitles are present;
- long translated labels wrap/ellipsize inside the card and never resize it.

## Non-negotiable boundaries

- US owns these changes. Avoid changing FerriteLib generic theme semantics.
- Do not change FL `Warning`/`Danger` aliases to obtain cyan or attention styling.
- Do not fabricate a `NotReached` diagnostic state.
- Do not change current/previous evaluation provenance.
- Do not change audio calculations, routing, save schema, migration behavior, or voice-pack discovery.
- Do not add animation in this pass.
- Do not push, publish, install, tag, or create a PR.

## Required validation

Run from a clean or clearly documented worktree state:

1. Existing US local verification script.
2. Focused UI/layout tests.
3. Any existing Schema 2, text-fit, and narrow-width harness tests.
4. Screenshot or harness evidence at logical widths 1024, 736, 480, and 320.
5. Chinese and English localization coverage.
6. Static review that help visibility is not bound to `active-tab`.
7. If FL harness restore/build fails, capture the exact command and failure phase; do not call FL green.

For geometry assertions, verify:

- diagnostic control right-edge deviation is at most 1 px after rounding;
- navigation card left/right edges are equal;
- selected navigation card bounds equal unselected bounds;
- mood parameter labels are present and not clipped;
- slider widths match within 1 px across parameters and moods;
- closed help drawer reserves zero help width;
- open help drawer has independent center/help scroll regions.

## Leader report format

Return one concise report with:

- worker assignments and owned files;
- changed files grouped by outcome;
- tests/commands and pass/fail results;
- screenshots or harness artifact paths;
- known limitations and in-game-only evidence gaps;
- any requested follow-up work.

Do not claim release readiness from browser/harness evidence alone when runtime evidence is missing.

## Reference documents

Read these before implementation:

- `UniversalSqueaker/docs/review/us-settings-visual-revision-handoff.md`
- `UniversalSqueaker/modding_documents/us-ui-deepseek-team-handoff.md` (if present)
- `UniversalSqueaker/Source/UniversalSqueaker/UI/Layout.Schema2.xml`
- `UniversalSqueaker/Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs`
- existing diagnostics ruling and attention-color ruling documents in the workspace

Begin by reporting the current HEAD, worktree status, relevant existing tests, and the exact files you will assign to each worker. Then implement, review, and validate the complete contract above.

## Explicit FL exception — world-HUD camera indicator

`Patch_GlobalControlsUtility_CameraIndicator.cs` contains a direct `Widgets.Label` call. This is an intentional, documented exception to the FL-first settings UI rule.

Reason: the camera indicator is a single-purpose overlay that must be painted in the original RimWorld world HUD lower-right region. It is not part of the Mod Settings window and must remain on the native HUD integration path unless a future FL API explicitly supports that exact host surface without changing placement or input behavior.

For this task:

- Do not migrate, refactor, or expand the camera-indicator HUD path.
- Do not count this existing `Widgets.Label` call as a violation of the Mod Settings FL-first requirement.
- Do not add any new direct `GUI.*`, `Widgets.*`, `Event.current`, manual scroll-view, or custom input-routing calls to the settings page.
- Report the camera indicator as an accepted exception if auditing direct UI calls.
