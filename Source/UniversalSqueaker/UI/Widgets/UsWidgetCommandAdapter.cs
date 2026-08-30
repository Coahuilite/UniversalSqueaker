using System;

namespace UniversalSqueaker.UI;

/// <summary>
/// Converts the existing US component command stream (business <see cref="UiCommand"/>) into the
/// neutral Ferrite command stream while reusing the current stateless US components unchanged.
/// </summary>
internal static class UsWidgetCommandAdapter
{
    public static Action<UiCommand> For(Action<FerriteLib.UiKit.UiCommand> emit)
    {
        return business =>
        {
            string name = business.Kind switch
            {
                UiCommandKind.SetMode => "SetMode",
                UiCommandKind.SelectDomain => "SelectDomain",
                UiCommandKind.TogglePack => "TogglePack",
                UiCommandKind.ForgetUnavailable => "ForgetUnavailable",
                UiCommandKind.ToggleEgg => "ToggleEgg",
                UiCommandKind.SetDistancePreset => "SetDistancePreset",
                UiCommandKind.SetGlobalVolume => "SetGlobalVolume",
                UiCommandKind.SetDistanceRange => "SetDistanceRange",
                UiCommandKind.ToggleBasic => "ToggleBasic",
                UiCommandKind.SetActionTuningScope => "SetActionTuningScope",
                UiCommandKind.SetTuningLayer => "SetTuningLayer",
                UiCommandKind.SetTuningDomain => "SetTuningDomain",
                UiCommandKind.SetMoodTuning => "SetMoodTuning",
                UiCommandKind.ToggleBaselinePreset => "ToggleBaselinePreset",
                UiCommandKind.ToggleBaselineRace => "ToggleBaselineRace",
                UiCommandKind.ToggleBaselineXenotype => "ToggleBaselineXenotype",
                UiCommandKind.ImportBaselinePreset => "ImportBaselinePreset",
                UiCommandKind.SetActiveTab => "SetActiveTab",
                UiCommandKind.ScrollToSection => "ScrollToSection",
                UiCommandKind.SetDomainFilter => "SetDomainFilter",
                UiCommandKind.SetPackFilter => "SetPackFilter",
                UiCommandKind.SetRaceFilter => "SetRaceFilter",
                UiCommandKind.SetXenotypeFilter => "SetXenotypeFilter",
                _ => ""
            };

            if (name.Length == 0) return;

            var payload = new UsCommandPayload
            {
                Kind = business.Kind,
                Mode = business.Mode,
                Scope = business.Scope,
                RaceDefName = business.RaceDefName,
                TargetDefName = business.TargetDefName,
                Arg = business.Arg,
                Flag = business.Flag
            };

            emit(new FerriteLib.UiKit.UiCommand(name, payload));
        };
    }
}
