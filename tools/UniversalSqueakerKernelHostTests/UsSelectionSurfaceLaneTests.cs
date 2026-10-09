using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// T21: the SELECTED domain row must be visible at a glance - a fill and a 3px left rail - and the
/// surface that paints it must not take the row's hit band with it.
///
/// <para>
/// WHY THIS LANE EXISTS (the defect it answers): the maintainer pressed a domain row, the press ARRIVED
/// and was consumed (10 hits, event-after=Used, a 484x70 band), and the window still looked unchanged -
/// so the feature read as broken. Selection only ever changed the title INK (SelectedKey), while the fill
/// and the rail were recorded debt. Discoverability is the product property here, not polish: a state the
/// player cannot see is a state the player does not have.
/// </para>
///
/// <para>
/// WHAT IT MEASURES: the stub's solid recorder (DrawBoxSolidRects / DrawBoxSolidColors, reached by
/// reflection - the harness compiles against the game reference assembly, so the fields exist only at
/// runtime) plus the recorded button rects. Both are the rects the widgets HANDED to the draw outlet, so
/// they live in the same space and are compared directly; only the content viewport origin is read from
/// the snapshot, to tell the row column from the navigation column.
/// </para>
///
/// <para>
/// MUTATION LEDGER (each red observed in the T21 batch):
/// <list type="bullet">
/// <item><b>TheSelectedRowPaintsAFillAndARail</b> - MUTATION-PROVEN: making the kind return before it
/// paints (or dropping the manifest element) leaves no accent solid at the band, and the failure names
/// every solid that WAS painted in that band.</item>
/// <item><b>UnselectedRowsPaintNeither</b> - MUTATION-PROVEN: dropping the selected check in the kind
/// paints a rail on EVERY row, and this is the assertion that catches it (the positive half cannot: it
/// only asks for one).</item>
/// <item><b>PackCardTemplateDeclaresTheSurface</b> - a surface dropped from the card template, or a
/// second one added, reddens by id.</item>
/// </list>
/// </para>
///
/// <para>
/// NOT CLAIMED: the look. No stub renders a pixel a player can see; whether a #3A311F fill plus a 3px
/// gold rail reads as "this row is current" next to the navigation rail needs a real screen, and that
/// confirmation belongs to the maintainer (T21 acceptance).
/// </para>
/// </summary>
internal static class UsSelectionSurfaceLaneTests
{
    private const string SurfaceKind = "us/selection-surface";

    /// <summary>The page size the row geometry was measured at through the T21/T22 rounds (800x720), on
    /// purpose: this lane's numbers must stay comparable with the sibling row measurements, and disagree
    /// rather than agree by construction. (The 960x530 real-window size is where T22's two-column question is
    /// argued; a first cut of this lane used it and found the Packs layer rows were not materialized in that
    /// snapshot at all, which is a separate fact about that size and not something to fold in here.)</summary>
    private const float PageWidth = 800f;
    private const float PageHeight = 720f;

    /// <summary>The row template, by id: since US-PACK1 ONE template draws both kinds - card header and
    /// domain row - and a surface dropped from it is named rather than merely uncounted.</summary>
    private static readonly string[] RowTemplates = { "pack-card-row" };

    public static int RunAll()
    {
        Step("the pack-card template declares the row-state surface", PackCardTemplateDeclaresTheSurface);
        Step("the selected row paints a fill and a 3px rail", TheSelectedRowPaintsAFillAndARail);
        Step("unselected rows paint neither", UnselectedRowsPaintNeither);
        Console.WriteLine("UsSelectionSurfaceLaneTests ALL PASS");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1: the declaration, read off the live manifest
    // ---------------------------------------------------------------------------------------------

    private static void PackCardTemplateDeclaresTheSurface()
    {
        using UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true });

        foreach (string template in RowTemplates)
        {
            Assert(host.Manifest.Templates.ContainsKey(template),
                "the manifest must declare the row template '" + template + "'");
            UiElementSpec root = host.Manifest.Templates[template];

            List<UiElementSpec> surfaces = Descendants(root).Where(e => e.Kind == SurfaceKind).ToList();
            Assert(surfaces.Count == 1,
                "the '" + template + "' template must declare exactly ONE " + SurfaceKind
                + " (T21: the selected row's fill and 3px rail), got " + surfaces.Count);
            UiElementSpec surface = surfaces[0];
            Assert(surface.TryGetAttribute("Bind", out string bind) && bind == "selected",
                "'" + template + "' surface must read the row's own 'selected' bool, got '" + bind + "'");
            Assert(!surface.TryGetAttribute("ActionBind", out _)
                && !surface.TryGetAttribute("CommandBind", out _),
                "'" + template + "' surface must NOT take input: it is a sibling of the hit band, and a"
                + " surface that also hit would give the row two competing hit areas");
            Assert(!surface.TryGetAttribute("Chrome", out _),
                "'" + template + "' surface must not declare Chrome: it paints its own fill and rail, and a"
                + " chrome would add the engine's surface on top of them");

            // NON-INTERACTIVE, asserted rather than assumed: this element claims no hover and therefore must
            // not appear in the hover-claim catalog. HelpKey is what puts an element into that catalog, so a
            // HelpKey here would silently EXPAND VerifyHoverClaimsMatchCatalogItems with a surface that has no
            // interaction to explain.
            Assert(!surface.TryGetAttribute("HelpKey", out string help),
                "'" + template + "' surface must NOT declare HelpKey: it takes no input, and a HelpKey would"
                + " add a non-interactive surface to the hover-claim catalog (got '" + help + "')");
            Assert(surface.TryGetAttribute("Height", out string surfaceHeight) && surfaceHeight == "MatchContent",
                "'" + template + "' surface must take the row's measured content height"
                + " (Height=\"MatchContent\"), got '" + surfaceHeight + "': a height of its own would either"
                + " paint a band that is not the row or pull the row's height away from the text column's");

            // CONTROL, re-censused for US-PACK1: the template now carries TWO buttons - the domain row's
            // hit band and the header's expand command. The HIT BAND is identified by the appearance it
            // must have: Chrome=none. An expand command that dropped its chrome would paint a second
            // competing band and reddens here by count, naming the buttons it found.
            List<UiElementSpec> allButtons = Descendants(root).Where(e => e.Kind == "input/button").ToList();
            List<UiElementSpec> hits = allButtons
                .Where(e => e.TryGetAttribute("Chrome", out string c) && c == "none")
                .ToList();
            Assert(hits.Count == 1 && allButtons.Count == 2,
                "G3 still holds: '" + template + "' must keep exactly ONE Chrome=none hit band - the"
                + " visible state belongs to the surface, not to the band that must not move; buttons: "
                + string.Join(",", allButtons.Select(b => b.Id)) + " (appearance-less: " + hits.Count + ")");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: the painted pixel - the half a player sees
    // ---------------------------------------------------------------------------------------------

    private static void TheSelectedRowPaintsAFillAndARail()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            using UiHost host = UsKernelSettingsHost.Create(
                new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
            // The PACKS tab, explicitly: the host opens on Overview, and the first cut of this lane measured
            // the Overview preset list's expanded row (which also paints RowRail.Selected) instead of a
            // domain row. A lane that grades the wrong widget is not evidence about this one.
            host.Bindings.Invoke("set-tab", "Packs");
            // US-PACK1: the card keys register during the FIRST projection pass and the domain rows
            // materialize only once a card is open - so arrange once, open the us.sang card through the
            // very funnel write the header's expand button invokes, and let the measured arranges below
            // see the expanded page. A snapshot taken before the open carries headers only.
            host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
            host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
            UiTheme theme = UsTheme.Surface();
            // The two tokens the SHIPPED selected treatment uses (UsKernelDraw.RowSurface): the selected
            // plane is theme.Selected and RowRail.Selected's rail ink is theme.TextSecondary. Named through
            // the theme rather than written down, so the lane and the widget cannot drift apart.
            // TASK-32 RE-CUT (product and lane in the same batch; no criterion changed). TWO CHANNELS, stated
            // apart: the RECORDER stores what the widget WROTE (an RGBA parameter, alpha included), while the
            // player sees what IMGUI blends. This lane asserts the WRITTEN channel against the widget's own
            // tokens and checks the alpha explicitly, because alpha IS the evidence here - comparing RGB alone
            // is what made the composited reading look like a raw-token reading last round. The perceived
            // channel (PaintedFill) is asserted in PaletteLaneTests, which owns the contrast criteria.
            Color accent = UsSelectionSurfaceWidget.PaintedRail(theme);
            Color activeFill = UsSelectionSurfaceWidget.SelectedFill(theme);
            Assert(Math.Abs(activeFill.a - 0.57f) <= 0.002f,
                "the written fill parameter must carry alpha 0.57 (the tinted accent), got a=" + activeFill.a);
            Assert(Math.Abs(accent.a - 1f) <= 0.002f,
                "the written rail parameter must be OPAQUE, got a=" + accent.a);

            // ARRANGE FIRST, then draw. Measured the hard way: a snapshot taken AFTER a DrawChecked carries
            // only the container rects (14 of them - banner/body-row/content-scroll/footer/...), no widget ids
            // at all, so every row lookup missed and the lane measured nothing while looking like it ran.
            // DeclarativePacksLaneTests reads its snapshot before drawing for the same reason.
            UiLayoutSnapshot snapshot = Arrange(host);
            Rect content = Viewport(snapshot);

            ClearSolids();
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
            IList rects = Recorded("DrawBoxSolidRects");
            IList colors = Recorded("DrawBoxSolidColors");
            Assert(rects.Count == colors.Count,
                "the stub's two solid recorders must stay in step: " + rects.Count + " rects vs "
                + colors.Count + " colours");

            var solids = new List<(Rect Rect, Color Colour)>();
            for (int i = 0; i < rects.Count; i++) solids.Add(((Rect)rects[i]!, (Color)colors[i]!));
            List<Rect> bands = ButtonsDrawn(host);

            // The rail candidates: the SURFACE's own rail colour (TextPrimary per SelectedRail's measured
            // ruling - a gold rail on the gold fill would be invisible), 3px wide, and - the discriminator
            // that keeps the navigation card and the section headers out - sitting at the left edge of a
            // recorded HIT BAND whose height equals the rail's. The section header's gold rail is now out
            // by COLOUR as well as height: the census asks for the surface's rail token, not the accent.
            var rowRails = new List<(Rect Rect, Rect Band)>();
            foreach ((Rect rect, Color colour) in solids)
            {
                if (!SameColor(colour, accent)) continue;
                if (Math.Abs(rect.width - UsSelectionSurfaceWidget.RailWidth) > 0.5f) continue;

                // SPACE DISCIPLINE, measured the hard way: the recorded solids are CONTENT-LOCAL while the
                // arranged viewport rect is PAGE space, so the first cut's "x >= viewport.x" filter compared
                // two different origins and silently dropped every solid left of the page-space viewport -
                // including the very rail it was looking for (local x=12 against a page x of 236). The only
                // space test that is safe here is "inside the content scroll at all", i.e. local x >= 0; the
                // navigation column lives at a NEGATIVE local x and is excluded by that.
                if (rect.x < 0f) continue;
                Rect band = bands.FirstOrDefault(b => Math.Abs(b.x - rect.x) <= 0.5f
                    && b.y <= rect.y + 0.5f && b.yMax >= rect.yMax - 0.5f
                    // US-PACK1 discriminator: every row control records at its own group origin, so
                    // x/y containment alone also matches the taller nav band (200x281) - the rail is as
                    // tall as ITS row, so only a band of the rail's own height can be its band.
                    && Math.Abs(b.height - rect.height) <= 0.5f);
                if (band.width <= 0f) continue;
                rowRails.Add((rect, band));
            }

            Assert(rowRails.Count == 1,
                "exactly ONE domain row may paint the accent rail (the selected one); painted row rails: "
                + rowRails.Count + " - solids inside the content scroll: "
                + Describe(solids.Where(s => s.Rect.x >= 0f)));
            Rect rail = rowRails[0].Rect;

            // US-PACK1: the ROW BAND is the surface's own painted fill rect. The hit band is NARROWER
            // than the row (the domain row's hit stops where the switch band begins - 462 vs 524 measured),
            // so "the fill spans the whole row" is checked between the FILL and the DECLARED row, never
            // against the hit. The rail's job in the census is identification (which 3px solid belongs to
            // a row button at all); the height equality with the fill is then the design claim: one band.
            var fills = solids.Where(s => SameColor(s.Colour, activeFill)
                    && Math.Abs(s.Rect.height - rail.height) <= 0.5f
                    && s.Rect.width > rail.width * 10f)
                .ToList();
            Assert(fills.Count == 1,
                "exactly ONE row-wide Active fill may sit at the rail's height (" + Hex(activeFill)
                + "); fills: " + Describe(solids.Where(s => SameColor(s.Colour, activeFill))));
            Rect row = fills[0].Rect;

            Assert(Math.Abs(rail.y - row.y) <= 0.5f && Math.Abs(rail.x - row.x) <= 0.5f,
                "the rail must start at the row band's top-left corner, got "
                + Describe(rail) + " vs row " + Describe(row));

            // IDENTITY LINK, in the ONE dimension the two spaces agree on. MEASURED: the recorded rects are
            // in the recording space (a row's children are recorded at the ROW's own origin) while RectById
            // is page space minus the viewport origin. Only size can be compared, not the origin. So: the
            // painted band must be ROW-SIZED against a declared domain row.
            List<Rect> declaredRows = DeclaredRows(snapshot);
            Assert(declaredRows.Any(r => Math.Abs(r.width - row.width) <= 0.5f
                    && Math.Abs(r.height - row.height) <= 0.5f),
                "the band that painted the fill and rail must be ROW-SIZED like a declared domain row;"
                + " declared rows: " + string.Join(" ", declaredRows.Select(Describe))
                + " vs band " + Describe(row));

            // A3: the surface's Measure returns 0, and MatchContent is what gives it the row's height. That
            // was documentation until here; now it is asserted, on the elements the manifest declares.
            // M7: this assertion has NO own red record - any manifest change that alters the surface's height
            // also trips step 1's Height="MatchContent" check, which runs first. Recorded as a GUARD.
            List<Rect> surfaceRects = snapshot.RectById.Keys
                .Where(key => key.StartsWith("pack-card-row-surface#", StringComparison.Ordinal))
                .Select(key => Local(snapshot, key))
                .ToList();
            Assert(surfaceRects.Count >= 1,
                "the arranged snapshot must carry the row-state surface elements, got " + surfaceRects.Count);
            Assert(surfaceRects.Any(r => r.height > 1f && Math.Abs(r.height - row.height) <= 0.5f),
                "the surface must be ARRANGED WITH THE ROW'S HEIGHT (MatchContent), not with its own Measure"
                + " of 0: surfaces=" + string.Join(" ", surfaceRects.Select(Describe))
                + " vs row " + Describe(row));

            Console.WriteLine("[t21-row] row=" + Describe(row) + " rail=" + Describe(rail)
                + " accent=" + Hex(accent) + " fill=" + Hex(activeFill) + " solidsOnPage=" + solids.Count);
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3: the negative half - the surface is state-driven, not unconditional
    // ---------------------------------------------------------------------------------------------

    private static void UnselectedRowsPaintNeither()
    {
        Program.SetTranslatorResolver(Program.ReadKeyedTable("English"));
        try
        {
            using UiHost host = UsKernelSettingsHost.Create(
                new RecordingSettingsSource { RichData = true }, new Program.StubMetrics());
            host.Bindings.Invoke("set-tab", "Packs");
            // US-PACK1 setup, same as step 2: one pass registers the header keys, the funnel write opens
            // the card, and the snapshot below is taken with the domain rows materialized.
            host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
            host.Bindings.Invoke(UsWriteBindings.ItemKey("pack-card-keys", "us.sang", "toggle-pack-card"), "");
            UiTheme theme = UsTheme.Surface();
            // TASK-32 RE-CUT (product and lane in the same batch; no criterion changed). TWO CHANNELS, stated
            // apart: the RECORDER stores what the widget WROTE (an RGBA parameter, alpha included), while the
            // player sees what IMGUI blends. This lane asserts the WRITTEN channel against the widget's own
            // tokens and checks the alpha explicitly, because alpha IS the evidence here - comparing RGB alone
            // is what made the composited reading look like a raw-token reading last round. The perceived
            // channel (PaintedFill) is asserted in PaletteLaneTests, which owns the contrast criteria.
            Color accent = UsSelectionSurfaceWidget.PaintedRail(theme);
            Color activeFill = UsSelectionSurfaceWidget.SelectedFill(theme);
            Assert(Math.Abs(activeFill.a - 0.57f) <= 0.002f,
                "the written fill parameter must carry alpha 0.57 (the tinted accent), got a=" + activeFill.a);
            Assert(Math.Abs(accent.a - 1f) <= 0.002f,
                "the written rail parameter must be OPAQUE, got a=" + accent.a);

            UiLayoutSnapshot snapshot = Arrange(host);

            ClearSolids();
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
            IList rects = Recorded("DrawBoxSolidRects");
            IList colors = Recorded("DrawBoxSolidColors");
            var solids = new List<(Rect Rect, Color Colour)>();
            for (int i = 0; i < rects.Count; i++) solids.Add(((Rect)rects[i]!, (Color)colors[i]!));
            List<Rect> bands = ButtonsDrawn(host);

            // The subjects are the domain rows, identified by SIZE in the recorded space and cross-checked
            // against the engine's own row ids in page space. Two earlier cuts failed here and both are worth
            // keeping: (1) selecting bands by "inside the content column and taller than 20px" graded a band
            // that belongs to another widget which legitimately paints theme.Selected; (2) since US-PACK1
            // the ONE template draws both row kinds, and a card header is a taller row than a domain row -
            // so the census keys on the domain item-key shape (the '|' scope separator), see DeclaredRows.
            List<Rect> declaredRows = DeclaredRows(snapshot);
            Assert(declaredRows.Count >= 2,
                "the opened card must draw at least its race row and its xenotype row for this negative half"
                + " to have subjects, got " + declaredRows.Count);
            float rowWidth = declaredRows[0].width;
            float rowHeight = declaredRows[0].height;
            Assert(declaredRows.All(r => Math.Abs(r.width - rowWidth) <= 0.5f
                    && Math.Abs(r.height - rowHeight) <= 0.5f),
                "every declared domain row must share one size for the recorded-space filter below to identify"
                + " them: " + string.Join(" ", declaredRows.Select(Describe)));

            // THE STATE HALF: exactly one declared row answers selected. Read through the fail-soft bool
            // query the carrier's own SelectedKey resolution uses. The walk covers EVERY card-row instance -
            // headers included, since one template draws both kinds - and a header's "selected" resolves
            // false (the production binder parses the key and a separator-less key has no owning domain),
            // so the claim stays exactly "the operating domain's row, and nothing else, is selected".
            var selectedKeys = new List<string>();
            foreach (string key in snapshot.RectById.Keys.Where(k =>
                k.StartsWith("pack-card-row#", StringComparison.Ordinal)))
            {
                string bindKey = UsWriteBindings.ItemKey(
                    "pack-card-keys", key.Substring(key.IndexOf('#') + 1), "selected");
                if (host.Bindings.TryGetBool(bindKey, out bool isSelected) && isSelected) selectedKeys.Add(bindKey);
            }

            Assert(selectedKeys.Count == 1,
                "exactly ONE declared domain row may answer selected while the fixture holds one selected"
                + " domain, got " + selectedKeys.Count + " [" + string.Join(",", selectedKeys) + "]");

            // THE PAINT HALF, and it is a COUNT rather than a per-row position ON PURPOSE. Measured: the
            // recorded rects live in the recording space, where a row's children are recorded at the ROW's
            // own origin - every same-sized domain row comes back as the same rect - so position cannot tell
            // two rows apart there. What the mutation changes is how MANY row-sized surfaces exist: one rail
            // and one fill today, one for EVERY domain row the moment the state check is dropped. That is
            // the assertion below.
            var rowFills = solids.Where(s => SameColor(s.Colour, activeFill)
                && Math.Abs(s.Rect.width - rowWidth) <= 0.5f
                && Math.Abs(s.Rect.height - rowHeight) <= 0.5f).ToList();
            var rowRails = solids.Where(s => SameColor(s.Colour, accent)
                && Math.Abs(s.Rect.width - UsSelectionSurfaceWidget.RailWidth) <= 0.5f
                && Math.Abs(s.Rect.height - rowHeight) <= 0.5f).ToList();

            Assert(rowRails.Count == 1,
                "exactly ONE row-sized rail may be painted while one domain is selected, got "
                + rowRails.Count + " of " + declaredRows.Count + " rows: " + Describe(rowRails));
            Assert(rowFills.Count == 1,
                "exactly ONE row-sized selected fill may be painted while one domain is selected, got "
                + rowFills.Count + " of " + declaredRows.Count + " rows: " + Describe(rowFills));

            Console.WriteLine("[t21-unselected] declaredRows=" + declaredRows.Count + " selectedKeys="
                + selectedKeys.Count + " rowFills=" + rowFills.Count + " rowRails=" + rowRails.Count);
        }
        finally
        {
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>The DOMAIN rows the engine itself arranged, in page space minus the content viewport
    /// origin. Since US-PACK1 one template draws both kinds, the census keys on the item shape: a header's
    /// item key is the bare pack key; a domain row's carries the '|' scope separator.</summary>
    private static List<Rect> DeclaredRows(UiLayoutSnapshot snapshot)
    {
        return snapshot.RectById.Keys
            .Where(key => key.StartsWith("pack-card-row#", StringComparison.Ordinal)
                && key.Substring(key.IndexOf('#') + 1).IndexOf('|') >= 0)
            .Select(key => Local(snapshot, key))
            .ToList();
    }

    private static bool HasRail(List<(Rect Rect, Color Colour)> solids, Color accent, Rect band)
    {
        return solids.Any(s => SameColor(s.Colour, accent)
            && Math.Abs(s.Rect.width - UsSelectionSurfaceWidget.RailWidth) <= 0.5f
            && Math.Abs(s.Rect.x - band.x) <= 0.5f && s.Rect.y <= band.y + 0.5f
            && s.Rect.yMax >= band.yMax - 0.5f);
    }

    // ---------------------------------------------------------------------------------------------
    // Seams
    // ---------------------------------------------------------------------------------------------

    /// <summary>The rects the draw outlet was handed, in the space the widgets painted in. The override
    /// CONSUMES each button so a recording pass cannot fire the row actions while it measures.</summary>
    private static List<Rect> ButtonsDrawn(UiHost host)
    {
        var seen = new List<Rect>();
        FieldInfo field = RequireField("ButtonOverride", typeof(Func<Rect, bool>));
        field.SetValue(null, new Func<Rect, bool>(rect =>
        {
            seen.Add(rect);
            return true;
        }));
        try
        {
            host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        }
        finally
        {
            field.SetValue(null, null);
        }

        return seen;
    }

    private static FieldInfo RequireField(string name, Type type)
    {
        FieldInfo? field = typeof(UiNative).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "REFLECTION BLOCKER: UiNative." + name + " is not the expected seam on net472; this lane"
                + " cannot record a button and must not pass silently");
        }

        if (field.FieldType != type)
        {
            throw new InvalidOperationException(
                "UiNative." + name + " is " + field.FieldType.Name + ", expected " + type.Name);
        }

        return field;
    }

    private static IList Recorded(string fieldName)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException(
                "the runtime stub does not record '" + fieldName + "'; this lane cannot observe a draw and"
                + " must not pass silently");
        }

        var list = field.GetValue(null) as IList;
        if (list == null) throw new InvalidOperationException("the stub's '" + fieldName + "' recorder is not a list");
        return list;
    }

    private static void ClearSolids()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod(
            "ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null)
        {
            throw new InvalidOperationException("the runtime stub does not expose ClearDrawBoxSolidCalls");
        }

        clear.Invoke(null, null);
    }

    /// <summary>Two passes: the first materializes the Repeat subtrees, and the snapshot returned by the
    /// first pass does not carry the materialized row ids yet. A lane that read the first snapshot found NO
    /// 'pack-card-row#' key at all and would have measured nothing.</summary>
    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        return host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
    }

    private static Rect Viewport(UiLayoutSnapshot snapshot)
    {
        if (!snapshot.Viewports.TryGetValue("content-scroll", out Rect viewport))
        {
            throw new InvalidOperationException("the arranged snapshot carries no 'content-scroll' viewport");
        }

        return viewport;
    }

    /// <summary>An arranged element's rect in the coordinate space the recorded UiNative rects live in: the
    /// content scroll's local space (page rect minus the viewport origin). Comparing the two spaces directly
    /// is the documented cross-space trap this suite already paid for once - the rects look plausible and
    /// match nothing. The same precondition as that lane's helper applies: the element must be a DIRECT
    /// descendant of the scroll content, with no further scoped container in between.</summary>
    private static Rect Local(UiLayoutSnapshot snapshot, string id)
    {
        if (!snapshot.RectById.TryGetValue(id, out Rect page))
        {
            throw new InvalidOperationException("the arranged snapshot carries no '" + id + "'");
        }

        Rect viewport = Viewport(snapshot);
        return new Rect(page.x - viewport.x, page.y - viewport.y, page.width, page.height);
    }

    private static bool SameColor(Color a, Color b)
    {
        return Math.Abs(a.r - b.r) <= 0.0005f && Math.Abs(a.g - b.g) <= 0.0005f
            && Math.Abs(a.b - b.b) <= 0.0005f && Math.Abs(a.a - b.a) <= 0.0005f;
    }

    private static string Hex(Color color)
    {
        return "#" + Channel(color.r) + Channel(color.g) + Channel(color.b);
    }

    private static string Channel(float value)
    {
        int channel = Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        return channel.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Describe(Rect rect)
    {
        return "(x=" + Num(rect.x) + " y=" + Num(rect.y) + " w=" + Num(rect.width) + " h=" + Num(rect.height) + ")";
    }

    private static string Describe(IEnumerable<(Rect Rect, Color Colour)> painted)
    {
        return "[" + string.Join(", ", painted.Select(p => Describe(p.Rect) + " " + Hex(p.Colour))) + "]";
    }

    private static string Num(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static List<UiElementSpec> Descendants(UiElementSpec root)
    {
        var all = new List<UiElementSpec>();
        void Walk(UiElementSpec element)
        {
            all.Add(element);
            foreach (UiElementSpec child in element.Children) Walk(child);
        }

        Walk(root);
        return all;
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("UsSelectionSurfaceLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
