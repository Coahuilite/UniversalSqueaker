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
        return Create(source, metrics, out _);
    }

    /// <summary>
    /// Host creation that also hands back the page's write-binding registry (<see cref="UsWriteBindings"/>).
    /// The kernel-host harness is the only caller that needs it: it enumerates the registered write keys so
    /// the D1/D6 revision-clock lane covers every one of them instead of a hand-list. Production code and
    /// every other lane use the two-argument overload.
    /// </summary>
    public static UiHost Create(
        IUsKernelSettingsSource source, ITextMetrics metrics, out UsWriteBindings writes)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (metrics == null) throw new ArgumentNullException(nameof(metrics));

        UsKernelWidgetRegistrar.EnsureRegistered();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(ReadManifest());

        // The retractable help drawer is DECLARATIVE: the manifest's help-scroll element carries
        // VisibleKey="help-open", so the element stays in the definition and the engine simply does not
        // arrange it while the drawer is closed. Staying in the definition is the whole point - it is
        // what lets the Scroll keep its node AND its scroll position across a close/open. The pre-0.5
        // root-list VARIANT (a rebuilt root list with the help element omitted) relied on a removed
        // element keeping its node, which the 0.6 carrier no longer does: PruneNodesExcept releases a
        // removed identity together with its scroll position (UiSession.PruneNodesExcept).
        var bumper = new SessionRevisionBumper();
        // One translation seam instance for both the host and the per-item text bindings: the checklist's
        // row meta line is composed from a Keyed template, and a second seam would be a second language.
        var translation = new UsKernelTranslation();
        writes = BuildBindings(source, bumper, translation);
        UiHost host = new(
            Source,
            manifest,
            writes.Bindings,
            UsTheme.Surface(),
            metrics,
            translation);
        bumper.Attach(host.Session);
        // No first-frame reconciliation is needed any more: the manifest's VisibleKey is resolved
        // through the "help-open" binding on every arrange, so the RETRACTED default in the page state
        // is what the very first frame already follows.
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

        /// <summary>
        /// Advances the clock the engine's snapshot cache is keyed on, so the next arrange re-reads the
        /// page's declared visibility. This is REQUIRED for the declarative drawer: the cache compares
        /// <c>cachedContentRevision == ctx.Session.ContentRevision</c> and returns the previous snapshot
        /// when every term matches, so without a bump a closed drawer keeps its old geometry and the
        /// "no reserved column" assertions fail on the very next measure.
        /// </summary>
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

    // S4-3, the timing card. The interval atom is a float slider (input/slider validates float), and the
    // window it declares in the manifest is 1..600 ticks - the same window the retired us/timing widget
    // clamped into. The multiplier step and window are the widget's constants, moved to the host with the
    // two commands the declarative stepper buttons fire.
    private const float IntervalTicksFloor = 1f;
    private const float IntervalTicksCeil = 600f;
    private const float SecondsPerTick = 60f;
    private const float MultiplierStep = 0.1f;
    private const float MultiplierFloor = 0f;
    private const float MultiplierCeil = 3f;

    // S4-3b, the attenuation card. The three preset buttons carry SelectedKey bindings, so each needs its
    // own read-only bool under the name the manifest declares.
    private const string PresetConservativeKey = "distance-preset-conservative";
    private const string PresetBalancedKey = "distance-preset-balanced";
    private const string PresetStrongKey = "distance-preset-strong";

    // The three buttons' payloads: the same names the SelectedKey bindings compare against.
    private const string PresetConservativeValueKey = "distance-preset-conservative-value";
    private const string PresetBalancedValueKey = "distance-preset-balanced-value";
    private const string PresetStrongValueKey = "distance-preset-strong-value";

    /// <summary>
    /// The preset a clicked button names. The declarative button's payload is a STRING (ButtonWidget
    /// validates <c>BindAction&lt;string&gt;</c>), so the enum parse lives on this side of the boundary. An
    /// unrecognised name is <see cref="SqueakDistancePreset.Custom"/> - the same fail-soft answer the
    /// retired composite gave, so a renamed preset degrades to "the model is on none of these buttons"
    /// instead of dropping the click.
    /// </summary>
    private static SqueakDistancePreset ParseDistancePreset(string name)
    {
        return Enum.TryParse(name ?? "", ignoreCase: true, out SqueakDistancePreset preset)
            && Enum.IsDefined(typeof(SqueakDistancePreset), preset)
            ? preset
            : SqueakDistancePreset.Custom;
    }

    /// <summary>One preset button's own selected answer, read from the same value the status sentence prints.</summary>
    private static bool IsDistancePreset(IUsKernelSettingsSource source, SqueakDistancePreset preset)
    {
        return source.BuildView().DistancePreset == preset;
    }

    /// <summary>
    /// The attenuation card's read-out: the preset's display name, then the range it covers. The composite
    /// printed exactly this pair through the same two outlets (a Keyed name and
    /// <see cref="AttenuationMath.FormatRangeDisplay"/>), so the sentence is unchanged - only the place that
    /// assembles it moved, because the manifest has no format expression and this is presentation data.
    /// </summary>
    private static string AttenuationStatus(IUsKernelSettingsSource source, IUiTranslation translation)
    {
        VoicePacksViewState view = source.BuildView();
        float min = view.DistanceRangeMin;
        float max = view.DistanceRangeMax;
        AttenuationMath.SanitizeRange(ref min, ref max);
        return DistancePresetDisplay(view.DistancePreset, translation)
            + "  " + AttenuationMath.FormatRangeDisplay(min, max);
    }

    /// <summary>
    /// Display text for a preset. The stored value is the enum name that <c>set-distance-preset</c> writes,
    /// so only this label mapping is translated - the same split the retired composite documented.
    /// </summary>
    private static string DistancePresetDisplay(SqueakDistancePreset preset, IUiTranslation translation)
    {
        return preset switch
        {
            SqueakDistancePreset.Conservative => translation.Translate("US.Distance.Preset.Conservative"),
            SqueakDistancePreset.Balanced => translation.Translate("US.Distance.Preset.Balanced"),
            SqueakDistancePreset.Strong => translation.Translate("US.Distance.Preset.Strong"),
            _ => translation.Translate("US.Distance.Preset.Custom"),
        };
    }

    /// <summary>
    /// The one interval sentence the card paints and sizes, built from the live value so the
    /// <c>text/wrapped</c> atom's band is the wrap of the very string that is drawn. The retired composite
    /// measured its caption band against a hand-written worst-case SAMPLE constant; here measure and draw
    /// read the same string by construction, so the drift that constant could develop is fixed rather than
    /// relocated.
    /// </summary>
    private static string IntervalCaption(IUsKernelSettingsSource source, IUiTranslation translation)
    {
        string seconds = (source.BuildView().GlobalMinIntervalTicks / SecondsPerTick)
            .ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
        return string.Format(translation.Translate("US.Tuning.MinInterval"), seconds);
    }


    private static UsWriteBindings BuildBindings(
        IUsKernelSettingsSource source, SessionRevisionBumper bumper, IUiTranslation translation)
    {
        Action bump = bumper.Bump;
        VoicePacksPageState state = source.ViewState;
        var bindings = new UiBindings();
        // Every WRITE registration below goes through this funnel; read-only/options bindings stay on the
        // raw surface, because they are not write keys and the revision-clock contract does not apply to
        // them. The funnel records each key it registers, which is what lets the harness enumerate the
        // write set instead of restating it (see UsWriteBindings for the measured scope).
        var writes = new UsWriteBindings(bindings);

        // Navigation / page chrome.
        // The engine's Tab gate reads this key, and the page's visible sections follow the value it
        // answers - so it IS a display write and it must move the same clock. Measured 2026-09-24: no
        // current writer goes through this setter (the carrier never writes ActiveTabKey; the nav's
        // "set-tab" action is the writer and it bumps), so this is the one-line close of a stale-view
        // trap for the NEXT writer rather than a live defect. The dedicated lane
        // ActiveTabWriteAdvancesSharedRevision drives it through the binding and reddens if the bump goes.
        writes.Value<string>(UiBindings.ActiveTabKey, () => state.ActiveTab, value =>
        {
            source.SetActiveTab(value);
            bump();
        });
        bindings.BindReadOnly<string>("active-section", () => state.ActiveSectionKey);
        // Tab switches and scroll-to change which sections are visible (and the active section),
        // so they bump the session content revision through the Host boundary.
        writes.Action<string>("set-tab", tab =>
        {
            source.SetActiveTab(tab);
            bumper.ResetScroll();
            bump();
        });
        writes.Action<string>("scroll-to", sectionKey =>
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
        // (clicks invisible until a workspace switch). Every write binding below registers through
        // UsWriteBindings, so the DisplayWriteAdvancesSharedRevision contract lane in the kernel-host
        // harness enumerates the whole write set instead of restating it, and a new write key with no
        // probe reddens that lane.
        writes.Value<SqueakVoicePackMode>(
            "mode",
            () => source.BuildView().Mode,
            value => { source.SetMode(value); bump(); });

        // Basic: global volume. The 0..1 <-> percent split is a BINDING-side projection (spec 5.2), not a
        // widget's arithmetic: the slider atom owns the 0..1 value and the number-field atom owns the
        // 0..100 points, and both read the same business setter. The caption is a read-only string the
        // page binds instead of formatting in C#, because the manifest has no format expression.
        writes.Value<float>("global-volume", () => source.BuildView().GlobalVolumeFactor, value => { source.SetGlobalVolume(value); bump(); });
        writes.Value<float>(
            "global-volume-percent",
            () => source.BuildView().GlobalVolumeFactor * 100f,
            value => { source.SetGlobalVolume(value / 100f); bump(); });
        bindings.BindReadOnly<string>(
            "global-volume-caption",
            () => string.Format(
                translation.Translate("US.Tuning.GlobalVolume"),
                ((int)Math.Round(source.BuildView().GlobalVolumeFactor * 100f)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"));

        // Basic: distance preset/range + attenuation chart.
        bindings.BindReadOnly<float>("distance-range-min", () => source.BuildView().DistanceRangeMin);
        bindings.BindReadOnly<float>("distance-range-max", () => source.BuildView().DistanceRangeMax);
        bindings.BindReadOnly<string>("distance-preset", () => source.BuildView().DistancePreset.ToString());
        // S4-3b: the three preset buttons are declarative input/button atoms, and a button with an
        // ActionBind validates ValidateAction<string> (ButtonWidget: "with a payload the consumer binds
        // BindAction<string>, without one BindCommand"). The payload is therefore the preset name as a
        // string - the same shape select-domain already uses for a row's own key - and the enum parse
        // lives here, on the host side of the boundary.
        writes.Action<string>(
            "set-distance-preset",
            name => { source.SetDistancePreset(ParseDistancePreset(name)); bump(); });
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("attenuation-points", () => BuildAttenuationPoints(source.BuildView()));
        // The status sentence is the same read-out the composite printed; the manifest has no format
        // expression, so the host builds the one string the atom paints and measures.
        bindings.BindReadOnly<string>("attenuation-status", () => AttenuationStatus(source, translation));
        // Each button's own selected answer (SelectedKey). One read-only bool per preset, computed from the
        // CURRENT preset through the same comparison the status line uses, so the highlighted button and the
        // sentence can never name two different presets.
        bindings.BindReadOnly<bool>(PresetConservativeKey, () => IsDistancePreset(source, SqueakDistancePreset.Conservative));
        bindings.BindReadOnly<bool>(PresetBalancedKey, () => IsDistancePreset(source, SqueakDistancePreset.Balanced));
        bindings.BindReadOnly<bool>(PresetStrongKey, () => IsDistancePreset(source, SqueakDistancePreset.Strong));
        // Each button's own payload. A declarative input/button with a PayloadKey opens the STRING contract
        // (G2), so each datum lives in a value binding rather than as a manifest literal - the same shape
        // the layer rows use, minus the repeater (these three are static, so the keys are plain read-only
        // strings and no item scope is involved).
        bindings.BindReadOnly<string>(PresetConservativeValueKey, () => nameof(SqueakDistancePreset.Conservative));
        bindings.BindReadOnly<string>(PresetBalancedValueKey, () => nameof(SqueakDistancePreset.Balanced));
        bindings.BindReadOnly<string>(PresetStrongValueKey, () => nameof(SqueakDistancePreset.Strong));
        writes.Action<UiChartPointChange>("attenuation-point", change => { ApplyAttenuationPoint(source, source.BuildView(), change); bump(); });

        // Basic: toggles.
        // S4-1: these rows are declarative now, so the value binding IS the toggle - input/checkbox writes
        // the inverse of the bool it read through this setter, and the seven toggle-* action bindings the
        // composites invoked are retired with them (no second write channel onto one value). The egg row's
        // two state sentences are gated by VisibleKey, so the manifest keeps their keys and the host only
        // answers "which of the two is true".
        writes.Value<bool>("allow-eggs", () => source.BuildView().AllowEasterEggs, value => { source.SetEasterEggs(value); bump(); });
        bindings.BindReadOnly<bool>("easter-egg-on", () => source.BuildView().AllowEasterEggs);
        bindings.BindReadOnly<bool>("easter-egg-off", () => !source.BuildView().AllowEasterEggs);
        writes.Value<bool>("scale-cooldown", () => source.BuildView().ScaleCooldownWithTimeSpeed, value => { source.SetBasicToggle(SqueakBasicToggle.ScaleCooldown, value); bump(); });
        writes.Value<bool>("scale-talking", () => source.BuildView().ScaleFrequencyWithTalking, value => { source.SetBasicToggle(SqueakBasicToggle.ScaleTalking, value); bump(); });
        writes.Value<bool>("scale-population", () => source.BuildView().ScalePeriodicWithAudiblePopulation, value => { source.SetBasicToggle(SqueakBasicToggle.ScalePopulation, value); bump(); });
        writes.Value<bool>("camera-indicator", () => source.BuildView().ShowCameraIndicator, value => { source.SetCameraIndicator(value); bump(); });

        // Basic: the eat-precision pair. The parent gates the child, but the guard deliberately lives in
        // ONE place (the widget refuses to invoke while disabled; the settings layer forces the child to
        // false when the parent closes; PostLoadInit normalises a hand-edited file). Do not add a third
        // guard here: a binding-level guard would mask a widget that stops honouring the disabled state.
        writes.Value<bool>("eat-precision", () => source.BuildView().EatPrecisionEnabled, value => { source.SetEatPrecision(value); bump(); });
        writes.Value<bool>("eat-precision-include-drugs", () => source.BuildView().EatPrecisionIncludeDrugs, value => { source.SetEatPrecisionIncludeDrugs(value); bump(); });

        // Timing: global interval floor + cooldown multiplier (cheap runtime statics, display writes).
        // S4-3 dissolved us/timing into manifest atoms. The interval is ONE value in two units, so the
        // declarative card keeps the same split the global-volume card uses: the slider atom owns the
        // machine ticks (a float binding, because input/slider validates float) and the number-field atom
        // owns the player's seconds projection. Both read and write the same business setter.
        writes.Value<float>(
            "interval-ticks",
            () => source.BuildView().GlobalMinIntervalTicks,
            value =>
            {
                source.SetGlobalMinIntervalTicks(Mathf.RoundToInt(Mathf.Clamp(value, IntervalTicksFloor, IntervalTicksCeil)));
                bump();
            });
        writes.Value<float>(
            "interval-seconds",
            () => source.BuildView().GlobalMinIntervalTicks / SecondsPerTick,
            value =>
            {
                source.SetGlobalMinIntervalTicks(Mathf.Max(
                    (int)IntervalTicksFloor,
                    Mathf.RoundToInt(Mathf.Clamp(value, IntervalTicksFloor / SecondsPerTick, IntervalTicksCeil / SecondsPerTick) * SecondsPerTick)));
                bump();
            });
        // The caption is a read-only projection of the same value, built HERE and nowhere else: the atom
        // measures exactly the string it paints (see the manifest comment on this card).
        bindings.BindReadOnly<string>("timing-interval-caption", () => IntervalCaption(source, translation));
        // The declarative stepper: a button fires a command, and the step (0.1, clamped 0..3) stays here.
        writes.Command(
            "timing-multiplier-minus",
            () =>
            {
                source.SetGlobalCooldownMultiplier(Mathf.Clamp(
                    source.BuildView().GlobalCooldownMultiplier - MultiplierStep, MultiplierFloor, MultiplierCeil));
                bump();
            });
        writes.Command(
            "timing-multiplier-plus",
            () =>
            {
                source.SetGlobalCooldownMultiplier(Mathf.Clamp(
                    source.BuildView().GlobalCooldownMultiplier + MultiplierStep, MultiplierFloor, MultiplierCeil));
                bump();
            });
        writes.Value<float>("cooldown-multiplier", () => source.BuildView().GlobalCooldownMultiplier, value => { source.SetGlobalCooldownMultiplier(value); bump(); });

        // Diagnostics: dev logging level + vanilla debug-menu localization.
        writes.Value<SqueakDevLoggingMode>("dev-logging", () => source.BuildView().DevLoggingMode, value => { source.SetDevLoggingMode(value); bump(); });
        writes.Value<bool>("localize-debug-menu", () => source.BuildView().LocalizeDebugActions, value => { source.SetLocalizeDebugActions(value); bump(); });

        // Tuning: layer/domain/scope/mood/baseline.
        bindings.BindReadOnly<int>("tuning-layer", () => state.TuningLayer);
        // Layer and domain switches swap the tuning editor's content, so they bump the revision.
        writes.Action<int>("set-tuning-layer", layer => { source.SetTuningLayer(layer); bump(); });
        bindings.BindReadOnly<string>("tuning-race", () => state.TuningRaceDefName);
        bindings.BindReadOnly<string>("tuning-xeno", () => state.TuningXenotypeDefName);
        bindings.BindReadOnly<IReadOnlyList<TuningDomainOptionView>>("tuning-domains", () => source.BuildView().TuningDomains);
        writes.Action<UsTuningDomainSelection>(
            "set-tuning-domain",
            selection => { source.SetTuningDomain(selection.RaceDefName, selection.TargetDefName); bump(); });
        bindings.BindReadOnly<IReadOnlyList<ActionScopeRowView>>("action-scopes", () => source.BuildView().ActionScopes);
        writes.Action<UsScopeWrite>("set-action-scope", write => { source.SetActionScope(write.ActionKey, write.Scope); bump(); });
        bindings.BindReadOnly<IReadOnlyList<MoodTuningRowView>>("mood-rows", () => source.BuildView().MoodTuningRows);
        writes.Action<UsMoodWrite>("set-mood-tuning", write => { source.SetMoodTuning(write.Mood, write.Factor, write.Value); bump(); });
        writes.Action<UsMoodPresetReset>("reset-mood-to-preset", write => { source.ResetMoodToPreset(write.Mood); bump(); });
        bindings.BindReadOnly<IReadOnlyList<BaselinePresetView>>("baseline-presets", () => source.BuildView().BaselinePresets);
        // Preset expand/collapse, per-row selection and import all reflow the preset tree.
        writes.Action<string>("toggle-baseline-preset", preset => { source.ToggleBaselinePreset(preset); bump(); });
        writes.Action<UsBaselineRaceToggle>(
            "toggle-baseline-race",
            toggle => { source.ToggleBaselineRace(toggle.PresetDefName, toggle.RaceDefName, toggle.Selected); bump(); });
        writes.Action<UsBaselineXenoToggle>(
            "toggle-baseline-xenotype",
            toggle => { source.ToggleBaselineXenotype(toggle.PresetDefName, toggle.RaceDefName, toggle.XenotypeDefName, toggle.Selected); bump(); });
        writes.Action<string>("import-baseline", preset => { source.ImportBaselinePreset(preset); bump(); });

        // Packs: filters, selection, checklist.
        bindings.BindReadOnly<IReadOnlyList<RaceLayerRowView>>("races", () => source.BuildView().Races);
        bindings.BindReadOnly<IReadOnlyList<VoicePackDomainView>>("xenotype-domains", () => source.BuildView().XenotypeDomains);
        bindings.BindReadOnly<VoicePackDomainView?>("selected-domain", () => source.BuildView().SelectedDomain);
        // S4-2: the two layer cards are declarative row sets now, and each row's identity is the payload its
        // input/button carries (ButtonWidget.PayloadKey, scoped per item). The payload is therefore a STRING
        // - the row's own business key - and decoding it back into (scope, race, target) is the host's job.
        // The key name is unchanged on purpose: it stays the one layout-affecting "which domain is selected"
        // write, and UsKernelContractInvariantTests keeps pinning it by name.
        //
        // The key shape is the projection's own contract: a race row's key IS its raceDefName; a xenotype
        // row's key is "<raceDefName>|<targetDefName>". '|' is safe where '/' and '#' are not - the engine
        // refuses an item key carrying either of those (UiLayoutEngine.AcceptItemKey).
        writes.Action<string>("select-domain", key => { SelectDomainByKey(source, key); bump(); });
        // The two row sets the Repeats are built from: one ordered business key per row, and the projection
        // that names them also owns their item-local binding namespace (see LayerRowBindings).
        var raceRows = new LayerRowBindings(source, writes, bindings, translation, bump, RaceRowsKey, SqueakVoicePackScope.Race);
        bindings.BindReadOnly<IReadOnlyList<string>>(RaceRowsKey, () =>
        {
            IReadOnlyList<string> keys = RaceRowKeys(source);
            raceRows.Ensure(keys);
            return keys;
        });
        var xenotypeRows = new LayerRowBindings(source, writes, bindings, translation, bump, XenotypeRowsKey, SqueakVoicePackScope.Xenotype);
        bindings.BindReadOnly<IReadOnlyList<string>>(XenotypeRowsKey, () =>
        {
            IReadOnlyList<string> keys = XenotypeRowKeys(source);
            xenotypeRows.Ensure(keys);
            return keys;
        });
        writes.Action<UsPackToggle>(
            "toggle-pack",
            toggle => { source.ToggleVoicePack(toggle.Scope, toggle.RaceDefName, toggle.TargetDefName, toggle.PackKey, toggle.Enabled); bump(); });
        // Forget Unavailable changes the domain's pack list, so it reflows the checklist.
        writes.Action<UsDomainIdentity>(
            "forget-unavailable",
            identity => { source.ForgetUnavailable(identity.Scope, identity.RaceDefName, identity.TargetDefName); bump(); });
        // Filter/search value writes change which rows are visible, so they bump the revision.
        writes.Value<string>("race-filter", () => state.RaceFilter, value => { source.SetRaceFilter(value); bump(); });
        writes.Value<string>("xenotype-filter", () => state.XenotypeFilter, value => { source.SetXenotypeFilter(value); bump(); });
        writes.Value<string>("pack-filter", () => state.PackFilter.Author ?? "", value => { source.SetPackFilter(value); bump(); });
        writes.Action<string>("set-pack-filter", value => { source.SetPackFilter(value); bump(); });
        writes.Action<string>(
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
        writes.Value<string>("search-text", () => state.SearchText, value => { source.SetSearchText(value); bump(); });
        // Step B-1: the declarative row set's identity projection - the ordered item keys of the selected
        // domain that the CURRENT search accepts, produced by the one predicate the composite widget's row
        // loop also uses (UsChecklistFilter). The query is read from the page state the "search-text"
        // binding above reads, so the key list and the screen cannot disagree about which pack a search
        // accepted; the both-directions contract is asserted by ChecklistItemsLaneTests.
        var checklistItems = new ChecklistItemBindings(source, writes, bindings, translation, bump);
        bindings.BindReadOnly<IReadOnlyList<string>>(ChecklistItemsKey, () =>
        {
            // The projection OWNS its item-local binding namespace: the rows the list names are exactly the
            // rows whose per-item keys must resolve when the engine materializes them, and the engine reads
            // this binding during Measure - before it measures or draws a single row - so registering here
            // is what makes "the list and the screen are one answer" true by construction rather than by
            // hope. A page-level pre-registration cannot do it: the pack set is a runtime projection of the
            // catalog and the selected domain, and no creation-time table can enumerate it.
            IReadOnlyList<string> keys = ChecklistPackKeys(source);
            checklistItems.Ensure(keys);
            return keys;
        });
        // The empty states and the "is there a domain at all" gate are read-only bools over the same
        // projection, so a card can never draw both a list and an empty sentence.
        bindings.BindReadOnly<bool>("checklist-has-domain", () => source.BuildView().SelectedDomain.HasValue);
        bindings.BindReadOnly<bool>("checklist-empty-nodomain", () => !source.BuildView().SelectedDomain.HasValue);
        bindings.BindReadOnly<bool>("checklist-empty-domain", () => EmptyDomain(source, noRows: false));
        bindings.BindReadOnly<bool>("checklist-empty-search", () => EmptyDomain(source, noRows: true));
        bindings.BindReadOnly<UiDomainFilter>("domain-filter", () => state.DomainFilter);
        writes.Action<UsDomainFilterWrite>("set-domain-filter", write => { source.SetDomainFilter(write.Kind, write.Flag); bump(); });
        // Help panel (C+A; D2 retired the pinned-selection channel with the index list). The section
        // fallback stays a binding because it is business state; the hover claim does not - since FL
        // P3 the per-pass claim machine lives on the session (UsKernelDraw.HelpHover claims it, the
        // panel and the accent border read ctx.Session.HoverClaim).
        bindings.BindReadOnly<string>("help-section-key", () => source.SectionHelpKey(state.ActiveSectionKey));

        // Retractable help drawer: INDEPENDENT per-window visibility state, never the engine's
        // active-tab gate (the manifest's drawer elements deliberately carry no Tab). Both writes advance
        // the session revision through the bumper, so a toggle re-arranges the page and never recreates
        // the host/session.
        writes.Value<bool>(
            "help-open",
            () => state.HelpDrawerOpen,
            value => { source.SetHelpDrawerOpen(value); bump(); });

        // (乙1) ONE player intent, TWO mutually exclusive presentations. help-open stays the only thing the
        // header toggle writes; which presentation it produces is a SCREEN question, not a page-width one:
        // a page can be narrow because the window cannot widen (a capped logical screen, which a 1920
        // monitor reaches at UI scale >= ~2.5) or because the window is genuinely small. Only the first
        // would have the drawer eat the centre column, and WindowChromeLayout answers that purely.
        // Read-only because the player never writes it: a second writable flag would be a second truth.
        // Both read the screen lazily per arrange, so a screen change is picked up on the next bump - the
        // same edge the window resizes on.
        bool drawerWidensTheScreen() =>
            WindowChromeLayout.DrawerWidensTheWindow(Verse.UI.screenWidth, Verse.UI.screenHeight);
        bool drawerIsNarrow() => state.HelpDrawerOpen && !drawerWidensTheScreen();
        bindings.BindReadOnly<bool>("help-open-wide", () => state.HelpDrawerOpen && drawerWidensTheScreen());
        bindings.BindReadOnly<bool>("help-open-narrow", drawerIsNarrow);

        // The body yields its SLOT to the narrow band rather than sharing the page with it (shape fixed
        // 2026-09-21b). This is the same single player intent read one step further, not a second state:
        // with the band arranged, this element is not ARRANGED, so the band is page-root's only flexible
        // fill child and is handed the whole leftover by construction. It is hidden through VisibleKey and
        // never removed: the definition keeps the element, so its node, its sub-tree and every scroll
        // position survive the swap - the player's place in the centre column comes back with the body.
        // Read-only, like the two presentations: the player never writes it, and a writable copy would be a
        // second truth about which presentation is showing.
        bindings.BindReadOnly<bool>("body-visible", () => !drawerIsNarrow());
        // A COMMAND, not an action with a payload: the manifest's header button is a core
        // `input/button` with no PayloadKey, and ButtonWidget validates that shape with ValidateCommand
        // (ButtonWidget.cs:62-71 -> UiBindings.cs:412-418). The payload the old registration took was
        // discarded anyway, so this is the same write with the contract the declaring element actually has.
        writes.Command(
            "toggle-help-drawer",
            () => { source.SetHelpDrawerOpen(!state.HelpDrawerOpen); bump(); });

        return writes;
    }

    /// <summary>The Race card's Repeat Items binding: one business key per race domain row.</summary>
    private const string RaceRowsKey = "race-rows";

    /// <summary>The Xenotype card's Repeat Items binding: one business key per (race, xenotype) row.</summary>
    private const string XenotypeRowsKey = "xenotype-rows";

    /// <summary>
    /// The character that joins a xenotype row's two identity halves. <c>'|'</c> on purpose: the engine
    /// refuses a row key carrying <c>'/'</c> or the item-key separator (UiLayoutEngine.AcceptItemKey), and
    /// this key has to survive both as a binding-namespace segment and as a command payload.
    /// </summary>
    private const char RowKeySeparator = '|';

    /// <summary>
    /// Decodes a row's own key back into the business selection. One decode point, so the payload a row
    /// sends and the domain the model receives cannot drift: the key IS the identity, and a row that carries
    /// no separator is a race row by construction.
    /// </summary>
    private static void SelectDomainByKey(IUsKernelSettingsSource source, string key)
    {
        int split = key.IndexOf(RowKeySeparator);
        if (split < 0)
        {
            source.SelectDomain(SqueakVoicePackScope.Race, key, "");
            return;
        }

        source.SelectDomain(
            SqueakVoicePackScope.Xenotype, key.Substring(0, split), key.Substring(split + 1));
    }

    /// <summary>The race rows' ordered keys: the race defName IS the row's identity.</summary>
    private static IReadOnlyList<string> RaceRowKeys(IUsKernelSettingsSource source)
    {
        IReadOnlyList<RaceLayerRowView> rows = source.BuildView().Races;
        var keys = new List<string>(rows?.Count ?? 0);
        if (rows == null) return keys;
        for (int i = 0; i < rows.Count; i++) keys.Add(rows[i].RaceDefName);
        return keys;
    }

    /// <summary>The xenotype rows' ordered keys: "&lt;raceDefName&gt;|&lt;targetDefName&gt;".</summary>
    private static IReadOnlyList<string> XenotypeRowKeys(IUsKernelSettingsSource source)
    {
        IReadOnlyList<VoicePackDomainView> rows = source.BuildView().XenotypeDomains;
        var keys = new List<string>(rows?.Count ?? 0);
        if (rows == null) return keys;
        for (int i = 0; i < rows.Count; i++)
        {
            keys.Add(rows[i].RaceDefName + RowKeySeparator + rows[i].TargetDefName);
        }

        return keys;
    }

    /// <summary>
    /// A declarative layer row's item-local binding namespace
    /// (<c>race-rows.&lt;itemKey&gt;.&lt;declaredKey&gt;</c>), registered on demand by the projection that
    /// names the rows - the same shape, and for the same reason, as the checklist's
    /// <see cref="ChecklistItemBindings"/>: the row set is a runtime projection and the binding registry has
    /// no prefix resolution, so "register exactly the rows the list just named" is the only honest form.
    ///
    /// <para>
    /// Four read-only keys per row: <c>payload</c> (the row's own key, which is what
    /// <c>input/button.PayloadKey</c> hands to <c>select-domain</c>), <c>title</c>, <c>detail</c> and
    /// <c>selected</c>. Every getter resolves the CURRENT view rather than a captured row, so a row's text
    /// and its selected state are live model data.
    /// <para>
    /// <c>selected</c> is the key the carrier gained in e929fa11: SelectedKey answers which row is selected,
    /// so it is item-scoped like the other binding roles, and the template's title element resolves THIS
    /// row's answer. Without the per-row registration the title would fall back to the unresolved report
    /// (all rows unselected), which is exactly what DeclarativePacksLaneTests asserts against.
    /// </para>
    /// </para>
    /// </summary>
    private sealed class LayerRowBindings
    {
        private readonly IUsKernelSettingsSource source;
        private readonly UsWriteBindings writes;
        private readonly UiBindings bindings;
        private readonly IUiTranslation translation;
        private readonly Action bump;
        private readonly string itemsKey;
        private readonly SqueakVoicePackScope scope;
        private readonly HashSet<string> registered = new(StringComparer.Ordinal);

        internal LayerRowBindings(
            IUsKernelSettingsSource source,
            UsWriteBindings writes,
            UiBindings bindings,
            IUiTranslation translation,
            Action bump,
            string itemsKey,
            SqueakVoicePackScope scope)
        {
            this.source = source;
            this.writes = writes;
            this.bindings = bindings;
            this.translation = translation;
            this.bump = bump;
            this.itemsKey = itemsKey;
            this.scope = scope;
        }

        /// <summary>Registers the item-local keys of the given rows, once per key.</summary>
        internal void Ensure(IReadOnlyList<string> keys)
        {
            for (int i = 0; i < keys.Count; i++) Register(keys[i]);
        }

        private void Register(string key)
        {
            if (string.IsNullOrEmpty(key) || !registered.Add(key)) return;

            string prefix = itemsKey + "." + key + ".";
            bindings.BindReadOnly<string>(prefix + "payload", () => key);
            bindings.BindReadOnly<string>(prefix + "title", () => Title(key));
            bindings.BindReadOnly<string>(prefix + "detail", () => Detail(key));
            // B1: this row's own selected answer. One bool per row, computed from the CURRENT selection, so
            // the template's SelectedKey can never read another row's state.
            bindings.BindReadOnly<bool>(prefix + "selected", () => IsSelected(key));
            // ActionBind is item-scoped inside a template too (UiLayoutEngine.QualifyItemBinding), so one
            // declared ActionBind cannot serve every row: the element asks for
            // "<items>.<itemKey>.select-domain" and the host registers exactly one command per row. The
            // payload stays load-bearing - it is what the command decodes - so a row that carried another
            // row's key would still select the wrong domain, which is the property the lane presses for.
            writes.ItemAction<string>(itemsKey, key, "select-domain", payload =>
            {
                SelectDomainByKey(source, payload);
                // A display write: which domain is selected drives the checklist's contents and (before
                // S4-2) the row's own ink, so the session clock must advance or the revision-gated view
                // keeps serving the pre-write projection.
                bump();
            });
        }

        /// <summary>The row this key names in the CURRENT view, or null when the model dropped it. Null is a
        /// legal frame: a row's node outlives its data for one frame, and the leaf's documented answer to an
        /// unresolvable bound string is its empty default plus one report.</summary>
        private RaceLayerRowView? RaceRow(string key)
        {
            IReadOnlyList<RaceLayerRowView> rows = source.BuildView().Races;
            if (rows == null) return null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].RaceDefName, key, StringComparison.Ordinal)) return rows[i];
            }

            return null;
        }

        private VoicePackDomainView? XenotypeRow(string key)
        {
            int split = key.IndexOf(RowKeySeparator);
            if (split < 0) return null;

            string race = key.Substring(0, split);
            string target = key.Substring(split + 1);
            IReadOnlyList<VoicePackDomainView> rows = source.BuildView().XenotypeDomains;
            if (rows == null) return null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].RaceDefName, race, StringComparison.Ordinal)
                    && string.Equals(rows[i].TargetDefName, target, StringComparison.Ordinal))
                {
                    return rows[i];
                }
            }

            return null;
        }

        private string Title(string key)
        {
            if (scope == SqueakVoicePackScope.Race)
            {
                RaceLayerRowView? row = RaceRow(key);
                return row.HasValue
                    ? UsPacksText.TitleWithState(translation, row.Value.DisplayName, row.Value.State)
                    : "";
            }

            VoicePackDomainView? domain = XenotypeRow(key);
            if (!domain.HasValue) return "";

            // The xenotype row's title is the name qualified by its race's translated label - the same
            // composition the composite drew, through the same resolver.
            string name = UsPacksText.Format(
                translation, UsPacksText.KeyXenotypeRaceContext, domain.Value.DisplayName, domain.Value.RaceDisplay);
            return UsPacksText.TitleWithState(translation, name, domain.Value.State);
        }

        /// <summary>
        /// Whether THIS row is the selected domain. The predicate is the composite's own, kept verbatim: a
        /// race row compares the race defName under the Race scope, a xenotype row compares both halves under
        /// the Xenotype scope. Zero or one row can answer true - the model holds one selected domain.
        /// </summary>
        private bool IsSelected(string key)
        {
            VoicePackDomainView? domain = source.BuildView().SelectedDomain;
            if (!domain.HasValue) return false;

            if (scope == SqueakVoicePackScope.Race)
            {
                return domain.Value.Scope == SqueakVoicePackScope.Race
                    && string.Equals(domain.Value.RaceDefName, key, StringComparison.Ordinal);
            }

            int split = key.IndexOf(RowKeySeparator);
            if (split < 0) return false;
            return domain.Value.Scope == SqueakVoicePackScope.Xenotype
                && string.Equals(domain.Value.RaceDefName, key.Substring(0, split), StringComparison.Ordinal)
                && string.Equals(domain.Value.TargetDefName, key.Substring(split + 1), StringComparison.Ordinal);
        }

        private string Detail(string key)
        {
            if (scope == SqueakVoicePackScope.Race)
            {
                RaceLayerRowView? row = RaceRow(key);
                return row.HasValue
                    ? UsPacksText.DetailText(translation, row.Value.EnabledCount, row.Value.CandidateCount)
                    : "";
            }

            VoicePackDomainView? domain = XenotypeRow(key);
            return domain.HasValue
                ? UsPacksText.DetailText(translation, domain.Value.EnabledCount, domain.Value.CandidateCount)
                : "";
        }
    }

    /// <summary>
    /// The ordered pack keys of the selected domain that the current search accepts. One predicate and one
    /// input with the drawn checklist: the query is the page state's own SearchText (the value the
    /// "search-text" binding reads and writes), never the cached view's copy, so a search write and the row
    /// set it produces are the same frame's answer.
    /// </summary>
    private static IReadOnlyList<string> ChecklistPackKeys(IUsKernelSettingsSource source)
    {
        VoicePackDomainView? domain = source.BuildView().SelectedDomain;
        return domain.HasValue
            ? UsChecklistFilter.Keys(domain.Value, source.ViewState.SearchText)
            : Array.Empty<string>();
    }

    /// <summary>
    /// The declarative checklist row's item-local binding namespace
    /// (<c>checklist-pack-keys.&lt;itemKey&gt;.&lt;declaredKey&gt;</c>), registered on demand by the
    /// projection that names the rows.
    /// <para>
    /// <b>Why on demand rather than at creation.</b> The row set is a runtime projection of the catalog and
    /// the selected domain, and the binding registry is a closed table with no prefix resolution - so the
    /// only two honest options are "register every pack the catalog could ever contain" (which a filtered or
    /// unloaded session cannot enumerate) or "register exactly the rows the list just named" (which is what
    /// the engine is about to materialize). This is the second one. It also keeps the invariant that matters:
    /// a key in the list ALWAYS has readable per-item bindings, so the library's fail-soft answer for an
    /// absent item-local key - draw the default, report once - is never what a player sees.
    /// </para>
    /// <para>
    /// Every getter resolves against the CURRENT view rather than a captured row: a row's label and its
    /// selected state are live model data, and a captured struct would freeze the row at registration time.
    /// The write resolves the domain at write time for the same reason - the item key is the identity
    /// carrier, and the domain it belongs to is whatever domain currently lists it.
    /// </para>
    /// </summary>
    private sealed class ChecklistItemBindings
    {
        private readonly IUsKernelSettingsSource source;
        private readonly UsWriteBindings writes;
        private readonly UiBindings bindings;
        private readonly IUiTranslation translation;
        private readonly Action bump;
        private readonly HashSet<string> registered = new(StringComparer.Ordinal);

        internal ChecklistItemBindings(
            IUsKernelSettingsSource source, UsWriteBindings writes, UiBindings bindings,
            IUiTranslation translation, Action bump)
        {
            this.source = source;
            this.writes = writes;
            this.bindings = bindings;
            this.translation = translation;
            this.bump = bump;
        }

        /// <summary>Registers the item-local keys of the given rows, once per key.</summary>
        internal void Ensure(IReadOnlyList<string> keys)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                Register(keys[i]);
            }
        }

        private void Register(string key)
        {
            if (string.IsNullOrEmpty(key) || !registered.Add(key)) return;

            string scope = ChecklistItemsKey + "." + key + ".";
            bindings.BindReadOnly<string>(scope + "label", () => Find(key)?.Label ?? "");
            bindings.BindReadOnly<string>(scope + "meta", () => Meta(key));
            bindings.BindReadOnly<string>(scope + "coverage", () => Find(key)?.Coverage ?? "");
            // The row's state: the one writable item-local key, so the checkbox's click IS the row's toggle.
            writes.ItemValue<bool>(ChecklistItemsKey, key, "enabled", () => Find(key)?.IsSelected ?? false, value => Toggle(key, value));
        }

        /// <summary>
        /// The row this key names in the CURRENT view, or null when the model no longer supplies it. Null is
        /// a legal frame: a row's node outlives the data for the frame in which the model dropped it, and the
        /// leaf's documented answer to an unresolvable bound string is its empty default plus one report.
        /// </summary>
        private VoicePackRowView? Find(string key)
        {
            VoicePackDomainView? domain = source.BuildView().SelectedDomain;
            if (!domain.HasValue) return null;

            IReadOnlyList<VoicePackRowView> packs = domain.Value.Packs;
            if (packs == null) return null;
            for (int i = 0; i < packs.Count; i++)
            {
                if (string.Equals(packs[i].Key, key, StringComparison.Ordinal)) return packs[i];
            }

            return null;
        }

        private string Meta(string key)
        {
            VoicePackRowView? row = Find(key);
            return UsPacksText.Format(
                translation, KeyPackChecklistMeta, row?.ModName ?? "", row?.Author ?? "");
        }

        private void Toggle(string key, bool enabled)
        {
            VoicePackDomainView? domain = source.BuildView().SelectedDomain;
            if (!domain.HasValue) return;
            source.ToggleVoicePack(
                domain.Value.Scope, domain.Value.RaceDefName, domain.Value.TargetDefName, key, enabled);
            // A display write: the row's selected state and the "Forget dangling" count both flow back to
            // the screen, so the session clock must advance or the revision-gated view keeps the old row.
            bump();
        }
    }

    /// <summary>The Repeat's Items binding: the ordered item keys the declarative row set is built from.</summary>
    private const string ChecklistItemsKey = "checklist-pack-keys";

    /// <summary>The pack row's composed meta line ("Mod — Author"); the one Keyed template it needs.</summary>
    private const string KeyPackChecklistMeta = "US.Packs.Checklist.PackMeta";

    /// <summary>
    /// True when the selected domain exists and shows no row: <paramref name="noRows"/> picks the "the
    /// domain has no packs at all" sentence, false the "the search matched nothing" one - the same split the
    /// composite widget drew before the empty states became manifest elements.
    /// </summary>
    private static bool EmptyDomain(IUsKernelSettingsSource source, bool noRows)
    {
        VoicePackDomainView? domain = source.BuildView().SelectedDomain;
        if (!domain.HasValue) return false;
        return noRows ? domain.Value.Packs.Count > 0 && ChecklistPackKeys(source).Count == 0 : domain.Value.Packs.Count == 0;
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
        Stream? stream = typeof(UsKernelSettingsHost).Assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded Schema=2 layout resource '{ManifestResourceName}' was not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

}