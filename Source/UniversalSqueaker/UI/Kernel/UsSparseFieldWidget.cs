using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Sparse numeric parsing over FL's native editor, focus, IME and capture owner.</summary>
public sealed class UsSparseFieldWidget : IUiWidget, IUiPointerLayout
{
    public const string KindName = "us/sparse-field";
    private UiElementSpec spec = UiElementSpec.Empty;
    private UiEditSession<string>? observedEditor;
    public string Kind => KindName;
    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope,
        KindName, () => new UsSparseFieldWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"),
            new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Bind", binding: UiBindingRole.Value), new UiAttributeDefinition("Field"),
            new UiAttributeDefinition("LabelKey"), new UiAttributeDefinition("EffectiveBind"), new UiAttributeDefinition("OptionsBind"),
            new UiAttributeDefinition("HelpAction"), new UiAttributeDefinition("HelpPayload"))));
    public void Configure(UiElementSpec value) => spec = value;
    private string Binding => spec.TryGetAttribute("Bind", out string key) ? key : spec.Id;
    private string Field => spec.TryGetAttribute("Field", out string name) ? name : "parameter";
    private string Attr(string name) => spec.TryGetAttribute(name, out string value) ? value : "";
    public void Validate(IUiBindingAccess bindings, string path)
    {
        bindings.ValidateValue<string>(Binding, path);
        if (Attr("EffectiveBind") != "") bindings.ValidateValue<string>(Attr("EffectiveBind"), path);
        if (Attr("OptionsBind") != "" && bindings.Choices(Attr("OptionsBind")).ValueType != typeof(string)) throw new InvalidOperationException("Sparse boolean choices must be strings at " + path);
        if (Attr("HelpAction") != "") { bindings.ValidateAction<string>(Attr("HelpAction"), path); bindings.ValidateValue<string>(Attr("HelpPayload"), path); }
    }
    private float LabelHeight(UiWidgetContext ctx) => Attr("LabelKey") == "" ? 0 : ctx.Metrics.MeasureText(ctx.Translation.Translate(Attr("LabelKey")), UiFont.Small, ctx.ViewWidth) + 2;
    private string ErrorText(UiWidgetContext ctx) => ctx.Translation.Translate(Field == "activity" ? "US.Activity.Invalid" : "US.UI.RowInvalid");
    private bool InvalidDraft(UiWidgetContext ctx) => observedEditor?.Valid == false;
    public float Measure(UiWidgetContext ctx) => LabelHeight(ctx) + 28 + (Attr("EffectiveBind") == "" ? 0 : 2 + ctx.Metrics.MeasureText(ctx.Bindings.Get<string>(Attr("EffectiveBind")), UiFont.Small, ctx.ViewWidth))
        + (InvalidDraft(ctx) ? 2 + ctx.Metrics.MeasureText(ErrorText(ctx), UiFont.Small, ctx.ViewWidth) : 0);
    public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
    {
        float label = LabelHeight(ctx); yield return new UiPointerRegion(new UiLayoutRect(bounds.X, bounds.Y + label, bounds.Width, 28));
    }
    private bool Parse(string text, out string value)
    {
        value = text.Trim();
        if (value == "") return Field != "activity";
        if (Field == "interval") return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks) && ticks >= 0;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number)) return false;
        return Field == "pitch" ? number > 0 && number <= float.MaxValue : Field == "volume" ? number >= 0 && number <= float.MaxValue
            : Field == "activity" ? number > 0 : Field == "interaction-seconds" ? number >= 0 : true;
    }
    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        string binding = Binding;
        float label = LabelHeight(ctx);
        if (label > 0) UsKernelDraw.Label(new UiLayoutRect(rect.X, rect.Y, rect.Width, label - 2), ctx.Translation.Translate(Attr("LabelKey")), ctx, UiFont.Small);
        float fieldWidth = Attr("HelpAction") == "" ? rect.Width : Math.Min(180, Math.Max(1, rect.Width - 34));
        var fieldRect = new UiLayoutRect(rect.X, rect.Y + label, fieldWidth, 28);
        if (Attr("HelpAction") != "" && UsKernelDraw.SelectionButton(new UiLayoutRect(rect.X + fieldWidth + 6, rect.Y + label, 28, 28), ctx, "i", ctx.Theme, false, controlId: "explain-field", itemKey: binding))
            ctx.Bindings.Invoke(Attr("HelpAction"), ctx.Bindings.Get<string>(Attr("HelpPayload")));
        if (Attr("EffectiveBind") != "") UsKernelDraw.Label(new UiLayoutRect(rect.X, fieldRect.YMax + 2, rect.Width, Math.Max(1, rect.YMax - fieldRect.YMax - 2)), ctx.Bindings.Get<string>(Attr("EffectiveBind")), ctx, UiFont.Small);
        if (Attr("OptionsBind") != "")
        {
            var choices = ctx.Bindings.Choices(Attr("OptionsBind")).Read().Select(x => new UiChoice<string>(x.TextKey == "" ? x.Text : ctx.Translation.Translate(x.TextKey), (string)x.Value!, x.HelpRef)).ToArray();
            UsKernelDraw.Dropdown(fieldRect, binding, ctx, ctx.Bindings.Get<string>(binding), choices, value => ctx.Bindings.Set(binding, value));
            return;
        }
        ctx.Controls.Edit(fieldRect, ctx, binding, ctx.Bindings.Get<string>(binding), x => x, Parse, false, false,
            value => ctx.Bindings.Set(binding, value), () => ctx.Bindings.Get<string>(binding));
        var editor = ctx.Controls.Editor<string>(ctx, binding, x => x, Parse);
        observedEditor = editor;
        // Feedback is drawn through the normal theme, never a second text control.
        if (editor.Modified || !editor.Valid)
            UiThemeDraw.Surface(ctx.Drawing, new UiLayoutRect(fieldRect.X, fieldRect.YMax - 2, fieldRect.Width, 2), ctx.Theme,
                ctx.Theme[editor.Valid ? UiPaletteRole.Accent : UiPaletteRole.DangerBorder], ctx.Theme[editor.Valid ? UiPaletteRole.Accent : UiPaletteRole.DangerBorder]);
        if (!editor.Valid)
        {
            float y = fieldRect.YMax + 2 + (Attr("EffectiveBind") == "" ? 0 : 2 + ctx.Metrics.MeasureText(ctx.Bindings.Get<string>(Attr("EffectiveBind")), UiFont.Small, rect.Width));
            UsKernelDraw.Label(new UiLayoutRect(rect.X, y, rect.Width, ctx.Metrics.MeasureText(ErrorText(ctx), UiFont.Small, rect.Width)), ErrorText(ctx), ctx, UiFont.Small);
        }
    }
}
