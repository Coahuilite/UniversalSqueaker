using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// Registers every kernel-owned US composite widget kind in the greenfield
/// <see cref="UiWidgetRegistry"/> under the US scope, each with its creation-time
/// allowed-attribute contract. The kernel Host resolves these kinds and rejects unknown attributes
/// at creation time; no old neutral registry is involved.
/// </summary>
public static class UsKernelWidgetRegistrar
{
    public const string Scope = "coahuilite.universalsqueaker";

    private static readonly object Gate = new();
    private static bool registered;

    public static void EnsureRegistered()
    {
        // Self-healing: when a test harness cleared the widget registry, re-register instead of
        // skipping on the stale flag. Keeps repeated production calls idempotent while the
        // registry's own duplicate-kind rejection stays diagnosable.
        if (registered && UiWidgetRegistry.KnownKinds(Scope).Count > 0) return;

        lock (Gate)
        {
            if (registered && UiWidgetRegistry.KnownKinds(Scope).Count > 0) return;

            UsNavWidget.Register();
            UsSectionHeaderWidget.Register();
            // US-RESET1: the confirmation-gated restore entry. A ceremony the declarative vocabulary
            // cannot express: a press that opens the ordinary UiKit question window and stages the
            // effect key - input/button can only name a write binding, and a write-nothing ceremony
            // must not enter the write registry (UiSourceInvariant gate).
            UsResetEntryWidget.Register();
            // T21: the THIRD growth on the kind pin, and the same class of addition as the two below it - a
            // surface the declarative vocabulary cannot express. A selected domain row needs a FILL and a
            // vertical 3px rail; chrome/rule paints horizontal lines only, and the row's only
            // surface-capable element is its appearance-less hit band (Chrome="none"), which must not start
            // painting because doing so MOVES its geometry at 320px (S4-2 measured exactly that). So the
            // surface is a sibling that paints and does not hit.
            UsSelectionSurfaceWidget.Register();
            // R2 (2026-09-28): UsSquareToggleWidget is retired with its registration. The nine ON/OFF
            // controls are the carrier's `input/checkbox` with `Appearance="switch"` now: one boolean
            // BEHAVIOUR (two-state read-back, whole-band hit rule, the disabled funnel) with two looks, which
            // is exactly what the additive attribute carries. The old reason for a US kind - "the knob's end
            // depends on a bound value and no declarative element can express that" - is what the carrier's
            // switch appearance now expresses, in the shared vocabulary, over the shared input path.
            UsPageTitleWidget.Register();
            UsModeRowWidget.Register();
            // T3-2: UsDiagnosticsWidget is retired with its kind. The Overview diagnostics card is a declared
            // Section over atoms now (three input/button options + one string action + the page's own square
            // toggle), so the registration line and the widget file went together.
            UsScopeTreeWidget.Register();
            UsPresetListWidget.Register();
            UsFilterBarWidget.Register();
            UsVoicePackChecklistWidget.Register();
            UsKernelFooterWidget.Register();
            UsHelpPanelWidget.Register();
            UsCameraReadoutWidget.Register();
            // UIR-002/003: the Overview's all-loaded-race board - conditional status sentences over a
            // runtime-variable row set with two distinct write exits (navigation + exact enable). One
            // consumer kind, and both exits are the page's typed bindings; no raw IMGUI, no second engine.
            UsRaceBoardWidget.Register();
            // UIR-004: the final-fallback table editor, migrated from the tuning tree's third area to
            // the Packs page. Its bindings/commands are the page's pre-existing ones - this is the SAME
            // editor under its own return layer, not a second authority.
            UsFallbackEditorWidget.Register();

            registered = true;
        }
    }

    /// <summary>Shared attribute schema for US section composites.</summary>
    internal static readonly UiAttributeSchema SectionSchema = new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Title"), new UiAttributeDefinition("TitleKey"), new UiAttributeDefinition("HelpRef"), new UiAttributeDefinition("Tab"), new UiAttributeDefinition("Hidden", UiAttributeKind.Boolean), new UiAttributeDefinition("Height", UiAttributeKind.Length), new UiAttributeDefinition("TitleHidden", UiAttributeKind.Boolean));
}
