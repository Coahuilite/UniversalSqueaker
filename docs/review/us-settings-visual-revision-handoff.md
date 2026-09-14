# US Settings UI Revision Handoff: Width Consistency, Mood Modulation, and Navigation Geometry

Date: 2026-09-13
Audience: DeepSeek implementation team
Scope: UniversalSqueaker settings UI only. Do not change FerriteLib generic Warning/Danger semantics, diagnostics data contracts, or unrelated runtime behavior.

## Product judgment

The current settings window is usable, but the screenshot exposes three release-blocking clarity issues:

1. Diagnostic/support rows do not share one visual width contract. Labels, selectors, and checkboxes terminate at different x positions, making the page look assembled from unrelated controls.
2. Mood modulation is not understandable to players. The four mood names are still English (`Good`, `Neutral`, `Bad`, `Break`), and the three parameters are visually abbreviated or partially hidden. A player must be able to identify Pitch, Volume, and Jitter without guessing.
3. The left navigation cards do not have a stable vertical rhythm. Their outer widths are close, but the card content and selected state make the stack appear uneven. Navigation must use one fixed card width and one predictable row height.

The screenshots are visual evidence only. Confirm all final dimensions in the real game and existing harness tests.

## Task A — unify diagnostic/support geometry

Target: the Overview page's diagnostic support section and any similar three-choice support rows.

Use one shared row component/layout rule for every row in the section:

```text
[full-width label/content area............................] [control]
```

Rules:

- The section content width is the width of the center scroll viewport minus the same horizontal insets used by other sections.
- Every row fills that width.
- The right control column has one fixed width; its left edge is identical in every row.
- Do not let the label column size itself independently per row.
- Checkboxes and segmented choices must align to the same right edge.
- If a row has three choices (Auto / On / Off), use equal-width choice cells or a single segmented control with a fixed total width. Do not render three unrelated text boxes whose widths depend on translated text.
- Keep the row height constant at the existing dense/regular token. Do not solve alignment by allowing one row to become taller.
- The diagnostic section title, row separators, and bottom border must share the same content width.

Recommended model:

```text
support-section
  title row: fill width
  repeated support-row: fill width, fixed control column
    label: fill remaining width
    control: fixed width
```

Acceptance checks:

- Capture Overview at 1024, 736, 480, and 320 logical widths.
- At each width, measure the right edge of every diagnostic row control; max deviation must be 0–1 px after rounding.
- No translated label may push the control column or create horizontal overflow.
- Existing settings behavior and save semantics remain unchanged.

## Task B — redesign mood modulation rows

Target: Tuning page, Mood Modulation section.

The player-facing labels must be localized and explicit:

- `良好` / `Good`
- `中性` / `Neutral`
- `低落` / `Bad`
- `崩溃` / `Break`

Use the project's existing keyed localization contract. Do not hard-code English or Chinese in widget code. Preserve the actual product mood names (`Good`, `Neutral`, `Bad`, `Break`); do not replace them with placeholder concepts such as calm/joy/alert/curious.

Each mood becomes a two-line card. This is the required structure:

```text
+----------------------------------------------------------------+
| [Mood name]                              [reset] [preset]       |
| Pitch:  [−] [numeric input] [+]                               |
|         [---------------- slider ----------------]              |
| Volume: [−] [numeric input] [+]                               |
|         [---------------- slider ----------------]              |
| Jitter: [−] [numeric input] [+]                               |
|         [---------------- slider ----------------]              |
+----------------------------------------------------------------+
```

For a compact implementation, the three parameters may share a consistent parameter-row template, where each parameter has:

- a fully localized label (`音高`, `音量`, `抖动`; English `Pitch`, `Volume`, `Jitter`),
- minus button, numeric input, plus button on the first line,
- slider on the next line, aligned to the numeric control group,
- identical control widths across Pitch/Volume/Jitter and across all four moods.

Do not place all three sliders in one crowded horizontal strip. The screenshot shows why: abbreviated labels, narrow inputs, and squeezed controls make the relationship between parameter and value unclear.

Recommended order inside each mood card:

1. Mood header and reset actions.
2. Pitch parameter block.
3. Volume parameter block.
4. Jitter parameter block.

Use the same min/max/step and value formatting already defined by the settings source. This task is a presentation/localization correction; do not silently alter tuning semantics.

Acceptance checks:

- Chinese and English screenshots both show all four mood names and all three parameter names in full.
- A player can identify which slider changes Pitch, Volume, or Jitter without hover help.
- Numeric input and +/- buttons are on the same line for every parameter.
- Slider is on the following line and spans the same track width for every parameter.
- No horizontal clipping at 736, 480, or 320 logical widths. At narrow widths, cards may stack vertically, but controls must remain usable.
- Existing values round-trip through slider, numeric input, +/- and reset actions.

## Task C — normalize left navigation cards

Target: `us/nav` and its page/category cards.

Use one navigation geometry contract:

- One fixed column width supplied by the page layout.
- Every card fills the column width exactly.
- One fixed minimum card height; selected and unselected cards use the same outer height unless a deliberate two-line variant is specified by the layout.
- Apply the same left/right padding, border thickness, and text inset to every card.
- Selected state may change border/accent/text color, but must not change width, height, or padding.
- Long subtitles must wrap or ellipsize inside the card; they must never resize the card.
- Keep a constant vertical gap between cards.

The visual target is a clean stack:

```text
[ Overview       ]
[ Distance       ]
[ Voice Packs    ]
[ Tuning         ]
[ Presets        ]
```

If a subtitle is retained, reserve the same two-line text band for every card so the stack remains geometrically stable.

Acceptance checks:

- Measure left and right edges of all cards: identical after rounding.
- Selected navigation card has identical outer bounds to unselected cards.
- Switching pages does not move the center column or resize the nav column.
- Chinese and English labels do not change card width.

## Implementation boundaries

- Prefer changes in US-owned layout resources/widgets and US localization keys.
- Do not use `active-tab` as a help-drawer visibility binding.
- Do not change FL's `Warning` alias or global theme slots to solve any of these visual issues.
- Do not fabricate new diagnostics states or alter current/previous evidence provenance.
- Do not add animation in this pass. Geometry and readability come first.

## Validation/reporting

Run the existing US verification script and focused UI tests. Add or update focused layout assertions for:

- diagnostic right-column alignment,
- navigation card equal bounds,
- mood parameter label presence and two-line geometry,
- Chinese/English localization key coverage,
- narrow-width overflow behavior.

Report:

1. changed files;
2. exact commands and pass/fail output;
3. screenshots at wide and narrow widths in both languages;
4. any remaining issue that can only be validated in RimWorld runtime.

Do not push, publish, install, tag, or create a PR as part of this task.
