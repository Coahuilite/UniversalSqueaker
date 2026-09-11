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
    private const int HelpHoverGracePasses = 15;
    private const string Source = "coahuilite.universalsqueaker";
    private const string ManifestResourceName = "UniversalSqueaker.UI.Layout.Schema2.xml";

    /// <summary>Production entry point: the Host measures text with the real Verse text engine.</summary>
    public static UiHost Create(IUsKernelSettingsSource source)
    {
        UiNative.Trace = SqueakLog.PopupTrace;
        return Create(source, VerseFerriteTextMetrics.Instance);
    }

    /// <summary>
    /// Host creation with an explicit text-metrics model. A harness must pass the very same instance it
    /// attaches to <see cref="UiFitAudit"/>: layout sized by one model and checked against another makes
    /// every band that fell back to its constant look like an overflow, and hides the ones that genuinely
    /// are. Outside the game the Verse engine returns no usable heights at all, so the injected model is
    /// the only way this seam can measure anything.
    /// </summary>
    public static UiHost Create(IUsKernelSettingsSource source, ITextMetrics metrics)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (metrics == null) throw new ArgumentNullException(nameof(metrics));

        UsKernelWidgetRegistrar.EnsureRegistered();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(ReadManifest());
        var bumper = new SessionRevisionBumper();
        UiBindings bindings = BuildBindings(source, bumper);
        UiHost host = new(
            Source,
            manifest,
            bindings,
            UiTheme.DarkGold,
            metrics,
            new UsKernelTranslation());
        bumper.Attach(host.Session);
        // D10 (maintainer ruling 2026-09-06): a finished hover claim keeps explaining the panel for
        // this many IMGUI passes, which is what stops the overview from flashing while the pointer
        // crosses the gap between two adjacent controls. The library owns the rule and ships no
        // default on purpose - the consumer that needed the grace picks its length. Counted in passes,
        // not seconds: the settings window opens with forcePause, where game time is frozen.
        host.Session.HoverGraceFrames = HelpHoverGracePasses;
        // The view cache must expire on the same clock as the layout cache, or a write landing in a
        // frame's popup pass arranges against the previous view while the next frame draws a fresh
        // view into the stale snapshot - the 2026-09-04 filter misalignment.
        if (source is UsKernelSettingsSource production)
        {
            production.AttachRevisionSource(() => host.Session.ContentRevision);
        }

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

            // FL 0.4.0 keys scroll positions by node identity, not by the string a page declares:
            // SetScrollPosition takes a UiNode and ScrollPositions is keyed by node (the string
            // overload is gone with the batch-C node work, bd4d1b5). GetNodeByElementId is the
            // carrier's own bridge from the one string this page owns to that identity.
            //
            // A null lookup means the element has not been arranged in this session yet, so there is
            // no scroll state to reset. Skipping is equivalent to the old write, not a silent
            // behaviour change: UiSession.GetScrollPosition answers Vector2.zero for a node that
            // holds nothing, so "no entry" and "entry = zero" are indistinguishable to every reader,
            // and these two calls never wrote anything but zero.
            UiNode? contentNode = session.GetNodeByElementId(ContentScrollId);
            if (contentNode != null)
            {
                session.SetScrollPosition(contentNode, Vector2.zero);
            }

            UiNode? helpNode = session.GetNodeByElementId(HelpScrollId);
            if (helpNode != null)
            {
                session.SetScrollPosition(helpNode, Vector2.zero);
            }
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
        bindings.BindValue<string>(UiBindings.ActiveTabKey, () => state.ActiveTab, source.SetActiveTab);
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
        // Display-write contract: every write whose value flows back to the screen through
        // BuildView must advance the session clock (bump), or the revision-gated view cache keeps
        // serving the pre-write projection until some other bumping write lands - the D1/D6 defect
        // (clicks invisible until a workspace switch). Display-write bindings are asserted by the
        // DisplayWriteAdvancesRevision contract lane in the kernel-host harness.
        bindings.BindValue<SqueakVoicePackMode>(
            "mode",
            () => source.BuildView().Mode,
            value => { source.SetMode(value); bump(); });

        // Basic: global volume.
        bindings.BindValue<float>("global-volume", () => source.BuildView().GlobalVolumeFactor, value => { source.SetGlobalVolume(value); bump(); });

        // Basic: distance preset/range + attenuation chart.
        bindings.BindReadOnly<float>("distance-range-min", () => source.BuildView().DistanceRangeMin);
        bindings.BindReadOnly<float>("distance-range-max", () => source.BuildView().DistanceRangeMax);
        bindings.BindReadOnly<string>("distance-preset", () => source.BuildView().DistancePreset.ToString());
        bindings.BindAction<SqueakDistancePreset>("set-distance-preset", preset => { source.SetDistancePreset(preset); bump(); });
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("attenuation-points", () => BuildAttenuationPoints(source.BuildView()));
        bindings.BindAction<UiChartPointChange>("attenuation-point", change => { ApplyAttenuationPoint(source, source.BuildView(), change); bump(); });

        // Basic: toggles.
        bindings.BindValue<bool>("allow-eggs", () => source.BuildView().AllowEasterEggs, value => { source.SetEasterEggs(value); bump(); });
        bindings.BindValue<bool>("scale-cooldown", () => source.BuildView().ScaleCooldownWithTimeSpeed, value => { source.SetBasicToggle(SqueakBasicToggle.ScaleCooldown, value); bump(); });
        bindings.BindValue<bool>("scale-talking", () => source.BuildView().ScaleFrequencyWithTalking, value => { source.SetBasicToggle(SqueakBasicToggle.ScaleTalking, value); bump(); });
        bindings.BindValue<bool>("scale-population", () => source.BuildView().ScalePeriodicWithAudiblePopulation, value => { source.SetBasicToggle(SqueakBasicToggle.ScalePopulation, value); bump(); });
        bindings.BindAction<bool>("toggle-egg", value => { source.SetEasterEggs(value); bump(); });
        bindings.BindAction<bool>("toggle-scale-cooldown", value => { source.SetBasicToggle(SqueakBasicToggle.ScaleCooldown, value); bump(); });
        bindings.BindAction<bool>("toggle-scale-talking", value => { source.SetBasicToggle(SqueakBasicToggle.ScaleTalking, value); bump(); });
        bindings.BindAction<bool>("toggle-scale-population", value => { source.SetBasicToggle(SqueakBasicToggle.ScalePopulation, value); bump(); });
        bindings.BindValue<bool>("camera-indicator", () => source.BuildView().ShowCameraIndicator, value => { source.SetCameraIndicator(value); bump(); });
        bindings.BindAction<bool>("toggle-camera-indicator", value => { source.SetCameraIndicator(value); bump(); });

        // Timing: global interval floor + cooldown multiplier (cheap runtime statics, display writes).
        bindings.BindValue<int>("min-interval", () => source.BuildView().GlobalMinIntervalTicks, value => { source.SetGlobalMinIntervalTicks(value); bump(); });
        bindings.BindValue<float>("cooldown-multiplier", () => source.BuildView().GlobalCooldownMultiplier, value => { source.SetGlobalCooldownMultiplier(value); bump(); });

        // Diagnostics: dev logging level + vanilla debug-menu localization.
        bindings.BindValue<SqueakDevLoggingMode>("dev-logging", () => source.BuildView().DevLoggingMode, value => { source.SetDevLoggingMode(value); bump(); });
        bindings.BindValue<bool>("localize-debug-menu", () => source.BuildView().LocalizeDebugActions, value => { source.SetLocalizeDebugActions(value); bump(); });

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
        bindings.BindAction<UsScopeWrite>("set-action-scope", write => { source.SetActionScope(write.ActionKey, write.Scope); bump(); });
        bindings.BindReadOnly<IReadOnlyList<MoodTuningRowView>>("mood-rows", () => source.BuildView().MoodTuningRows);
        bindings.BindAction<UsMoodWrite>("set-mood-tuning", write => { source.SetMoodTuning(write.Mood, write.Factor, write.Value); bump(); });
        bindings.BindAction<UsMoodPresetReset>("reset-mood-to-preset", write => { source.ResetMoodToPreset(write.Mood); bump(); });
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
        // The filter dropdowns display translated labels but write machine tokens: the options
        // binding carries the (display, value) pair through, so a Chinese client never shows a raw
        // defName in the trigger or the list. Authors are proper nouns: display == value.
        bindings.BindOptions<FilterOptionView>("race-filter-options", () => source.BuildView().RaceFilterOptions);
        bindings.BindOptions<FilterOptionView>("xenotype-filter-options", () => source.BuildView().XenotypeFilterOptions);
        bindings.BindOptions<FilterOptionView>("author-options", () => source.BuildView().Authors
            .Select(author => new FilterOptionView(author, author))
            .ToList());
        bindings.BindValue<string>("search-text", () => state.SearchText, value => { source.SetSearchText(value); bump(); });
        bindings.BindReadOnly<UiDomainFilter>("domain-filter", () => state.DomainFilter);
        bindings.BindAction<UsDomainFilterWrite>("set-domain-filter", write => { source.SetDomainFilter(write.Kind, write.Flag); bump(); });
        // Help panel (C+A; D2 retired the pinned-selection channel with the index list). The section
        // fallback stays a binding because it is business state; the hover claim does not - since FL
        // P3 the per-pass claim machine lives on the session (UsKernelDraw.HelpHover claims it, the
        // panel and the accent border read ctx.Session.HoverClaim).
        bindings.BindReadOnly<string>("help-section-key", () => source.SectionHelpKey(state.ActiveSectionKey));

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
