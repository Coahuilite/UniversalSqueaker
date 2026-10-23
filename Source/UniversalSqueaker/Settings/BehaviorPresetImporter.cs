using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalSqueaker;

/// <summary>Explicit player-owned, incremental template import. Unmentioned fields and rows survive.</summary>
public static class BehaviorPresetImporter
{
    public sealed class Selection
    {
        public readonly HashSet<string> RaceDefNames = new(StringComparer.Ordinal);
        public readonly HashSet<string> XenotypeDomainKeys = new(StringComparer.Ordinal);
    }
    public static string XenotypeDomainKey(string race, string xeno) => race + "\n" + xeno;

    public static IReadOnlyDictionary<string, BehaviorValues> ValuesFor(UsBehaviorPresetDef preset, string race, string xeno)
    {
        var values = new Dictionary<string, BehaviorValues>(StringComparer.Ordinal);
        var parents = preset.races.Where(x => x.raceDefName == race).ToArray();
        if (parents.Length > 1) throw new ArgumentException("DuplicatePresetRace:" + race);
        var parent = parents.SingleOrDefault();
        if (parent == null) return values;
        var children = parent.xenotypes.Where(x => x.xenotypeDefName == xeno).ToArray();
        if (xeno != "" && children.Length > 1) throw new ArgumentException("DuplicatePresetChild:" + race + ":" + xeno);
        var child = xeno == "" ? null : children.SingleOrDefault();
        if (xeno != "" && child == null) return values;
        void Add(IEnumerable<PresetActionValues> rows)
        {
            foreach (var row in rows)
            {
                if (!Authoring.AuthorXml.ActionId(row.ActionId) || !row.Values.Valid || !row.Values.ApplicableTo("Pawn")) throw new ArgumentException("InvalidPlayerTemplate");
                if (!values.TryGetValue(row.ActionId, out var target)) values.Add(row.ActionId, target = new BehaviorValues());
                target.Overlay(row.Values);
            }
        }
        if (child == null || child.inheritFromRace) Add(parent.actions);
        if (child != null) Add(child.actions);
        return values;
    }

    public static PresetImportResult Import(UsBehaviorPresetDef preset, Selection selection, GameSettings settings)
    {
        int actions = 0, fields = 0;
        // Form the entire candidate first. Invalid template data leaves the live settings unchanged.
        var previous = settings.actionTuning;
        try
        {
            settings.actionTuning = previous.Select(ActionTuningRecord.Clone).ToList();
            foreach (var race in preset.races)
            {
                void Apply(string xeno)
                {
                    foreach (var pair in ValuesFor(preset, race.raceDefName, xeno))
                    {
                        var current = settings.ReadPlayerActionOverride(pair.Key, race.raceDefName, xeno);
                        current.Overlay(pair.Value);
                        settings.ReplacePlayerValues(pair.Key, race.raceDefName, xeno, current, preset.defName);
                        actions++; fields += pair.Value.PresentKeys().Count();
                    }
                }
                if (selection.RaceDefNames.Contains(race.raceDefName)) Apply("");
                foreach (var xeno in race.xenotypes)
                    if (selection.XenotypeDomainKeys.Contains(XenotypeDomainKey(race.raceDefName, xeno.xenotypeDefName))) Apply(xeno.xenotypeDefName);
            }
        }
        catch { settings.actionTuning = previous; throw; }
        return new PresetImportResult(actions, fields);
    }
}

public readonly struct PresetImportResult
{
    public readonly int ActionsImported, FieldsImported;
    public PresetImportResult(int actions, int fields) { ActionsImported = actions; FieldsImported = fields; }
}
