using System;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>The existing bounded browser, composed for one named final-table inventory.</summary>
public sealed class UsListBrowserWidget : IUiWidget
{
    public const string KindName = "us/list-browser";
    private string list = "table";
    public string Kind => KindName;
    public static void Register() => UiWidgetRegistry.Define(new UiWidgetDefinition(UsKernelWidgetRegistrar.Scope,
        KindName, () => new UsListBrowserWidget(), new UiAttributeSchema(new UiAttributeDefinition("Id"), new UiAttributeDefinition("Kind"), new UiAttributeDefinition("List"))));
    public void Configure(UiElementSpec spec) { if (spec.TryGetAttribute("List", out string id)) list = id; if (list != "table" && list != "candidate" && list != "sound") throw new ArgumentException("Unknown final-table inventory"); }
    public void Validate(IUiBindingAccess bindings, string path) => UsBrowseControls.Validate(bindings, path, list);
    public float Measure(UiWidgetContext ctx) => UsBrowseControls.Height(ctx, list, true, ctx.ViewWidth);
    public void Draw(UiLayoutRect rect, UiWidgetContext ctx) => UsBrowseControls.Draw(rect.X, rect.Y, rect.Width, ctx, list, list != "sound", true);
}
