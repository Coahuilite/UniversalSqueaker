using System;

using UnityEngine;

using FerriteLib.UiKit.Kernel;


namespace UniversalSqueaker.UI;

/// <summary>
/// US-RESET1: a confirmation-gated restore entry for the declarative cards. The button itself is NOT a
/// write binding - the UiSourceInvariant gate allows raw registrations in the UsWriteBindings funnel
/// only, and a ceremony that writes nothing until the answer arrives must not enter the revision-clock
/// registry either (opening a question changes no page state; the EFFECT key behind it does). So this
/// composite owns exactly what the VF1 fallback editor already owns: the press opens ONE ordinary UiKit
/// confirmation window (the UsConfirmWindow precedent - not the Remix double-swap), and the staged
/// action runs the declared effect key through the same UiBindings the page writes go through.
///
/// <para>
/// Attributes: TextKey (the button caption), TitleKey/MessageKey (the question; the message lists the
/// fields the restore writes and names what it keeps - display copy pinned to the backend's field set
/// by the reset harness), EffectKey (the registered command the confirmed press invokes exactly once)
/// and the usual Height/Width/HelpRef. Cancel closes the window and runs nothing; a second Open drops
/// the previous staged action, so no late answer can write after the player changed their mind.
/// </para>
/// </summary>
public sealed class UsResetEntryWidget : IUiWidget
{
    public const string Kind = "us/reset-entry";

    /// <summary>The band the button is drawn in when nothing declares a height.</summary>
    private const float DefaultHeight = 24f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, Kind, () => new UsResetEntryWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("TextKey"), new UiAttributeDefinition("TitleKey"), new UiAttributeDefinition("MessageKey"), new UiAttributeDefinition("EffectKey"), new UiAttributeDefinition("HelpRef"), new UiAttributeDefinition("Tab"), new UiAttributeDefinition("Hidden", UiAttributeKind.Boolean), new UiAttributeDefinition("Height", UiAttributeKind.Length), new UiAttributeDefinition("Width", UiAttributeKind.Length), new UiAttributeDefinition("AlignX"), new UiAttributeDefinition("Emphasis"))));
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        // The effect key is a per-element ATTRIBUTE, and Validate sees no spec - the same asymmetry
        // every keyed composite has. A wrong key cannot rot silently: the staged action runs through
        // TryInvokeCommand (an unregistered key answers false and writes nothing), and the ResetUi
        // lane presses this widget's real button and requires the effect to land.
    }

    public float Measure(UiWidgetContext ctx)
    {
        return spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float height)
            ? Math.Max(1f, height)
            : UiTextFonts.RowHeight(ctx, minimum: DefaultHeight);
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        if (UsKernelDraw.StyledButton(
                rect, ctx,
                UsKernelDraw.Keyed(ctx, Attribute("TextKey")),
                false))
        {
            IUiBindingAccess editorBindings = ctx.Bindings;
            string effectKey = Attribute("EffectKey");
            if (effectKey.Length == 0) return;
            UsConfirmWindow.Open(
                UsKernelDraw.Keyed(ctx, Attribute("TitleKey")),
                UsKernelDraw.Keyed(ctx, Attribute("MessageKey")),
                UsKernelDraw.Keyed(ctx, "US.Reset.Confirm"),
                () => editorBindings.TryInvokeCommand(effectKey));
        }
    }

    private string Attribute(string name)
        => spec.TryGetAttribute(name, out string value) ? value ?? "" : "";
}
