using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>Sparse numeric parsing over FL's native editor, focus, IME and capture owner.</summary>
public sealed partial class UsSparseFieldWidget : IUiWidget, IUiPointerLayout
{
    public const string KindName = "us/sparse-field";
    private UiElementSpec spec = UiElementSpec.Empty;
    private UiEditSession<string>? observedEditor;
    public string Kind => KindName;
    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope,
        KindName, () => new UsSparseFieldWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"),
            new UiAttributeDefinition("Kind"), new UiAttributeDefinition("Bind", binding: UiBindingRole.Value), new UiAttributeDefinition("Field"),
            new UiAttributeDefinition("LabelKey"), new UiAttributeDefinition("EffectiveBind", binding: UiBindingRole.Value), new UiAttributeDefinition("OptionsBind", binding: UiBindingRole.Choices),
            new UiAttributeDefinition("LabelBind", binding: UiBindingRole.Value), new UiAttributeDefinition("HelpRefBind", binding: UiBindingRole.Value), new UiAttributeDefinition("HelpContextBind", binding: UiBindingRole.Value),
            new UiAttributeDefinition("HelpAction", binding: UiBindingRole.Command), new UiAttributeDefinition("HelpPayload", binding: UiBindingRole.Value))));
    public void Configure(UiElementSpec value) { spec = value; layoutEnvironment = null; }
    private string Binding => spec.TryGetAttribute("Bind", out string key) ? key : spec.Id;
    private string Field => spec.TryGetAttribute("Field", out string name) ? name : "parameter";
    private string Attr(string name) => spec.TryGetAttribute(name, out string value) ? value : "";
    public void Validate(IUiBindingAccess bindings, string path)
    {
        bindings.ValidateValue<string>(Binding, path);
        if (Attr("EffectiveBind") != "") bindings.ValidateValue<string>(Attr("EffectiveBind"), path);
        if (Attr("OptionsBind") != "" && bindings.Choices(Attr("OptionsBind")).ValueType != typeof(string)) throw new InvalidOperationException("Sparse boolean choices must be strings at " + path);
        foreach (string name in new[] { "LabelBind", "HelpRefBind", "HelpContextBind" })
            if (Attr(name) != "") bindings.ValidateValue<string>(Attr(name), path);
        if (Attr("HelpAction") != "") { bindings.ValidateAction<string>(Attr("HelpAction"), path); bindings.ValidateValue<string>(Attr("HelpPayload"), path); }
    }
    private float LabelHeight(UiWidgetContext ctx) => Attr("LabelKey") == "" ? 0 : UsKernelDraw.Text(ctx).MeasureText(ctx.Translation.Translate(Attr("LabelKey")), ctx.ViewWidth) + 2;
    private float EffectiveHeight(UiWidgetContext ctx, float width) => Attr("EffectiveBind") == "" ? 0
        : UsKernelDraw.Text(ctx).MeasureText(ctx.Bindings.Get<string>(Attr("EffectiveBind")), width);
    private string ErrorText(UiWidgetContext ctx) => ctx.Translation.Translate(Field == "activity" ? "US.Activity.Invalid" : "US.UI.RowInvalid");
    private bool InvalidDraft(UiWidgetContext ctx) => observedEditor?.Valid == false;
    public float Measure(UiWidgetContext ctx) => Attr("LabelBind") != "" ? CompactLayout(ctx, ctx.ViewWidth).Height : LabelHeight(ctx) + UsKernelDraw.ControlHeight(ctx)
        + (Attr("EffectiveBind") == "" ? 0 : 2 + EffectiveHeight(ctx, ctx.ViewWidth))
        + (InvalidDraft(ctx) ? 2 + UsKernelDraw.Text(ctx).MeasureText(ErrorText(ctx), ctx.ViewWidth) : 0);
    public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
    {
        if (Attr("LabelBind") != "")
        {
            var layout = CompactLayout(ctx, bounds.Width);
            yield return new UiPointerRegion(new UiLayoutRect(bounds.X + layout.FieldX, bounds.Y + layout.FieldY, layout.FieldWidth, layout.FieldHeight));
            yield break;
        }
        float label = LabelHeight(ctx); yield return new UiPointerRegion(new UiLayoutRect(bounds.X, bounds.Y + label, bounds.Width, UsKernelDraw.ControlHeight(ctx)));
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
        if (Attr("LabelBind") != "") { DrawCompact(rect, ctx); return; }
        string binding = Binding;
        float label = LabelHeight(ctx);
        if (label > 0) UsKernelDraw.StyledLabel(ctx, new UiLayoutRect(rect.X, rect.Y, rect.Width, label - 2), ctx.Translation.Translate(Attr("LabelKey")));
        float fieldWidth = Attr("HelpAction") == "" ? rect.Width : Math.Min(180, Math.Max(1, rect.Width - 34));
        float fieldHeight = UsKernelDraw.ControlHeight(ctx);
        var fieldRect = new UiLayoutRect(rect.X, rect.Y + label, fieldWidth, fieldHeight);
        if (Attr("HelpAction") != "" && UsKernelDraw.StyledButton(new UiLayoutRect(rect.X + fieldWidth + 6, rect.Y + label, 28, fieldHeight), ctx, "i", false, controlId: "explain-field", itemKey: binding))
            ctx.Bindings.Invoke(Attr("HelpAction"), ctx.Bindings.Get<string>(Attr("HelpPayload")));
        if (Attr("EffectiveBind") != "") UsKernelDraw.StyledLabel(ctx, new UiLayoutRect(rect.X, fieldRect.YMax + 2, rect.Width, EffectiveHeight(ctx, rect.Width)), ctx.Bindings.Get<string>(Attr("EffectiveBind")), emphasis: UiEmphasis.Muted);
        if (Attr("OptionsBind") != "")
        {
            var choices = ctx.Bindings.Choices(Attr("OptionsBind")).Read().Select(x => new UiChoice<string>(x.TextKey == "" ? x.Text : ctx.Translation.Translate(x.TextKey), (string)x.Value!, x.HelpRef)).ToArray();
            UsKernelDraw.StyledDropdown(fieldRect, binding, ctx, ctx.Bindings.Get<string>(binding), choices, value => ctx.Bindings.Set(binding, value));
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
            float y = fieldRect.YMax + 2 + (Attr("EffectiveBind") == "" ? 0 : 2 + EffectiveHeight(ctx, rect.Width));
            UsKernelDraw.StyledLabel(ctx, new UiLayoutRect(rect.X, y, rect.Width, UsKernelDraw.Text(ctx).MeasureText(ErrorText(ctx), rect.Width)), ErrorText(ctx), tone: UiStatusTone.Danger);
        }
    }

    private struct FieldLayout
    {
        internal float LabelWidth, LabelHeight, FieldX, FieldY, FieldWidth, FieldHeight, EffectiveX, EffectiveY, EffectiveWidth, EffectiveHeight, ErrorY, ErrorHeight, Height;
        internal string Label, Effective;
    }
    private FieldLayout CompactLayout(UiWidgetContext ctx, float width)
    {
        var text = UsKernelDraw.Text(ctx);
        FieldInputs inputs = CaptureFieldInputs(ctx, width);
        return FieldGeometry(ctx.Metrics, text, inputs);
    }
    private void DrawCompact(UiLayoutRect rect, UiWidgetContext ctx)
    {
        var layout = CompactLayout(ctx, rect.Width);
        UsKernelDraw.StyledLabel(ctx, new UiLayoutRect(rect.X, rect.Y, layout.LabelWidth, layout.LabelHeight), layout.Label, anchor: UiTextAnchor.UpperLeft);
        var field = new UiLayoutRect(rect.X + layout.FieldX, rect.Y + layout.FieldY, layout.FieldWidth, layout.FieldHeight);
        if (Attr("OptionsBind") != "")
        {
            var choices = ctx.Bindings.Choices(Attr("OptionsBind")).Read().Select(x => new UiChoice<string>(x.TextKey == "" ? x.Text : ctx.Translation.Translate(x.TextKey), (string)x.Value!, x.HelpRef)).ToArray();
            UsKernelDraw.StyledDropdown(field, Binding, ctx, ctx.Bindings.Get<string>(Binding), choices, value => ctx.Bindings.Set(Binding, value));
        }
        else
        {
            bool wasInvalid = InvalidDraft(ctx);
            ctx.Controls.Edit(field, ctx, Binding, ctx.Bindings.Get<string>(Binding), x => x, Parse, false, false,
                value => ctx.Bindings.Set(Binding, value), () => ctx.Bindings.Get<string>(Binding));
            observedEditor = ctx.Controls.Editor<string>(ctx, Binding, x => x, Parse);
            if (wasInvalid != InvalidDraft(ctx)) ctx.Invalidate();
            if (observedEditor.Modified || !observedEditor.Valid)
                UiThemeDraw.Solid(ctx.Drawing, new UiLayoutRect(field.X, field.YMax - 2, field.Width, 2), ctx.Theme[observedEditor.Valid ? UiPaletteRole.Accent : UiPaletteRole.DangerBorder]);
        }
        if (layout.EffectiveHeight > 0) UsKernelDraw.StyledLabel(ctx,
            new UiLayoutRect(rect.X + layout.EffectiveX, rect.Y + layout.EffectiveY, layout.EffectiveWidth, layout.EffectiveHeight), layout.Effective, anchor: UiTextAnchor.UpperLeft);
        if (layout.ErrorHeight > 0) UsKernelDraw.StyledLabel(ctx, new UiLayoutRect(rect.X, rect.Y + layout.ErrorY, rect.Width, layout.ErrorHeight), ErrorText(ctx), tone: UiStatusTone.Danger);
        if (Attr("HelpRefBind") != "")
        {
            string topic = ctx.Bindings.Get<string>(Attr("HelpRefBind"));
            if (topic.Length > 0 && ctx.Controls.IsMouseOver(rect, ctx))
            {
                ctx.Session.PublishHelp(topic);
                var anchor = ctx.State(UsHelpAnchor.StateKey, () => new UsHelpAnchor());
                anchor.Topic = topic; anchor.PublishedPass = ctx.Session.Frame;
                anchor.Label = layout.Label;
                anchor.Context = Attr("HelpContextBind") == "" ? "" : ctx.Bindings.Get<string>(Attr("HelpContextBind"));
            }
        }
    }
}
