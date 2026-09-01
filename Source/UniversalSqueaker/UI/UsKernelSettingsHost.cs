using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production settings Host adapter. It reads the real embedded Schema=2 page resource, registers
/// every US kernel widget kind, builds the complete typed binding/action table against the
/// <see cref="IUsKernelSettingsSource"/> business boundary, and returns a validated
/// <see cref="UiHost"/>. Any schema, kind, attribute, missing-binding or type error fails here at
/// creation time; the Settings Window treats that as a whole-page fallback.
///
/// Id/Bind/OptionsBind/ActionBind semantics are unified:
///  - Id = element identity (unique; also the scroll/section key);
///  - Bind = typed value-binding key, falling back to Id when absent;
///  - OptionsBind = typed options key;
///  - ActionBind = typed action key.
/// </summary>
public static class UsKernelSettingsHost
{
    private const string Source = "coahuilite.universalsqueaker";
    private const string ManifestResourceName = "UniversalSqueaker.UI.Layout.Schema2.xml";

    public static UiHost Create(IUsKernelSettingsSource source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        UsKernelWidgetRegistrar.EnsureRegistered();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(ReadManifest());
        var bumper = new SessionRevisionBumper();
        UiBindings bindings = BuildBindings(source, bumper);
        UiHost host = new(
            Source,
            manifest,
            bindings,
            UiTheme.DarkGold,
            VerseFerriteTextMetrics.Instance,
            new UsKernelTranslation());
        bumper.Attach(host.Session);
        return host;
    }

    /// <summary>
    /// Delayed session revision bump for the Host binding boundary. Bindings are built before the
    /// Host (and its session) exists, so the bumper starts detached and attaches once the Host is
    /// created; invocation only ever happens during Draw, after attachment.
    /// </summary>
    private sealed class SessionRevisionBumper
    {
        private UiSession? session;

        public void Attach(UiSession value)
        {
            session = value;
        }

        public void Bump()
        {
            session?.ClosePopup();
            session?.BumpContentRevision();
        }

        /// <summary>
        /// Resets the session-owned scroll positions for the two page scroll containers. Used on
        /// workspace switches so the centre content and right help start at their top; other
        /// layout-affecting writes only bump the revision and keep the user's scroll place.
        /// </summary>
        public void ResetScroll()
        {
            if (session == null) return;
            session.SetScrollPosition(ContentScrollId, Vector2.zero);
            session.SetScrollPosition(HelpScrollId, Vector2.zero);
        }

        public void SetScrollTarget(string elementId)
        {
            session?.SetScrollTarget(elementId);
        }
    }

    private const string ContentScrollId = "content-scroll";
    private const string HelpScrollId = "help-scroll";

    private static UiBindings BuildBindings(IUsKernelSettingsSource source, SessionRevisionBumper bumper)
    {
        Action bump = bumper.Bump;
        VoicePacksPageState state = source.ViewState;
        var bindings = new UiBindings();

        // Navigation / page chrome.
        bindings.BindValue<string>("active-tab", () => state.ActiveTab, source.SetActiveTab);
        bindings.BindReadOnly<string>("active-section", () => state.ActiveSectionKey);
        // Tab switches and scroll-to change which sections are visible (and the active section),
        // so they bump the session content revision through the Host boundary.
        bindings.BindAction<string>("set-tab", tab =>
        {
            source.SetActiveTab(tab);
            bumper.ResetScroll();
            bump();
        });
        bindings.BindAction<string>("scroll-to", sectionKey =>
        {
            source.ScrollToSection(sectionKey);
            bumper.SetScrollTarget(sectionKey);
            bump();
        });
        bindings.BindReadOnly<string>("banner-text", () => source.BuildView().BannerText);
        bindings.BindReadOnly<string>("build-identity", () => source.BuildIdentity);
        bindings.BindReadOnly<string>("save-status", () => source.SaveStatus);
        bindings.BindReadOnly<bool>("is-dirty", () => source.IsDirty);

        // Basic: mode.
        bindings.BindValue<SqueakVoicePackMode>(
            "mode",
            () => source.BuildView().Mode,
            source.SetMode);

        // Basic: global volume.
        bindings.BindValue<float>("global-volume", () => source.BuildView().GlobalVolumeFactor, source.SetGlobalVolume);

        // Basic: distance preset/range + attenuation chart.
        bindings.BindReadOnly<float>("distance-range-min", () => source.BuildView().DistanceRangeMin);
        bindings.BindReadOnly<float>("distance-range-max", () => source.BuildView().DistanceRangeMax);
        bindings.BindReadOnly<string>("distance-preset", () => source.BuildView().DistancePreset.ToString());
        bindings.BindAction<SqueakDistancePreset>("set-distance-preset", source.SetDistancePreset);
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("attenuation-points", () => BuildAttenuationPoints(source.BuildView()));
        bindings.BindAction<UiChartPointChange>("attenuation-point", change => ApplyAttenuationPoint(source, source.BuildView(), change));

        // Basic: toggles.
        bindings.BindValue<bool>("allow-eggs", () => source.BuildView().AllowEasterEggs, source.SetEasterEggs);
        bindings.BindValue<bool>("scale-cooldown", () => source.BuildView().ScaleCooldownWithTimeSpeed, value => source.SetBasicToggle(SqueakBasicToggle.ScaleCooldown, value));
        bindings.BindValue<bool>("scale-talking", () => source.BuildView().ScaleFrequencyWithTalking, value => source.SetBasicToggle(SqueakBasicToggle.ScaleTalking, value));
        bindings.BindValue<bool>("scale-population", () => source.BuildView().ScalePeriodicWithAudiblePopulation, value => source.SetBasicToggle(SqueakBasicToggle.ScalePopulation, value));
        bindings.BindAction<bool>("toggle-egg", source.SetEasterEggs);
        bindings.BindAction<bool>("toggle-scale-cooldown", value => source.SetBasicToggle(SqueakBasicToggle.ScaleCooldown, value));
        bindings.BindAction<bool>("toggle-scale-talking", value => source.SetBasicToggle(SqueakBasicToggle.ScaleTalking, value));
        bindings.BindAction<bool>("toggle-scale-population", value => source.SetBasicToggle(SqueakBasicToggle.ScalePopulation, value));
        bindings.BindValue<bool>("camera-indicator", () => source.BuildView().ShowCameraIndicator, source.SetCameraIndicator);
        bindings.BindAction<bool>("toggle-camera-indicator", source.SetCameraIndicator);

        // Tuning: layer/domain/scope/mood/baseline.
        bindings.BindReadOnly<int>("tuning-layer", () => state.TuningLayer);
        // Layer and domain switches swap the tuning editor's content, so they bump the revision.
        bindings.BindAction<int>("set-tuning-layer", layer => { source.SetTuningLayer(layer); bump(); });
        bindings.BindReadOnly<string>("tuning-race", () => state.TuningRaceDefName);
        bindings.BindReadOnly<string>("tuning-xeno", () => state.TuningXenotypeDefName);
        bindings.BindReadOnly<IReadOnlyList<TuningDomainOptionView>>("tuning-domains", () => source.BuildView().TuningDomains);
        bindings.BindAction<UsTuningDomainSelection>(
            "set-tuning-domain",
            selection => { source.SetTuningDomain(selection.RaceDefName, selection.TargetDefName); bump(); });
        bindings.BindReadOnly<IReadOnlyList<ActionScopeRowView>>("action-scopes", () => source.BuildView().ActionScopes);
        bindings.BindAction<UsScopeWrite>("set-action-scope", write => source.SetActionScope(write.ActionKey, write.Scope));
        bindings.BindReadOnly<IReadOnlyList<MoodTuningRowView>>("mood-rows", () => source.BuildView().MoodTuningRows);
        bindings.BindAction<UsMoodWrite>("set-mood-tuning", write => source.SetMoodTuning(write.Mood, write.Factor, write.Value));
        bindings.BindReadOnly<IReadOnlyList<BaselinePresetView>>("baseline-presets", () => source.BuildView().BaselinePresets);
        // Preset expand/collapse, per-row selection and import all reflow the preset tree.
        bindings.BindAction<string>("toggle-baseline-preset", preset => { source.ToggleBaselinePreset(preset); bump(); });
        bindings.BindAction<UsBaselineRaceToggle>(
            "toggle-baseline-race",
            toggle => { source.ToggleBaselineRace(toggle.PresetDefName, toggle.RaceDefName, toggle.Selected); bump(); });
        bindings.BindAction<UsBaselineXenoToggle>(
            "toggle-baseline-xenotype",
            toggle => { source.ToggleBaselineXenotype(toggle.PresetDefName, toggle.RaceDefName, toggle.XenotypeDefName, toggle.Selected); bump(); });
        bindings.BindAction<string>("import-baseline", preset => { source.ImportBaselinePreset(preset); bump(); });

        // Packs: filters, selection, checklist.
        bindings.BindReadOnly<IReadOnlyList<RaceLayerRowView>>("races", () => source.BuildView().Races);
        bindings.BindReadOnly<IReadOnlyList<VoicePackDomainView>>("xenotype-domains", () => source.BuildView().XenotypeDomains);
        bindings.BindReadOnly<VoicePackDomainView?>("selected-domain", () => source.BuildView().SelectedDomain);
        bindings.BindAction<UsDomainSelection>(
            "select-domain",
            selection => { source.SelectDomain(selection.Scope, selection.RaceDefName, selection.TargetDefName); bump(); });
        bindings.BindAction<UsPackToggle>(
            "toggle-pack",
            toggle => { source.ToggleVoicePack(toggle.Scope, toggle.RaceDefName, toggle.TargetDefName, toggle.PackKey, toggle.Enabled); bump(); });
        // Forget Unavailable changes the domain's pack list, so it reflows the checklist.
        bindings.BindAction<UsDomainIdentity>(
            "forget-unavailable",
            identity => { source.ForgetUnavailable(identity.Scope, identity.RaceDefName, identity.TargetDefName); bump(); });
        // Filter/search value writes change which rows are visible, so they bump the revision.
        bindings.BindValue<string>("race-filter", () => state.RaceFilter, value => { source.SetRaceFilter(value); bump(); });
        bindings.BindValue<string>("xenotype-filter", () => state.XenotypeFilter, value => { source.SetXenotypeFilter(value); bump(); });
        bindings.BindValue<string>("pack-filter", () => state.PackFilter.Author ?? "", value => { source.SetPackFilter(value); bump(); });
        bindings.BindAction<string>("set-pack-filter", value => { source.SetPackFilter(value); bump(); });
        bindings.BindAction<string>(
            "clear-pack-filters",
            _ =>
            {
                source.SetDomainFilter(SqueakDomainFilterKind.EnabledOnly, false);
                source.SetDomainFilter(SqueakDomainFilterKind.ConflictOnly, false);
                source.SetDomainFilter(SqueakDomainFilterKind.OrphanOnly, false);
                source.SetRaceFilter("");
                source.SetXenotypeFilter("");
                source.SetPackFilter("");
                source.SetSearchText("");
                bump();
            });
        bindings.BindOptions<string>("race-filter-options", () => source.BuildView().RaceFilterOptions.Select(option => option.Value).ToList());
        bindings.BindOptions<string>("xenotype-filter-options", () => source.BuildView().XenotypeFilterOptions.Select(option => option.Value).ToList());
        bindings.BindOptions<string>("author-options", () => source.BuildView().Authors);
        bindings.BindValue<string>("search-text", () => state.SearchText, value => { source.SetSearchText(value); bump(); });
        bindings.BindReadOnly<UiDomainFilter>("domain-filter", () => state.DomainFilter);
        bindings.BindAction<UsDomainFilterWrite>("set-domain-filter", write => { source.SetDomainFilter(write.Kind, write.Flag); bump(); });

        // Help panel.
        bindings.BindReadOnly<string>("help-section-key", () => source.SectionHelpKey(state.ActiveSectionKey));
        bindings.BindReadOnly<string>("help-hover", () => state.HelpHoverKey);
        bindings.BindReadOnly<string>("help-selection", () => state.HelpSelectionKey);
        bindings.BindAction<string>("set-help-hover", source.SetHelpHover);
        // Help selection switches the displayed help text, which changes the help panel height.
        bindings.BindAction<string>("set-help-selection", key => { source.SetHelpSelection(key); bump(); });

        return bindings;
    }

    /// <summary>Four normalized attenuation points: start locked at 100%, end locked at 0%.</summary>
    private static List<Vector2> BuildAttenuationPoints(VoicePacksViewState view)
    {
        float minNorm = InverseLerp(AttenuationMath.MinDistance, AttenuationMath.MaxDistance, view.DistanceRangeMin);
        float maxNorm = InverseLerp(AttenuationMath.MinDistance, AttenuationMath.MaxDistance, view.DistanceRangeMax);
        return new List<Vector2>
        {
            new Vector2(0f, 1f),
            new Vector2(Clamp01(minNorm), 1f),
            new Vector2(Clamp01(maxNorm), 0f),
            new Vector2(1f, 0f)
        };
    }

    private static void ApplyAttenuationPoint(IUsKernelSettingsSource source, VoicePacksViewState view, UiChartPointChange change)
    {
        float min = view.DistanceRangeMin;
        float max = view.DistanceRangeMax;
        float distance = Mathf.Lerp(AttenuationMath.MinDistance, AttenuationMath.MaxDistance, Mathf.Clamp01(change.X));
        if (change.Index == 1)
        {
            min = Mathf.Clamp(distance, AttenuationMath.MinDistance, max - AttenuationMath.MinRange);
        }
        else if (change.Index == 2)
        {
            max = Mathf.Clamp(distance, min + AttenuationMath.MinRange, AttenuationMath.MaxDistance);
        }
        else
        {
            return;
        }

        source.SetDistanceRange(min, max);
    }

    private static float InverseLerp(float a, float b, float value)
    {
        if (a == b) return 0f;
        return (value - a) / (b - a);
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    private static string ReadManifest()
    {
        using Stream? stream = typeof(UsKernelSettingsHost).Assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded Schema=2 layout resource '{ManifestResourceName}' was not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

}
