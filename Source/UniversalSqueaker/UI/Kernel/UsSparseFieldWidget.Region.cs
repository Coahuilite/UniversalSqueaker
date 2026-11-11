using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public sealed partial class UsSparseFieldWidget
{
    private sealed class CompactDisplay : UiRegionDisplay
    {
        internal readonly FieldLayout Layout;
        internal readonly string Error;
        internal CompactDisplay(FieldLayout layout, string error)
            : base(layout.Height, null, new[] { new UiPointerRegion(new UiLayoutRect(
                layout.FieldX, layout.FieldY, layout.FieldWidth, layout.FieldHeight)) })
        { Layout = layout; Error = error; }
    }

    public UiRegionDisplay Build(UiWidgetContext ctx, UiRegionSnapshot inputs)
    {
        // Production opts in only the compact behavior fields. Independent/legacy widgets still
        // have their complete Draw/Measure/PointerRegions path without an attachment.
        if (Attr("LabelBind") == "") throw new NotSupportedException("Region display requires a compact sparse field.");
        // Repeat Configure has already expanded binding attributes to @item/... addresses.
        // Build receives the production item packet, whose keys are the original short fields;
        // ordinary full-address reads remain exclusively on the Operate path below.
        string effective = Attr("EffectiveBind") == "" ? "" : inputs.Get<string>("field-effective");
        bool invalid = InvalidDraft(ctx); // Existing editor only; Build never creates or drives it.
        string error = invalid ? ErrorText(ctx) : "";
        var values = new FieldInputs(inputs.Get<string>("field-title"), effective, error,
            ctx.ViewWidth, UsKernelDraw.ControlHeight(ctx, 30), invalid);
        return new CompactDisplay(FieldGeometry(ctx.Metrics, UsKernelDraw.Text(ctx), values), error);
    }

    public void Operate(UiLayoutRect rect, UiWidgetContext ctx, UiRegionDisplay display)
    {
        var layout = ((CompactDisplay)display).Layout;
        var field = new UiLayoutRect(rect.X + layout.FieldX, rect.Y + layout.FieldY, layout.FieldWidth, layout.FieldHeight);
        if (Attr("OptionsBind") != "")
        {
            // Current choices/model and original native selector own execution, not the immutable
            // representation. Keep its controls, popup/help, qualification and IDs in original order.
            var choices = ctx.Bindings.Choices(Attr("OptionsBind")).Read().Select(x => new UiChoice<string>(
                x.TextKey == "" ? x.Text : ctx.Translation.Translate(x.TextKey), (string)x.Value!, x.HelpRef)).ToArray();
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
                UiThemeDraw.Solid(ctx.Drawing, new UiLayoutRect(field.X, field.YMax - 2, field.Width, 2),
                    ctx.Theme[observedEditor.Valid ? UiPaletteRole.Accent : UiPaletteRole.DangerBorder]);
        }
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

    public void Paint(UiLayoutRect rect, UiWidgetContext ctx, UiRegionDisplay display)
    {
        var compact = (CompactDisplay)display;
        var layout = compact.Layout;
        // No ordinary getter, editor/control or help operation on this pure path. No PaintClip
        // claim: preserve actual painting unless the backend proves an empty effective clip.
        UsKernelDraw.StyledLabel(ctx, new UiLayoutRect(rect.X, rect.Y, layout.LabelWidth, layout.LabelHeight),
            layout.Label, anchor: UiTextAnchor.UpperLeft);
        if (layout.EffectiveHeight > 0) UsKernelDraw.StyledLabel(ctx,
            new UiLayoutRect(rect.X + layout.EffectiveX, rect.Y + layout.EffectiveY, layout.EffectiveWidth, layout.EffectiveHeight),
            layout.Effective, anchor: UiTextAnchor.UpperLeft);
        if (layout.ErrorHeight > 0) UsKernelDraw.StyledLabel(ctx,
            new UiLayoutRect(rect.X, rect.Y + layout.ErrorY, rect.Width, layout.ErrorHeight), compact.Error, tone: UiStatusTone.Danger);
    }
}
