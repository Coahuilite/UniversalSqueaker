#if US_OFFLINE_WORKBENCH
using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

// The offline workbench adds optional author semantics to the same production widget.
public sealed partial class UsSparseFieldWidget : IUiDescriptiveWidget, IUiSemanticWidget
{
    public UiDescription Describe(UiWidgetContext ctx)
    {
        string name = Attr("LabelBind") != "" ? ctx.Bindings.Get<string>(Attr("LabelBind"))
            : Attr("LabelKey") != "" ? ctx.Translation.Translate(Attr("LabelKey")) : Field;
        string model = ctx.Bindings.Get<string>(Binding);
        string text = "model=" + model;
        if (observedEditor != null)
            text += "\ndraft=" + observedEditor.Draft + "; valid=" + observedEditor.Valid + "; conflict=" + observedEditor.Conflict;
        else text += "\ndraft=NotSampled; editor=not-created";
        if (Attr("EffectiveBind") != "") text += "\n" + ctx.Bindings.Get<string>(Attr("EffectiveBind"));
        return new UiDescription(UiDescriptionRole.Text, name, text, available: !ctx.IsDisabled);
    }

    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext ctx, int maximum)
    {
        if (maximum <= 0) return Array.Empty<UiSemanticAction>();
        // Declare intent only. Controls owns current input/writability/focus qualification;
        // reads neither create nor observe an editor into a different state.
        if (Attr("OptionsBind") == "")
            return new[] { UiToolOperation.SetDraft, UiToolOperation.CommitEdit, UiToolOperation.CancelEdit }
                .Take(maximum).Select(operation => new UiSemanticAction(operation, "editor")).ToArray();
        if (!ctx.Bindings.IsWritable(Binding)) return Array.Empty<UiSemanticAction>();
        return ctx.Bindings.Choices(Attr("OptionsBind")).Read().Take(maximum).Select(choice =>
            new UiSemanticAction(UiToolOperation.Select, "option", (string)choice.Value!,
                choice.TextKey == "" ? choice.Text : ctx.Translation.Translate(choice.TextKey))).ToArray();
    }

    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
    {
        if (Attr("OptionsBind") == "")
        {
            if (action.Part != "editor" || (action.Operation != UiToolOperation.SetDraft
                && action.Operation != UiToolOperation.CommitEdit && action.Operation != UiToolOperation.CancelEdit)) return false;
            // Exactly the ordinary Edit's string codec, non-Live policy and typed binding route.
            bool handled = ctx.Controls.EditSemantic<string>(ctx, Binding, action.Operation, value.Text,
                text => text, Parse, false, candidate => ctx.Bindings.Set(Binding, candidate), () => ctx.Bindings.Get<string>(Binding));
            if (handled) observedEditor = ctx.Controls.Editor<string>(ctx, Binding, text => text, Parse);
            return handled;
        }
        if (action.Operation != UiToolOperation.Select || action.Part != "option" || Attr("OptionsBind") == ""
            || ctx.IsDisabled || !ctx.Bindings.IsWritable(Binding)) return false;
        var matches = ctx.Bindings.Choices(Attr("OptionsBind")).Read().Where(option => Equals(option.Value, action.Item)).Take(1).ToArray();
        if (matches.Length == 0) return false;
        var choice = matches[0];
        var owner = ctx.Child(Binding);
        var target = owner.Controls.InteractionTarget(owner, Binding, "option", choice.Value);
        var input = owner.Session.Interactions.Activate(target, new UiInputEligibility(!owner.IsDisabled, owner.Bindings.IsWritable(Binding)));
        UiValueBehavior.Request(target, UiValueBehavior.Selection(input, (string)choice.Value!), candidate => owner.Bindings.Set(Binding, candidate));
        return input.Request;
    }
}
#endif
