using System;
using System.Collections.Generic;
using System.Reflection;

using UniversalSqueaker;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// PRE1/Tuning (VF1定稿 A1/A2): the action interval/probability multipliers become VISIBLE and
/// EDITABLE. Until this slice the data and runtime were fully live (ActionTuningRecord flags, the
/// layered merge, the timing/probability consumption) while the UI projected and wrote nothing -
/// the maintainer's own comment in UniversalSqueakerSettings.cs named that fact. This lane drives
/// the REAL settings writer and the REAL model fold (private BuildActionScopes, reflection like
/// MoodLayoutFocusedTests uses for ProjectMoodTuningRows) - not the RecordingSettingsSource's
/// fixture view - so the write -> fold -> projection round trip is one production chain.
///
/// Honest labels (AGENTS evidence discipline): the clauses below are CHARACTERIZATION GUARDS unless a
/// named revert was actually executed and its red/green logged; the executed proof is recorded in the
/// checkpoint evidence file, not asserted here in advance:
///  - defaults 1/1 with source -1 (no layer supplies)                    [guard]
///  - a global write shows as own+effective at layer 0, source 0         [guard]
///  - race/global isolation: each identity folds only its suppliers      [guard]
///  - a race write overrides the global in the fold, per field          [guard]
///  - clearing one field leaves the other field's value live             [guard]
///  - an imported probability > 2 survives UNCLAMPED; NaN/Infinity       [guard]
///    intervals sanitize to 1
///  - stale duplicate identity rows: last-wins interval fold [PROOF: first-wins backout;
///    named red/green in checkpoint evidence]; dedup/anchor retention remain [guard]
///  - production mood reset target withheld when a resolved preset has no mood entry
///    [PROOF: Ready-gate backout, named red/green in checkpoint evidence]
/// </summary>
internal static class TuningMultiplierLaneTests
{
    private static readonly MethodInfo BuildActionScopes = typeof(VoicePacksPageModel)
        .GetMethod("BuildActionScopes", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo SetActionTuning = typeof(UniversalSqueakerSettings)
        .GetMethod("SetActionTuning", BindingFlags.NonPublic | BindingFlags.Instance)!;

    public static int RunAll()
    {
        // An unresolved anchor is a real absent-definition case. Supply that answer through the
        // production lookup seam for every step; the harness does not load the game database.
        UniversalSqueaker.Runtime.SqueakGameDefs.BaselineByName = _ => null;
        UniversalSqueaker.Runtime.SqueakGameDefs.AllBaselines = () => Array.Empty<UniversalSqueakerTuningBaselineDef>();
        try
        {
            Step("defaults: effective 1/1, no own, no source", Defaults);
            Step("global write projects own + effective + source 0", GlobalWrite);
            Step("race write overrides the global per field", RaceOverride);
            Step("single-field clear keeps the other field live", SingleFieldClear);
            Step("probability > 2 survives unclamped; NaN interval sanitizes", ValueDomain);
            Step("duplicate identities: last-wins fold, dedup write, anchor keeps the row", DuplicateAccount);
            Step("reset-to-preset refuses without row/anchor/entry and writes nothing", ResetGuard);
            Step("PRE1: real baseline import A then B feeds the production folds and reset", BaselineImportRoundTrip);

            Console.WriteLine("TuningMultiplierLaneTests ALL PASS");
            return 0;
        }
        finally
        {
            UniversalSqueaker.Runtime.SqueakGameDefs.ResetToGameDefaults();
        }
    }

    // babyEnabled=false: the fold takes the flag as a parameter precisely so this lane can run the
    // REAL fold in the stub harness (Verse.ModsConfig is not loadable here); the default fixture
    // writes no baby-gated rows, so the projections below are the production answer for a
    // non-biotech host too.
    private static IReadOnlyList<ActionScopeRowView> Fold(UniversalSqueakerSettings settings, int layer, string race, string xeno)
        => (IReadOnlyList<ActionScopeRowView>)BuildActionScopes.Invoke(null, new object[] { settings, layer, race, xeno, false })!;

    private static ActionScopeRowView Row(IReadOnlyList<ActionScopeRowView> rows, string key)
    {
        foreach (ActionScopeRowView row in rows)
        {
            if (row.ActionKey == key) return row;
        }

        throw new InvalidOperationException("the fold lost the built-in action '" + key + "'");
    }

    private static void Write(UniversalSqueakerSettings settings, string key, bool interval, float? value, string race = "", string xeno = "")
        => SetActionTuning.Invoke(settings, new object[] { key, race, xeno, interval, value! });

    private static void Defaults()
    {
        var settings = new UniversalSqueakerSettings();
        ActionScopeRowView eat = Row(Fold(settings, 0, "", ""), "Eat");
        Assert(!eat.HasOwnInterval && !eat.HasOwnProbability, "nothing was written, so nothing is own");
        Assert(Math.Abs(eat.EffectiveInterval - 1f) < 0.001f && Math.Abs(eat.EffectiveProbability - 1f) < 0.001f,
            "the effective multipliers start at 1/1, got " + eat.EffectiveInterval + "/" + eat.EffectiveProbability);
        Assert(eat.IntervalSourceLayer == -1 && eat.ProbabilitySourceLayer == -1,
            "no layer supplies, so the source answer is the DEFAULT (-1), got "
            + eat.IntervalSourceLayer + "/" + eat.ProbabilitySourceLayer);
    }

    private static void GlobalWrite()
    {
        var settings = new UniversalSqueakerSettings();
        Write(settings, "Eat", interval: true, 1.5f);
        ActionScopeRowView eat = Row(Fold(settings, 0, "", ""), "Eat");
        Assert(eat.HasOwnInterval && Math.Abs(eat.OwnInterval - 1.5f) < 0.001f,
            "the global layer owns what it wrote, got own=" + eat.OwnInterval + " own=" + eat.HasOwnInterval);
        Assert(Math.Abs(eat.EffectiveInterval - 1.5f) < 0.001f && eat.IntervalSourceLayer == 0,
            "the effective interval is the global write, source layer 0");
        Assert(!eat.HasOwnProbability && Math.Abs(eat.EffectiveProbability - 1f) < 0.001f,
            "the probability field was never touched and stays inherited-default");
    }

    private static void RaceOverride()
    {
        var settings = new UniversalSqueakerSettings();
        Write(settings, "Eat", interval: true, 1.5f);
        Write(settings, "Eat", interval: true, 2.5f, race: "Ratkin");
        Write(settings, "Eat", interval: false, 0.5f, race: "Ratkin");

        IReadOnlyList<ActionScopeRowView> atRace = Fold(settings, 1, "Ratkin", "");
        ActionScopeRowView eat = Row(atRace, "Eat");
        Assert(eat.HasOwnInterval && Math.Abs(eat.OwnInterval - 2.5f) < 0.001f,
            "the race layer owns its interval write");
        Assert(Math.Abs(eat.EffectiveInterval - 2.5f) < 0.001f && eat.IntervalSourceLayer == 1,
            "the race interval overrides the global one in the fold, source 1, got "
            + eat.EffectiveInterval + "/" + eat.IntervalSourceLayer);
        Assert(Math.Abs(eat.EffectiveProbability - 0.5f) < 0.001f && eat.ProbabilitySourceLayer == 1,
            "the race probability write rides the same fold independently");

        // The isolation claim stated with the fold's REAL identity: the global fold (layer 0, empty
        // race/xeno) is supplied ONLY by the global record, so it keeps its own 1.5 at source 0 - the
        // race write does not leak into the global identity. The race view above is where 2.5/source 1
        // lives; borrowing that answer here would pin nothing about isolation (PM instrument review).
        ActionScopeRowView globalView = Row(Fold(settings, 0, "", ""), "Eat");
        Assert(globalView.HasOwnInterval && Math.Abs(globalView.OwnInterval - 1.5f) < 0.001f
                && Math.Abs(globalView.EffectiveInterval - 1.5f) < 0.001f && globalView.IntervalSourceLayer == 0,
            "the global fold keeps its OWN 1.5 at source 0 - the race override is invisible to this"
            + " identity, got own=" + globalView.OwnInterval + " effective=" + globalView.EffectiveInterval
            + " source=" + globalView.IntervalSourceLayer);
    }

    private static void SingleFieldClear()
    {
        var settings = new UniversalSqueakerSettings();
        Write(settings, "Eat", interval: true, 1.5f);
        Write(settings, "Eat", interval: false, 0.8f);
        Write(settings, "Eat", interval: true, null);

        ActionScopeRowView eat = Row(Fold(settings, 0, "", ""), "Eat");
        Assert(!eat.HasOwnInterval && Math.Abs(eat.EffectiveInterval - 1f) < 0.001f && eat.IntervalSourceLayer == -1,
            "clearing the interval restored inheritance, got " + eat.EffectiveInterval + "/" + eat.IntervalSourceLayer);
        Assert(eat.HasOwnProbability && Math.Abs(eat.EffectiveProbability - 0.8f) < 0.001f,
            "the probability field survived the interval clear - the fields are independent accounts");
    }

    private static void ValueDomain()
    {
        var settings = new UniversalSqueakerSettings();
        Write(settings, "Eat", interval: false, 2.5f);
        ActionScopeRowView eat = Row(Fold(settings, 0, "", ""), "Eat");
        Assert(Math.Abs(eat.EffectiveProbability - 2.5f) < 0.001f,
            "an imported probability multiplier above 2 must NOT be silently clamped to 2 by the writer "
            + "(the runtime clamps the FINAL probability, not the multiplier), got " + eat.EffectiveProbability);

        Write(settings, "Move", interval: true, float.NaN);
        Assert(Math.Abs(Row(Fold(settings, 0, "", ""), "Move").EffectiveInterval - 1f) < 0.001f,
            "NaN intervals sanitize through the runtime's own rule to 1");
        Write(settings, "Move", interval: true, float.PositiveInfinity);
        Assert(Math.Abs(Row(Fold(settings, 0, "", ""), "Move").EffectiveInterval - 1f) < 0.001f,
            "Infinity intervals sanitize to 1 as well");
        Write(settings, "Move", interval: false, -3f);
        Assert(Math.Abs(Row(Fold(settings, 0, "", ""), "Move").EffectiveProbability - 0f) < 0.001f,
            "negative probabilities floor at 0, they do not wrap");
    }

    private static void DuplicateAccount()
    {
        var settings = new UniversalSqueakerSettings();
        Write(settings, "Eat", interval: true, 1.5f);
        Write(settings, "Eat", interval: false, 0.8f);

        // A stale pre-D2 row for the SAME identity carrying only the interval: the fold is the
        // union of the identity group, last-wins per field, so both values stay live.
        FieldInfo list = typeof(UniversalSqueakerSettings).GetField("actionTuning",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!;
        var records = (System.Collections.IList)list.GetValue(settings)!;
        object stale = Activator.CreateInstance(records.GetType().GenericTypeArguments[0])!;
        void Set(string name, object value) => stale.GetType().GetField(name)!.SetValue(stale, value);
        Set("actionKey", "Eat");
        Set("raceDefName", "");
        Set("xenotypeDefName", "");
        Set("hasIntervalMultiplier", true);
        Set("intervalMultiplier", 3.25f);
        Set("sourcePresetDefName", "SomePreset");
        records.Add(stale);

        ActionScopeRowView eat = Row(Fold(settings, 0, "", ""), "Eat");
        Assert(Math.Abs(eat.EffectiveInterval - 3.25f) < 0.001f,
            "the later duplicate row wins the interval field in the fold (the runtime merges the same way), got "
            + eat.EffectiveInterval);
        Assert(Math.Abs(eat.EffectiveProbability - 0.8f) < 0.001f,
            "the other row's probability stays live - the fold is per field, not per row");

        Write(settings, "Eat", interval: true, null);
        eat = Row(Fold(settings, 0, "", ""), "Eat");
        Assert(Math.Abs(eat.EffectiveInterval - 1f) < 0.001f && !eat.HasOwnInterval,
            "clearing the interval clears it across EVERY row of the identity group, not just the last one");
        Assert(eat.HasPresetAnchor,
            "the anchor row survives the clear (CarriesAnyActionTuningField counts the source) - it is "
            + "the reset-to-preset precondition");
        Assert(Math.Abs(eat.EffectiveProbability - 0.8f) < 0.001f,
            "and the probability field of the group survived untouched, as the D2 account requires");
    }

    private static void ResetGuard()
    {
        var settings = new UniversalSqueakerSettings();
        MethodInfo reset = typeof(UniversalSqueakerSettings).GetMethod("ResetActionTuningToPreset",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        bool noRow = (bool)reset.Invoke(settings, new object[] { "Eat", "", "", null! })!;
        Assert(!noRow, "no row at all: refused");

        Write(settings, "Eat", interval: true, 1.5f);
        bool noAnchor = (bool)reset.Invoke(settings, new object[] { "Eat", "", "", null! })!;
        Assert(!noAnchor, "a row without an anchor: refused");
        Assert(Math.Abs(Row(Fold(settings, 0, "", ""), "Eat").EffectiveInterval - 1.5f) < 0.001f,
            "and the refusal wrote nothing");
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("TuningMultiplierLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // PRE1 (PM instrument review): the baseline import must reach the PRODUCTION folds through the
    // production importer with REAL def instances - stub-built rows are not evidence for this
    // assembly path. The baseline lookup seam supplies real defs A and B; the importer writes; the
    // real action fold and the real mood fold (BuildMoodTuningRows, reflection like the mood lane)
    // answer with the imported values, the anchor names the LAST preset, and reset-to-preset
    // re-applies B's entry after a manual edit.
    private static void BaselineImportRoundTrip()
    {
        UniversalSqueakerTuningBaselineDef A = Preset("us.presetA", "Preset A", interval: 1.4f, probability: 0.6f, pitch: 1.2f, volume: 1f);
        UniversalSqueakerTuningBaselineDef B = Preset("us.presetB", "Preset B", interval: 1.8f, probability: 1f, pitch: 0.9f, volume: 1.1f);
        // The game's def database is not in the stub (gate-15's constructed-type rule); the product
        // seam SqueakGameDefs is the single door, so the lane registers A and B on the seam and the
        // REAL ResolveActionPreset/anchor/reset code paths run against them unchanged.
        var registry = new Dictionary<string, UniversalSqueakerTuningBaselineDef>(StringComparer.Ordinal)
        {
            [A.defName] = A,
            [B.defName] = B,
        };
        UniversalSqueaker.Runtime.SqueakGameDefs.BaselineByName = name =>
            registry.TryGetValue(name, out UniversalSqueakerTuningBaselineDef? def) ? def : null;
        UniversalSqueaker.Runtime.SqueakGameDefs.AllBaselines = () => new[] { A, B };
        try
        {
            var settings = new UniversalSqueakerSettings();
            var selection = new BaselinePresetImporter.Selection();
            selection.RaceDefNames.Add("Ratkin");
            BaselineImportResult first = BaselinePresetImporter.Import(A, selection, settings);
            Assert(first.ActionsImported >= 1 && first.MoodsImported >= 1,
                "import A must land at least one action and one mood record, got " + first.ActionsImported + "/" + first.MoodsImported);
            BaselineImportResult second = BaselinePresetImporter.Import(B, selection, settings);
            Assert(second.ActionsImported >= 1,
                "import B must land its action records, got " + second.ActionsImported);

            ActionScopeRowView eat = Row(Fold(settings, 1, "Ratkin", ""), "Eat");
            Assert(Math.Abs(eat.EffectiveInterval - 1.8f) < 0.001f && eat.HasOwnInterval,
                "the race fold must show B's interval after the A-then-B import, got " + eat.EffectiveInterval);
            Assert(eat.HasPresetAnchor && eat.PresetResetReady
                    && eat.ResetPresetTarget == "Preset B",
                "the anchor names the LAST imported preset and the reset is Ready, got anchor="
                + eat.HasPresetAnchor + " ready=" + eat.PresetResetReady + " target='" + eat.ResetPresetTarget + "'");

            IReadOnlyList<MoodTuningRowView> moods = MoodFold(settings, 1, "Ratkin", "");
            MoodTuningRowView good = First(moods);
            Assert(Math.Abs(good.EffectivePitch - 0.9f) < 0.001f && Math.Abs(good.EffectiveVolume - 1.1f) < 0.001f,
                "the mood fold must show B's imported factors at the race layer, got "
                + good.EffectivePitch + "/" + good.EffectiveVolume);
            Assert(good.PitchSourceLayer == 1 && good.VolumeSourceLayer == 1,
                "the imported mood factors must report the RACE layer as their supplier, got "
                + good.PitchSourceLayer + "/" + good.VolumeSourceLayer);
            Assert(good.PresetReset == SqueakMoodResetPresetState.Ready && good.ResetPresetTarget == "Preset B",
                "the real mood fold exposes B as a Ready reset target after import");
            registry.Remove(B.defName);
            MoodTuningRowView unavailable = First(MoodFold(settings, 1, "Ratkin", ""));
            Assert(unavailable.PresetReset != SqueakMoodResetPresetState.Ready && unavailable.ResetPresetTarget.Length == 0,
                "an unresolved mood anchor never projects a usable reset target");
            Assert(unavailable.PitchSourceLayer == 1 && unavailable.VolumeSourceLayer == 1,
                "losing the reset definition does not change the suppliers of the stored factors");
            registry[B.defName] = B;
            var importedMoods = B.races[0].moods;
            B.races[0].moods = new List<BaselineMoodTuning>();
            MoodTuningRowView missingEntry = First(MoodFold(settings, 1, "Ratkin", ""));
            Assert(missingEntry.PresetReset != SqueakMoodResetPresetState.Ready && missingEntry.ResetPresetTarget.Length == 0,
                "a resolved preset without this mood entry never projects a usable reset target");
            B.races[0].moods = importedMoods;

            // Manual edit over the import, then the production reset re-applies B's entry.
            Write(settings, "Eat", interval: true, 2.2f, race: "Ratkin");
            Assert(Math.Abs(Row(Fold(settings, 1, "Ratkin", ""), "Eat").EffectiveInterval - 2.2f) < 0.001f,
                "the manual edit must win the fold before any reset");
            MethodInfo reset = typeof(UniversalSqueakerSettings)
                .GetMethod("ResetActionTuningToPreset", BindingFlags.NonPublic | BindingFlags.Instance)!;
            bool ok = (bool)reset.Invoke(settings, new object[] { "Eat", "Ratkin", "", B })!;
            Assert(ok, "reset-to-preset must accept a resolved anchor with an entry for this action/identity");
            Assert(Math.Abs(Row(Fold(settings, 1, "Ratkin", ""), "Eat").EffectiveInterval - 1.8f) < 0.001f,
                "the reset must re-apply B's baseline interval, not A's and not the manual 2.2");
        }
        finally
        {
            UniversalSqueaker.Runtime.SqueakGameDefs.ResetToGameDefaults();
        }
    }

    private static UniversalSqueakerTuningBaselineDef Preset(
        string defName, string label, float interval, float probability, float pitch, float volume)
        => new()
        {
            defName = defName,
            presetLabel = label,
            races = new List<BaselineRaceEntry>
            {
                new()
                {
                    raceDefName = "Ratkin",
                    actions = new List<BaselineActionTuning>
                    {
                        new() { actionKey = "Eat", intervalMultiplier = interval, probabilityMultiplier = probability },
                    },
                    moods = new List<BaselineMoodTuning>
                    {
                        new() { mood = SqueakMood.Good, pitchFactor = pitch, volumeFactor = volume },
                    },
                },
            },
        };

    private static readonly MethodInfo BuildMoodTuningRows = typeof(VoicePacksPageModel)
        .GetMethod("BuildMoodTuningRows", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static IReadOnlyList<MoodTuningRowView> MoodFold(UniversalSqueakerSettings settings, int layer, string race, string xeno)
        => (IReadOnlyList<MoodTuningRowView>)BuildMoodTuningRows.Invoke(null, new object[] { settings, layer, race, xeno })!;

    private static MoodTuningRowView First(IReadOnlyList<MoodTuningRowView> rows)
    {
        foreach (MoodTuningRowView row in rows)
        {
            if (row.Mood == SqueakMood.Good) return row;
        }

        throw new InvalidOperationException("the mood fold must carry the Good row the import wrote");
    }
}
