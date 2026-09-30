# R4-B / B3+B4 — preset subtree on the shared row composition (US)

Status: **migrated, PM integration checked (2026-09-30).** Release/Dev compilation and the focused host lane pass locally; actual game rendering and native Def texture loading remain unverified.

## The shape

`us/preset-list` keeps its card chrome, section title, Import button, empty-list text and its three typed
actions (`toggle-baseline-preset`, `UsBaselineRaceToggle`, `UsBaselineXenoToggle`). Only its RACE and
XENOTYPE row drawing moved onto FL's shared per-row band, so `container/tree` and this card run the same
per-row code - one implementation, two consumers. The preset header is unchanged (its own two bands, the
Import button, the text expander and the full-band expand target).

## What the card calls (exact names)

- `FerriteLib.UiKit.Kernel.UiRowBand.Measure(row, theme, rowHeight)` - the reserved height, so the card's
  measure and the band it draws cannot disagree.
- `FerriteLib.UiKit.Kernel.UiRowBand.Draw(row, band, ctx, rowHeight, style, actions, layout)`.
- `FerriteLib.UiKit.Kernel.UiRowBandLayout(indentStep: …, checkboxRightInset: …, controlSize: …)` - the card's
  18f/12f level step, 10px right-edge inset and 18px checkbox visual. Hierarchy indentation is applied once
  by the band; row keys include the actual preset/race/xenotype identity. Measure and draw share row heights.
- `FerriteLib.UiKit.Kernel.UiRowBandActions(body, checkbox)` - each callback receives `row.Key`; the card
  ignores the argument and closes over that row's own `preset` / `race` / `xenotype` values instead, so
  there is no key parsing and no new dispatcher.
- `FerriteLib.UiKit.Kernel.Widgets.UiTreeRow` - `key`, `depth`, `text`, `checkable`, `isChecked`, `image`.
- `FerriteLib.UiKit.Kernel.UiThemeDraw.Image` (inside the band) - null answers false, so a row with no icon
  simply reserves no picture.

## The adapter boundary (unchanged, accepted)

`UI/Model/VoicePacksPageModel.cs:543` - `ResolveXenotypeIcon`, the ONE resource lookup, Verse side:
`ResolveXenotypeDef(name)?.Icon` over the null-safe `DefDatabase<XenotypeDef>.GetNamedSilentFail`. The value
rides `BaselineXenotypeView.Image` (`VoicePacksViewState.cs:349`, `Texture2D?`) and reaches the band as
data. Absent definition, no Biotech, null texture: all null, no substitute glyph. `UI/Kernel` accepts
`Texture2D` row data but performs no texture loading or Def lookup.

## The lane

`UniversalSqueakerKernelHostTests/Program.cs` - new step "preset rows compose through the shared row band
(R4-B)" - `PresetRowsComposeThroughTheSharedBand` (helpers `PresetCard`/`PresetCardHeight`,
`DrawnBoxAtX`, `PressAt`, `Recorded`, `ClearDrawBoxSolidCalls`). Extended the existing kernel-host suite.
It asserts: an icon grows the card through the shared measure while the same model with no icon does not
(the null fallback); the composition paints a box in the card's right-end control column and a real press
on THAT drawn box writes the row's own toggle and not expansion; collapsing reserves less than expanding;
and two hosts over two model instances keep separate expanded state. Fixture support:
`RecordingSettingsSource.XenotypeIcon`, `PresetExpanded`, `PresetToggleFlipsExpandedState`.

## Unverified

Real-game appearance and native icon lookup. `XenotypeDef.Icon` was confirmed against the 1.6.4871 reference
by metadata inspection, not by running the game; the stub scan explicitly records that loaded-Defs boundary.
The local lane uses supplied textures and exercises actual host draw/input against runtime stubs. It does
not establish real RimWorld runtime correctness.
