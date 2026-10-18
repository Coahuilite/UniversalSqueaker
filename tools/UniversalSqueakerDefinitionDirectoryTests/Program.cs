using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Verse;
using RimWorld;

using FerriteLib.UiKit.Kernel;

using UniversalSqueaker;
using UniversalSqueaker.UI;
using UniversalSqueaker.KernelHostTests;

namespace UniversalSqueaker.DefinitionDirectoryTests;

/// <summary>
/// OVT-005 (WO-US-OVERVIEW-THEMES-20261015): the isolated definition-directory harness.
/// Job A runs the PRODUCTION collector (product DLL: VerseDefinitionCollector +
/// RaceDefinitionCatalog) over rawDef fixtures on the EXTENDED stub runtime (frozen renderer
/// stubs unchanged + this task's definition model). Job B runs the REAL production pair -
/// UsKernelSettingsSource + UsKernelSettingsHost + UiHost over the shipped Schema2 manifest -
/// consuming exactly what the collector published: the no-pack HAR arrival, the four xenotype
/// states, the mech-HAR order, the band geometry (measure == draw), and zero persisted writes.
/// The fixtures are MODELS of the HAR shape (CO TEST-BOUNDARY), not a user's loaded mod list.
/// </summary>
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        // Same seam the frozen host harness uses: the product's compile-time 0Harmony
        // reference resolves to the stub assembly that carries the HarmonyLib.Harmony type token.
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            args.Name.StartsWith("0Harmony", StringComparison.Ordinal)
                ? typeof(UniversalSqueakerMod).BaseType!.Assembly
                : null;

        try
        {
            CollectorRawDefTests.RunAll();
            DirectoryHostSmokeTests.RunAll();
            NarrowXenoGeometryTests.RunAll();
        }
        catch (Exception ex)
        {
            Console.WriteLine("UNCAUGHT: " + ex);
            return 2;
        }

        if (failures > 0)
        {
            Console.Error.WriteLine("UniversalSqueakerDefinitionDirectoryTests FAILED (" + failures + ").");
            return 1;
        }

        Console.WriteLine("UniversalSqueakerDefinitionDirectoryTests ALL PASS");
        return 0;
    }

    internal static void Check(bool condition, string message)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + message);
            return;
        }

        failures++;
        Console.WriteLine("  FAIL: " + message);
    }
}

/// <summary>Fixture def shapes (compiled against the extended stubs; the product IL binds the
/// same names at runtime).</summary>
internal class RatkinDef : AlienRace.ThingDef_AlienRace
{
}

internal sealed class RatkinWarriorDef : RatkinDef
{
}

internal sealed class ScytherMechDef : AlienRace.ThingDef_AlienRace
{
}

internal static class Fixtures
{
    internal const string CorePackId = "Ludeon.RimWorld";
    internal const string BiotechPackId = "Ludeon.RimWorld.Biotech";

    internal static ModContentPack CorePack() => new ModContentPack(CorePackId, "Core", isCore: true);
    internal static ModContentPack BiotechPack() => new ModContentPack(BiotechPackId, "Biotech");
    internal static ModContentPack RatkinPack() => new ModContentPack("coahuilite.ratkin", "Ratkin Race Pack");
    internal static ModContentPack FolkPack() => new ModContentPack("someone.else.folk", "Other Folk Mod");

    internal static RaceProperties Humanlike() => new RaceProperties { Humanlike = true };
    internal static RaceProperties Animal() => new RaceProperties { Animal = true };
    internal static RaceProperties Mech() => new RaceProperties { IsMechanoid = true };
    internal static RaceProperties Entity() => new RaceProperties { IsAnomalyEntity = true };

    internal static T Pawn<T>(T def, string defName, string label, ThingCategory category,
        Type thingClass, RaceProperties? race, ModContentPack? pack) where T : ThingDef, new()
    {
        def.defName = defName;
        def.label = label;
        def.category = category;
        def.thingClass = thingClass;
        def.race = race;
        def.modContentPack = pack;
        return def;
    }

    /// <summary>The full scenario graph the collector must classify. The corpse SHARES the live
    /// pawn's RaceProperties instance - exactly the 1.7-proven generator behaviour
    /// (thingDef.race = pawnDef.race) that made "race != null" admit corpses.</summary>
    internal static void Seed()
    {
        DefDatabase<ThingDef>.Clear();
        DefDatabase<XenotypeDef>.Clear();

        RaceProperties ratkinRace = Humanlike();
        DefDatabase<ThingDef>.Add(Pawn(new RatkinDef(), "Ratkin", "ratkin", ThingCategory.Pawn,
            typeof(Pawn), ratkinRace, RatkinPack()));
        DefDatabase<ThingDef>.Add(Pawn(new RatkinWarriorDef(), "RatkinWarrior", "ratkin warrior",
            ThingCategory.Pawn, typeof(Pawn), Humanlike(), RatkinPack()));
        // THE corpse: shares the live def's race object, Item category, Corpse thingClass.
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "Corpse_Ratkin", "ratkin corpse",
            ThingCategory.Item, typeof(Corpse), ratkinRace, RatkinPack()));
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "Steel", "steel", ThingCategory.Item,
            typeof(object), race: null!, pack: CorePack()));
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "Wall", "wall", ThingCategory.Building,
            typeof(object), race: null!, pack: CorePack()));
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "Human", "human", ThingCategory.Pawn,
            typeof(Pawn), Humanlike(), CorePack()));
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "thirdfolk", "Third Folk", ThingCategory.Pawn,
            typeof(Pawn), Humanlike(), FolkPack()));
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "Wild_boar", "wild boar", ThingCategory.Pawn,
            typeof(Pawn), Animal(), CorePack()));
        // mech built ON the HAR framework: both axes at once (PM mech-HAR addendum).
        DefDatabase<ThingDef>.Add(Pawn(new ScytherMechDef(), "Scyther", "scyther", ThingCategory.Pawn,
            typeof(Pawn), Mech(), RatkinPack()));
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "Ghoul", "ghoul", ThingCategory.Pawn,
            typeof(Pawn), Entity(), BiotechPack()));
        // same label, different stable identity
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "zoo.a", "Twin", ThingCategory.Pawn,
            typeof(Pawn), Animal(), CorePack()));
        DefDatabase<ThingDef>.Add(Pawn(new ThingDef(), "aqua.b", "Twin", ThingCategory.Pawn,
            typeof(Pawn), Animal(), BiotechPack()));

        DefDatabase<XenotypeDef>.Add(Xeno("x_free", "Ashen", CorePack()));
        DefDatabase<XenotypeDef>.Add(Xeno("x_orphan", "Bone", BiotechPack()));
        DefDatabase<XenotypeDef>.Add(Xeno("x_mod", "Mycel", FolkPack()));
    }

    private static XenotypeDef Xeno(string defName, string label, ModContentPack pack)
    {
        var def = new XenotypeDef { defName = defName, label = label };
        def.modContentPack = pack;
        return def;
    }
}

internal static class CollectorRawDefTests
{
    internal static void RunAll()
    {
        Console.WriteLine("[collector] rawDef fixtures over the PRODUCTION collector");
        Fixtures.Seed();
        UsDefinitionSnapshot snap = VerseDefinitionCollector.Collect();

        UsRaceIdentity? Find(string defName)
        {
            foreach (UsRaceIdentity r in snap.Races)
            {
                if (r.DefName == defName) return r;
            }

            return null;
        }

        Program.Check(Find("Ratkin") is { IsHar: true, IsHumanlike: true } ratkin
                && ratkin.Source == UsRaceSourceKind.ThirdParty
                && ratkin.SourcePackageId == "coahuilite.ratkin"
                && ratkin.SourcePackageName == "Ratkin Race Pack",
            "HAR humanlike recognised through the ThingDef type chain, source ID AND NAME kept");
        Program.Check(Find("RatkinWarrior") is { IsHar: true },
            "DERIVED HAR type (RatkinWarrior : Ratkin : ThingDef_AlienRace) recognised");
        Program.Check(Find("Scyther") is { IsHar: true, IsMechanoid: true },
            "mech HAR keeps BOTH axes (framework + mechanoid)");
        Program.Check(Find("Corpse_Ratkin") == null && Find("Steel") == null && Find("Wall") == null,
            "corpse sharing the live race object, items and buildings are EXCLUDED by real facts");
        Program.Check(Find("Human") is { Source: UsRaceSourceKind.Core } && Find("Ghoul") is { IsAnomalyEntity: true },
            "vanilla humanlike + Anomaly entity flags read from the real properties");
        Program.Check(Find("zoo.a") != null && Find("aqua.b") != null,
            "same-label different-ID rows both survive");
        Program.Check(Find("thirdfolk") is { IsHar: false, Source: UsRaceSourceKind.ThirdParty },
            "non-HAR third-party humanlike stays visible without a HAR mark");

        int xenos = 0;
        bool xenoSource = false;
        foreach (UsXenotypeIdentity x in snap.Xenotypes)
        {
            xenos++;
            if (x.DefName == "x_mod" && x.Source == UsRaceSourceKind.ThirdParty
                && x.SourcePackageName == "Other Folk Mod") xenoSource = true;
        }

        Program.Check(xenos == 3 && xenoSource,
            "the FULL xenotype directory (3 loaded defs) with real per-def source ID + NAME");

        // Snapshot ownership: mutating the collector's working list cannot move a published copy.
        var mutable = new List<UsRaceIdentity> { Find("Human")!.Value };
        var owned = new UsDefinitionSnapshot(mutable, Array.Empty<UsXenotypeIdentity>());
        mutable.Clear();
        Program.Check(owned.Races.Count == 1, "the snapshot COPIES its input (mutating the caller's list cannot edit a published directory)");

        // Publish-once / reuse-on-reopen: the catalog is a volatile slot over the immutable snapshot.
        RaceDefinitionCatalog.Publish(owned);
        UsDefinitionSnapshot first = RaceDefinitionCatalog.Current;
        Program.Check(ReferenceEquals(first, RaceDefinitionCatalog.Current),
            "window reopen reuses the SAME snapshot instance (no per-GUI rebuild)");
        RaceDefinitionCatalog.Publish(snap);
        Program.Check(ReferenceEquals(RaceDefinitionCatalog.Current, snap), "publication swaps atomically");
    }
}

internal static class DirectoryHostSmokeTests
{
    private static readonly Vector2 Page = new(1280f, 720f);

    internal static void RunAll()
    {
        Console.WriteLine("[host] real UsKernelSettingsSource + UsKernelSettingsHost on the published directory");
        Fixtures.Seed();
        ModsConfig.BiotechActive = true;
        RaceDefinitionCatalog.CollectAndPublish(); // the PRODUCTION startup entry, once
        Full = RaceDefinitionCatalog.Current;

        var settings = new UniversalSqueakerSettings();
        var source = new UsKernelSettingsSource(settings);
        using UiHost host = UsKernelSettingsHost.Create(source, new Metrics());

        host.Bindings.Invoke("set-tab", "Overview");
        Arrange(host);
        UsRaceBoardView view = View(host);

        Program.Check(view.Rows.Count == 5
                && view.Rows[0].RaceDefName == "Ratkin" && view.Rows[1].RaceDefName == "RatkinWarrior"
                && view.Rows[2].RaceDefName == "Scyther" && view.Rows[3].RaceDefName == "Human"
                && view.Rows[4].RaceDefName == "thirdfolk",
            "default Player races: HAR identities (incl. the MECH HAR) lead, vanilla humanlike next, "
                + "other third-party humanlike after; corpse/animal/mech/entity absent");
        Program.Check(view.TotalRaces == 9 && view.DisplayedRaces == 5,
            "the count line separates the screen (5) from the full directory (9)");

        // All group + xenotype band expanded: band sits AFTER the humanlike tier, BEFORE creatures.
        source.ViewState.BoardGroup = UsBoardBrowsePolicy.BoardGroup.All;
        source.ViewState.BoardXenoExpanded = true;
        Bump(host);
        view = View(host);
        Program.Check(view.Rows.Count == 9, "All shows the whole directory");
        Program.Check(view.Xenotypes.Count == 3 && view.XenotypeBandIndex == 5,
            "expanded band lists every loaded xenotype and rides the humanlike segment (index 5, before creatures)");
        // PM test-invariants item 1: the measure==draw proof uses TWO directories with the SAME
        // race-row count, the SAME xenotype count and the SAME per-row heights - the only difference
        // is where the band index lands. The height delta must then be EXACTLY one section line,
        // and the real draw trace must show the section caption once more in the humanlike-adjacent
        // layout than in the index-0 layout (the browse toggle carries the same textKey, so the
        // proof counts occurrences, never mere presence).
        float sectionLine = 18f + RowGapConst; // calibrated Tiny line advance + the row gap
        var bandA = new UsDefinitionSnapshot(new[]
        {
            Identity("thirdfolk", "Third Folk", UsRaceSourceKind.ThirdParty, UsRaceLifeKind.Humanlike),
            Identity("Human", "human", UsRaceSourceKind.Core, UsRaceLifeKind.Humanlike),
        }, new[] { Xen("x_free", "Ashen", UsRaceSourceKind.Core), Xen("x_mod", "Mycel", UsRaceSourceKind.ThirdParty) });
        var bandB = new UsDefinitionSnapshot(new[]
        {
            Identity("Wild_boar", "wild boar", UsRaceSourceKind.Core, UsRaceLifeKind.Animal),
            Identity("Ghoul", "ghoul", UsRaceSourceKind.OfficialDlc, UsRaceLifeKind.AnomalyEntity),
        }, new[] { Xen("x_free", "Ashen", UsRaceSourceKind.Core), Xen("x_mod", "Mycel", UsRaceSourceKind.ThirdParty) });

        RaceDefinitionCatalog.Publish(bandA);
        Bump(host);
        view = View(host);
        Program.Check(view.XenotypeBandIndex == 2 && view.Rows.Count == 2 && view.Xenotypes.Count == 2,
            "band A (humanlike present): index 2, two race rows, two xenos (same chip profile as B)");
        float heightA = Arrange(host).RectById["race-board-cards"].height;
        int traceA = CountDrawn(Verse.Widgets.LabelTexts, "US.Board.XenoDir", host);

        RaceDefinitionCatalog.Publish(bandB);
        Bump(host);
        view = View(host);
        Program.Check(view.XenotypeBandIndex == 0 && view.Rows.Count == 2 && view.Xenotypes.Count == 2,
            "band B (no humanlike): index 0, SAME rows, SAME xenos");
        float heightB = Arrange(host).RectById["race-board-cards"].height;
        int traceB = CountDrawn(Verse.Widgets.LabelTexts, "US.Board.XenoDir", host);

        Program.Check(Mathf.Abs((heightA - heightB) - sectionLine) < 0.01f,
            "exact title-height relation: A - B == one section line (A " + heightA.ToString("F1")
                + ", B " + heightB.ToString("F1") + ", expected delta " + sectionLine.ToString("F1") + ")");
        Program.Check(traceA == traceB + 1,
            "real draw trace: the section caption is DRAWN exactly once more in A than in B "
                + "(A " + traceA + ", B " + traceB + ") - measure and draw agree");
        RaceDefinitionCatalog.Publish(Full);
        Bump(host);

        // No-pack HAR arrival through the REAL action chain: identity survives, zero writes.
        RaceDefinitionCatalog.Publish(Full);
        Bump(host);
        int selectionsBefore = settings.voicePackSelections.Count;
        SqueakVoicePackMode modeBefore = settings.voicePackMode;
        string swatchBefore = settings.uiSwatchId;
        float volumeBefore = settings.globalVolumeFactor;

        host.Bindings.Invoke("race-board-open", "Ratkin");
        UiLayoutSnapshot after = Arrange(host);
        VoicePacksPageState st = source.ViewState;
        Program.Check(st.RaceFilter == "Ratkin" && st.SelectedScope == SqueakVoicePackScope.Race
                && st.SelectedRaceDefName == "Ratkin",
            "the arrival names the exact no-pack HAR race (filter + selected identity)");
        VoicePackDomainView? selected = host.Bindings.Get<VoicePackDomainView?>("selected-domain");
        Program.Check(selected != null && selected.Value.RaceDefName == "Ratkin"
                && selected.Value.CandidateCount == 0 && selected.Value.State == SqueakVoicePackDomainState.Available,
            "ResolveSelectedDomain answers the REAL empty state for the arrived race - no first-domain fallback");
        after = Arrange(host);
        selected = host.Bindings.Get<VoicePackDomainView?>("selected-domain");
        Program.Check(selected != null && selected.Value.RaceDefName == "Ratkin",
            "identity does not drift across a second BuildView/arrange");
        Program.Check(settings.voicePackSelections.Count == selectionsBefore
                && settings.voicePackMode == modeBefore && settings.uiSwatchId == swatchBefore
                && Math.Abs(settings.globalVolumeFactor - volumeBefore) < 0.0001f,
            "navigation wrote NOTHING persisted: no selection, mode, swatch or volume change");

        // The FOUR xenotype states on the production chain.
        XenoArrival(source, settings, host, "x_free", null, SqueakVoicePackDomainState.Available, "loaded, no packs, no saved keys");
        XenoArrival(source, settings, host, "x_orphan", new[] { "us.gone" }, SqueakVoicePackDomainState.Orphan,
            "loaded, no packs, SAVED keys -> saved-choice-missing (old meaning kept)");
        XenoArrival(source, settings, host, "x_gone", new[] { "us.z" }, SqueakVoicePackDomainState.TargetUnavailable,
            "NOT loaded -> target not loaded (never rewritten to a lie)");
        ModsConfig.BiotechActive = false;
        XenoArrival(source, settings, host, "x_free", null, SqueakVoicePackDomainState.Dormant,
            "Biotech off -> dormant (the settings layer's own first answer)");
        ModsConfig.BiotechActive = true;

        // Chips + search through the host writes (display only). PM test-invariants item 3: the
        // browse section now CARRIES a real non-empty selection and proves its full content
        // survives every browse write - not an empty Count==0.
        settings.voicePackSelections.Add(new VoicePackSelectionRecord
        {
            scope = SqueakVoicePackScope.Race,
            raceDefName = "Human",
            xenotypeDefName = "",
            enabledPackKeys = new List<string> { "us.kiiro:call", "us.kiiro:idle" },
        });
        string selectionsBeforeBrowse = SelectionsSnapshot(settings);
        SqueakVoicePackMode modeBeforeBrowse = settings.voicePackMode;
        host.Bindings.Invoke("board-filter", "har");
        Bump(host);
        view = View(host);
        Program.Check(view.Rows.Count == 3 && view.Rows[0].Identity.IsHar,
            "the HAR chip narrows to framework identity across life kinds (mech HAR included)");
        host.Bindings.Invoke("board-filter", "har"); // toggle off
        host.Bindings.Set("board-search", "coahuilite");
        Bump(host);
        view = View(host);
        Program.Check(view.Rows.Count == 3, "source search by package id finds the HAR family");
        host.Bindings.Set("board-search", "");
        Bump(host);
        Program.Check(SelectionsSnapshot(settings) == selectionsBeforeBrowse
                && settings.voicePackMode == modeBeforeBrowse,
            "browse controls wrote nothing persisted: the SEEDED selection's identity and full key list survive");

        // The xeno directory row's own arrival (race-board-xeno-open): exact target, honest empty.
        string snapXenoOpen = SelectionsSnapshot(settings);
        host.Bindings.Invoke("race-board-xeno-open", "x_mod");
        Arrange(host);
        selected = host.Bindings.Get<VoicePackDomainView?>("selected-domain");
        Program.Check(st.XenotypeFilter == "x_mod" && selected != null
                && selected.Value.TargetDefName == "x_mod" && selected.Value.CandidateCount == 0,
            "a no-pack mod xenotype arrives with its exact target identity and a real empty state");
        Program.Check(SelectionsSnapshot(settings) == snapXenoOpen,
            "the xeno arrival wrote nothing persisted (content-identical selection set)");

        // CO review item 8: a REAL window lifecycle, not two getter reads. Dispose this host (the
        // window closed), build a NEW source + host over the same settings (the window reopened),
        // arrange AND draw it, and prove the lifecycle consumed the SAME published snapshot - the
        // directory was not re-collected, the facts did not drift, and the reopen drew the page.
        host.Dispose();
        var source2 = new UsKernelSettingsSource(settings);
        using UiHost host2 = UsKernelSettingsHost.Create(source2, new Metrics());
        host2.Bindings.Invoke("set-tab", "Overview");
        source2.ViewState.BoardGroup = UsBoardBrowsePolicy.BoardGroup.All;
        host2.MeasureAndArrange(new Vector2(1280f, 720f));
        Program.Check(ReferenceEquals(RaceDefinitionCatalog.Current, Full),
            "reopen reuses the SAME published snapshot instance (no re-collection on window lifecycle)");
        UsRaceBoardView view2 = host2.Bindings.Get<UsRaceBoardView>("race-board-view");
        Program.Check(view2.TotalRaces == 9 && view2.Rows.Count == 9
                && view2.Rows[0].RaceDefName == "Ratkin",
            "the reopened host projects the identical directory through the real pipeline");
        Verse.Widgets.LabelTexts.Clear();
        host2.DrawChecked(new Rect(0f, 0f, 1280f, 720f));
        bool reopenedDrew = false;
        foreach (object o in Verse.Widgets.LabelTexts)
        {
            if (o is string s && s.IndexOf("Ratkin Race Pack", StringComparison.Ordinal) >= 0) reopenedDrew = true;
        }

        Program.Check(reopenedDrew, "the REBUILT host actually drew the reused directory (real draw, not a getter)");
    }

    private static void XenoArrival(UsKernelSettingsSource source, UniversalSqueakerSettings settings, UiHost host, string target,
        string[]? savedKeys, SqueakVoicePackDomainState want, string why)
    {
        if (savedKeys != null)
        {
            settings.voicePackSelections.Add(new VoicePackSelectionRecord
            {
                scope = SqueakVoicePackScope.Xenotype,
                raceDefName = "Human",
                xenotypeDefName = target,
                enabledPackKeys = new List<string>(savedKeys),
            });
        }

        string snap0 = SelectionsSnapshot(settings); // AFTER fixture seeding: the ARRIVAL must move nothing
        VoicePacksPageState st = source.ViewState;
        st.SelectedScope = SqueakVoicePackScope.Xenotype;
        st.SelectedRaceDefName = savedKeys != null ? "Human" : "";
        st.SelectedTargetName = target;
        st.DomainSelectionCanceled = false;
        Bump(host);
        VoicePackDomainView? selected = host.Bindings.Get<VoicePackDomainView?>("selected-domain");
        Program.Check(selected != null && selected.Value.TargetDefName == target
                && selected.Value.State == want,
            "xeno state " + want + ": " + why + " (got " + (selected?.State.ToString() ?? "null") + ")");
        Program.Check(selected == null || string.Equals(selected.Value.RaceDefName, st.SelectedRaceDefName,
                StringComparison.Ordinal),
            "xeno state " + want + ": the race context is not rewritten");
        // PM test-invariants item 2: content BEFORE cleanup. The seeded record must still carry its
        // exact domain identity and its FULL key list, and the whole selection set must be
        // byte-identical to the pre-arrival snapshot (a rewrite of keys would pass a Count check
        // and would survive a later Clear - so Clear only happens AFTER the content is proven).
        if (savedKeys != null)
        {
            VoicePackSelectionRecord? rec = settings.voicePackSelections.FirstOrDefault(r =>
                r.scope == SqueakVoicePackScope.Xenotype && r.raceDefName == "Human"
                && r.xenotypeDefName == target);
            Program.Check(rec != null && rec.enabledPackKeys.Count == savedKeys.Length
                    && rec.enabledPackKeys.Zip(savedKeys, (a, b) => a == b).All(x => x),
                "xeno state " + want + ": the saved record kept its EXACT identity and full key list");
        }

        Program.Check(SelectionsSnapshot(settings) == snap0,
            "xeno state " + want + ": zero writes - the whole selection set is content-identical");
        if (savedKeys != null)
        {
            settings.voicePackSelections.Clear(); // fixture cleanup AFTER the content was proven
        }
    }

    private static string SelectionsSnapshot(UniversalSqueakerSettings settings)
    {
        return string.Join(";", settings.voicePackSelections.Select(r =>
            r.scope + "|" + r.raceDefName + "|" + r.xenotypeDefName + "|" + string.Join(",", r.enabledPackKeys)));
    }

    private static UsDefinitionSnapshot Full = UsDefinitionSnapshot.Empty;

    private const float RowGapConst = 2f; // UsRaceBoardWidget.RowGap

    private static UsRaceIdentity Identity(string def, string label, UsRaceSourceKind source,
        UsRaceLifeKind life, bool har = false)
        => new UsRaceIdentity(def, label, source,
            har ? "coahuilite.ratkin" : "Ludeon.RimWorld", har ? "Ratkin Race Pack" : "Core", har, life);

    /// <summary>One real production draw pass, then count the recorded label occurrences of text.
    /// Clears the recorder first so the count is THIS layout, not the session history.</summary>
    private static int CountDrawn(System.Collections.IList recorder, string text, UiHost host)
    {
        recorder.Clear();
        Verse.Widgets.LabelRects.Clear();
        Verse.Widgets.LabelColors.Clear();
        host.DrawChecked(new Rect(0f, 0f, 1280f, 720f));
        int n = 0;
        foreach (object o in recorder)
        {
            if (o is string s && s == text) n++;
        }

        return n;
    }

    private static UsXenotypeIdentity Xen(string def, string label, UsRaceSourceKind source)
        => new UsXenotypeIdentity(def, label, source, "ludeon.core", "Core");

    private static UsRaceBoardView View(UiHost host) => host.Bindings.Get<UsRaceBoardView>("race-board-view");

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.MeasureAndArrange(Page);
        return host.MeasureAndArrange(Page);
    }

     /// <summary>Re-arms the session clock so the revision-cached view rebuilds after a direct
    /// browse-state write (the same net-zero toggle the real band would produce twice).</summary>
    private static void Bump(UiHost host)
    {
        host.Bindings.Invoke("board-xeno-toggle");
        host.Bindings.Invoke("board-xeno-toggle");
    }

}

/// <summary>
/// PM narrow/source addendum on the REAL production pair: a xenotype row with ONE quick-enable
/// candidate, a long display name, at the 760x524 policy viewport. The title and the chips must
/// share the action column's boundary (no intrusion into the enable button), the enable press
/// must fire through the carrier's own button seam and write EXACTLY the (Xenotype, race, target,
/// pack-key) identity, and the source chip must SHOW the real package name in the recorded draw.
/// The sound pack is seeded as a VALID production def through the stub databases - the real
/// validator, catalog refresh and write funnel run unmodified.
/// </summary>
internal static class NarrowXenoGeometryTests
{
    internal static void RunAll()
    {
        Console.WriteLine("[narrow] long xeno name + unique quick-enable at 760x524 on the real pair");
        // Directory: one HAR race (real package name), Human, one creature, the long xeno.
        var races = new List<UsRaceIdentity>
        {
            new UsRaceIdentity("Ratkin", "ratkin", UsRaceSourceKind.ThirdParty,
                "coahuilite.ratkin", "Ratkin Race Pack", true, UsRaceLifeKind.Humanlike),
            new UsRaceIdentity("Human", "human", UsRaceSourceKind.Core, "Ludeon.RimWorld", "Core",
                false, UsRaceLifeKind.Humanlike),
            new UsRaceIdentity("Wild_boar", "wild boar", UsRaceSourceKind.Core, "Ludeon.RimWorld", "Core",
                false, UsRaceLifeKind.Animal),
        };
        var xenos = new List<UsXenotypeIdentity>
        {
            new UsXenotypeIdentity("x_long",
                "Extremely Long Xenotype Designation For Narrow Geometry",
                UsRaceSourceKind.OfficialDlc, "Ludeon.RimWorld.Biotech", "Biotech"),
        };
        RaceDefinitionCatalog.Publish(new UsDefinitionSnapshot(races, xenos));

        // ONE valid production pack naming x_long (real validator + catalog refresh).
        DefDatabase<SqueakVoicePackDef>.Clear();
        var pack = new SqueakVoicePackDef
        {
            scope = SqueakVoicePackScope.Xenotype,
            raceDefName = "Human",
            targetDefName = "x_long",
            weight = 1f,
        };
        pack.defName = "US_longpack";
        pack.label = "Long Pack";
        var packer = new ModContentPack("coahuilite.longpack", "Long Pack Maker");
        packer.ModMetaData = new ModMetaData("coahuilite.longpack");
        pack.modContentPack = packer;
        var sound = new SoundDef { context = Verse.Sound.SoundContext.MapOnly };
        sound.defName = "US_longsound";
        sound.subSounds = new List<Verse.Sound.SubSoundDef> { new Verse.Sound.SubSoundDef { onCamera = false, grains = new List<Verse.Sound.AudioGrain> { new Verse.Sound.AudioGrain() } } };
        pack.actions = new List<SqueakVoicePackAction>
        {
            new SqueakVoicePackAction { action = SqueakAction.Call, sounds = new List<SoundDef> { sound } },
        };
        DefDatabase<SqueakVoicePackDef>.Add(pack);
        ModsConfig.BiotechActive = true;
        var settings = new UniversalSqueakerSettings();
        // DIAGNOSTIC: surface the REAL validator verdict through reflection (internal contract).
        var validatorType = typeof(SqueakVoicePackDef).Assembly.GetType("UniversalSqueaker.SqueakVoicePackValidator");
        if (validatorType != null)
        {
            var getErrors = validatorType.GetMethod("GetErrors", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var errs = getErrors?.Invoke(null, new object[] { pack }) as System.Collections.IEnumerable;
            string joined = "";
            if (errs != null) { foreach (object o in (System.Collections.IEnumerable)errs) { joined += (joined.Length > 0 ? " | " : "") + o; } }
            Console.WriteLine("  [validator] " + (joined.Length > 0 ? joined : "CLEAN"));
        }
        else
        {
            Console.WriteLine("  [validator] type not found");
        }
        SqueakXenotypeCatalog.Refresh(settings);
        Console.WriteLine("  [catalog] xenotypePacks=" + SqueakXenotypeCatalog.Current.XenotypePacksByDefName.Count
            + " raceDefNames=" + SqueakXenotypeCatalog.Current.RaceDefNames.Count);
        Program.Check(SqueakXenotypeCatalog.Current.XenotypePacksByDefName.ContainsKey("x_long"),
            "the seeded pack passes the REAL validator and assembles into the sound catalog");

        // PM evidence clarification A: the dormant leg must prove CONTENT PRESERVATION, not an
        // empty-set equality. Seed a non-empty OLD saved selection on an unrelated domain (the
        // Human RACE domain, two full keys - no pack supplies it, so it is the honest
        // saved-choice-missing shape) BEFORE any browse/press. x_long stays a live quick
        // candidate (its own Xenotype domain has no saved keys yet).
        settings.voicePackSelections.Add(new VoicePackSelectionRecord
        {
            scope = SqueakVoicePackScope.Race,
            raceDefName = "Human",
            xenotypeDefName = "",
            enabledPackKeys = new List<string> { "us.old:call", "us.old:idle" },
        });
        string oldSelectionsContent = "Race|Human||us.old:call,us.old:idle";

        var source = new UsKernelSettingsSource(settings);
        using UiHost host = UsKernelSettingsHost.Create(source, new Metrics());
        host.Bindings.Invoke("set-tab", "Overview");
        host.Bindings.Invoke("toggle-intro");
        source.ViewState.BoardXenoExpanded = true;
        host.Bindings.Invoke("board-group", "all");    // xeno band + creature rows share the screen
        host.MeasureAndArrange(new Vector2(760f, 524f));

        // Record the drawn label texts/rects and every button rect the carrier sees.
        var labelRects = Verse.Widgets.LabelRects;
        var labelTexts = Verse.Widgets.LabelTexts;
        var labelColors = Verse.Widgets.LabelColors;
        labelRects.Clear(); labelTexts.Clear(); labelColors.Clear();
        var buttons = new List<Rect>();
        var seam = typeof(UiNative).GetField("ButtonOverride",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic)!;
        object? previous = seam.GetValue(null);
        seam.SetValue(null, new Func<Rect, bool>(r => { buttons.Add(r); return false; }));
        host.DrawChecked(new Rect(0f, 0f, 760f, 524f));
        seam.SetValue(null, previous);

        Rect title = default; bool foundTitle = false;
        for (int i = 0; i < labelTexts.Count && i < labelRects.Count; i++)
        {
            if (labelTexts[i] is string s && s.StartsWith("Extremely", StringComparison.Ordinal)
                    && labelRects[i] is Rect r)
            {
                title = r; foundTitle = true; break;
            }
        }

        Program.Check(foundTitle, "the long xenotype title is drawn on the narrow page");
        Rect enable = default; bool foundEnable = false;
        foreach (Rect b in buttons)
        {
            if (Math.Abs(b.width - 200f) < 0.5f && b.y < title.y + 90f && b.yMax > title.y - 4f)
            {
                enable = b; foundEnable = true; break;
            }
        }

        Program.Check(foundEnable, "the unique quick-enable button is drawn for the x_long row");
        Program.Check(title.xMax <= enable.x + 0.5f,
            "narrow geometry: the long title stops at the action column (title xMax " + title.xMax.ToString("F1")
                + " vs enable x " + enable.x.ToString("F1") + ") - no intrusion into the clickable band");

        // Source DISPLAY (PM item 3): the race row's chip carries the REAL package name in the draw.
        bool showsName = false;
        for (int i = 0; i < labelTexts.Count; i++)
        {
            if (labelTexts[i] is string s && s.IndexOf("Ratkin Race Pack", StringComparison.Ordinal) >= 0) { showsName = true; break; }
        }


        Program.Check(showsName, "the race source chip SHOWS the real package name in the recorded draw (display, not only search)");

        // CO item 9: the XENO row's OWN source caption and the chips' right boundary.
        bool showsXenoSource = false;
        Rect chipRightMost = default;
        for (int i = 0; i < labelTexts.Count && i < labelRects.Count; i++)
        {
            if (labelTexts[i] is string s && labelRects[i] is Rect lr
                    && s.IndexOf("Biotech", StringComparison.Ordinal) >= 0 && lr.y > title.y)
            {
                showsXenoSource = true;
                if (lr.xMax > chipRightMost.xMax) chipRightMost = lr;
            }
        }


        Program.Check(showsXenoSource,
            "the xeno row carries its OWN source caption (Biotech) in the recorded draw - not borrowed from a race row");
        Program.Check(chipRightMost.xMax <= enable.x + 0.5f,
            "narrow geometry: the xeno source chip also stops at the action column (chip xMax "
                + chipRightMost.xMax.ToString("F1") + " vs enable x " + enable.x.ToString("F1") + ")");

        // PM dormant-contract leg: with Biotech OFF the xeno axis offers NO quick-enable exit -
        // the enable button must be GONE from the real draw while the directory row itself, its
        // supply count and every saved fact stay complete (dormant is a display gate on the
        // candidate, never a data loss). Then back ON for the positive press below.
        string selectionsBeforeDormant = string.Join(";", settings.voicePackSelections.Select(r =>
            r.scope + "|" + r.raceDefName + "|" + r.xenotypeDefName + "|" + string.Join(",", r.enabledPackKeys)));
        ModsConfig.BiotechActive = false;
        host.Bindings.Invoke("board-group", "all");
        host.Bindings.Invoke("board-group", "all");
        host.MeasureAndArrange(new Vector2(760f, 524f));
        var offButtons = new List<Rect>();
        seam.SetValue(null, new Func<Rect, bool>(r => { offButtons.Add(r); return false; }));
        host.DrawChecked(new Rect(0f, 0f, 760f, 524f));
        seam.SetValue(null, previous);
        bool enableGone = true;
        foreach (Rect b in offButtons)
        {
            if (Math.Abs(b.width - 200f) < 0.5f && b.y < title.y + 90f && b.yMax > title.y - 4f) enableGone = false;
        }

        Program.Check(enableGone, "Biotech off: no quick-enable button is drawn on the xeno row (dormant exit gate)");
        string offSnapshot = string.Join(";", settings.voicePackSelections.Select(r =>
            r.scope + "|" + r.raceDefName + "|" + r.xenotypeDefName + "|" + string.Join(",", r.enabledPackKeys)));
        Program.Check(offSnapshot == selectionsBeforeDormant && offSnapshot == oldSelectionsContent,
            "Biotech off: the NON-EMPTY old record (Race|Human + both keys) survives byte-for-byte; "
                + "no quick candidate was collected and no write happened");
        Program.Check(settings.voicePackMode == SqueakVoicePackMode.Fallback,
            "Biotech off kept the sound-source mode (dormant is an environment answer, not a policy change)");
        VoicePackSelectionRecord? offRaceRecord = settings.voicePackSelections.FirstOrDefault(r =>
            r.scope == SqueakVoicePackScope.Race && r.raceDefName == "Human");
        Program.Check(offRaceRecord != null && offRaceRecord.enabledPackKeys.Count == 2
                && offRaceRecord.enabledPackKeys[0] == "us.old:call" && offRaceRecord.enabledPackKeys[1] == "us.old:idle",
            "Biotech off: the old record's FULL enabled keys are intact (field-level, not just a set string)");
        ModsConfig.BiotechActive = true;
        host.Bindings.Invoke("board-group", "all");
        host.Bindings.Invoke("board-group", "all");
        host.MeasureAndArrange(new Vector2(760f, 524f));

        // The real press through the carrier seam: exactly the x_long identity is written.
        int fired = 0;
        seam.SetValue(null, new Func<Rect, bool>(r =>
        {
            if (Math.Abs(r.width - 200f) < 0.5f && Math.Abs(r.y - enable.y) < 0.5f
                    && Math.Abs(r.x - enable.x) < 0.5f)
            {
                fired++;
                return true;
            }

            return false;
        }));
        host.DrawChecked(new Rect(0f, 0f, 760f, 524f));
        seam.SetValue(null, previous);
        Program.Check(fired == 1, "the enable press fired exactly once through the real button seam");
        VoicePackSelectionRecord? record = settings.voicePackSelections.FirstOrDefault(r =>
            r.scope == SqueakVoicePackScope.Xenotype && r.xenotypeDefName == "x_long");
        Program.Check(settings.voicePackSelections.Count == 2 && record != null
                && record.raceDefName == "Human"
                && record.enabledPackKeys.Count == 1
                && record.enabledPackKeys[0] == "coahuilite.longpack:US_longpack",
            "the press wrote EXACTLY the (Xenotype, Human, x_long, pack key) identity - one NEW domain record");
        VoicePackSelectionRecord? oldStill = settings.voicePackSelections.FirstOrDefault(r =>
            r.scope == SqueakVoicePackScope.Race && r.raceDefName == "Human");
        Program.Check(oldStill != null && oldStill.enabledPackKeys.Count == 2
                && oldStill.enabledPackKeys[0] == "us.old:call" && oldStill.enabledPackKeys[1] == "us.old:idle",
            "the ON press added its exact domain WITHOUT touching the old record's identity or keys");
        Program.Check(settings.voicePackMode == SqueakVoicePackMode.Fallback,
            "and the sound-source mode is untouched");
    }
}

internal sealed class Metrics : ITextMetrics
{
    public float MeasureText(string text, UiFont font, float width)
    {
        float need = MeasureWidth(text, font);
        if (need <= 0f) return LineHeight(font);
        int lines = (int)Math.Ceiling(need / Math.Max(1f, width));
        return lines * LineHeight(font);
    }

    public float MeasureWidth(string text, UiFont font)
    {
        float em = font switch
        {
            UiFont.Tiny => 12f,
            UiFont.Medium => 18f,
            _ => 16f,
        };

        float units = 0f;
        foreach (char c in text ?? "")
        {
            units += IsWide(c) ? 2f : 1f;
        }

        return units * em * 0.5f;
    }

    private static float LineHeight(UiFont font) => font switch
    {
        UiFont.Tiny => 18f,
        UiFont.Medium => 30f,
        _ => 21.33333f,
    };

    private static bool IsWide(char c)
    {
        return c >= '\u2E80' && (
            c <= '\u303F'
            || (c >= '\u3400' && c <= '\u4DBF')
            || (c >= '\u4E00' && c <= '\u9FFF')
            || (c >= '\uAC00' && c <= '\uD7AF')
            || (c >= '\uF900' && c <= '\uFAFF')
            || (c >= '\uFF00' && c <= '\uFF60')
            || (c >= '\uFFE0' && c <= '\uFFE6'));
    }
}
