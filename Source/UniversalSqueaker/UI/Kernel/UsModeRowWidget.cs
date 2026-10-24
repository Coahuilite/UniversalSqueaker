using System;
using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>US-owned typed VoicePack mode selector; no enum/string conversion crosses the Host boundary.</summary>
public sealed class UsModeRowWidget : IUiWidget
{
    public const string Kind = "us/mode-row";
    private const float Height = 36f;
    private const float Gap = 6f;
    private const float Padding = 4f;

    /// <summary>Mode values stay the typed enum the "mode" binding carries; only the display name is keyed.</summary>
    private static readonly (SqueakVoicePackMode Mode, string LabelKey, string HelpRef)[] Options =
    {
        (SqueakVoicePackMode.Vanilla, "US.Tuning.Mode.Vanilla", "us/mode-row/vanilla"),
        (SqueakVoicePackMode.Fallback, "US.Tuning.Mode.Fallback", "us/mode-row/fallback"),
        (SqueakVoicePackMode.Remix, "US.Tuning.Mode.Remix", "us/mode-row/remix"),
        (SqueakVoicePackMode.Disabled, "US.Tuning.Mode.Disabled", "us/mode-row/disabled")
    };

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope, Kind, () => new UsModeRowWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Bind", binding: UiBindingRole.Value), new UiAttributeDefinition("Tab"), new UiAttributeDefinition("Hidden", UiAttributeKind.Boolean))));
    }

    public void Configure(UiElementSpec value)
    {
        spec = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        bindings.ValidateValue<SqueakVoicePackMode>(BindingKey(), elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        return Height + Padding * 2f;
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        string binding = BindingKey();
        ctx.Bindings.TryGet(binding, out SqueakVoicePackMode current);
        float width = Math.Max(1f, (rect.Width - Padding * 2f - Gap * (Options.Length - 1)) / Options.Length);
        float x = rect.X + Padding;
        for (int i = 0; i < Options.Length; i++)
        {
            UiLayoutRect optionRect = new(x, rect.Y + Padding, width, Height);
            UsKernelDraw.HelpHover(optionRect, ctx, Options[i].HelpRef);
            if (UsKernelDraw.SelectionButton(optionRect, ctx, ctx.Translation.Translate(Options[i].LabelKey), ctx.Theme, Options[i].Mode == current, controlId: binding, itemKey: Options[i].Mode))
            {
                ctx.Bindings.Set(binding, Options[i].Mode);
            }
            x += width + Gap;
        }
    }

    private string BindingKey()
    {
        return spec.TryGetAttribute("Bind", out string bind) && bind.Length > 0 ? bind : spec.Id;
    }
}
