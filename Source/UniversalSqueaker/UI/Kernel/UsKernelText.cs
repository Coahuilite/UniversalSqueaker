using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

public static partial class UsKernelDraw
{
    /// <summary>Adopt the retained element's actual font. Unauthored legacy contexts keep their
    /// original Small/Tiny default; explicit author Font and FontSize remain FL-owned.</summary>
    internal static UiTextRun Text(UiWidgetContext ctx, UiFont legacy = UiFont.Small,
        UiTextPurpose purpose = UiTextPurpose.Label)
        => LegacyDefault(ctx)
            ? UiTextFonts.Resolve(ctx.Metrics, null, new UiTextFont(legacy), purpose, ctx.ElementPath)
            : UiTextFonts.Resolve(ctx, purpose);

    private static bool LegacyDefault(UiWidgetContext ctx) => !UiTextFonts.Requested(ctx.Theme).Size.HasValue
        && (ctx.Properties.ResolvedFont.Origin == UiPropertyOrigin.Fallback || string.IsNullOrEmpty(ctx.Properties.ResolvedFont.Source));

    // Legacy measure-only callers need no execution. Painting binds the same preset description to
    // the existing drawing port; authored text always uses FL's paired retained-context resolution.
    internal static UiTextRun TextForDrawing(UiWidgetContext ctx, UiFont legacy = UiFont.Small)
        => LegacyDefault(ctx) ? UiTextFonts.Resolve(ctx.Metrics, ctx.Drawing, new UiTextFont(legacy), elementPath: ctx.ElementPath)
            : UiTextFonts.Resolve(ctx);

    internal static void StyledLabel(UiWidgetContext ctx, UiLayoutRect rect, string text,
        UiFont legacy = UiFont.Small, UiTextAnchor anchor = UiTextAnchor.MiddleLeft,
        bool singleLine = false, UiStatusTone tone = UiStatusTone.Neutral, UiEmphasis emphasis = UiEmphasis.Normal)
        => TextForDrawing(ctx, legacy).Draw(rect, text, ctx.Theme.Styles.Resolve(tone, emphasis, writable: !ctx.IsDisabled).Text, anchor, singleLine);

    internal static float ControlHeight(UiWidgetContext ctx, float minimum = 28)
        => Math.Max(UiTextFonts.RowHeight(ctx, minimum: minimum),
            UiTextFonts.RowHeight(ctx, UiTextPurpose.Editor, minimum));

    internal static float TextBandHeight(float width, UiTextRun text, string value, float floor)
        => Math.Max(floor, text.MeasureText(value ?? "", Math.Max(1, width)));

    internal static float RowLabelHeight(float width, UiWidgetContext ctx, string value, UiTextRun text)
        => TextBandHeight(RowLabelWidth(width), text, value, RowVisualHeight(ctx));

    internal static float LineHeightOf(UiTextRun text) => Math.Max(1, text.MeasureText("Ag", 100000));

    internal static string Ellipsized(string value, UiTextRun text, float width)
    {
        if (string.IsNullOrEmpty(value) || text.MeasureWidth(value) <= width) return value;
        if (text.MeasureWidth("…") > width) return "";
        int lo = 0, hi = value.Length;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (text.MeasureWidth(value.Substring(0, mid) + "…") <= width) lo = mid; else hi = mid - 1;
        }
        return value.Substring(0, lo) + "…";
    }

    internal static string EllipsizedToLines(string value, UiTextRun text, float width, int maxLines)
    {
        if (string.IsNullOrEmpty(value) || maxLines <= 0) return value ?? "";
        float height = LineHeightOf(text) * maxLines;
        if (text.MeasureText(value, width) <= height) return value;
        if (text.MeasureText("…", width) > height) return "";
        int lo = 0, hi = value.Length;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (text.MeasureText(value.Substring(0, mid) + "…", width) <= height) lo = mid; else hi = mid - 1;
        }
        return value.Substring(0, lo) + "…";
    }

    internal static bool StyledButton(UiLayoutRect rect, UiWidgetContext ctx, string label, bool selected,
        bool danger = false, string controlId = "activation", object? itemKey = null)
    {
        RowSurface(ctx.Drawing, rect, ctx.Theme, ctx.Controls.IsMouseOver(rect), selected ? RowRail.Selected : RowRail.None, danger);
        var outlet = new UiLayoutRect(rect.X + SelectionButtonLabelInset, rect.Y,
            Math.Max(1, rect.Width - SelectionButtonLabelInset * 2), rect.Height);
        var text = TextForDrawing(ctx, UiFont.Tiny);
        text.Draw(outlet, Ellipsized(label, text, outlet.Width),
            ctx.Theme.Styles.Resolve(danger ? UiStatusTone.Danger : selected ? UiStatusTone.Active : UiStatusTone.Neutral, writable: !ctx.IsDisabled).Text,
            singleLine: true);
        return ctx.Controls.Activation(rect, ctx, controlId, controlId, itemKey).Request;
    }

    internal static void StyledDropdown(UiLayoutRect rect, string popupId, UiWidgetContext ctx, string current,
        IReadOnlyList<KeyValuePair<string, string>> options, Action<string> onSelected)
    {
        var choices = new UiChoice<string>[options.Count];
        for (int i = 0; i < options.Count; i++) choices[i] = new UiChoice<string>(options[i].Key, options[i].Value);
        StyledDropdown(rect, popupId, ctx, current, choices, onSelected);
    }

    internal static void StyledDropdown<T>(UiLayoutRect rect, string popupId, UiWidgetContext ctx, T current,
        IReadOnlyList<UiChoice<T>> choices, Action<T> onSelected)
    {
        if (choices.Count == 0) return;
        UiWidgetContext helpOwner = ctx;
        ctx = ctx.Child(popupId);
        var text = UiTextFonts.Resolve(ctx);
        string display = "";
        foreach (var choice in choices) if (EqualityComparer<T>.Default.Equals(choice.Value, current)) { display = ChoiceText(choice, ctx); break; }
        bool open = ctx.Session.IsPopupOpen(popupId);
        var style = ctx.Theme.Styles.Resolve(open ? UiStatusTone.Active : UiStatusTone.Neutral, writable: !ctx.IsDisabled);
        // SelectorField's empty label paints only its existing chrome/arrow. The paired run owns ink/fit.
        UiThemeDraw.SelectorField(ctx.Drawing, rect, "", ctx.Theme, style, text.Font.Alias);
        var outlet = UiThemeDraw.SelectorTextOutlet(rect, ctx.Theme);
        text.Draw(outlet, Ellipsized(display, text, outlet.Width), style.Text, singleLine: true);
        ctx.Controls.DropdownButton(rect, popupId, ctx);
        if (!ctx.Session.IsPopupOpen(popupId) || !ctx.Session.OpenPopupAnchor.HasValue) return;
        var anchor = ctx.Session.OpenPopupAnchor.Value;
        float width = anchor.Width;
        foreach (var choice in choices) width = Math.Max(width, text.MeasureWidth(ChoiceText(choice, ctx)) + 12);
        if (ctx.Session.HostViewport.Width > 0) width = Math.Min(width, ctx.Session.HostViewport.Width);
        anchor.Width = width;
        var popup = UiTextFonts.PopupRect(ctx, anchor, choices.Count, ctx.Session.HostViewport);
        ctx.Session.RegisterPopupDraw(() => {
            UiFitAudit.BeginElement(ctx.ElementPath + "/popup");
            try {
                int hovered = UiPopup.DrawChoices(popup, popupId, ctx, choices, current, onSelected);
                if (ctx.Session.IsActive && ctx.Session.IsPopupOpen(popupId)) PopupHelp(helpOwner, choices, hovered);
            }
            finally { UiFitAudit.EndElement(); }
        });
    }
}
