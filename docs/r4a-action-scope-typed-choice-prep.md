# R4-A / A2 — action-scope typed choice: consumer integration (US)

Status: **integrated** against the landed FL seam. Not compiled or run (PM owns the build slot; every
behavioural claim here is unverified runtime).

## The anti-pattern that was removed

`Source/UniversalSqueaker/UI/Kernel/UsScopeTreeWidget.cs` bound the scope as text and parsed it back:
options were `KeyValuePair<string, string>` of (label, `scope.ToString()`) and the commit path reversed
it with `Enum.TryParse(selected, true, out SqueakActionScope parsed)` before
`Invoke("set-action-scope", new UsScopeWrite(...))`. The domain dropdown had the same shape with a
`\u0001` delimiter + `Split`.

Both are gone. There is no `ToString()` on a bound value, no `Enum.TryParse`/`Split` on a commit path,
and no string-keyed write anywhere in the flow.

## What the flow is now

- `UsKernelDraw.Dropdown<T>` (`UsKernelDraw.cs:643`) takes `IReadOnlyList<UiChoice<T>>` and an
  `Action<T>`; the label is `UiChoice.Text` and the committed value is `UiChoice.Value`. The trigger
  matches the current value by **value**, never by label. The string overload (`:610`) keeps its exact
  display rule and shares the rendering through the private `DrawDropdownCore` (`:684`), so the popup
  layer push, the clamp/flip rule and the owner-id hit rule still exist once.
- The row list FL's `UiPopup.DrawOptionList` draws is still string-payloaded, so the payload is the
  **row index** and the chosen row maps back to its own choice. Matching by label would collapse two
  options that share a label.
- `UsScopeTreeWidget.DrawScopeRow` (`:413-432`) builds `UiChoice<SqueakActionScope?>` — the Auto entry's
  value is a real `null` — sets the current value to `row.HasOwnScope ? row.Scope : null`, and commits
  `new UsScopeWrite(row.ActionKey, scope)` with the option's own value. Null-inherit is a value, not an
  empty token.
- The domain dropdown now carries `UsTuningDomainSelection` instead of a joined string.

Unchanged on purpose: `SupportedStates(row.Action)` filtering, `ScopeLabelKey` (EN/ZH labels),
`us/scope-tree/action-scope` and `us/scope-tree/auto` option help, the inherited-scope hint, the tree
geometry and the selection rendering.

## FL seam consumed (exact names)

- `FerriteLib.UiKit.Kernel.UiChoice<T>` — `Text`, `Value` (`ferritelib/.../Kernel/UiChoice.cs:17`).
- `FerriteLib.UiKit.Kernel.UiPopup.DrawOptionList` / `UiPopup.OptionHeight` — the unchanged row geometry.

Not used: `IUiTypedChoices`. It exists for a **non-generic** widget that cannot name the consumer's `T`;
`UsScopeTreeWidget` can, so it consumes the carrier directly and needs no type-erased probe. Nothing
from this flow is missing.

## Lane

`UniversalSqueakerKernelHostTests/Program.cs` — new step "a scope option commits its typed value, never
its label (R4-A)" → `TypedScopeChoiceCommitsTheValue` (+ `ClickScopeOption`/`PressScopePopup`/
`TestActionOf`). Extended the existing kernel-host suite; no parallel suite. It drives the real
composite: opens the real popup, presses a real option row, and asserts the committed
`SqueakActionScope` instance (and `null` for the inherit row) arrived at `SetActionScope`, so a commit
that took the displayed text would red. A press that misses the option list must commit nothing, so the
typed path is not a wider hit target than the row rule was.
