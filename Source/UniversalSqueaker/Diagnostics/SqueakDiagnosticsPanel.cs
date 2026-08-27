using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using UniversalSqueaker.UI;
using Verse;

namespace UniversalSqueaker;

/// <summary>
/// Draggable, non-pausing diagnostics detail panel for <see cref="SqueakDiagnosticsOverlay"/>.
/// Selected mode shows one pawn's current state plus the 17-gate chain (three-state rows);
/// Visible mode lists up to 16 pawns. Formatted text is rebuilt only when
/// <see cref="SqueakDiagnosticsOverlay.Revision"/> changes — never a per-frame re-snapshot or
/// re-layout. Closing the panel (native X or double-Esc) turns the whole diagnostics session off
/// via <see cref="SqueakDiagnosticsOverlay.NotifyPanelClosed"/>.
/// </summary>
internal sealed class SqueakDiagnosticsPanel : Window
{
    private const float TitleRowHeight = 26f;
    private const float ModeBadgeWidth = 96f;
    private const float CloseXReserve = 28f;
    private const float RowHeight = 22f;
    private const float HeaderHeight = 30f;
    private const float DotWidth = 18f;
    private const float StateTextWidth = 64f;
    private const float KeepGrabPx = 24f;
    private const float EscArmSeconds = 3f;
    private const float HintHeight = 22f;
    private const float FieldRowHeight = 19f;
    private const float SectionTitleHeight = 21f;
    private const float SpaceSm = 8f;
    private const float ScrollbarWidth = 16f;

    private enum GateState { Pass, Block, Pending }

    private sealed class GateLine
    {
        public string Name = string.Empty;
        public string Value = string.Empty;
        public GateState State;
    }

    private readonly List<GateLine> gates = new();
    private readonly List<VisibleRow> rows = new();
    private int cachedRevision = -1;
    private SqueakDiagnosticsMode cachedMode = SqueakDiagnosticsMode.Off;
    private string cachedPawnText = string.Empty;
    private bool cachedPawnReady;
    private string cachedActionText = string.Empty;
    private string cachedAudioText = string.Empty;
    private float escArmedUntil = -1f;
    private bool showSeconds;
    private Vector2 scroll;

    private readonly struct VisibleRow
    {
        public readonly Color Dot;
        public readonly string PawnText;
        public readonly string Action;
        public readonly string Cooldown;
        public readonly string Audio;
        public readonly bool Ready;

        public VisibleRow(Color dot, string pawnText, string action, string cooldown, string audio, bool ready)
        {
            Dot = dot; PawnText = pawnText; Action = action; Cooldown = cooldown; Audio = audio; Ready = ready;
        }
    }

    public SqueakDiagnosticsPanel()
    {
        // Non-modal diagnostic panel: never pauses the game, never absorbs surrounding input,
        // keeps camera motion alive. Mirrors the SR panel flags.
        forcePause = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        draggable = true;
        doCloseX = true;
        closeOnCancel = false; // Esc handled manually: two presses within EscArmSeconds close (armed state machine).
        closeOnAccept = false;
        closeOnClickedOutside = false;
        onlyOneOfTypeAllowed = true;
        focusWhenOpened = false;
        onlyDrawInDevMode = true;
    }

    public override Vector2 InitialSize => new(420f, 600f);

    public override void PreClose()
    {
        base.PreClose();
        SqueakDiagnosticsOverlay.NotifyPanelClosed();
    }

    public override void WindowOnGUI()
    {
        base.WindowOnGUI();
        // Keep the title bar grabbable after dragging: at least KeepGrabPx of the window must
        // stay on screen so the panel can never be dragged fully off-screen.
        windowRect.x = Mathf.Clamp(windowRect.x, Mathf.Min(0f, KeepGrabPx - windowRect.width), Verse.UI.screenWidth - KeepGrabPx);
        windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Verse.UI.screenHeight - KeepGrabPx));
    }

    public override void OnCancelKeyPressed()
    {
        // Esc does not close immediately: first press arms a 3s window, second press closes.
        // The direct Window.InnerWindowOnGUI call reaches this override regardless of closeOnCancel.
        float now = Time.realtimeSinceStartup;
        if (now > escArmedUntil)
        {
            escArmedUntil = now + EscArmSeconds;
        }
        else
        {
            escArmedUntil = -1f;
            Close();
        }
        Event.current?.Use();
    }

    public override void DoWindowContents(Rect inRect)
    {
        SectionFrame.Draw(inRect, SectionFrame.SurfaceKind.Raised);
        Rect inner = inRect.ContractedBy(SpaceSm);

        RebuildIfStale();

        // Title row; keep the right edge clear for the vanilla small close X.
        Rect titleRect = new(inner.x, inner.y, Mathf.Max(1f, inner.width - ModeBadgeWidth - SpaceSm - CloseXReserve), TitleRowHeight);
        string titleText = "US.Diagnostics.Title".Translate().ToString();
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Medium;
        float titleH = Mathf.Max(TitleRowHeight, Text.CalcHeight(titleText, titleRect.width));
        titleRect.height = titleH;
        GUI.color = UiPalette.Gold;
        Widgets.Label(titleRect, titleText);
        Text.Font = oldFont;
        GUI.color = oldColor;

        // Right of the title: tick/s toggle then mode toggle (cycles Selected <-> Visible).
        float controlsY = inner.y + (titleH - 22f) * .5f;
        Rect secondsRect = new(inner.xMax - CloseXReserve - 42f - 4f - ModeBadgeWidth, controlsY, 42f, 22f);
        DrawSecondsToggle(secondsRect);
        Rect modeRect = new(inner.xMax - CloseXReserve - ModeBadgeWidth, controlsY, ModeBadgeWidth, 22f);
        DrawModeToggle(modeRect);

        Rect body = new(inner.x, inner.y + titleH + SpaceSm,
            inner.width, Mathf.Max(1f, inner.yMax - HintHeight - (inner.y + titleH + SpaceSm)));
        switch (SqueakDiagnosticsOverlay.Mode)
        {
            case SqueakDiagnosticsMode.Selected:
                DrawSelected(body);
                break;
            case SqueakDiagnosticsMode.Visible:
                DrawVisible(body);
                break;
            default:
                EmptyState.Draw(body, "US.Diagnostics.Panel.Empty".Translate().ToString());
                break;
        }

        // Fixed 22px hint slot at the bottom: only filled while Esc-close is armed, so the
        // layout never jumps between armed/unarmed states.
        Rect hintRect = new(inner.x, inner.yMax - HintHeight, inner.width, HintHeight);
        if (Time.realtimeSinceStartup <= escArmedUntil)
        {
            Color hintOldColor = GUI.color;
            TextAnchor hintOldAnchor = Text.Anchor;
            GameFont hintOldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UiPalette.Gold;
            Widgets.Label(hintRect, "US.Diagnostics.CloseHint".Translate());
            Text.Anchor = hintOldAnchor;
            Text.Font = hintOldFont;
            GUI.color = hintOldColor;
        }
    }

    private static void DrawModeToggle(Rect rect)
    {
        bool selected = SqueakDiagnosticsOverlay.Mode == SqueakDiagnosticsMode.Selected;
        if (Widgets.ButtonText(rect, selected
                ? "US.Diagnostics.Mode.Selected".Translate().ToString()
                : "US.Diagnostics.Mode.Visible".Translate().ToString()))
        {
            SqueakDiagnosticsOverlay.SetMode(selected ? SqueakDiagnosticsMode.Visible : SqueakDiagnosticsMode.Selected);
        }
    }

    private void DrawSecondsToggle(Rect rect)
    {
        if (Widgets.ButtonText(rect, showSeconds ? "s" : "t"))
        {
            showSeconds = !showSeconds;
        }
    }

    private void DrawSelected(Rect body)
    {
        if (gates.Count == 0)
        {
            EmptyState.Draw(body, "US.Diagnostics.Panel.NoPawn".Translate().ToString());
            return;
        }

        Rect headerRect = new(body.x, body.y, body.width, HeaderHeight);
        SectionFrame.Draw(headerRect, SectionFrame.SurfaceKind.Base);
        Rect badgeRect = new(headerRect.xMax - 72f - SpaceSm, headerRect.y + 3f, 72f, headerRect.height - 6f);
        StatusBanner.Draw(badgeRect, cachedPawnReady
            ? "US.Diagnostics.Ready".Translate().ToString() : "US.Diagnostics.Blocked".Translate().ToString(),
            cachedPawnReady ? SectionFrame.SurfaceKind.Success : SectionFrame.SurfaceKind.Base);
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.white;
        Widgets.Label(new Rect(headerRect.x + SpaceSm, headerRect.y, Mathf.Max(1f, badgeRect.x - headerRect.x - SpaceSm * 2f), headerRect.height),
            cachedPawnText);
        Text.Font = oldFont;
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;

        // Section 1 (2 fixed rows): current action + dispatched audio.
        float y = headerRect.yMax;
        DrawSectionTitle(new Rect(body.x, y, body.width, SectionTitleHeight), "Current state");
        y += SectionTitleHeight;
        DrawFieldRow(new Rect(body.x, y, body.width, FieldRowHeight), "Current action", cachedActionText);
        y += FieldRowHeight;
        DrawFieldRow(new Rect(body.x, y, body.width, FieldRowHeight), "Dispatched audio", cachedAudioText);
        y += FieldRowHeight;

        // Section 2: the 17-gate chain, inside a vertical scroll view when it overflows.
        DrawSectionTitle(new Rect(body.x, y, body.width, SectionTitleHeight), "Gate chain");
        y += SectionTitleHeight;
        Rect viewport = new(body.x, y, body.width, Mathf.Max(1f, body.yMax - y));
        DrawGateChain(viewport);
    }

    private void DrawGateChain(Rect viewport)
    {
        float contentHeight = gates.Count * FieldRowHeight;
        bool scrollable = contentHeight > viewport.height;
        float contentWidth = scrollable ? Mathf.Max(1f, viewport.width - ScrollbarWidth) : viewport.width;
        Rect content = new(0f, 0f, contentWidth, contentHeight);
        if (scrollable)
        {
            Widgets.BeginScrollView(viewport, ref scroll, content);
        }

        for (int i = 0; i < gates.Count; i++)
        {
            GateLine gate = gates[i];
            DrawFieldRow(new Rect(0f, i * FieldRowHeight, contentWidth, FieldRowHeight), gate.Name, gate.Value, ColorFor(gate.State));
        }

        if (scrollable)
        {
            Widgets.EndScrollView();
        }
    }

    private static void DrawSectionTitle(Rect rect, string label)
    {
        Color oldColor = GUI.color;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        GUI.color = UiPalette.Gold;
        Widgets.Label(rect, label);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
            new Color(UiPalette.Gold.r, UiPalette.Gold.g, UiPalette.Gold.b, .25f));
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private static void DrawFieldRow(Rect rect, string label, string value, Color? valueColor = null)
    {
        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        float labelWidth = rect.width * .42f;
        GUI.color = UiPalette.Muted;
        Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label);
        Rect valueRect = new(rect.x + labelWidth + SpaceSm, rect.y,
            Mathf.Max(1f, rect.xMax - (rect.x + labelWidth + SpaceSm)), rect.height);
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = valueColor ?? Color.white;
        Widgets.Label(valueRect, value);
        Text.Anchor = oldAnchor;
        Text.Font = oldFont;
        GUI.color = oldColor;
    }

    private void DrawVisible(Rect body)
    {
        if (rows.Count == 0)
        {
            EmptyState.Draw(body, "US.Diagnostics.Panel.Empty".Translate().ToString());
            return;
        }

        float contentHeight = rows.Count * RowHeight;
        bool scrollable = contentHeight > body.height;
        float contentWidth = scrollable ? Mathf.Max(1f, body.width - ScrollbarWidth) : body.width;
        Rect content = new(0f, 0f, contentWidth, contentHeight);
        if (scrollable)
        {
            Widgets.BeginScrollView(body, ref scroll, content);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            VisibleRow row = rows[i];
            Rect rowRect = new(0f, i * RowHeight, contentWidth, RowHeight);
            if (i % 2 == 0)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(.08f, .075f, .067f, .65f));
            }

            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            // Columns: dot | PawnText (flexible) | Action (short) | Cooldown | Audio | Ready/Blocked (right).
            float stateX = rowRect.xMax - StateTextWidth;
            float audioX = stateX - 96f;
            float cooldownX = audioX - 66f;
            float actionX = rowRect.x + DotWidth + SpaceSm;
            float actionW = Mathf.Max(1f, cooldownX - actionX - SpaceSm);
            GUI.color = row.Dot;
            Widgets.Label(new Rect(rowRect.x + 2f, rowRect.y, DotWidth, rowRect.height), SqueakDiagnosticsOverlay.Mark);
            GUI.color = Color.white;
            Widgets.Label(new Rect(actionX, rowRect.y, actionW, rowRect.height), row.PawnText);
            GUI.color = UiPalette.Muted;
            Widgets.Label(new Rect(cooldownX, rowRect.y, 60f, rowRect.height), row.Action);
            Widgets.Label(new Rect(audioX, rowRect.y, 60f, rowRect.height), row.Cooldown);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = Color.white;
            Widgets.Label(new Rect(stateX, rowRect.y, Mathf.Max(1f, rowRect.xMax - stateX), rowRect.height), row.Audio);
            GUI.color = row.Ready ? UiPalette.Success : UiPalette.Gold;
            Widgets.Label(new Rect(stateX, rowRect.y, Mathf.Max(1f, rowRect.xMax - stateX), rowRect.height),
                row.Ready ? "US.Diagnostics.Ready".Translate().ToString() : "US.Diagnostics.Blocked".Translate().ToString());
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
            GUI.color = oldColor;
        }

        if (scrollable)
        {
            Widgets.EndScrollView();
        }
    }

    private static Color ColorFor(GateState state) => state switch
    {
        GateState.Pass => UiPalette.Success,
        GateState.Block => UiPalette.Gold,
        GateState.Pending => UiPalette.Selected,
        _ => Color.white
    };

    /// <summary>Rebuilds the formatted text cache on Repaint only when the overlay revision (or mode) changed. Zero per-frame re-snapshot/re-layout work.</summary>
    private void RebuildIfStale()
    {
        if (Event.current == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        SqueakDiagnosticsMode currentMode = SqueakDiagnosticsOverlay.Mode;
        int currentRevision = SqueakDiagnosticsOverlay.Revision;
        if (cachedRevision == currentRevision && cachedMode == currentMode)
        {
            return;
        }

        cachedRevision = currentRevision;
        cachedMode = currentMode;
        switch (currentMode)
        {
            case SqueakDiagnosticsMode.Selected:
                RebuildSelected();
                break;
            case SqueakDiagnosticsMode.Visible:
                RebuildVisible();
                break;
            default:
                gates.Clear();
                rows.Clear();
                cachedPawnText = string.Empty;
                cachedActionText = string.Empty;
                cachedAudioText = string.Empty;
                break;
        }
    }

    private void RebuildSelected()
    {
        gates.Clear();
        cachedPawnText = string.Empty;
        cachedActionText = string.Empty;
        cachedAudioText = string.Empty;

        Pawn? pawn = SqueakDiagnosticsOverlay.SelectedPawn;
        SqueakDiagnosticsOverlay.CachedPawn? entry = null;
        if (pawn != null)
        {
            foreach (SqueakDiagnosticsOverlay.CachedPawn candidate in SqueakDiagnosticsOverlay.CachedPawns)
            {
                if (ReferenceEquals(candidate.Pawn, pawn))
                {
                    entry = candidate;
                    break;
                }
            }
        }

        if (entry == null)
        {
            return;
        }

        SqueakDiagnosticSnapshot s = entry.Snapshot;
        cachedPawnReady = SqueakDiagnosticsOverlay.ReadyFor(s);
        cachedPawnText = $"{entry.Pawn.LabelShort} ({entry.Pawn.def.defName})";

        // Section 1 values.
        cachedActionText = s.CurrentTimingAction.HasValue
            ? SqueakLabels.Action(s.CurrentTimingAction.Value)
            : "—";
        cachedAudioText = "—";
        if (s.LastSignificantOutcome.HasValue && s.LastSignificantOutcome.Value.Outcome == SqueakTriggerOutcome.Dispatched)
        {
            cachedAudioText = FormatDispatched(s.LastSignificantOutcome.Value);
        }

        gates.Clear();
        gates.AddRange(BuildGates(s, entry.Pawn, showSeconds));
    }

    /// <summary>
    /// Pure read-only gate-chain projection (17 gates G0-G16). Never consumes Rand, never
    /// writes production state, never touches timestamps. <paramref name="showSeconds"/> only
    /// switches cooldown display units; it does not alter any sampled data.
    /// </summary>
    private static List<GateLine> BuildGates(SqueakDiagnosticSnapshot s, Pawn pawn, bool showSeconds)
    {
        List<GateLine> result = new(20);

        void Add(string name, GateState state, string value)
        {
            result.Add(new GateLine { Name = name, Value = value, State = state });
        }

        string Cooldown(int? remainingTicks, float? remainingSeconds)
        {
            if (remainingTicks.HasValue)
            {
                return showSeconds ? $"{remainingTicks.Value / 60f:0.00}s" : $"{remainingTicks.Value}t";
            }

            if (remainingSeconds.HasValue)
            {
                return showSeconds ? $"{remainingSeconds.Value:0.00}s" : $"{remainingSeconds.Value * 60f:0.00}t";
            }

            return "—";
        }

        // Gate G0: Disabled bypass. The panel only opens in DevMode; when the mode is Disabled
        // the whole trigger chain is bypassed, so that is the current blocker.
        bool modeDisabled = SqueakRuntimeResolver.Current.VoicePackMode == SqueakVoicePackMode.Disabled;
        Add("Disabled bypass", modeDisabled ? GateState.Block : GateState.Pass,
            modeDisabled ? "Blocked" : "Pass");

        // G1: on map.
        bool onMap = pawn.Spawned && pawn.MapHeld == Find.CurrentMap;
        Add("On map", onMap ? GateState.Pass : GateState.Block, onMap ? "Pass" : "Blocked");

        // G2: on screen.
        bool onScreen = Find.CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position);
        Add("On screen", onScreen ? GateState.Pass : GateState.Block, onScreen ? "Pass" : "Blocked");

        // G3: plan present/configured. Snapshot timing is only computed along the configured-plan
        // path, so a non-null CurrentTimingAction implies a usable plan; no action => N/A.
        bool hasAction = s.CurrentTimingAction.HasValue;
        Add("Plan", hasAction ? GateState.Pass : GateState.Pass, hasAction ? "Pass" : "N/A");

        // G4: identity gate (external path only). Non-external => N/A; external => evaluate
        // IsPlayerControlled / Downed / Awake.
        bool externalPath = s.CurrentTriggerMode == SqueakTriggerMode.External;
        if (!externalPath)
        {
            Add("Identity", GateState.Pass, "N/A");
        }
        else
        {
            bool idPass = pawn.IsPlayerControlled && !pawn.Downed && pawn.Awake();
            Add("Identity", idPass ? GateState.Pass : GateState.Block, idPass ? "Pass" : "Blocked");
        }

        // G5: action gate (external action): built-in actions pass; external actions require
        // AllowExternalActions. The snapshot only exposes built-in enum actions (external actions
        // are string-keyed and never become CurrentTimingAction), so this gate is Pass by
        // construction for the built-in path; kept as a distinguishable chain entry.
        if (!hasAction)
        {
            Add("Action gate", GateState.Pass, "N/A");
        }
        else
        {
            bool isBuiltIn = s.CurrentTimingAction.HasValue && IsBuiltInAction(s.CurrentTimingAction.Value);
            if (isBuiltIn)
            {
                Add("Action gate", GateState.Pass, "Pass");
            }
            else
            {
                bool allowed = ActionEntryRegistry.Current.AllowExternalActions;
                Add("Action gate", allowed ? GateState.Pass : GateState.Block,
                    allowed ? "Pass" : "Blocked");
            }
        }

        // G6: scope enabled.
        Add("Scope enabled", s.CurrentActionEnabled ? GateState.Pass : GateState.Block,
            s.CurrentActionEnabled ? "Pass" : "Blocked");

        // G7: scope match (ActiveCommand). Only ActiveCommand-scope actions are evaluated;
        // everything else is N/A.
        bool activeCommandScope = hasAction && s.CurrentTimingAction.HasValue
            && SqueakActionDefinitions.Get(s.CurrentTimingAction.Value).DefaultScope == SqueakActionScope.ActiveCommand;
        if (!activeCommandScope)
        {
            Add("Scope match", GateState.Pass, "N/A");
        }
        else
        {
            bool isActive = pawn.Drafted || (pawn.CurJob?.playerForced ?? false);
            Add("Scope match", isActive ? GateState.Pass : GateState.Block,
                isActive ? "Pass" : "Blocked");
        }

        // G8: startup phase.
        Add("Startup", s.StartupPending ? GateState.Block : GateState.Pass,
            s.StartupPending ? "Blocked" : "Pass");

        // G9: probability gate (random, blue with probability values).
        Add("Probability", GateState.Pending, FormatPercent(s.EffectiveProbability, s.BaseProbability));

        // G10: action cooldown.
        Add("Action cooldown", s.Timing.ActionReady ? GateState.Pass : GateState.Block,
            Cooldown(s.Timing.ActionRemainingTicks, s.Timing.ActionRemainingSeconds));

        // G11: global cooldown.
        bool globalApplicable = s.Timing.GlobalApplicable;
        Add("Global cooldown", !globalApplicable || s.Timing.GlobalReady ? GateState.Pass : GateState.Block,
            Cooldown(s.Timing.GlobalRemainingTicks, null));

        // G12: vocal organ gate.
        bool vocalOk = s.VocalCapability.VocalOrganEfficiency > SqueakVocalCapability.VocalSilenceThreshold;
        Add("Vocal organ", vocalOk ? GateState.Pass : GateState.Block,
            vocalOk ? "Pass" : "Blocked");

        // G13: talking gate (probability, blue).
        Add("Talking", GateState.Pending, FormatPercent(s.VocalCapability.TalkingChance, null));

        // G14: audio pool no candidate.
        bool noCandidate = s.LastEvaluation.HasValue && s.LastEvaluation.Value.Outcome == SqueakTriggerOutcome.NoSoundFallback;
        Add("Audio pool", noCandidate ? GateState.Block : GateState.Pass,
            noCandidate ? "Blocked" : "Pass");

        // G15: playability rejected.
        bool eligibilityRejected = s.LastEvaluation.HasValue && s.LastEvaluation.Value.Outcome == SqueakTriggerOutcome.EligibilityRejected;
        Add("Playability", eligibilityRejected ? GateState.Block : GateState.Pass,
            eligibilityRejected ? "Blocked" : "Pass");

        // G16: dispatch success.
        bool dispatched = s.LastEvaluation.HasValue && s.LastEvaluation.Value.Outcome == SqueakTriggerOutcome.Dispatched;
        Add("Dispatch", GateState.Pass,
            dispatched && s.LastEvaluation.HasValue ? FormatDispatched(s.LastEvaluation.Value) : "Pass");

        return result;
    }

    private static bool IsBuiltInAction(SqueakAction action)
    {
        string? key = UniversalSqueaker.Kernel.ActionKey.For(action);
        return key != null && UniversalSqueaker.Kernel.BuiltInActionKeys.Contains(key);
    }

    private static string FormatPercent(float? value, float? baseValue)
    {
        if (!value.HasValue)
        {
            return "—";
        }

        return baseValue.HasValue
            ? $"{value.Value:0.###} / {baseValue.Value:0.###}"
            : $"{value.Value:0.###}";
    }

    private static string FormatDispatched(SqueakRecentOutcome outcome)
    {
        string sound = outcome.Sound?.defName ?? "?";
        return string.IsNullOrEmpty(outcome.PoolStableKey)
            ? sound
            : $"{outcome.PoolStableKey} : {sound}";
    }

    private string FormatCooldownDisplay(int? remainingTicks, float? remainingSeconds)
    {
        if (remainingTicks.HasValue)
        {
            return showSeconds ? $"{remainingTicks.Value / 60f:0.00}s" : $"{remainingTicks.Value}t";
        }

        if (remainingSeconds.HasValue)
        {
            return showSeconds ? $"{remainingSeconds.Value:0.00}s" : $"{remainingSeconds.Value * 60f:0.00}t";
        }

        return "—";
    }

    private void RebuildVisible()
    {
        rows.Clear();
        IReadOnlyList<SqueakDiagnosticsOverlay.CachedPawn> entries = SqueakDiagnosticsOverlay.CachedPawns;
        for (int i = 0; i < entries.Count; i++)
        {
            SqueakDiagnosticsOverlay.CachedPawn entry = entries[i];
            SqueakDiagnosticSnapshot s = entry.Snapshot;
            bool ready = SqueakDiagnosticsOverlay.ReadyFor(s);
            string pawnText = $"{entry.Pawn.LabelShort} ({entry.Pawn.def.defName})";
            string action = s.CurrentTimingAction.HasValue ? SqueakLabels.Action(s.CurrentTimingAction.Value) : "—";
            string cooldown = FormatCooldownDisplay(s.Timing.ActionRemainingTicks, s.Timing.ActionRemainingSeconds);
            string globalCooldown = FormatCooldownDisplay(s.Timing.GlobalRemainingTicks, null);
            string audio = "—";
            if (s.LastSignificantOutcome.HasValue && s.LastSignificantOutcome.Value.Outcome == SqueakTriggerOutcome.Dispatched)
            {
                audio = FormatDispatched(s.LastSignificantOutcome.Value);
            }

            rows.Add(new VisibleRow(entry.MarkColor, pawnText, action, $"{cooldown}/{globalCooldown}", audio, ready));
        }
    }
}