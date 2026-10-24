using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

internal static class InteractionPreservationTests
{
    private static readonly UiLayoutRect View = new(0, 0, 1280, 1800);
    internal static void Run()
    {
        HelpScrollAndTheme();
        PackIdentityAndNativeAction();
        DirectoryNativeActions();
        FallbackCancelLayers();
        TextFitAndHit();
    }
    private static void HelpScrollAndTheme()
    {
        // The original BH1 scroll fixture uses the real translated catalog, whose maximum body
        // exceeds the declared viewport. Raw resource keys are not a valid long-content fixture.
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        var source = new RecordingSettingsSource { RichData = true };
        using var page = new Program.Page(UsKernelSettingsHost.Create(source, Program.Metrics));
        var view = new UiLayoutRect(0, 0, 760, 524);
        var closed = page.Host.MeasureAndArrange(view.Size); page.Draw(view);
        Program.Check(!closed.RectById.ContainsKey("help-scroll"), "help opens retracted");
        int revision = page.Host.Session.ContentRevision;
        Program.Press(page, view, "help-toggle");
        Program.Check(page.Host.Bindings.Get<bool>("help-open") && page.Host.Session.ContentRevision == revision + 1, "real footer hit opens help with one revision");
        var open = page.Host.MeasureAndArrange(view.Size); page.Draw(view);
        Program.Check(open.RectById["body-row"].Width == closed.RectById["body-row"].Width
            && open.Viewports["nav-column"].Width == closed.Viewports["nav-column"].Width, "help never widens body or navigation");
        Program.Check(Math.Abs((closed.RectById["body-row"].Height - open.RectById["body-row"].Height)
            - (open.Viewports["help-scroll"].Height + 8)) < .05f, "body returns exactly declared help140 plus rootgap8");
        page.Host.Bindings.Invoke("set-tab", "Packs"); page.Draw(view);
        page.Host.SetScrollPosition("content-scroll", new UiLayoutPoint(0, 60));
        page.Host.SetScrollPosition("help-scroll", new UiLayoutPoint(0, 90)); page.Draw(view);
        UiLayoutPoint content = page.Host.GetScrollPosition("content-scroll"), help = page.Host.GetScrollPosition("help-scroll");
        UiNode helpNode = page.Host.Session.GetNodeByElementId("help-scroll")!;
        Program.Check(content.Y > 0 && help.Y > 0 && content != help, "body and help really hold independent nonzero scroll: content=" + content.Y + " help=" + help.Y);
        page.Host.Bindings.Set("help-open", false); page.Draw(view);
        page.Host.Bindings.Set("help-open", true); page.Draw(view);
        Program.Check(ReferenceEquals(helpNode, page.Host.Session.GetNodeByElementId("help-scroll"))
            && page.Host.GetScrollPosition("help-scroll") == help && page.Host.GetScrollPosition("content-scroll") == content, "close/open retains help node and both scroll positions");
        var before = page.Host.MeasureAndArrange(view.Size);
        var colour = page.Host.StyleResolver.Baseline[UiPaletteRole.Accent];
        UsKernelSettingsHost.ApplySwatchLive(new GameSettings(), page.Host, UsTheme.ChromeTheme, "royalty"); page.Draw(view);
        var after = page.Host.MeasureAndArrange(view.Size);
        Program.Check(before.RectById.Count == after.RectById.Count && before.RectById.All(pair => after.RectById.TryGetValue(pair.Key, out var rect) && rect == pair.Value), "live theme changes zero actual arranged rects");
        Program.Check(page.Host.StyleResolver.Baseline[UiPaletteRole.Accent] != colour
            && page.Host.GetScrollPosition("help-scroll") == help && page.Host.GetScrollPosition("content-scroll") == content, "theme really repaints while preserving independent scroll");
        // Outside the actual button must not toggle it; the following real press closes it once.
        var toggle = Program.WindowRect(page, view, "help-toggle");
        Program.Click(page, view, new UiLayoutRect(toggle.XMax + 100, toggle.Y, 2, toggle.Height));
        Program.Check(page.Host.Bindings.Get<bool>("help-open"), "outside footer hit does not activate help");
        Program.Click(page, view, toggle);
        Program.Check(!page.Host.Bindings.Get<bool>("help-open"), "inside footer hit activates exactly its own control");
        Program.SetTranslatorResolver(null);
    }
    private static void PackIdentityAndNativeAction()
    {
        var source = new RecordingSettingsSource { RichData = true };
        using var page = new Program.Page(UsKernelSettingsHost.Create(source, Program.Metrics));
        source.RevisionSource = () => page.Host.Session.ContentRevision;
        page.Host.Bindings.Invoke("set-tab", "Packs"); page.Draw(View);
        Program.Press(page, View, "pack-card-row-expand#us.sang");
        Program.Check(source.TogglePackCardCount == 1, "actual materialized header press expands named pack exactly once; count=" + source.TogglePackCardCount);
        string race = "pack-card-row-check#us.sang|human", xeno = "pack-card-row-check#us.sang|human|sanguophage";
        Program.Press(page, View, race);
        Program.Check(source.PackWriteCount == 1 && source.LastPackKey == "us.sang" && source.LastPackScope == SqueakVoicePackScope.Race
            && source.LastPackRace == "human" && source.LastPackTarget == "" && source.LastPackEnabled == true, "actual row switch writes one exact Race identity");
        UiNode held = page.Host.Session.GetNodeByElementId(race)!;
        source.ReversePackOrder = true; page.Host.Invalidate(); page.Draw(View);
        Program.Check(ReferenceEquals(held, page.Host.Session.GetNodeByElementId(race)), "reordered materialized rows retain identity by item key, not index");
        Program.Press(page, View, xeno);
        Program.Check(source.PackWriteCount == 2 && source.LastPackScope == SqueakVoicePackScope.Xenotype
            && source.LastPackRace == "human" && source.LastPackTarget == "sanguophage" && source.LastPackEnabled == false,
            "reordered same-pack Xeno switch writes its own exact target once");
        string raceBind = UsWriteBindings.ItemKey("pack-card-keys", "us.sang|human", "enabled");
        Program.Check(page.Host.Bindings.Get<bool>(raceBind), "Xeno write does not overwrite neighboring Race read-back");
        Program.Press(page, View, "pack-card-row-hit#us.sang|human|sanguophage");
        // The retained recording source records SelectDomain; it deliberately does not mirror the
        // full selected-domain projection (the original PackCards lane asserts these same fields).
        Program.Check(source.LastSelectedScope == SqueakVoicePackScope.Xenotype
            && source.LastSelectedRace == "human" && source.LastSelectedTarget == "sanguophage", "actual domain hit keeps Namespace/target identity after reorder");
    }
    private static UiDevGeometrySnapshot Geometry(Program.Page page, string id)
    {
        page.Host.Diagnostics.GeometryEnabled = true; page.Host.Session.SetScrollTarget(id);
        // Public input snapshots sample pointer down/up, not idle Repaint. An outside release
        // observes all control queries without making a business request.
        page.Draw(View, EventType.MouseUp, new UiLayoutPoint(-1000, -1000));
        Program.Check(page.Host.Diagnostics.TryGetGeometrySnapshot(out var result) && result != null && result.InputsDropped == 0, "complete public control input observation " + id);
        return result!;
    }
    private static void DirectoryNativeActions()
    {
        var source = new RecordingSettingsSource { RichData = true, SeedRaceBoard = new[] {
            RecordingSettingsSource.SeedRow("pig", "Pig", racePacks: 1, supplyKeys: new[] { "us.pig" }, quickKey: "us.pig", quickScope: "Race"),
            RecordingSettingsSource.SeedRow("human", "Human", racePacks: 1, supplyKeys: new[] { "us.sang2" }, quickKey: "us.sang2", quickScope: "Race")
        }};
        using var page = new Program.Page(UsKernelSettingsHost.Create(source, Program.Metrics));
        var geometry = Geometry(page, "race-board-cards");
        var input = geometry.Inputs.Where(x => x.ElementId == "race-board-cards").ToArray();
        Program.Check(input.Length == 6, "two directory rows expose enable/open/body controls separately; actual=" + input.Length
            + " recorded=" + string.Join("|", geometry.Inputs.Select(x => x.ElementId + ":" + x.Rect)));
        Program.Click(page, View, input[3].Rect);
        Program.Check(source.QuickEnableCount == 1 && source.LastQuickRace == "human" && source.LastQuickPackKey == "us.sang2"
            && source.LastQuickScope == SqueakVoicePackScope.Race && source.LastMode == null, "actual non-first directory enable writes named pack/target, never mode");
        input = Geometry(page, "race-board-cards").Inputs.Where(x => x.ElementId == "race-board-cards").ToArray();
        Program.Click(page, View, input[4].Rect);
        Program.Check(source.LastRaceBoardOpenRace == "human" && source.LastRaceBoardScrollKey == "pack-card-row#us.sang2"
            && page.Host.Bindings.Get<string>(UiBindings.ActiveTabBind) == "Packs", "actual directory Open arrives at named second pack row");
        page.Draw(View);
        Program.Check(page.Host.Session.GetNodeByElementId("pack-card-row#us.sang2") != null, "arrival resolves actual materialized target rather than only retaining a command token");
    }
    private static void FallbackCancelLayers()
    {
        var source = new RecordingSettingsSource { RichData = true, FallbackTableAutoRace = "RaceA" };
        using var page = new Program.Page(UsKernelSettingsHost.Create(source, Program.Metrics));
        page.Host.Bindings.Invoke("set-tab", "Packs");
        source.ViewState.FallbackSelectedEntryAction = "us.core/Call";
        var geometry = Geometry(page, "fallback-editor");
        var inputs = geometry.Inputs.Where(x => x.ElementId == "fallback-editor" || x.Path.Contains("/fallback-editor/@")).ToArray();
        Program.Check(inputs.Length > 1, "actual fallback editor exposes interactive rows");
        // Open then dismiss its native race popup: this establishes the actual subtree as Cancel subject.
        Program.Click(page, View, inputs[0].Rect);
        Program.Check(page.Host.Session.OpenPopupId != null && page.Host.TryHandleCancel(), "fallback popup gets first Cancel"); page.Draw(View);
        Program.Check(page.Host.TryHandleCancel(), "next Cancel reaches actual fallback entry layer"); page.Draw(View);
        Program.Check(source.ViewState.FallbackSelectedEntryAction.Length == 0 && !source.ViewState.FallbackTableCanceled, "entry return leaves selected table");
        Program.Check(page.Host.TryHandleCancel(), "next Cancel reaches fallback table layer"); page.Draw(View);
        Program.Check(source.ViewState.FallbackTableCanceled && source.ViewState.FallbackSelectedRace.Length == 0
            && source.FallbackDeleteCalls == 0 && source.FallbackRestoreCalls == 0 && source.PackWriteCount == 0, "fallback returns write no table/config/audio business values");
        Program.Check(!page.Host.TryHandleCancel(), "declined fallback reaches root without hidden extra layer");
        page.Host.Dispose();
        Program.Check(!page.Host.Session.IsActive && page.Host.Session.OpenPopupId == null && page.Host.Session.ActiveEditNode == null,
            "host close releases popup/editor state");
    }
    private static void TextFitAndHit()
    {
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            var source = new RecordingSettingsSource { RichData = true, BuildIdentityOverride = "0.6.0+deliberately-long-current-build-identity-for-overflow", SaveStatusOverride = "Failed", IsDirtyOverride = true };
            var reports = new List<UiOverflowReport>();
            UiFitAudit.Reset(); UiFitAudit.Attach(Program.Metrics, reports.Add); UiFitAudit.Enabled = true;
            try
            {
                using var page = new Program.Page(UsKernelSettingsHost.Create(source, Program.Metrics));
                page.Host.Bindings.Set("help-open", true);
                page.Draw(new UiLayoutRect(0, 0, 760, 524));
                var affected = reports.Where(r => r.ElementPath.Contains("help-panel") || r.ElementPath.EndsWith("/footer", StringComparison.Ordinal)).ToArray();
                Program.Check(affected.Length == 0, language + " actual long footer and help bands fit measured text: " + string.Join("|", affected.Select(x => x.ElementPath + ":" + x.Needed + "/" + x.Available)));
                var layout = page.Host.MeasureAndArrange(new UiLayoutPoint(760, 524));
                Program.Check(layout.RectById["help-toggle"].X >= layout.RectById["footer"].XMax - .05f
                    && layout.RectById["help-toggle"].Height == 26, "actual footer text and button hit bands do not overlap");
                int prior = reports.Count;
                UiThemeDraw.Label(page.Execution, new UiLayoutRect(0, 0, 2, 2), "positive overflow control", UsTheme.Surface(), singleLine: true);
                Program.Check(reports.Count > prior, "text-fit guard detects real undersized paint positive control");
            }
            finally { UiFitAudit.Enabled = false; UiFitAudit.Detach(); }
        }
        Program.SetTranslatorResolver(null);
    }
}
