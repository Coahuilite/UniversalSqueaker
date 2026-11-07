using System;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelSettingsHost
{
    private static void PrepareHelpLayout(UiHost host, VoicePacksPageState state, UiLayoutPoint available, Action<float> applyHeight)
    {
        if (!state.HelpPanelOpen) return;
        SynchronizeHelpPresentation(host, state, available.X);
        var ctx = host.CreateContext(available.X);
        var helpNode = host.Session.GetNodeByElementId("help-panel");
        if (helpNode == null)
        {
            host.MeasureAndArrange(available);
            helpNode = host.Session.GetNodeByElementId("help-panel");
        }
        if (helpNode == null) return;
        // CreateContext is intentionally unresolving. Apply against its authoritative Host theme,
        // not a previously resolved element theme, before sharing the widget's actual font pair.
        ctx = UiTextFonts.Apply(ctx.WithNode(helpNode), ctx.Theme);
        string topic = DisplayHelpRef(state);
        UsHelpCatalog.TryGetSection(host.Bindings.TryGet("help-section-ref", out string section) ? section : "", out var fallback);
        var display = UsHelpPanelLogic.Resolve(fallback, topic, key => UsKernelDraw.Keyed(ctx, key));
        string text = display.TitleText + "\n" + display.LabelText + "\n" + display.BodyText + "\n" + DisplayHelpContext(state) + "\n" + state.FollowHelpLabel;
        var labelRun = UsKernelDraw.Text(ctx);
        var bodyRun = UsKernelDraw.Text(ctx);
        var small = labelRun.CaptureEnvironment();
        var tiny = bodyRun.CaptureEnvironment();
        if (state.HelpLayoutRevision == host.Session.ContentRevision && state.HelpViewWidth == available.X
            && state.HelpViewHeight == available.Y && state.HelpLayoutText == text && small != null && tiny != null
            && state.HelpLabelFont == labelRun.Font && state.HelpBodyFont == bodyRun.Font && state.HelpTextPurpose == labelRun.Purpose
            && state.HelpLabelRequested == labelRun.Requested && state.HelpBodyRequested == bodyRun.Requested
            && small.Equals(state.HelpSmallEnvironment) && tiny.Equals(state.HelpTinyEnvironment)) return;
        // Current text/environment may change without a business write (language or hover grace).
        if (state.HelpLayoutText != text || !Equals(small, state.HelpSmallEnvironment)
            || !Equals(tiny, state.HelpTinyEnvironment) || state.HelpLabelFont != labelRun.Font || state.HelpBodyFont != bodyRun.Font
            || state.HelpLabelRequested != labelRun.Requested || state.HelpBodyRequested != bodyRun.Requested
            || state.HelpTextPurpose != labelRun.Purpose) host.Invalidate();
        for (int pass = 0; pass < 2; pass++)
        {
            var layout = host.MeasureAndArrange(available);
            if (!layout.RectById.TryGetValue("business-body", out var body)
                || !layout.RectById.TryGetValue("footer-band", out var footer)
                || !layout.RectById.TryGetValue("help-header", out var header)
                || !layout.ScrollContents.TryGetValue("help-scroll", out var content)) break;
            // Use the real available bottom and measured footer height: an initial oversized
            // placeholder may itself push footer.Y down. Padding/gaps belong to our manifest,
            // not to copied window chrome. The resulting cap is independent of that placeholder.
            float budget = Math.Max(0f, (available.Y - 12f - footer.Height - 6f - body.Y) / 3f);
            float height = Math.Min(budget, header.Height + 4f + content.Height);
            if (Math.Abs(state.HelpHeight - height) < .01f) break;
            state.HelpHeight = height;
            applyHeight(height);
        }
        state.HelpViewWidth = available.X; state.HelpViewHeight = available.Y;
        state.HelpLayoutText = text; state.HelpLayoutRevision = host.Session.ContentRevision;
        state.HelpSmallEnvironment = small; state.HelpTinyEnvironment = tiny;
        state.HelpLabelFont = labelRun.Font; state.HelpBodyFont = bodyRun.Font; state.HelpTextPurpose = labelRun.Purpose;
        state.HelpLabelRequested = labelRun.Requested; state.HelpBodyRequested = bodyRun.Requested;
    }

}
