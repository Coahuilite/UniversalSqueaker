using System;
using System.Collections.Generic;

using UniversalSqueaker.UI;

namespace UniversalSqueaker.UiLogicTests;

/// <summary>
/// CORRECTIONS-R1 topic 1: the board's state matrix on REAL plain inputs - no game, no Verse, no
/// fake mirror. RaceBoardProjection is ordinary data logic over the adapter's fact bundles, so the
/// full grid the correction demands (race+xeno / xeno-only / empty fallback table / saved-choice-
/// missing / Disabled / Vanilla, plus the quick-enable ambiguity rule and the pure navigation intent)
/// runs exactly as production will run it. (The file compiles into this zero-game assembly via the
/// csproj includes; the same source files the mod builds.)
/// </summary>
internal static class RaceBoardProjectionTests
{
    public static void RunAll()
    {
        RaceAndXenoCountSeparately();
        XenoOnlyFactsAndIntent();
        EmptyFallbackTableIsNeverReady();
        SavedChoiceMissingPerDomain();
        ModeAxesAreIndependent();
        QuickEnableAmbiguity();
        TargetUnloadIsNotSavedMissing();
        BrowseBandRules();
        Console.WriteLine("  RaceBoardProjectionTests OK");
    }

    private static void RaceAndXenoCountSeparately()
    {
        var facts = new[]
        {
            new UsRaceBoardFacts("human", "Human",
                packs: new[]
                {
                    Pack(SqueakVoicePackScope.Race, "", "us.a", "A"),
                    Pack(SqueakVoicePackScope.Xenotype, "sanguophage", "us.b", "B"),
                },
                domains: new[]
                {
                    Domain(SqueakVoicePackScope.Race, "", new[] { "us.a" }),
                    Domain(SqueakVoicePackScope.Xenotype, "sanguophage", new[] { "us.b" }),
                }, identity: Ident("", "")),
        };
        IReadOnlyList<RaceBoardRow> rows = BuildAll(facts, SqueakVoicePackMode.Fallback);
        Assert(rows.Count == 1, "one row");
        RaceBoardRow r = rows[0];
        Assert(r.RacePackCount == 1 && r.EnabledCount == 1 && r.XenoPackCount == 1 && r.XenoEnabledCount == 1,
            "race and xeno supply/selection count separately");
        Assert(!r.XenotypeSupplyOnly, "race supply means NOT xeno-only");
        Assert(r.SupplyCardKeys.Count == 2, "both cards listed for navigation");
    }

    private static void XenoOnlyFactsAndIntent()
    {
        var facts = new[]
        {
            new UsRaceBoardFacts("archoteria", "Archoteifer",
                packs: new[] { Pack(SqueakVoicePackScope.Xenotype, "archoteifer_high", "us.a", "A") },
                domains: new[] { Domain(SqueakVoicePackScope.Xenotype, "archoteifer_high", Array.Empty<string>()) }, identity: Ident("", "")),
        };
        RaceBoardRow r = BuildAll(facts, SqueakVoicePackMode.Fallback)[0];
        Assert(r.RacePackCount == 0 && r.XenoPackCount == 1 && r.XenotypeSupplyOnly,
            "xeno-only facts land on the row");
        Assert(r.FirstXenoTarget == "archoteifer_high", "the first xeno target is recorded");
        UsRaceNavigationIntent intent = RaceBoardProjection.DeriveIntent(r);
        Assert(intent.DomainScope == "Xenotype" && intent.DomainTarget == "archoteifer_high",
            "the intent selects the REAL xeno domain, not an empty race domain");
        Assert(intent.ScrollDestination == "pack-card-row#us.a",
            "the destination is the FL-materialized row identity, got " + intent.ScrollDestination);
    }

    private static void EmptyFallbackTableIsNeverReady()
    {
        var facts = new[]
        {
            new UsRaceBoardFacts("pig", "Pig", Array.Empty<UsBoardPackFact>(), Array.Empty<UsRaceDomainFact>(),
                hasFallbackTable: true, fallbackIsPlayerTable: false, fallbackSoundCount: 0, identity: Ident("", "")),
        };
        RaceBoardRow r = BuildAll(facts, SqueakVoicePackMode.Fallback)[0];
        Assert(r.HasFinalFallback && r.FallbackSoundCount == 0,
            "table-with-zero-mapped-sounds survives as its OWN state (never 'ready')");
        UsRaceNavigationIntent intent = RaceBoardProjection.DeriveIntent(r);
        Assert(intent.ScrollDestination == "packs-fallback",
            "fallback-only navigates to the STATIC editor destination");
        Assert(intent.ExpandCardKeys.Count == 0, "nothing to expand without supply");
    }

    private static void SavedChoiceMissingPerDomain()
    {
        var facts = new[]
        {
            new UsRaceBoardFacts("human", "Human",
                packs: new[] { Pack(SqueakVoicePackScope.Race, "", "us.a", "A") },
                domains: new[]
                {
                    Domain(SqueakVoicePackScope.Race, "", new[] { "us.a", "us.gone" }),
                    Domain(SqueakVoicePackScope.Xenotype, "sang", new[] { "us.also-gone" }),
                }, identity: Ident("", "")),
        };
        RaceBoardRow r = BuildAll(facts, SqueakVoicePackMode.Fallback)[0];
        Assert(r.OrphanCount == 1 && r.EnabledCount == 1, "race-domain stale key counts as saved-missing, the live one enabled");
        Assert(r.XenoOrphanCount == 1 && r.XenoEnabledCount == 0, "xeno-domain stale key counts on ITS axis");
    }

    private static void ModeAxesAreIndependent()
    {
        var packs = new[] { Pack(SqueakVoicePackScope.Race, "", "us.a", "A") };
        var domains = new[] { Domain(SqueakVoicePackScope.Race, "", Array.Empty<string>()) };
        var facts = new[] { new UsRaceBoardFacts("human", "Human", packs, domains, identity: Ident("", "")) };

        Assert(!BuildAll(facts, SqueakVoicePackMode.Disabled)[0].ModeUsesPacks,
            "Disabled answers the US-USE axis off");
        Assert(!BuildAll(facts, SqueakVoicePackMode.Vanilla)[0].ModeUsesPacks,
            "Vanilla answers the US-USE axis off");
        Assert(BuildAll(facts, SqueakVoicePackMode.Fallback)[0].ModeUsesPacks
                && BuildAll(facts, SqueakVoicePackMode.Remix)[0].ModeUsesPacks,
            "Fallback and Remix answer it on");
        // The supply axis never flips with the mode (Disabled keeps every recorded selection):
        RaceBoardRow disabled = BuildAll(
            new[] { new UsRaceBoardFacts("human", "Human", packs,
                new[] { Domain(SqueakVoicePackScope.Race, "", new[] { "us.a" }) },
                hasFallbackTable: false, fallbackIsPlayerTable: false, fallbackSoundCount: 0, identity: Ident("", "")) },
            SqueakVoicePackMode.Disabled)[0];
        Assert(disabled.EnabledCount == 1 && !disabled.ModeUsesPacks,
            "bypass keeps the selection facts: axis 1 unchanged, axis 2 off");
    }

    private static void QuickEnableAmbiguity()
    {
        // Two not-yet-enabled packs -> no quick target (navigate only).
        var two = new[]
        {
            new UsRaceBoardFacts("human", "Human",
                new[]
                {
                    Pack(SqueakVoicePackScope.Race, "", "us.a", "A"),
                    Pack(SqueakVoicePackScope.Race, "", "us.b", "B"),
                },
                new[] { Domain(SqueakVoicePackScope.Race, "", Array.Empty<string>()) }, identity: Ident("", "")),
        };
        Assert(BuildAll(two, SqueakVoicePackMode.Fallback)[0].QuickEnablePackKey.Length == 0,
            "ambiguous supply offers no enable");

        // Exactly one -> enable names pack + target labels; already enabled -> none.
        var one = new[]
        {
            new UsRaceBoardFacts("pig", "Pig",
                new[] { Pack(SqueakVoicePackScope.Xenotype, "pig_highstealer", "us.a", "Pig Snort", "Highstealer") },
                new[] { Domain(SqueakVoicePackScope.Xenotype, "pig_highstealer", Array.Empty<string>()) }, identity: Ident("", "")),
        };
        RaceBoardRow quick = BuildAll(one, SqueakVoicePackMode.Fallback)[0];
        Assert(quick.QuickEnablePackKey == "us.a" && quick.QuickEnableScope == "Xenotype"
                && quick.QuickEnableTarget == "pig_highstealer"
                && quick.QuickEnablePackLabel == "Pig Snort" && quick.QuickEnableTargetLabel == "Highstealer",
            "the single candidate carries its full display identity for the button");

        var on = new[]
        {
            new UsRaceBoardFacts("pig", "Pig",
                new[] { Pack(SqueakVoicePackScope.Race, "", "us.a", "A") },
                new[] { Domain(SqueakVoicePackScope.Race, "", new[] { "us.a" }) }, identity: Ident("", "")),
        };
        Assert(BuildAll(on, SqueakVoicePackMode.Fallback)[0].QuickEnablePackKey.Length == 0,
            "already-enabled offers no second enable");

        // Biotech off: the only xeno candidate is dormant-by-environment -> no quick target.
        var xenoNoBio = new[]
        {
            new UsRaceBoardFacts("pig", "Pig",
                new[] { Pack(SqueakVoicePackScope.Xenotype, "pig_highstealer", "us.a", "A", "HS", biotech: false) },
                new[] { Domain(SqueakVoicePackScope.Xenotype, "pig_highstealer", Array.Empty<string>()) }, identity: Ident("", "")),
        };
        Assert(BuildAll(xenoNoBio, SqueakVoicePackMode.Fallback)[0].QuickEnablePackKey.Length == 0,
            "a dormant-by-environment xeno is never a quick-enable target");
    }

    private static void TargetUnloadIsNotSavedMissing()
    {
        var facts = new[]
        {
            new UsRaceBoardFacts("human", "Human",
                new[] { Pack(SqueakVoicePackScope.Xenotype, "gone_xeno", "us.a", "A", "Gone") },
                new[]
                {
                    new UsRaceDomainFact(SqueakVoicePackScope.Xenotype, "gone_xeno",
                        SqueakVoicePackDomainState.TargetUnavailable, Array.Empty<string>()),
                }, identity: Ident("", "")),
        };
        RaceBoardRow r = BuildAll(facts, SqueakVoicePackMode.Fallback)[0];
        Assert(r.XenoTargetUnavailable && r.XenoOrphanCount == 0,
            "target-not-loaded and saved-choice-missing stay distinct facts");
    }

    // ---- OVT-002 browse matrix (identity axes, groups, filters, search, ordering, xeno band) ----

    private static void BrowseBandRules()
    {
        var har = Fact("ratkin", "Ratkin", UsRaceSourceKind.ThirdParty, har: true, life: UsRaceLifeKind.Humanlike);
        var human = Fact("Human", "Human", UsRaceSourceKind.Core, life: UsRaceLifeKind.Humanlike);
        var alien = Fact("thirdfolk", "Third Folk", UsRaceSourceKind.ThirdParty, life: UsRaceLifeKind.Humanlike);
        var boar = Fact("Wild_boar", "wild boar", UsRaceSourceKind.Core, life: UsRaceLifeKind.Animal);
        var scout = Fact("Scaler", "scaler", UsRaceSourceKind.OfficialDlc, life: UsRaceLifeKind.Mechanoid);
        var entity = Fact("Ghoul", "Ghoul", UsRaceSourceKind.OfficialDlc, life: UsRaceLifeKind.AnomalyEntity);
        var facts = new[] { boar, alien, entity, human, scout, har };
        var bundle = new UsRaceBoardFactsBundle(facts, Array.Empty<UsXenotypeBoardFacts>());

        UsRaceBoardView player = RaceBoardProjection.Build(
            bundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.PlayerRaces);
        Assert(player.Rows.Count == 3, "Player races = HAR + vanilla humanlike + other humanlike, got " + player.Rows.Count);
        Assert(player.Rows[0].RaceDefName == "ratkin" && player.Rows[1].RaceDefName == "Human"
                && player.Rows[2].RaceDefName == "thirdfolk",
            "the default order is HAR first, vanilla humanlike next, other third-party last - never animal-first");
        Assert(player.TotalRaces == 6 && player.DisplayedRaces == 3,
            "the count line separates shown from the FULL directory (3/6)");

        UsRaceBoardView creatures = RaceBoardProjection.Build(
            bundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.Creatures);
        Assert(creatures.Rows.Count == 1, "WO r3 animal scope excludes mechanoids/entities");
        Assert(creatures.Rows[0].RaceDefName == "Wild_boar" && !UsBoardBrowsePolicy.InGroup(scout.Identity, UsBoardBrowsePolicy.BoardGroup.Creatures)
                && !UsBoardBrowsePolicy.InGroup(entity.Identity, UsBoardBrowsePolicy.BoardGroup.Creatures),
            "only the animal is in Animals; mechanical/entity facts remain distinct");

        UsRaceBoardView all = RaceBoardProjection.Build(
            bundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.All);
        Assert(all.Rows.Count == 6, "All shows the whole directory");

        UsRaceBoardView harOnly = RaceBoardProjection.Build(
            bundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.All,
            UsBoardBrowsePolicy.BoardFilter.Har);
        Assert(harOnly.Rows.Count == 1 && harOnly.Rows[0].Identity.IsHar,
            "the HAR chip narrows to framework facts only - the MECH-HAR case keeps BOTH axes when present");
        UsRaceBoardView vanillaRaces = RaceBoardProjection.Build(
            bundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.All,
            UsBoardBrowsePolicy.BoardFilter.VanillaRaces);
        Assert(vanillaRaces.Rows.Count == 1 && vanillaRaces.Rows[0].RaceDefName == "Human",
            "vanilla races = Core/DLC humanlike only");
        UsRaceBoardView vanillaCreatures = RaceBoardProjection.Build(
            bundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.All,
            UsBoardBrowsePolicy.BoardFilter.VanillaCreatures);
        Assert(vanillaCreatures.Rows.Count == 3,
            "vanilla creatures = boar (Core animal) + scaler (DLC mech) + ghoul (DLC entity) = 3; the HAR/ThirdParty rows never join");

        // Package ids differ per row (the Fact helper gives everyone "mod.pkg" - override the
        // two searched rows so NAME search and SOURCE search are proven separately).
        var harSearched = new UsRaceBoardFacts("ratkin", "Ratkin", Array.Empty<UsBoardPackFact>(),
            Array.Empty<UsRaceDomainFact>(),
            identity: new UsRaceIdentity("ratkin", "Ratkin", UsRaceSourceKind.ThirdParty, "coahuilite.ratkin", "Ratkin Race Pack", true, UsRaceLifeKind.Humanlike));
        var searchedFacts = new[] { boar, alien, entity, human, scout, harSearched };
        var searchedBundle = new UsRaceBoardFactsBundle(searchedFacts, Array.Empty<UsXenotypeBoardFacts>());
        UsRaceBoardView search = RaceBoardProjection.Build(
            searchedBundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.All,
            UsBoardBrowsePolicy.BoardFilter.None, "third");
        Assert(search.Rows.Count == 1 && search.Rows[0].RaceDefName == "thirdfolk",
            "name search hits the display name / def name ([third] -> Third Folk), got " + search.Rows.Count);
        UsRaceBoardView sourceSearch = RaceBoardProjection.Build(
            searchedBundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.All,
            UsBoardBrowsePolicy.BoardFilter.None, "coahuilite");
        Assert(sourceSearch.Rows.Count == 1 && sourceSearch.Rows[0].RaceDefName == "ratkin",
            "SOURCE search matches the package id - the mod author string finds the HAR race");

        // Same existing HAR-mech scenario, with the WO r3 life-kind admission rule.
        var mechHar = new UsRaceBoardFacts("skitter", "Skitter", Array.Empty<UsBoardPackFact>(),
            Array.Empty<UsRaceDomainFact>(),
            identity: new UsRaceIdentity("skitter", "Skitter", UsRaceSourceKind.ThirdParty,
                "coahuilite.mechs", "Mech HAR Pack", true, UsRaceLifeKind.Mechanoid));
        var mechBundle = new UsRaceBoardFactsBundle(new[] { boar, human, mechHar }, Array.Empty<UsXenotypeBoardFacts>());
        UsRaceBoardView playerWithMech = RaceBoardProjection.Build(
            mechBundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.PlayerRaces);
        Assert(playerWithMech.Rows.Count == 1 && playerWithMech.Rows[0].RaceDefName == "Human"
                && !UsBoardBrowsePolicy.InGroup(mechHar.Identity, UsBoardBrowsePolicy.BoardGroup.PlayerRaces),
            "HAR does not admit a non-humanlike race to the default scope");
        UsRaceBoardView creaturesWithMech = RaceBoardProjection.Build(
            mechBundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.Creatures);
        Assert(creaturesWithMech.Rows.Count == 1 && creaturesWithMech.Rows[0].RaceDefName == "Wild_boar",
            "the explicit Animals scope also respects life kind, not HAR tier");
        UsRaceBoardView harChipMech = RaceBoardProjection.Build(
            mechBundle, SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.All,
            UsBoardBrowsePolicy.BoardFilter.Har);
        Assert(harChipMech.Rows.Count == 1 && harChipMech.Rows[0].Identity.IsMechanoid,
            "the HAR chip answers framework identity across life kinds - the mech HAR survives it");

        // Same label, different stable identity: never merged, deterministic order.
        var twinA = Fact("zoo.a", "Twin", UsRaceSourceKind.Core, life: UsRaceLifeKind.Animal);
        var twinB = Fact("aqua.b", "Twin", UsRaceSourceKind.OfficialDlc, life: UsRaceLifeKind.Animal);
        UsRaceBoardView twins = RaceBoardProjection.Build(
            new UsRaceBoardFactsBundle(new[] { twinB, twinA }, Array.Empty<UsXenotypeBoardFacts>()),
            SqueakVoicePackMode.Fallback, UsBoardBrowsePolicy.BoardGroup.All);
        Assert(twins.Rows.Count == 2 && twins.Rows[0].RaceDefName == "aqua.b" && twins.Rows[1].RaceDefName == "zoo.a",
            "same-label different-ID rows stay separate and order by defName after the label");

        // Xenotype directory band: only present when expanded; counts always report the total.
        var xenoBundle = new UsRaceBoardFactsBundle(facts, new[]
        {
            new UsXenotypeBoardFacts(new UsXenotypeIdentity("x.b", "Bone", UsRaceSourceKind.OfficialDlc, "ludeon.biotech", "Biotech"), 0, 0, "", "", "", ""),
            new UsXenotypeBoardFacts(new UsXenotypeIdentity("x.a", "Ash", UsRaceSourceKind.Core, "ludeon.core", "Core"), 1, 0, "Human", "us.p", "Pack", "Human"),
        });
        UsRaceBoardView collapsed = RaceBoardProjection.Build(xenoBundle, SqueakVoicePackMode.Fallback);
        Assert(collapsed.Xenotypes.Count == 0 && collapsed.TotalXenotypes == 2,
            "collapsed band: zero rows but the total still reports 2 (no silent shrink)");
        UsRaceBoardView expanded = RaceBoardProjection.Build(xenoBundle, SqueakVoicePackMode.Fallback,
            xenotypeDirectoryExpanded: true);
        Assert(expanded.Xenotypes.Count == 2 && expanded.Xenotypes[0].DefName == "x.a"
                && expanded.Xenotypes[1].DefName == "x.b",
            "expanded band lists EVERY loaded xenotype (no-pack included) ordered by label");
        Assert(expanded.Xenotypes[0].QuickEnablePackKey == "us.p"
                && expanded.Xenotypes[1].QuickEnablePackKey.Length == 0,
            "the single-pack xenotype carries its exact quick-enable identity; the no-pack one navigates only");
    }

    private static UsRaceBoardFacts Fact(string def, string label, UsRaceSourceKind source,
        bool har = false, UsRaceLifeKind life = UsRaceLifeKind.Humanlike)
        => new UsRaceBoardFacts(def, label, Array.Empty<UsBoardPackFact>(), Array.Empty<UsRaceDomainFact>(),
            identity: new UsRaceIdentity(def, label, source, "mod.pkg", "Mod Package", har, life));

    // ---- fixture helpers (plain data only) ----

    private static UsBoardPackFact Pack(SqueakVoicePackScope scope, string target, string key, string label,
        string targetLabel = "", bool biotech = true)
        => new UsBoardPackFact(scope, target, key, label, targetLabel.Length > 0 ? targetLabel : target, biotech);

    /// <summary>OVT-001 cutover: identity is a REQUIRED plain fact. The matrix rows below carry the
    /// ordinary Unknown/Other collector answers unless the scenario names a real axis (Fact()).
    /// </summary>
    private static UsRaceIdentity Ident(string def, string label)
        => new UsRaceIdentity(def, label, UsRaceSourceKind.Unknown, "", "", false, UsRaceLifeKind.Other);

    private static UsRaceDomainFact Domain(SqueakVoicePackScope scope, string target, IReadOnlyList<string> enabled)
        => new UsRaceDomainFact(scope, target, SqueakVoicePackDomainState.Available, enabled);

    private static IReadOnlyList<RaceBoardRow> BuildAll(
        UsRaceBoardFacts[] facts, SqueakVoicePackMode mode)
        => RaceBoardProjection.Build(
            new UsRaceBoardFactsBundle(facts, Array.Empty<UsXenotypeBoardFacts>()), mode,
            UsBoardBrowsePolicy.BoardGroup.All).Rows;

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("RaceBoardProjection matrix: " + message);
    }
}
