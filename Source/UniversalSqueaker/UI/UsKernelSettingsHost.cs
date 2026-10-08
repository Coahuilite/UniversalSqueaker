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
        return Create(source, (RemixConfirmationFlow?)null);
    }

    /// <summary>
    /// SA1.3: the production entry with the parent window's Remix confirmation flow. The settings
    /// window owns one flow per window instance and passes it here; the "mode" write below then routes
    /// an ENTER-into-Remix through the two-step dialog instead of committing directly. A null flow (the
    /// harness defaults, any host without a parent window) keeps the direct typed write.
    /// </summary>
    public static UiHost Create(IUsKernelSettingsSource source, RemixConfirmationFlow? remixFlow)
    {
        UiNative.Trace = SqueakLog.PopupTrace;
        return Create(source, VerseFerriteTextMetrics.Instance, out _, remixFlow);
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
        IUsKernelSettingsSource source, ITextMetrics metrics, out UsWriteBindings writes,
        RemixConfirmationFlow? remixFlow = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (metrics == null) throw new ArgumentNullException(nameof(metrics));

        UsKernelWidgetRegistrar.EnsureRegistered();

        // One translation seam instance for the host and the per-item text bindings: the checklist's
        // row meta line is composed from a Keyed template, and a second seam would be a second
        // language.
        var translation = new UsKernelTranslation();
        // D4: the raw embedded manifest is parsed as shipped - the domain viewports carry NO
        // consumer-side number. The earlier creation-time floor-width injection was deleted per the
        // PM ruling of 2026-10-07; the budget is the engine's own VisibleRows="4.5" counting on the
        // arranged rows (FL capability in the working tree - the US build slot must use the freeze
        // that carries it).
        UiLayoutManifest manifest = UiLayoutManifest.Parse(ReadManifest());

        // The bottom help panel (BH1) is DECLARATIVE: the manifest's help-scroll Scroll carries
        // VisibleKey="help-open", so the element stays in the definition and the engine simply does not
        // arrange it while the panel is closed. Staying in the definition is the whole point - it is
        // what lets the Scroll keep its node AND its scroll position across a close/open. The pre-0.5
        // root-list VARIANT (a rebuilt root list with the help element omitted) relied on a removed
        // element keeping its node, which the 0.6 carrier no longer does: PruneNodesExcept releases a
        // removed identity together with its scroll position (UiSession.PruneNodesExcept).
        var bumper = new SessionRevisionBumper();
        // (translation was created above the manifest parse - the D4 budget measures with it.)
        writes = BuildBindings(source, bumper, translation, remixFlow);
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
            // R3-B: the layout-diagnosis commands are PER HOST, so the production source is told which host
            // it draws. One place here rather than in every window; a harness fake attaches its own host
            // explicitly in the lane that needs per-host separation.
            production.AttachHost(host);
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
        /// workspace switches so the centre content and the bottom help panel start at their top; other
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

            UiNode? helpNode = session.GetNodeByElementId(HelpPanelScrollId);
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
    private const string HelpPanelScrollId = "help-scroll";

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

    // T3-2, the diagnostics card. The retired us/diagnostics composite drew a segmented control and wrote the
    // typed mode through ONE value binding; the declarative shape is three input/button atoms, so each button
    // carries its own read-only payload string (G2: a button with a PayloadKey opens the STRING action
    // contract) and its own read-only selected answer (SelectedKey). One constant per element, so a renamed
    // button and its binding cannot drift apart silently.
    private const string DevLoggingAutoKey = "dev-logging-auto";
    private const string DevLoggingEnabledKey = "dev-logging-enabled";
    private const string DevLoggingDisabledKey = "dev-logging-disabled";
    private const string DevLoggingAutoValueKey = "dev-logging-auto-value";
    private const string DevLoggingEnabledValueKey = "dev-logging-enabled-value";
    private const string DevLoggingDisabledValueKey = "dev-logging-disabled-value";

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

    /// <summary>
    /// The logging mode a clicked button names. The declarative button's payload is a STRING (ButtonWidget
    /// validates BindAction&lt;string&gt;), so the enum parse lives on this side of the boundary - the same
    /// shape set-distance-preset uses. An unrecognised name is Auto: the fail-soft answer the retired
    /// composite's own default gave, so a renamed option degrades to "the model is on none of these buttons"
    /// instead of dropping the click.
    /// </summary>
    private static SqueakDevLoggingMode ParseDevLoggingMode(string name)
    {
        return Enum.TryParse(name ?? "", ignoreCase: true, out SqueakDevLoggingMode mode)
            && Enum.IsDefined(typeof(SqueakDevLoggingMode), mode)
            ? mode
            : SqueakDevLoggingMode.Auto;
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
        IUsKernelSettingsSource source, SessionRevisionBumper bumper, IUiTranslation translation,
        RemixConfirmationFlow? remixFlow)
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
        bindings.BindReadOnly<bool>("save-status-visible", () => source.SaveStatusVisible);
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
            value =>
            {
                // SA1.3: ENTERING Remix from another mode is the one write the player confirms twice.
                // Leaving Remix and every other mode commit directly. With a flow present (the
                // production settings window) RequestRemix takes the request and returns true, so this
                // setter writes nothing; the flow runs the SAME SetMode+bump closure only on the second
                // step's explicit enable, and Cancel/ESC/close never reach it. A null flow (the harness
                // default and every host without a parent window) keeps the direct typed write, so the
                // existing mode lanes stay honest.
                if (remixFlow != null
                    && value == SqueakVoicePackMode.Remix
                    && source.BuildView().Mode != SqueakVoicePackMode.Remix
                    && remixFlow.RequestRemix(
                        () => { source.SetMode(SqueakVoicePackMode.Remix); bump(); }))
                {
                    return;
                }

                source.SetMode(value);
                bump();
            });

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
        bindings.BindReadOnly<string>("attenuation-axis-caption", () => string.Format(
            translation.Translate("US.Distance.AxisCaption"),
            AttenuationMath.MinDistance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            AttenuationMath.MaxDistance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
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
        writes.Value<bool>("allow-baby-actions", () => source.BuildView().AllowBabyActions, value => { source.SetBabyActions(value); bump(); });
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

        // Diagnostics (T3-2: us/diagnostics is retired). The three-way logging choice is three declarative
        // buttons over ONE string action, so the enum parse lives here on the host side of the boundary - the
        // same split set-distance-preset uses. Each button's payload and its selected answer are READ-ONLY, so
        // the manifest carries no machine token as a literal and exactly one button can answer "the model is
        // on me" (B1's per-element SelectedKey).
        writes.Action<string>(
            "set-dev-logging",
            name => { source.SetDevLoggingMode(ParseDevLoggingMode(name)); bump(); });
        bindings.BindReadOnly<string>(DevLoggingAutoValueKey, () => nameof(SqueakDevLoggingMode.Auto));
        bindings.BindReadOnly<string>(DevLoggingEnabledValueKey, () => nameof(SqueakDevLoggingMode.Enabled));
        bindings.BindReadOnly<string>(DevLoggingDisabledValueKey, () => nameof(SqueakDevLoggingMode.Disabled));
        bindings.BindReadOnly<bool>(DevLoggingAutoKey, () => source.BuildView().DevLoggingMode == SqueakDevLoggingMode.Auto);
        bindings.BindReadOnly<bool>(DevLoggingEnabledKey, () => source.BuildView().DevLoggingMode == SqueakDevLoggingMode.Enabled);
        bindings.BindReadOnly<bool>(DevLoggingDisabledKey, () => source.BuildView().DevLoggingMode == SqueakDevLoggingMode.Disabled);
        // The localize row is the page's own square toggle over the same writable bool the composite's
        // checkbox wrote; the manifest names it in both Bind and SelectedKey, like the camera-indicator row.
        writes.Value<bool>("localize-debug-menu", () => source.BuildView().LocalizeDebugActions, value => { source.SetLocalizeDebugActions(value); bump(); });

        // Developer layout diagnosis (R3-B). Three REAL commands the manifest dispatches: capture on/off,
        // the captured-rect outline, and ONE report. Nothing here is implicit - the geometry instrument used
        // to be switched on as a side effect of detailed logging, which is the coupling this replaces.
        //
        // Both switches are writable VALUE bindings over the source's own developer flags, which is the
        // cleanest truthful shape: the flag is set from whether the instrument HONOURED the request, so get
        // and set always agree and a refused request cannot leave a switch showing a capture that is not
        // running. The status sentence beside them is a READ-ONLY bound string for the same reason the timing
        // caption is: the unavailable case is carrier state the manifest has no expression for.
        writes.Value<bool>("layout-capture", () => source.LayoutCaptureOn,
            value => { source.SetLayoutCapture(value); bump(); });
        writes.Value<bool>("layout-outline", () => source.LayoutOutlineOn,
            value => { source.SetLayoutOutline(value); bump(); });
        writes.Command("request-layout-report", () => { source.RequestLayoutReport(); bump(); });
        // DT1: the Overview diagnostics band is the settings-window entry to the developer panel -
        // the tool is reachable with the settings window by definition open, never standalone. The
        // command IS a display write: the band button carries SelectedKey="dev-panel-open" (the panel's
        // live open state), so opening it changes what the page draws and the clock must follow. The
        // window seam goes through the source facade (the RequestLayoutReport precedent): production
        // opens the real panel, the harness records the call - Verse.Find is not stubbable here.
        bindings.BindReadOnly<bool>("dev-panel-open", () => UniversalSqueaker.UI.Dev.UsDevPanelWindow.Active != null);
        writes.Command("open-dev-panel", () => { source.OpenDeveloperPanel(); bump(); });
        bindings.BindReadOnly<string>("layout-diagnosis-status", () => source.LayoutDiagnosisStatus);
        // RPT1: the sentence that answers the Report click, printed immediately below the button by the
        // manifest. Read-only for the same reason the capture sentence is: the outcome is carrier state the
        // manifest has no expression for, and only the command above may change it.
        bindings.BindReadOnly<string>("layout-report-status", () => source.LayoutReportStatus);

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
        // VF1定稿 A2/A4: the multiplier fields and the action reset-to-preset join the same funnel -
        // they change what the action rows display, so they move the same clock.
        writes.Action<UsActionTuningWrite>("set-action-tuning", write => { source.SetActionTuning(write.ActionKey, write.IntervalField, write.Value); bump(); });
        writes.Action<string>("reset-action-to-preset", key => { source.ResetActionToPreset(key); bump(); });
    // VF1定稿: the tuning area + the final-fallback editor. The area is a value binding (the tabs
    // write it); the editor's lists are read-only projections of the store, and every command bumps
    // the clock because the editor's own rows change with them.
    writes.Value<int>("tuning-area", () => state.TuningArea, value => { source.SetTuningArea(value); bump(); });
    writes.Value<string>("tuning-selected-action", () => state.TuningSelectedAction, value => { source.SetTuningSelectedAction(value); bump(); });
    bindings.BindReadOnly<IReadOnlyList<FallbackRaceView>>("fallback-races", () => source.BuildView().FallbackRaces);
    bindings.BindReadOnly<string>("fallback-selected-race", () => source.BuildView().FallbackSelectedRace);
    bindings.BindReadOnly<IReadOnlyList<FallbackEntryView>>("fallback-entries", () => source.BuildView().FallbackEntries);
    bindings.BindReadOnly<string>("fallback-selected-entry", () => state.FallbackSelectedEntryAction);

    bindings.BindOptions<FilterOptionView>("fallback-sound-options", () => source.BuildView().FallbackSoundOptions);
    bindings.BindOptions<FilterOptionView>("fallback-candidate-options", () => source.BuildView().FallbackCandidateOptions);
    bindings.BindReadOnly<string>("fallback-status", () => source.BuildView().FallbackStatusKey);
    writes.Value<string>("fallback-sound-query", () => state.FallbackSoundQuery, value => { source.SetFallbackQueries(value, null); bump(); });
    writes.Value<string>("fallback-new-race-query", () => state.FallbackNewRaceQuery, value => { source.SetFallbackQueries(null, value); bump(); });
    writes.Action<UsFallbackSelection>("set-fallback-selection", write => { source.SetFallbackSelection(write.Race, write.EntryAction); bump(); });
    writes.Action<UsFallbackEntryWrite>("set-fallback-entry", write => { source.SetFallbackEntry(write.ActionKey, write.SoundDefName); bump(); });
    writes.Action<string>("create-fallback-table", race => { source.CreateFallbackTable(race); bump(); });
    writes.Command("restore-fallback-default", () => { source.RestoreFallbackDefault(); bump(); });
    writes.Action<string>("delete-fallback-table", race => { source.DeleteFallbackTable(race); bump(); });

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
        // V2 P2: the enable card's SCOPE line - which browsed domain its pack list belongs to. Composed here
        // from the SAME UsPacksText name the browse row shows plus the axis word, so it is translated, moves
        // with the selection and cannot be written by any control.
        bindings.BindReadOnly<string>("checklist-scope", () => ChecklistScopeText(source, translation));
        // US-PACK1: the page's ONE result body is the pack-card projection. The Repeat's item keys are
        // FLAT and ordered: a card header contributes its pack key, and while the card is expanded
        // (manual OR query-hit auto) each surviving domain row contributes "<packKey>|<race>[|<target>]".
        // One key, one row, one item-local namespace - the same on-demand registration discipline the
        // retired browse/checklist row sets used, so the list and the screen are one answer.
        //
        // select-domain keeps its name and its key shape (race rows: the raceDefName; xenotype rows:
        // "<race>|<target>") - UsKernelContractInvariantTests keeps pinning it by name. '|' is safe
        // where '/' and '#' are not (UiLayoutEngine.AcceptItemKey). The retired "toggle-pack" payload
        // action left with the checklist list: the card row's switch is its item-local "enabled" VALUE,
        // which writes the SAME per-domain identity through ToggleVoicePack - one writer per identity.
        writes.Action<string>("select-domain", key => { SelectDomainByKey(source, key); bump(); });
        var packCards = new PackCardItemBindings(source, writes, bindings, translation, bump);
        bindings.BindReadOnly<IReadOnlyList<string>>(PackCardKeysKey, () =>
        {
            IReadOnlyList<string> keys = PackCardKeys(source);
            packCards.Ensure(keys);
            return keys;
        });
        // Forget Unavailable changes the domain's pack list, so it reflows the checklist.
        writes.Action<UsDomainIdentity>(
            "forget-unavailable",
            identity => { source.ForgetUnavailable(identity.Scope, identity.RaceDefName, identity.TargetDefName); bump(); });
        // Filter writes change which cards/rows are visible, so they bump the revision. US-PACK1: the
        // unified keyword (search-text) plus the author/race/xenotype dropdowns and the state flags all
        // narrow the ONE card result; the two per-domain search boxes and their per-card Clear retired
        // with the browse cards, so "All" is the page's single reset gesture (clear-pack-filters).
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
        // D4: the declarative card-header dropdowns read UiOption pairs (the dropdown kind's options
        // contract); the FilterOptionView bindings above stay for the composites that map them.
        bindings.BindOptions<UiOption>("race-domain-options", () =>
        {
            var list = new List<UiOption>();
            foreach (FilterOptionView option in source.BuildView().RaceFilterOptions)
            {
                list.Add(new UiOption(option.DisplayName, option.Value));
            }

            return list;
        });
        bindings.BindOptions<UiOption>("xenotype-domain-options", () =>
        {
            var list = new List<UiOption>();
            foreach (FilterOptionView option in source.BuildView().XenotypeFilterOptions)
            {
                list.Add(new UiOption(option.DisplayName, option.Value));
            }

            return list;
        });

        bindings.BindOptions<FilterOptionView>("author-options", () => source.BuildView().Authors
            .Select(author => new FilterOptionView(author, author))
            .ToList());
        // US-PACK1: the unified keyword - the page's ONE text condition (the retired per-list searches
        // folded into it) - same write/bump/read-back shape as before, same single substring rule.
        writes.Value<string>("search-text", () => state.SearchText, value => { source.SetSearchText(value); bump(); });
        // US-PACK1: the retired checklist-pack-keys row set leaves its item bindings to PackCardItemBindings
        // (registered by the pack-card-keys projection above); the status-band composite keeps reading
        // selected-domain, and checklist-has-domain stays its gate - the BAND is still the operation
        // domain's, while the RESULT no longer needs a domain at all.
        bindings.BindReadOnly<bool>("checklist-has-domain", () => source.BuildView().SelectedDomain.HasValue);
        // The result region's two empties are different sentences: an over-narrowed filter still has
        // packs behind it, while an empty catalog has none to find. One bool each, both computed by the
        // SAME projection that produced the cards, so a note and the list can never both be on screen.
        bindings.BindReadOnly<bool>("pack-results-empty", () => source.BuildView().PackResultsEmpty);
        bindings.BindReadOnly<bool>("pack-no-packs", () => source.BuildView().PackNoPacks);
        // The condition echo: every ACTIVE condition of the region, named in the player's language, plus
        // the surviving-card count - §4.2's "每一条件的作用在同一筛选区可见". Composed here from the live
        // state and view; nothing writes it, so it cannot disagree with the controls.
        bindings.BindReadOnly<string>("pack-filter-summary", () => PackFilterSummary(source, state, translation));
        bindings.BindReadOnly<bool>("pack-filter-active", () => PackFilterActive(state));
        bindings.BindReadOnly<UiDomainFilter>("domain-filter", () => state.DomainFilter);
        writes.Action<UsDomainFilterWrite>("set-domain-filter", write => { source.SetDomainFilter(write.Kind, write.Flag); bump(); });
        // Help panel (C+A; D2 retired the pinned-selection channel with the index list). The section
        // fallback stays a binding because it is business state; the hover claim does not - since FL
        // P3 the per-pass claim machine lives on the session (UsKernelDraw.HelpHover claims it, the
        // panel and the accent border read ctx.Session.HoverClaim).
        bindings.BindReadOnly<string>("help-section-key", () => source.SectionHelpKey(state.ActiveSectionKey));

        // The retractable bottom help panel (BH1): INDEPENDENT per-window visibility state, never the
        // engine's active-tab gate (the manifest's panel element deliberately carries no Tab). Both writes
        // advance the session revision through the bumper, so a toggle re-arranges the page and never
        // recreates the host/session. ONE key, ONE presentation: help-open shows the panel above the
        // footer, and expanding it changes neither the window's width nor its position.
        writes.Value<bool>(
            "help-open",
            () => state.HelpPanelOpen,
            value => { source.SetHelpPanelOpen(value); bump(); });

        // A COMMAND, not an action with a payload: the manifest's footer button is a core
        // `input/button` with no PayloadKey, and ButtonWidget validates that shape with ValidateCommand
        // (ButtonWidget.cs:62-71 -> UiBindings.cs:412-418). The payload the old registration took was
        // discarded anyway, so this is the same write with the contract the declaring element actually has.
        // BH1 retired the two derived presentation keys (help-open-wide / help-open-narrow) and body-visible
        // together with the page-width feed they read: with one presentation there is nothing left to decide
        // from the box, and a derived bool the player never writes would only be a second truth to keep in
        // sync - which is what the pre-BH1 pre-resize defect lived on.
        writes.Command(
            "toggle-help-drawer",
            () => { source.SetHelpPanelOpen(!state.HelpPanelOpen); bump(); });
        // US-ESC1: the tree cancel layers the manifest declares with CancelBind (FL-IC2). The engine's
        // TryHandleCancel walks menu → held capture → open edit → the nearest EXECUTABLE CancelBind up
        // from the interaction subject; each Can here is the spec's "one press, one layer, decline means
        // climb past" answer, and none of these writes touches settings, persistence or routing - they
        // are session return states (§4.1). The page root (the five-workspace container) carries
        // cancel-help; the packs RESULT carries cancel-pack-results (collapse the manually opened cards)
        // and the card's operation-domain context carries cancel-domain-selection; the tuning COMPOSITE
        // carries the visible-branch row steps and the container that WRAPS it carries the context
        // return (review1 observation 1: 当前层域/区域 is its own Parent/CancelBind layer, active by business state, so
        // even the untouched default Global/Actions page returns once before the root may close). With
        // every layer declined the key reaches Verse and the main layer closes - "only the root
        // closes", one press after the last observable release.
        writes.Command(
            "cancel-help",
            () => { source.SetHelpPanelOpen(false); bump(); },
            () => state.HelpPanelOpen);
        writes.Command(
            "cancel-domain-selection",
            () => { source.CancelDomainSelection(); bump(); },
            () => source.CanCancelDomainSelection());
        // US-PACK1 result layer (§4.1): the pack-card body is its own return layer BETWEEN the operation
        // domain and the page root. It answers ONLY while the player has manually opened cards - a
        // query-auto-expanded card is the query's answer, not a layer, and an all-collapsed result has
        // nothing to release, so the veto climbs to cancel-help. Collapsing writes no settings and clears
        // no filter or selection.
        writes.Command(
            "cancel-pack-results",
            () => { source.CancelPackResults(); bump(); },
            () => source.CanCancelPackResults());
        writes.Command(
            "cancel-tuning-target",
            () => { source.CancelTuningTarget(); bump(); },
            () => source.CanCancelTuningTarget());
        writes.Command(
            "cancel-tuning-context",
            () => { VoicePacksPageModel.CancelTuningContext(state); bump(); },
            () => VoicePacksPageModel.CanCancelTuningContext(state));

        return writes;
    }

    /// <summary>US-PACK1: the ONE declarative row set of the Packs page - the pack-card result. The
    /// projection orders header keys and the domain rows of every EXPANDED card into one flat list, and
    /// registers each row's item-local namespace on demand (see <see cref="PackCardItemBindings"/>).</summary>
    private const string PackCardKeysKey = "pack-card-keys";

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

    /// <summary>
    /// US-PACK1: the flat item-key list the pack-card Repeat materializes. A collapsed card contributes
    /// ONLY its header key; an expanded card (manual OR query-hit auto) is followed by one row key per
    /// surviving domain ("<packKey>|<race>" / "<packKey>|<race>|<target>"). Order is the projection's -
    /// first appearance of each pack across the domain scan - and it is part of the contract, because
    /// Repeat reuses a row's node and state by key.
    /// </summary>
    private static IReadOnlyList<string> PackCardKeys(IUsKernelSettingsSource source)
    {
        IReadOnlyList<PackCardView> cards = source.BuildView().PackCards;
        var keys = new List<string>();
        for (int i = 0; i < cards.Count; i++)
        {
            PackCardView card = cards[i];
            if (string.IsNullOrEmpty(card.Key)) continue;
            keys.Add(card.Key);
            if (!card.Expanded) continue;
            for (int r = 0; r < card.Rows.Count; r++)
            {
                string rowKey = card.Rows[r].RowKey(card.Key);
                if (!string.IsNullOrEmpty(rowKey) && !keys.Contains(rowKey)) keys.Add(rowKey);
            }
        }

        return keys;
    }

    /// <summary>
    /// The pack-card row's item-local binding namespace (<c>pack-card-keys.&lt;itemKey&gt;.&lt;declaredKey&gt;</c>),
    /// registered on demand by the projection that names the rows - the same discipline the retired
    /// checklist/browse row sets established: the row set is a runtime projection, the registry has no
    /// prefix resolution, so "register exactly the rows the list just named" is the only honest form.
    /// <para>
    /// <b>Header vs domain is decided by the KEY, not by a flag the widget has to remember.</b> A key
    /// without the separator names a card header; one with it names that card's domain row. The reads
    /// resolve against the CURRENT view every frame (a captured struct would freeze the row), and the
    /// writes parse the key at write time: the switch's identity is (packKey, scope, race, target) -
    /// the per-domain whole-selection write the settings layer already owns, reached through exactly the
    /// old checklist path, so two rows of one pack can not address each other's selection.
    /// </para>
    /// <para>
    /// The per-kind keys are registered for the kind ONLY: a header never resolves "enabled" and a domain
    /// row never resolves "toggle-pack-card", because the template's other-kind children hide behind
    /// per-item VisibleKey bools and a hidden subtree is never materialized. The two VisibleKey bools
    /// themselves exist for both kinds, which is what lets one template draw both.
    /// </para>
    /// </summary>
    private sealed class PackCardItemBindings
    {
        private readonly IUsKernelSettingsSource source;
        private readonly UsWriteBindings writes;
        private readonly UiBindings bindings;
        private readonly IUiTranslation translation;
        private readonly Action bump;
        private readonly HashSet<string> registered = new(StringComparer.Ordinal);

        internal PackCardItemBindings(
            IUsKernelSettingsSource source, UsWriteBindings writes, UiBindings bindings,
            IUiTranslation translation, Action bump)
        {
            this.source = source;
            this.writes = writes;
            this.bindings = bindings;
            this.translation = translation;
            this.bump = bump;
        }

        internal void Ensure(IReadOnlyList<string> keys)
        {
            for (int i = 0; i < keys.Count; i++) Register(keys[i]);
        }

        private void Register(string key)
        {
            if (string.IsNullOrEmpty(key) || !registered.Add(key)) return;

            string scope = PackCardKeysKey + "." + key + ".";
            bool isHeader = key.IndexOf(RowKeySeparator) < 0;
            bindings.BindReadOnly<bool>(scope + "is-header", () => IsHeaderLive(key));
            bindings.BindReadOnly<bool>(scope + "is-domain", () => !IsHeaderLive(key));
            bindings.BindReadOnly<string>(scope + "payload", () => Payload(key));
            bindings.BindReadOnly<string>(scope + "title", () => Title(key));
            // The selection-surface is drawn for BOTH kinds (it is the template's hover/selected plate),
            // so "selected" must resolve on a header too. IsRowSelected parses the key as a domain and
            // returns false for a header (no owning card for a separator-less key), which is exactly the
            // header's answer: only a domain row can be the operating-domain highlight.
            bindings.BindReadOnly<bool>(scope + "selected", () => IsRowSelected(key));
            if (isHeader)
            {
                bindings.BindReadOnly<string>(scope + "meta", () => Meta(key));
                bindings.BindReadOnly<string>(scope + "coverage", () => FindCard(key)?.Coverage ?? "");
                bindings.BindReadOnly<bool>(scope + "expanded", () => FindCard(key)?.Expanded ?? false);
                writes.ItemAction<string>(PackCardKeysKey, key, "toggle-pack-card", _ =>
                {
                    source.TogglePackCard(key);
                    // A display write: the card's own body appears/disappears, so the clock must move.
                    bump();
                });
                return;
            }
            writes.ItemValue<bool>(
                PackCardKeysKey, key, "enabled",
                () => FindRowEnabled(key),
                value => ToggleRow(key, value));
            writes.ItemAction<string>(PackCardKeysKey, key, "select-domain", payload =>
            {
                SelectDomainByKey(source, payload);
                bump();
            });
        }

        /// <summary>Kind resolved LIVE, not from the registration-time parse: a key's shape is stable,
        /// but the answer must track the current projection frame like every other getter.</summary>
        private static bool IsHeaderLive(string key) => key.IndexOf(RowKeySeparator) < 0;

        /// <summary>The select-domain payload of a domain row is its DOMAIN identity (the pack half is
        /// already carried by the item key); a header's payload is its own card key.</summary>
        private static string Payload(string key)
        {
            int first = key.IndexOf(RowKeySeparator);
            return first < 0 ? key : key.Substring(first + 1);
        }

        private string Title(string key)
        {
            if (IsHeaderLive(key)) return FindCard(key)?.Label ?? "";
            PackCardView? card = FindOwningCard(key);
            if (card == null) return "";
            PackCardDomainRowView? row = RowOf(card.Value, key);
            if (!row.HasValue) return "";
            return row.Value.Scope == SqueakVoicePackScope.Xenotype
                ? UsPacksText.Format(translation, UsPacksText.KeyXenotypeRaceContext,
                    row.Value.DisplayName, ResolveRaceDisplay(card.Value, key))
                : row.Value.DisplayName;
        }

        private static string ResolveRaceDisplay(PackCardView card, string key)
        {
            PackCardDomainRowView? row = RowOf(card, key);
            return row.HasValue ? row.Value.RaceDefName : "";
        }

        private string Meta(string key)
        {
            PackCardView? card = FindCard(key);
            if (!card.HasValue) return "";
            return UsPacksText.Format(translation, KeyPackChecklistMeta, card.Value.ModName, card.Value.Author);
        }

        private PackCardView? FindCard(string key)
        {
            IReadOnlyList<PackCardView> cards = source.BuildView().PackCards;
            for (int i = 0; i < cards.Count; i++)
                if (string.Equals(cards[i].Key, key, StringComparison.Ordinal)) return cards[i];
            return null;
        }

        /// <summary>The card a domain-row key names by its pack half. Null when the model dropped the
        /// card this frame - a legal frame, answered like every other unresolvable bound key.</summary>
        private PackCardView? FindOwningCard(string rowKey)
        {
            int first = rowKey.IndexOf(RowKeySeparator);
            if (first <= 0) return null;
            return FindCard(rowKey.Substring(0, first));
        }

        private static PackCardDomainRowView? RowOf(PackCardView card, string rowKey)
        {
            for (int i = 0; i < card.Rows.Count; i++)
                if (string.Equals(card.Rows[i].RowKey(card.Key), rowKey, StringComparison.Ordinal)) return card.Rows[i];
            return null;
        }

        /// <summary>The switch's live answer for a domain row: the CURRENT view's row, never a captured
        /// frame - a toggle's echo must read back from the same projection the screen draws.</summary>
        private bool FindRowEnabled(string rowKey)
        {
            PackCardView? card = FindOwningCard(rowKey);
            if (!card.HasValue) return false;
            PackCardDomainRowView? row = RowOf(card.Value, rowKey);
            return row.HasValue && row.Value.IsEnabled;
        }

        /// <summary>The operating-domain highlight: true when this row's domain IS the selected domain
        /// (double-key compare for xenotypes, exactly the identity the model carries).</summary>
        private bool IsRowSelected(string rowKey)
        {
            VoicePackDomainView? domain = source.BuildView().SelectedDomain;
            if (!domain.HasValue) return false;
            PackCardView? card = FindOwningCard(rowKey);
            if (card == null) return false;
            PackCardDomainRowView? row = RowOf(card.Value, rowKey);
            if (!row.HasValue) return false;
            return row.Value.Scope == domain.Value.Scope
                && string.Equals(row.Value.RaceDefName, domain.Value.RaceDefName, StringComparison.Ordinal)
                && string.Equals(row.Value.TargetDefName, domain.Value.TargetDefName, StringComparison.Ordinal);
        }

        private void ToggleRow(string rowKey, bool enabled)
        {
            PackCardView? card = FindOwningCard(rowKey);
            if (card == null) return;
            PackCardDomainRowView? row = RowOf(card.Value, rowKey);
            if (!row.HasValue) return;
            source.ToggleVoicePack(row.Value.Scope, row.Value.RaceDefName, row.Value.TargetDefName, card.Value.Key, enabled);
            // A display write: the row's switch state and the header's enable badge both flow back to
            // the screen, so the session clock must advance or the revision-gated view keeps the old row.
            bump();
        }
    }

    /// <summary>US-PACK1: is any condition of the unified region active? Drives the echo's visibility -
    /// an unfiltered page does not need a sentence telling it is unfiltered.</summary>
    private static bool PackFilterActive(VoicePacksPageState state)
        => state != null
            && (state.SearchText.Trim().Length > 0
                || (state.PackFilter.Author ?? "").Length > 0
                || state.RaceFilter.Length > 0
                || state.XenotypeFilter.Length > 0
                || state.DomainFilter.EnabledOnly
                || state.DomainFilter.ConflictOnly
                || state.DomainFilter.OrphanOnly);

    /// <summary>
    /// US-PACK1 (§4.2 "每一条件的作用在同一筛选区可见"): the condition echo. Every ACTIVE condition is
    /// named with its own control's label key (the same keys the controls carry, so the echo and the
    /// control can never read differently), joined by the shared separator, and closed by the surviving-
    /// card count through a keyed template. Read-only over the live state and view; no control writes it.
    /// </summary>
    private static string PackFilterSummary(
        IUsKernelSettingsSource source, VoicePacksPageState state, IUiTranslation translation)
    {
        var parts = new List<string>();
        string keyword = (state.SearchText ?? "").Trim();
        if (keyword.Length > 0)
            parts.Add(UsPacksText.Format(translation, KeySummaryCondition,
                translation.Translate(KeyLabelKeyword), keyword));
        if (!string.IsNullOrEmpty(state.PackFilter.Author))
            parts.Add(UsPacksText.Format(translation, KeySummaryCondition,
                translation.Translate("US.Packs.Filter.Author"), state.PackFilter.Author));
        if (state.RaceFilter.Length > 0)
            parts.Add(UsPacksText.Format(translation, KeySummaryCondition,
                translation.Translate(KeyScopeRace), FilterDisplay(source, state.RaceFilter, isRace: true)));
        if (state.XenotypeFilter.Length > 0)
            parts.Add(UsPacksText.Format(translation, KeySummaryCondition,
                translation.Translate(KeyScopeXenotype), FilterDisplay(source, state.XenotypeFilter, isRace: false)));
        if (state.DomainFilter.EnabledOnly) parts.Add(translation.Translate("US.Packs.Filter.EnabledOnly"));
        if (state.DomainFilter.ConflictOnly) parts.Add(translation.Translate("US.Packs.Filter.Conflicts"));
        if (state.DomainFilter.OrphanOnly) parts.Add(translation.Translate("US.Packs.Filter.OrphanOnly"));

        int cards = source.BuildView().PackCards.Count;
        string count = UsPacksText.Format(translation, KeySummaryCount, cards.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return parts.Count == 0
            ? count
            : string.Join(" · ", parts) + " · " + count;
    }

    /// <summary>The dropdown's own display for a machine token (translated race/xenotype label), falling
    /// back to the token itself when the current options list no longer carries it.</summary>
    private static string FilterDisplay(IUsKernelSettingsSource source, string value, bool isRace)
    {
        IReadOnlyList<FilterOptionView> options = isRace
            ? source.BuildView().RaceFilterOptions
            : source.BuildView().XenotypeFilterOptions;
        for (int i = 0; i < options.Count; i++)
            if (string.Equals(options[i].Value, value, StringComparison.Ordinal)) return options[i].DisplayName;
        return value;
    }

    private const string KeySummaryCondition = "US.Packs.Filter.SummaryCondition";

    private const string KeySummaryCount = "US.Packs.Filter.SummaryCount";

    private const string KeyLabelKeyword = "US.Packs.Filter.Keyword";

    /// <summary>The pack row's composed meta line ("Mod — Author"); the one Keyed template it needs.</summary>
    private const string KeyPackChecklistMeta = "US.Packs.Checklist.PackMeta";

    /// <summary>The two browse-axis words the scope line leads with (the same words the filter dropdowns use).</summary>
    private const string KeyScopeRace = "US.Packs.Filter.Race";

    private const string KeyScopeXenotype = "US.Packs.Filter.Xenotype";

    /// <summary>
    /// The enable card's scope line: the browse axis plus the browsed domain's own name, composed through the
    /// same <see cref="UsPacksText"/> name the browse row draws and the existing "X — Y" keyed template. A
    /// xenotype domain keeps its race context (<c>US.Packs.Domain.XenotypeRaceContext</c>), so the line names
    /// the domain exactly as the selected browse row does. Empty when nothing is selected: the card's own
    /// no-domain empty state is the message then.
    /// </summary>
    private static string ChecklistScopeText(IUsKernelSettingsSource source, IUiTranslation translation)
    {
        VoicePackDomainView? domain = source.BuildView().SelectedDomain;
        if (!domain.HasValue) return "";

        bool xenotype = domain.Value.Scope == SqueakVoicePackScope.Xenotype;
        string axis = translation.Translate(xenotype ? KeyScopeXenotype : KeyScopeRace);
        string name = xenotype
            ? UsPacksText.Format(
                translation, UsPacksText.KeyXenotypeRaceContext, domain.Value.DisplayName, domain.Value.RaceDisplay)
            : domain.Value.RaceDisplay;
        return UsPacksText.Format(translation, UsPacksText.KeyNameWithState, axis, name);
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
