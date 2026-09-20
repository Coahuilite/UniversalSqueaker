using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// The S3 frame guard: the page frame is a SHAPE, and a shape can be asserted without a screenshot.
///
/// <para>
/// What this lane is for. S3 replaces the page skeleton (fixed header band, three columns, footer band),
/// and the acceptance for that slice is "five viewports of geometry: no overlap, no negative width, inside
/// the bounds". This is that acceptance, written BEFORE the skeleton moves, so it is green against the
/// CURRENT frame and every later step has to keep it green - a guard each step is re-derived against
/// instead of a milestone nobody re-runs.
/// </para>
/// <para>
/// The five viewports are not arbitrary round numbers. 736 is the innermost page width of the SMALLEST
/// window the shipped size policy can produce (<c>WindowChromeLayout.SettingsWidthFloor</c> 800 - 2 x 20
/// chrome - 2 x 12 page padding), i.e. the tightest case the wide three-column regime has to survive on a
/// real screen. 1280 is the widest the policy reaches (a 2560x1440 screen). 1024/480/320 are the probes
/// the rest of this suite already uses. Everything is asserted in BOTH shipped languages, with the drawer
/// in BOTH states, on every one of the five workspaces, because the frame is the one thing all of them
/// share.
/// </para>
/// <para>
/// What it deliberately does not do: it does not restate the engine's own breakpoint arithmetic as an
/// expectation - that would be a tautology. It asserts the structural facts that arithmetic has to
/// produce (the columns are either all side by side or all stacked, a stacked column shares the full inner
/// width, and a declared width is the arranged width in the wide regime). The two ENDPOINTS are product
/// invariants and are asserted outright: 1280 is a three-column frame, 320 is a stacked one.
/// </para>
/// <para>
/// <b>What the first run of this lane found, and what is mutation-proven.</b> Against the frame as it
/// stood, it reddened on a real defect: in the stacked (narrow) regime the fixed-width nav column
/// declared <c>Fill="true"</c>, so it took an equal height share (212px) of the stacked row while
/// <c>us/nav</c> measures 271px of natural content - the nav painted 60px into the column below it. The
/// fix removes the redundant <c>Fill</c> from a column that is width-fixed anyway, and the defect is NOT
/// player-reachable before it: the real window floor is 800 wide (inner 736), which keeps the row in its
/// three-column regime. It becomes reachable the moment a slice stacks the frame at a real width, which is
/// why the guard exists.
/// </para>
/// <para>
/// Evidence, per clause. <b>Mutation-proven:</b> the containment clause and the cross-parent overlap clause
/// (restoring <c>Fill="true"</c> on nav-column reddens both, naming the element and both rects), and the
/// containment clause alone (parking <c>banner</c> outside its parent's rect with
/// <c>AlignX="Left" OffsetX="-60"</c> reddens it with every other step still green). <b>Guarded, not
/// mutation-proven:</b> the finite/non-negative clause and the "every scroll box is a real box" clause -
/// the engine clamps both, so no manifest edit reaches them; they are future-regression guards and are
/// labelled as such rather than counted as evidence. The wide-regime <c>Width</c> clause is redundant on
/// purpose: raising the breakpoint so 1280 stacks reddens the suite, but on the older
/// <c>ThreeViewportMeasureAndDraw</c> version of the same invariant before this lane is reached.
/// </para>
/// </summary>
internal static class FrameGeometryLaneTests
{
    private static readonly float[] Widths = { 1280f, 1024f, 736f, 480f, 320f };
    private static readonly string[] Languages = { "English", "ChineseSimplified" };
    private static readonly string[] Tabs = { "Overview", "Distance", "Packs", "Tuning", "Presets" };

    private const float Height = 720f;
    private const float Tol = 0.51f;

    public static int RunAll()
    {
        Step("no degenerate rect, no sibling overlap, every element inside its parent", TheFrameHoldsEverywhere);
        Step("the three-column frame keeps its declared shape across the five viewports", TheFrameKeepsItsShape);
        Console.WriteLine("FrameGeometryLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Structural invariants over the whole matrix.
    // ---------------------------------------------------------------------------------------------

    private static void TheFrameHoldsEverywhere()
    {
        UiLayoutManifest manifest = LoadManifest();
        var problems = new List<string>();

        foreach (string language in Languages)
        {
            Program.SetTranslatorResolver(Program.ReadKeyedTable(language));
            try
            {
                foreach (float width in Widths)
                {
                    foreach (bool open in new[] { false, true })
                    {
                        foreach (string tab in Tabs)
                        {
                            var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
                            using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
                            host.Bindings.Invoke("set-tab", tab);
                            host.Bindings.Set("help-open", open);

                            // Arrange once to create the nodes the scroll write needs, then arrange the
                            // frame this lane records, then draw it: the measured and the drawn page are
                            // one geometry, which is the invariant the whole library exists to keep.
                            host.MeasureAndArrange(new Vector2(width, Height));
                            Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
                            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, Height));
                            host.DrawChecked(new Rect(0f, 0f, width, Height));

                            string context = width.ToString("0", CultureInfo.InvariantCulture) + "px/" + language
                                + "/" + tab + "/" + (open ? "open" : "closed");
                            CheckRects(snapshot, context, problems);
                            var window = new Rect(0f, 0f, width, Height);
                            var placed = new List<Placed>();
                            foreach (UiElementSpec root in manifest.Roots)
                            {
                                Collect(root, window, "<page>", new List<string>(), false, snapshot, context,
                                    placed, problems);
                            }

                            CheckOverlaps(placed, problems, context);
                        }
                    }
                }
            }
            finally
            {
                Program.SetTranslatorResolver(null);
            }
        }

        Assert(problems.Count == 0, Report(problems));
    }

    /// <summary>Every rect the frame published is finite and non-negative; every scroll box is real.</summary>
    private static void CheckRects(UiLayoutSnapshot snapshot, string context, List<string> problems)
    {
        foreach (KeyValuePair<string, Rect> pair in snapshot.RectById)
        {
            Rect rect = pair.Value;
            if (!Finite(rect) || rect.width < 0f || rect.height < 0f)
            {
                problems.Add(context + ": element '" + pair.Key + "' has a degenerate rect " + Fmt(rect));
            }
        }

        foreach (KeyValuePair<string, Rect> pair in snapshot.Viewports)
        {
            Rect rect = pair.Value;
            if (!Finite(rect) || rect.width <= 0f || rect.height <= 0f)
            {
                problems.Add(context + ": scroll viewport '" + pair.Key + "' must be a real box, got " + Fmt(rect));
            }
        }

        foreach (KeyValuePair<string, Rect> pair in snapshot.ScrollContents)
        {
            Rect rect = pair.Value;
            if (!Finite(rect) || rect.width <= 0f || rect.height < 0f)
            {
                problems.Add(context + ": scroll content '" + pair.Key + "' must be a real box, got " + Fmt(rect));
            }
        }
    }

    /// <summary>
    /// Walks the DECLARED tree (the manifest) and pairs each element with the rect the arrange published
    /// for it. An element with no published rect is not arranged, which is the visibility answer itself, so
    /// its whole subtree is skipped - that is how a Tab-gated or VisibleKey-gated section stays out of the
    /// assertions without this lane restating any gate.
    /// <para>
    /// The walk collects as it checks, because the overlap rule that matters is NOT "siblings must not
    /// overlap": the engine distributes a flow container's slots so that siblings cannot overlap by
    /// construction, which would make a sibling-only lane vacuous. What can and does go wrong is a child
    /// OVERFLOWING its own slot and landing on a cousin - measured here as the pre-S3 defect where the nav
    /// composite (271px of natural content) was handed an equal height share (212px) in the stacked frame
    /// and painted into the column below it. So the collected rects are compared pairwise across the whole
    /// coordinate space, excluding ancestors (a parent contains its children by definition).
    /// </para>
    /// </summary>
    private static void Collect(
        UiElementSpec spec,
        Rect reference,
        string space,
        List<string> ancestors,
        bool underOverlay,
        UiLayoutSnapshot snapshot,
        string context,
        List<Placed> placed,
        List<string> problems)
    {
        if (spec.Id.Length == 0) return;
        if (!snapshot.RectById.TryGetValue(spec.Id, out Rect rect)) return;

        string where = context + " '" + spec.Id + "'(" + spec.Kind + ")";

        if (!Finite(rect) || rect.width < 0f || rect.height < 0f)
        {
            problems.Add(where + ": degenerate rect " + Fmt(rect));
            return;
        }

        if (rect.x < reference.x - Tol || rect.y < reference.y - Tol
            || rect.xMax > reference.xMax + Tol || rect.yMax > reference.yMax + Tol)
        {
            problems.Add(where + ": leaves its parent's rect; element=" + Fmt(rect) + " parent=" + Fmt(reference));
        }

        bool overlayHere = underOverlay
            || string.Equals(spec.Kind, "Overlay", StringComparison.OrdinalIgnoreCase);
        placed.Add(new Placed
        {
            Id = spec.Id,
            Kind = spec.Kind,
            Rect = rect,
            Space = space,
            Ancestors = new List<string>(ancestors),
            UnderOverlay = overlayHere,
        });

        // A Scroll's children are arranged from the scroll's CONTENT origin, and the snapshot reports
        // that content box at (0,0) on purpose (it is the box handed to BeginScrollView). The children's
        // own rects are in the containing space, so the reference has to be the content box MOVED onto the
        // viewport's origin - comparing against the raw (0,0,..) box is the classic false red here.
        Rect childSpace = rect;
        string childSpaceKey = space;
        if (snapshot.ScrollContents.TryGetValue(spec.Id, out Rect content))
        {
            childSpace = new Rect(rect.x, rect.y, content.width, content.height);
            childSpaceKey = spec.Id;
        }

        var nextAncestors = new List<string>(ancestors) { spec.Id };
        foreach (UiElementSpec child in spec.Children)
        {
            Collect(child, childSpace, childSpaceKey, nextAncestors, overlayHere, snapshot, where, placed, problems);
        }
    }

    /// <summary>
    /// Pairwise overlap across one coordinate space. An <c>Overlay</c> subtree is exempt: placing children
    /// by reference point is exactly what an Overlay is for, so overlap there is the declaration. Ancestors
    /// are exempt for the same reason they are not siblings.
    /// </summary>
    private static void CheckOverlaps(List<Placed> placed, List<string> problems, string context)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            for (int j = i + 1; j < placed.Count; j++)
            {
                Placed a = placed[i];
                Placed b = placed[j];
                if (a.UnderOverlay || b.UnderOverlay) continue;
                if (!string.Equals(a.Space, b.Space, StringComparison.Ordinal)) continue;
                if (a.Ancestors.Contains(b.Id) || b.Ancestors.Contains(a.Id)) continue;
                if (!Overlaps(a.Rect, b.Rect)) continue;
                problems.Add(context + ": '" + a.Id + "'(" + a.Kind + ") " + Fmt(a.Rect)
                    + " overlaps '" + b.Id + "'(" + b.Kind + ") " + Fmt(b.Rect));
            }
        }
    }

    private sealed class Placed
    {
        public string Id = "";
        public string Kind = "";
        public Rect Rect;
        public string Space = "";
        public List<string> Ancestors = new List<string>();
        public bool UnderOverlay;
    }

    // ---------------------------------------------------------------------------------------------
    // 2. The frame's shape: three columns side by side, or a full-width stack - never a mixture.
    // ---------------------------------------------------------------------------------------------

    private static void TheFrameKeepsItsShape()
    {
        UiLayoutManifest manifest = LoadManifest();
        Dictionary<string, UiElementSpec> byId = Index(manifest);
        var problems = new List<string>();

        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            foreach (float width in Widths)
            {
                foreach (bool open in new[] { false, true })
                {
                    var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
                    using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
                    host.Bindings.Set("help-open", open);
                    UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, Height));

                    string context = "width=" + width.ToString("0", CultureInfo.InvariantCulture)
                        + " drawer=" + (open ? "open" : "closed");
                    Assert(snapshot.RectById.ContainsKey("nav-column"), context + ": the nav column must be arranged");
                    Assert(snapshot.Viewports.ContainsKey("content-scroll"), context + ": the centre column must be arranged");

                    Rect nav = snapshot.RectById["nav-column"];
                    Rect centre = snapshot.Viewports["content-scroll"];
                    bool hasHelp = snapshot.Viewports.TryGetValue("help-scroll", out Rect help);

                    Assert(hasHelp == open,
                        context + ": the drawer's column must exist exactly while the drawer is open");

                    if (Beside(nav, centre))
                    {
                        float declaredNav = DeclaredWidth(byId, "nav-column");
                        if (Math.Abs(nav.width - declaredNav) > Tol)
                        {
                            problems.Add(context + ": in the wide regime the nav column must keep its declared"
                                + " width " + declaredNav + ", got " + nav.width.ToString("0.##", CultureInfo.InvariantCulture));
                        }

                        if (hasHelp && !Beside(centre, help))
                        {
                            problems.Add(context + ": the wide regime needs the drawer BESIDE the centre column;"
                                + " centre=" + Fmt(centre) + " help=" + Fmt(help));
                        }
                    }
                    else if (Above(nav, centre))
                    {
                        // Narrow="Column": the three columns stack, so all three share x and width.
                        if (hasHelp && !Above(centre, help))
                        {
                            problems.Add(context + ": the stacked drawer must follow the centre column;"
                                + " centre=" + Fmt(centre) + " help=" + Fmt(help));
                        }

                        CheckStacked("nav-column", nav, centre, context, problems);
                        if (hasHelp) CheckStacked("help-scroll", help, centre, context, problems);
                    }
                    else
                    {
                        problems.Add(context + ": the nav column is neither beside nor above the centre column;"
                            + " nav=" + Fmt(nav) + " centre=" + Fmt(centre));
                    }
                }
            }
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }

        Assert(problems.Count == 0, Report(problems));

        // The two endpoints are product invariants, not arithmetic: the widest viewport is the shipped
        // three-column frame and the narrowest stacks. If a later slice moves the breakpoint past one of
        // these, this is the assertion that says so out loud.
        AssertShapeAt(1280f, expectWide: true);
        AssertShapeAt(320f, expectWide: false);
    }

    private static void AssertShapeAt(float width, bool expectWide)
    {
        var fake = new RecordingSettingsSource { RichData = true, EatPrecisionEnabled = true };
        using UiHost host = UsKernelSettingsHost.Create(fake, new Program.StubMetrics());
        host.Bindings.Set("help-open", true);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, Height));

        Assert(snapshot.RectById.ContainsKey("nav-column") && snapshot.Viewports.ContainsKey("content-scroll"),
            width + ": the shape assertion needs the nav and centre columns arranged");
        Assert(snapshot.Viewports.ContainsKey("help-scroll"), width + ": the shape assertion needs the drawer open");

        Rect nav = snapshot.RectById["nav-column"];
        Rect centre = snapshot.Viewports["content-scroll"];
        Rect help = snapshot.Viewports["help-scroll"];

        bool wide = Beside(nav, centre) && Beside(centre, help);
        bool stacked = Above(nav, centre) && Above(centre, help);
        Assert(expectWide ? wide : stacked,
            width + " must be a " + (expectWide ? "three-column" : "stacked") + " frame: nav=" + Fmt(nav)
            + " centre=" + Fmt(centre) + " help=" + Fmt(help));
    }

    private static void CheckStacked(string id, Rect rect, Rect centre, string context, List<string> problems)
    {
        if (Math.Abs(rect.x - centre.x) > Tol || Math.Abs(rect.width - centre.width) > Tol)
        {
            problems.Add(context + ": stacked column '" + id + "' must share the centre's x and width; got "
                + Fmt(rect) + " centre=" + Fmt(centre));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private static UiLayoutManifest LoadManifest()
    {
        using Stream? stream = typeof(UsKernelSettingsHost).Assembly
            .GetManifestResourceStream("UniversalSqueaker.UI.Layout.Schema2.xml");
        Assert(stream != null, "the embedded Schema=2 manifest must be readable");
        using var reader = new StreamReader(stream!);
        return UiLayoutManifest.Parse(reader.ReadToEnd());
    }

    private static Dictionary<string, UiElementSpec> Index(UiLayoutManifest manifest)
    {
        var byId = new Dictionary<string, UiElementSpec>(StringComparer.Ordinal);
        foreach (UiElementSpec root in manifest.Roots) Add(root, byId);
        return byId;
    }

    private static void Add(UiElementSpec spec, Dictionary<string, UiElementSpec> byId)
    {
        if (spec.Id.Length > 0) byId[spec.Id] = spec;
        foreach (UiElementSpec child in spec.Children) Add(child, byId);
    }

    private static float DeclaredWidth(Dictionary<string, UiElementSpec> byId, string id)
    {
        Assert(byId.TryGetValue(id, out UiElementSpec? spec), "the manifest must declare '" + id + "'");
        Assert(spec!.TryGetAttribute("Width", out string raw), "'" + id + "' must declare a Width");
        Assert(float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            "'" + id + "' must declare a NUMERIC Width for the wide-regime assertion, got '" + raw + "'");
        return float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static bool Beside(Rect left, Rect right)
    {
        return left.xMax <= right.x + Tol
            && left.y < right.yMax - Tol
            && right.y < left.yMax - Tol;
    }

    private static bool Above(Rect upper, Rect lower)
    {
        return upper.yMax <= lower.y + Tol
            && upper.x < lower.xMax - Tol
            && lower.x < upper.xMax - Tol;
    }

    private static bool Overlaps(Rect a, Rect b)
    {
        return a.x < b.xMax - Tol && b.x < a.xMax - Tol
            && a.y < b.yMax - Tol && b.y < a.yMax - Tol;
    }

    private static bool Finite(Rect rect)
    {
        return !float.IsNaN(rect.x) && !float.IsNaN(rect.y) && !float.IsNaN(rect.width) && !float.IsNaN(rect.height)
            && !float.IsInfinity(rect.x) && !float.IsInfinity(rect.y)
            && !float.IsInfinity(rect.width) && !float.IsInfinity(rect.height);
    }

    /// <summary>At most 25 violations are printed, so a systematic failure stays readable.</summary>
    private static string Report(List<string> problems, int max = 25)
    {
        var head = new List<string>();
        for (int i = 0; i < problems.Count && i < max; i++) head.Add("  " + problems[i]);
        if (problems.Count > max) head.Add("  ... and " + (problems.Count - max) + " more");
        return problems.Count + " frame violation(s):" + Environment.NewLine + string.Join(Environment.NewLine, head);
    }

    /// <summary>
    /// An explicit rect formatter: the stub's <c>Rect.ToString()</c> prints the type name, and a geometry
    /// lane whose failures cannot be read is a lane nobody can act on.
    /// </summary>
    private static string Fmt(Rect rect)
    {
        return "(" + rect.x.ToString("0.##", CultureInfo.InvariantCulture)
            + ", " + rect.y.ToString("0.##", CultureInfo.InvariantCulture)
            + ", " + rect.width.ToString("0.##", CultureInfo.InvariantCulture)
            + ", " + rect.height.ToString("0.##", CultureInfo.InvariantCulture) + ")";
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("FrameGeometryLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
