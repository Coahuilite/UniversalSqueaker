using System;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public sealed partial class UsSparseFieldWidget
{
    private readonly struct FieldInputs : IEquatable<FieldInputs>
    {
        internal readonly string Label, Effective, Error;
        internal readonly float Width, ControlHeight;
        internal readonly bool Invalid;
        internal FieldInputs(string label, string effective, string error, float width, float controlHeight,
            bool invalid)
        { Label = label; Effective = effective; Error = error; Width = width; ControlHeight = controlHeight;
            Invalid = invalid; }
        public bool Equals(FieldInputs other) => Label == other.Label && Effective == other.Effective && Error == other.Error
            && Width == other.Width && ControlHeight == other.ControlHeight && Invalid == other.Invalid;
    }
    private FieldInputs CaptureFieldInputs(UiWidgetContext ctx, float width)
    {
        // Real getters remain at each original Measure/PointerRegions/Draw call. Only the
        // subsequent geometry/text calculation is reusable, never a model or editor operation.
        string label = ctx.Bindings.Get<string>(Attr("LabelBind"));
        string effectiveKey = Attr("EffectiveBind");
        string effective = effectiveKey == "" ? "" : ctx.Bindings.Get<string>(effectiveKey);
        float height = UsKernelDraw.ControlHeight(ctx, 30);
        bool invalid = InvalidDraft(ctx);
        return new FieldInputs(label, effective, invalid ? ErrorText(ctx) : "", width, height, invalid);
    }
    private ITextMetrics? layoutMetrics;
    private UiTextFont layoutFont;
    private UiMeasureInputs? layoutEnvironment;
    private FieldInputs layoutInputs;
    private FieldLayout layoutResult;
    private FieldLayout FieldGeometry(ITextMetrics metrics, UiTextRun text, FieldInputs inputs)
    {
        UiMeasureInputs? environment = text.CaptureEnvironment();
        if (environment != null && environment.Equals(layoutEnvironment) && ReferenceEquals(metrics, layoutMetrics)
            && text.Font == layoutFont && inputs.Equals(layoutInputs)) return layoutResult;
        layoutEnvironment = null;
        FieldLayout result = CalculateField(text, inputs);
        if (environment != null)
        { layoutMetrics = metrics; layoutFont = text.Font; layoutInputs = inputs; layoutResult = result; layoutEnvironment = environment; }
        return result;
    }
    private static FieldLayout CalculateField(UiTextRun text, FieldInputs inputs)
    {
        float width = inputs.Width;
        bool inline = width >= 540;
        var layout = new FieldLayout { Label = inputs.Label, Effective = inputs.Effective,
            LabelWidth = inline ? Math.Min(220, width * .32f) : width,
            FieldWidth = Math.Min(160, width), FieldHeight = inputs.ControlHeight };
        layout.LabelHeight = text.MeasureText(layout.Label, Math.Max(1, layout.LabelWidth));
        layout.FieldX = inline ? layout.LabelWidth + 8 : 0;
        layout.FieldY = inline ? 0 : layout.LabelHeight + 4;
        bool effectiveInline = width - layout.FieldX - layout.FieldWidth - 8 >= 140;
        layout.EffectiveX = effectiveInline ? layout.FieldX + layout.FieldWidth + 8 : 0;
        layout.EffectiveY = effectiveInline ? layout.FieldY : layout.FieldY + layout.FieldHeight + 4;
        layout.EffectiveWidth = Math.Max(1, width - layout.EffectiveX);
        layout.EffectiveHeight = layout.Effective == "" ? 0 : text.MeasureText(layout.Effective, layout.EffectiveWidth);
        layout.Height = Math.Max(layout.LabelHeight, Math.Max(layout.FieldY + layout.FieldHeight, layout.EffectiveY + layout.EffectiveHeight));
        if (inputs.Invalid)
        {
            layout.ErrorY = layout.Height + 4;
            layout.ErrorHeight = text.MeasureText(inputs.Error, Math.Max(1, width));
            layout.Height = layout.ErrorY + layout.ErrorHeight;
        }
        return layout;
    }
}
