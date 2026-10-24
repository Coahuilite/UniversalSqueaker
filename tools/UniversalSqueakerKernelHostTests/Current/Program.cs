using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;
using UnityEngine;
using Verse;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>Fixed FL consumer tests. Native operations use the actual VerseUiExecution from the
/// selected DLL against identified platform doubles. No private static FL control override exists.</summary>
internal static partial class Program
{
    internal const string Scope = "coahuilite.universalsqueaker";
    internal static readonly StubMetrics Metrics = new();
    private static int assertions;
    private static string? lane;
    internal static string? ExistingCaseFilter;
    private static readonly UiLayoutRect View = new(0, 0, 1000, 1200);
    private static string Repository => FindRepository();
    private static int Main(string[] args)
    {
        Console.WriteLine("ENTRY current fixed-FL consumer ARGS=[" + string.Join(",", args) + "]");
        Console.WriteLine("FL=" + typeof(UiHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        Console.WriteLine("US=" + typeof(SqueakerMod).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => e.Name.StartsWith("0Harmony", StringComparison.Ordinal)
            ? typeof(SqueakerMod).BaseType!.Assembly : null;
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            if (e.Exception.GetType().Name == "UiRequestFailure" || e.Exception is UiReactiveFault)
                Console.WriteLine("REQUEST-FIRST-CHANCE " + e.Exception);
        };
        try
        {
            typeof(GameSettings).Assembly.GetType("UniversalSqueaker.GameRuntime", true)!
                .GetMethod("InitializeMainThread", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
            if (args.Length != 0 && args[0].StartsWith("--lane=", StringComparison.Ordinal)) lane = args[0].Substring(7);
            else if (args.Length != 0 && args[0] != "--fixed-fl") throw new Exception("Unknown test mode");
            if (lane != null && lane.StartsWith("existing-", StringComparison.Ordinal))
            {
                ExistingCaseFilter = lane.Substring("existing-".Length);
                lane = "existing diagnostics";
                Console.WriteLine("EXISTING CASE FILTER=" + ExistingCaseFilter);
            }
            string expectedCarrier = typeof(SqueakerMod).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "FerriteLib.SHA256").Value!;
            using (var carrierBytes = File.OpenRead(typeof(UiHost).Assembly.Location))
            using (var digest = System.Security.Cryptography.SHA256.Create())
                Check(BitConverter.ToString(digest.ComputeHash(carrierBytes)).Replace("-", "") == expectedCarrier,
                    "actual loaded FL DLL matches the product compiler-reference receipt");
            Run("manifest / five workspaces / bilingual help / geometry", WorkspaceAndResources);
            Run("ordinary getters / held property views / native Live and retained draft", EditingAndProperties);
            Run("typed choices / explicit and empty help / source and15passes / no sink", HelpAndTypedChoices);
            Run("full binding proxy / same-Host backing change", ProxyCapabilities);
            Run("real confirmation window / cancel zero / confirm once", Confirmation);
            Run("overlay execution lifetime / separate hosts / image ownership", OverlayAndImages);
            Run("Eat and Select actual production read-model", () => { DbEatUiTests.Run(); DcSelectUiTests.Run(); });
            Run("preservation: help scroll/theme geometry/fallback/list identities/text hit", InteractionPreservationTests.Run);
            Run("existing diagnostics panel lane (round-9 contract)", DiagnosticsPanelLaneTests.RunAll);
            Run("existing preset rows compose through the shared row band (R4-B)", PresetRowsComposeThroughTheSharedBand);
            Run("roots / RichDynamicDataMeasureAndDraw", RichDynamicDataMeasureAndDraw);
            Run("roots / CompositeDropdownPublishesCoveringRect", CompositeDropdownPublishesCoveringRect);
            Run("roots / TypedScopeChoiceCommitsTheValue", TypedScopeChoiceCommitsTheValue);
            Run("roots / LongAuthorFilterTruncatesTheDisplayOnly", LongAuthorFilterTruncatesTheDisplayOnly);
            Run("roots / PopupOverflowReportCarriesElementIdentity", PopupOverflowReportCarriesElementIdentity);
            Run("roots / PopupWidthFollowsWidestOptionThenViewport", PopupWidthFollowsWidestOptionThenViewport);
            Run("roots / OverlayDrawFailureDoesNotDoubleReserveRow", OverlayDrawFailureDoesNotDoubleReserveRow);
            Run("roots / SettingsWindowShellCarriesTheFailureContract", SettingsWindowShellCarriesTheFailureContract);
            Run("old-v4 / DistanceAndPresetsAtTheRealBoxes", V4RemainingLaneTests.DistanceAndPresetsAtTheRealBoxes);
            if (ran == 0) throw new Exception("No matching test group: " + lane);
            Console.WriteLine("PASS " + assertions + " fixed-FL consumer assertions; platform doubles, not native game/Config acceptance");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Fl08PublicObservation.DisposeExecutions(); SetTranslatorResolver(null); Event.current = null; }
    }
    private static int ran;
    private static void Run(string name, Action action)
    {
        if (lane != null && name.IndexOf(lane, StringComparison.OrdinalIgnoreCase) < 0) return;
        ran++; Console.WriteLine("BEGIN " + name); action(); Console.WriteLine("PASS " + name);
    }
    internal static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        assertions++; Console.WriteLine("OK " + message);
    }
    internal sealed class Page : IDisposable
    {
        internal readonly UiHost Host;
        internal readonly VerseUiExecution Execution;
        internal readonly Fl08PublicObservation.PaintedControlExecution Observation;
        internal Page(UiHost host, VerseUiExecution? execution = null) { Host = host; Execution = execution ?? new VerseUiExecution(); Observation = new(host, Execution); }
        internal void Draw(UiLayoutRect viewport, EventType phase = EventType.Repaint, UiLayoutPoint? pointer = null)
        {
            Event? previous = Event.current;
            try
            {
                Event.current = new Event { type = phase, button = 0, mousePosition = VerseUiExecution.Point(pointer ?? new UiLayoutPoint(-1000, -1000)) };
                Observation.BeginPass();
                Host.DrawFrame(viewport, Execution.CaptureFrame(), Observation);
                NoTrips(Host);
            }
            finally { Event.current = previous; }
        }
        public void Dispose() { try { Host.Dispose(); } finally { Execution.Dispose(); } }
    }
    private static void NoTrips(UiHost host)
    {
        var observation = host.Observe(new UiObservationBudget(512, 32));
        if (observation.Truncated) throw new Exception("Incomplete recovery observation");
        foreach (var node in observation.Nodes)
        {
            string? id = node.Facts.FirstOrDefault(f => f.Name == "identity")?.Value;
            UiNode? actual = id == null ? null : host.Session.GetNodeByElementId(id);
            if (actual != null && host.Session.IsTripped(actual))
            { host.Session.TryGetTripLog(actual, out string diagnostic); throw new Exception(node.Path + ": " + diagnostic); }
        }
    }
    internal static UiLayoutRect WindowRect(Page page, UiLayoutRect view, string id)
    {
        page.Host.Session.SetScrollTarget(id);
        page.Draw(view);
        var layout = page.Host.MeasureAndArrange(view.Size);
        Check(layout.RectById.ContainsKey(id), "actual completed Release arrangement available");
        Check(layout.VisibleIds.Count(value => value == page.Host.Session.GetNodeByElementId(id)!.Path) == 1, "exact arranged input identity " + id);
        return Fl08PublicObservation.NodeWindowRect(page.Host, page.Host.Session.GetNodeByElementId(id)!);
    }
    internal static void Click(Page page, UiLayoutRect view, UiLayoutRect rect)
    {
        Action<string>? priorTrace = page.Host.Session.Controls.Trace;
        page.Host.Session.Controls.Trace = text => { priorTrace?.Invoke(text); Console.WriteLine("CONTROL " + text); };
        try
        {
        var point = rect.Center;
        page.Draw(view, EventType.MouseDown, point);
        Console.WriteLine("GESTURE down rect=" + rect + " capture=" + page.Host.Session.Interactions.CaptureOwner?.Node + " native=" + GUIUtility.hotControl);
        if (page.Host.Diagnostics.TryGetGeometrySnapshot(out var down) && down != null)
            foreach (var input in down.Inputs.Where(x => x.Rect.Contains(point))) Console.WriteLine("INPUT " + input.ElementId + " " + input.Rect + " " + input.Verdict + " " + input.EventBefore + "->" + input.EventAfter);
        page.Draw(view, EventType.MouseUp, point);
        if (page.Host.Diagnostics.TryGetGeometrySnapshot(out var up) && up != null)
            foreach (var input in up.Inputs.Where(x => x.Rect.Contains(point))) Console.WriteLine("INPUT-UP " + input.ElementId + " " + input.Rect + " " + input.Verdict + " " + input.EventBefore + "->" + input.EventAfter);
        }
        finally { page.Host.Session.Controls.Trace = priorTrace; }
    }
    internal static void Press(Page page, UiLayoutRect view, string id) => Click(page, view, WindowRect(page, view, id));
    private static void WorkspaceAndResources()
    {
        var assembly = typeof(UsKernelSettingsHost).Assembly;
        foreach (string resource in new[] { "Layout.Schema2.xml", "Layout.Overlay.Schema2.xml", "Layout.RemixConfirm.Schema2.xml" })
        {
            using Stream stream = assembly.GetManifestResourceStream("UniversalSqueaker.UI." + resource)!;
            Check(stream != null, "real embedded " + resource);
            using var reader = new StreamReader(stream!);
            string xml = reader.ReadToEnd();
            Check(!xml.Contains("HelpKey=") && !xml.Contains("VisibleKey=") && !xml.Contains("PayloadKey="), "resource uses actual Ref/Bind vocabulary " + resource);
            UiLayoutManifest.Parse(xml);
        }
        foreach (string language in new[] { "English", "ChineseSimplified" })
        {
            var table = ReadKeyedTable(language); SetTranslatorResolver(table);
            using var page = new Page(UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, Metrics));
            Check(page.Host.Session.HelpGracePasses == 15, "US retains15 help passes " + language);
            foreach (float width in new[] { 760f, 480f })
            foreach (string tab in new[] { "Overview", "Packs", "Tuning", "Presets", "Advanced" })
            foreach (bool help in new[] { false, true })
            {
                page.Host.Bindings.Invoke("set-tab", tab); page.Host.Bindings.Set("help-open", help);
                var view = new UiLayoutRect(0, 0, width, 900);
                UiLayoutSnapshot layout = page.Host.MeasureAndArrange(view.Size);
                Check(layout.VisibleIds.Count > 10 && layout.RectById["footer"].Width > 0, language + "/" + width + "/" + tab + "/help=" + help + " actual layout");
                page.Draw(view);
                Check(layout.RectById.ContainsKey("help-panel") == help, "unified help arrangement follows declared VisibleBind");
            }
            Check(page.Host.Manifest.Roots.Count > 0 && page.Host.Bindings.BindingOwner != null, "actual manifest and concrete registry retained");
        }
        SetTranslatorResolver(null);
    }
    private static bool ParseText(string text, out string value) { value = text; return true; }
    private static void EditingAndProperties()
    {
        var source = new RecordingSettingsSource { RichData = true };
        using var page = new Page(UsKernelSettingsHost.Create(source, Metrics));
        page.Draw(View);
        var field = page.Host.ElementProperties("global-volume-slider");
        var parent = page.Host.ElementProperties("page-root");
        var metrics = field.ResolvedMetrics; var colours = field.ResolvedColours;
        float original = metrics["RowHeight"].Value;
        Check(metrics.Count == 5 && colours.Count == 30, "ordinary production property views read all geometry and palette roles");
        field.Metric("RowHeight").Set(60); page.Draw(View);
        Check(metrics["RowHeight"].Value == 60 && metrics["RowHeight"].Origin == UiPropertyOrigin.Instance, "held view sees override");
        field.Metric("RowHeight").Set(64); parent.Metric("RowHeight").Set(52); page.Draw(View);
        Check(metrics["RowHeight"].Value == 64 && ReferenceEquals(metrics, field.ResolvedMetrics) && ReferenceEquals(colours, field.ResolvedColours), "same held view sees update and local precedence");
        field.Metric("RowHeight").Remove(); parent.Metric("RowHeight").Set(56); page.Draw(View);
        Check(metrics["RowHeight"].Value == 56, "remove reveals current ancestor");
        parent.Metric("RowHeight").Remove(); page.Draw(View);
        Check(metrics["RowHeight"].Value == original, "remove restores declaration geometry");
        page.Host.Bindings.Invoke("set-tab", "Packs");
        Press(page, View, "packs-keyword");
        UiNode node = page.Host.Session.GetNodeByElementId("packs-keyword")!;
        UiWidgetContext ctx = page.Host.CreateContext(View.Width).WithNode(node);
        var editor = ctx.Controls.Editor<string>(ctx, "search-text", v => v, ParseText);
        InjectText(page, View, " LOCAL ");
        Check(source.LastSearchText == " LOCAL " && source.SearchWriteCount == 1, "actual native adapter input publishes ordinary Live once");
        Check(editor.Draft == " LOCAL ", "actual retained editor holds user draft");
        int identity = page.Host.Session.Identity;
        UsKernelSettingsHost.ApplySwatchLive(new GameSettings(), page.Host, UsTheme.ChromeTheme, "biotech");
        page.Draw(View);
        Check(page.Host.Session.Identity == identity && ReferenceEquals(editor, ctx.Controls.Editor<string>(ctx, "search-text", v => v, ParseText)), "theme switch preserves Host/editor identity");
        page.Host.SetDisplayDemand(false); source.SetSearchText("external"); page.Host.Invalidate();
        page.Host.SetDisplayDemand(true); page.Draw(View);
        Check(editor.Draft == " LOCAL " && editor.Conflict && source.SearchWriteCount == 2, "hide/show retains conflict with actual changed model, no automatic write");
        Press(page, View, "packs-keyword");
        Check(page.Host.TryHandleCancel(), "actual active editor handles Cancel"); page.Draw(View);
        Check(editor.Draft == "external" && source.SearchWriteCount == 2, "Cancel follows latest model and writes zero");
        Press(page, View, "packs-keyword"); source.SearchTransform = value => value.Trim().ToLowerInvariant();
        InjectText(page, View, " NEXT ");
        Check(source.LastSearchRequest == " NEXT " && source.LastSearchText == "next" && source.SearchWriteCount == 3, "request text remains distinct from normalized business model");
        Check(page.Host.TryHandleAccept(), "actual Live confirmation handled"); page.Draw(View);
        Check(editor.Draft == "next" && source.SearchWriteCount == 3, "confirm read-model does not publish Live twice");
    }
    private static void InjectText(Page page, UiLayoutRect view, string text)
    {
        FieldInfo seam = typeof(Widgets).GetField("TextControlOverride", BindingFlags.Public | BindingFlags.Static)!;
        Check(seam != null, "identified platform text double injection exists");
        string focused = GUI.GetNameOfFocusedControl(); bool sent = false;
        seam!.SetValue(null, new Func<Rect, string, string>((_, current) =>
        {
            if (sent || GUI.GetNameOfFocusedControl() != focused || focused.Length == 0 || (string)typeof(GUI).GetField("NextControlName")!.GetValue(null)! != focused) return current;
            sent = true; return text;
        }));
        try { page.Draw(view); Check(sent, "actual focused native text outlet received test input"); }
        finally { seam.SetValue(null, null); }
    }
    private static void HelpAndTypedChoices() => CurrentHelpTests.Run();
    private static void ProxyCapabilities() => BindingProxyTests.Run();
    private static void Confirmation()
    {
        int writes = 0;
        var stack = new WindowStack();
        UsConfirmWindow flow = UsConfirmWindow.Open("Question", "Same pending target", "Confirm", () => writes++, stack)!;
        UiWindowHost window = flow.OpenedWindow!;
        UiHost? host = null; window.HostAttached += value => host = value;
        Event.current = new Event { type = EventType.Repaint, mousePosition = new Vector2(-1000, -1000) };
        window.DoWindowContents(new Rect(0, 0, 500, 400));
        Check(host != null && window.Session == host.Session, "actual US confirmation UiPageWindow attaches real Host via its owned execution");
        Check(writes == 0, "opening/painting confirmation writes zero");
        // Drive the native window boundary, not a second manually constructed policy/table.
        window.OnCancelKeyPressed();
        Check(writes == 0, "native window Cancel writes zero");
        flow = UsConfirmWindow.Open("Question", "Same pending target", "Confirm", () => writes++, new WindowStack())!;
        window = flow.OpenedWindow!; host = null; window.HostAttached += value => host = value;
        Event.current = new Event { type = EventType.Repaint, mousePosition = new Vector2(-1000, -1000) };
        window.DoWindowContents(new Rect(0, 0, 500, 400));
        window.DoWindowContents(new Rect(0, 0, 500, 400));
        var confirmNode = host!.Session.GetNodeByElementId("confirm-yes");
        Check(confirmNode != null && confirmNode.IsArranged, "window-executed confirmation exposes completed public arrangement");
        var rect = Fl08PublicObservation.NodeWindowRect(host, confirmNode!);
        foreach (EventType phase in new[] { EventType.MouseDown, EventType.MouseUp })
        {
            Event.current = new Event { type = phase, button = 0, mousePosition = VerseUiExecution.Point(rect.Center) };
            window.DoWindowContents(new Rect(0, 0, 500, 400));
        }
        Check(writes == 1, "actual confirmation native press/release commits exactly once");
        flow.DialogBindings.Invoke("confirm-yes");
        Check(writes == 1, "answered confirmation cannot commit twice");
        var remix = new RemixConfirmationFlow(new WindowStack(), Metrics);
        Check(remix.RequestRemix(() => writes++), "actual two-step Remix request stages");
        remix.DialogBindings.Invoke("confirm-continue"); Check(writes == 1 && remix.Step == 2, "Remix step1 writes zero");
        remix.DialogBindings.Invoke("confirm-cancel"); Check(writes == 1 && remix.Step == 0, "Remix cancel writes zero");
        remix.RequestRemix(() => writes++); remix.DialogBindings.Invoke("confirm-continue"); remix.DialogBindings.Invoke("confirm-enable");
        Check(writes == 2 && remix.Step == 0, "actual Remix final confirmation writes once");
    }
    private static void OverlayAndImages()
    {
        var source = new RecordingOverlaySource();
        using var controller = new UsKernelOverlayController(source);
        Event.current = new Event { type = EventType.Repaint };
        float y = 500;
        Check(controller.TryDraw(12, 260, ref y) && y == 474 && controller.IsActive, "actual overlay captures frame and draws through owned execution");
        var execField = typeof(UsKernelOverlayController).GetField("execution", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var first = execField.GetValue(controller);
        y = 500; controller.TryDraw(12, 260, ref y);
        Check(ReferenceEquals(first, execField.GetValue(controller)), "overlay does not recreate execution each pass");
        source.Enabled = false; controller.Maintain();
        Check(!controller.IsActive && execField.GetValue(controller) == null, "overlay hides and releases execution");
        source.Enabled = true; controller.Maintain();
        Check(controller.IsActive && !ReferenceEquals(first, execField.GetValue(controller)), "overlay reopen owns a fresh execution");
        source.MapPresent = false; controller.Maintain(); Check(!controller.IsActive, "map-gone closes overlay");
        using var execution = new VerseUiExecution();
        var texture = new Texture2D(18, 24);
        UiImage image = execution.Images.Add(texture);
        var recording = new RecordingSettingsSource { RichData = true, XenotypeIcon = image };
        using var page = new Page(UsKernelSettingsHost.Create(recording, Metrics), execution);
        page.Host.Bindings.Invoke("set-tab", "Presets"); page.Draw(View);
        var textures = (IList)typeof(GUI).GetField("DrawTextureTextures")!.GetValue(null)!;
        Check(textures.Cast<object>().Any(value => ReferenceEquals(value, texture)), "actual neutral preset row paints its execution-scoped native image");
        execution.Images.Release(image);
        Check(texture.width == 18 && texture.height == 24, "image reference release does not destroy supplier texture");
    }
    internal static Dictionary<string, string> ReadKeyedTable(string language)
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(Path.Combine(Repository, "1.6", "Languages", language, "Keyed"), "*.xml"))
            foreach (var item in XDocument.Load(file).Root!.Elements()) table[item.Name.LocalName] = item.Value;
        return table;
    }
    internal static void SetTranslatorResolver(Dictionary<string, string>? table)
    {
        FieldInfo field = typeof(Translator).GetField("Resolve", BindingFlags.Public | BindingFlags.Static)!;
        field.SetValue(null, table == null ? null : new Func<string, string>(key => table.TryGetValue(key, out string? value) ? value : key));
    }
    private static string FindRepository()
    {
        string current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "About", "About.xml"))) current = Directory.GetParent(current)?.FullName ?? throw new Exception("Run from US repository");
        return current;
    }
    internal sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => Math.Max(1, (int)Math.Ceiling(MeasureWidth(text, font) / Math.Max(1, width))) * (font == UiFont.Tiny ? 18 : font == UiFont.Medium ? 30 : 21.33333f);
        public float MeasureWidth(string text, UiFont font) => (text ?? "").Sum(c => c >= 0x2e80 ? 1f : .5f) * (font == UiFont.Tiny ? 12 : font == UiFont.Medium ? 18 : 16);
    }
    internal sealed class StubTranslation : IUiTranslation { public string Translate(string key) => key.Translate(); public int TranslationRevision => 0; }
}
