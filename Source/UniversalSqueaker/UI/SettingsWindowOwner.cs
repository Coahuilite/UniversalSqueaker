using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using FerriteLib.UiKit.Kernel;
using HarmonyLib;

namespace UniversalSqueaker;

/// <summary>Native window lifetime and shell. Per-window page/FL state remains in its original owner.</summary>
internal sealed class SettingsWindowOwner
{
    private readonly VerseUiExecution shellExecution = new();
    private readonly SqueakerMod mod;
    private readonly GameSettingsPersistence persistence;
    private readonly HashSet<Window> settingsWindows = new();
    private readonly Dictionary<Type, FieldInfo?> optionsOwnerFields = new();
    private UI.GameSettingsWindow? activeSettingsWindow;
    // Native ordering, not Catalog activation: never close/recreate a live page to focus it.
    private static readonly FieldInfo? WindowList = AccessTools.Field(typeof(WindowStack), "windows");
    private static readonly MethodInfo? InsertWindow = AccessTools.Method(typeof(WindowStack), "InsertAtCorrectPositionInList", new[] { typeof(Window) });
    internal SettingsWindowOwner(SqueakerMod mod, GameSettingsPersistence persistence) { this.mod = mod; this.persistence = persistence; }
    internal static string BuildIdentity()
    {
        Assembly asm = typeof(SqueakerMod).Assembly;
        string informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "US.Footer.Build.Unknown".Translate();

#if US_DEV
        int plus = informational.IndexOf('+');
        if (plus >= 0 && plus + 1 < informational.Length)
        {
            string revision = informational[(plus + 1)..];
            if (revision.Length > 12)
            {
                revision = revision[..12];
            }

            return $"dev-{revision} ({informational})";
        }
#endif

        return informational;
    }



    /// <summary>
    /// Vanilla Mod Settings shell path. <see cref="Patches.Patch_RedirectModSettingsWindow"/> normally
    /// sends the framework to <see cref="UI.GameSettingsWindow"/>, so this body only runs
    /// when that redirect did not apply. It offers the in-house window instead of keeping a second
    /// settings implementation alive.
    /// </summary>
    internal void DrawShell(Rect inRect)
    {
        persistence.Tick();

        Rect body = inRect.ContractedBy(18f);
        UiTheme theme = UsTheme.Surface();
        Rect box = new Rect(body.x, body.y, body.width, Mathf.Min(96f, Mathf.Max(0f, body.height)));
        UiThemeDraw.Surface(shellExecution, VerseUiExecution.Rect(box), theme, theme[UiPaletteRole.PanelSurface], theme[UiPaletteRole.Border]);

        Rect inner = box.ContractedBy(12f);
        UiThemeDraw.Label(shellExecution, VerseUiExecution.Rect(new Rect(inner.x, inner.y, inner.width, 20f)), "US.Settings.Shell.Title".Translate(), theme, theme[UiPaletteRole.Foreground], UiFont.Small);
        UiThemeDraw.Label(shellExecution, VerseUiExecution.Rect(new Rect(inner.x, inner.y + 22f, inner.width, 34f)), "US.Settings.Shell.Body".Translate(SqueakLabels.SettingsCategory), theme, theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny);

        Rect open = new Rect(inner.x, inner.yMax - 28f, 220f, 28f);
        bool hovered = VerseUiExecution.Rect(open).Contains(shellExecution.CaptureFrame().Pointer);
        UiThemeDraw.Surface(shellExecution, VerseUiExecution.Rect(open), theme, hovered ? theme[UiPaletteRole.HoverSurface] : theme[UiPaletteRole.ControlSurface], theme[UiPaletteRole.Border]);
        UiThemeDraw.Label(shellExecution, VerseUiExecution.Rect(open), "US.Settings.Shell.Open".Translate(), theme, hovered ? theme[UiPaletteRole.Foreground] : theme[UiPaletteRole.SecondaryForeground], UiFont.Tiny, UiTextAnchor.MiddleCenter);
        // The context-free overload is deliberate here, and this is the only US call site left on it:
        // this button draws outside the kernel session (no UiWidgetContext exists for it), which is the
        // same exception FL documents for its own window chrome. Every in-tree control goes through
        // Button(rect, ctx) so an open popup can take the click.
        if (Widgets.ButtonInvisible(open))
        {
            OpenSettings();
        }
    }

    public void OpenSettings()
    {
        if (Find.WindowStack == null) return;
        if (activeSettingsWindow != null && Find.WindowStack.IsOpen(activeSettingsWindow))
        {
            try { ActivateExisting(Find.WindowStack, activeSettingsWindow); }
            catch (Exception error) { SqueakLog.SettingsOpenFailed(error); }
            return;
        }

        try
        {
            var window = new UniversalSqueaker.UI.GameSettingsWindow(mod);
            activeSettingsWindow = window;
            RegisterSettingsWindow(window);
            Find.WindowStack.Add(window);
        }
        catch (Exception ex)
        {
            activeSettingsWindow = null;
            SqueakLog.SettingsOpenFailed(ex);
        }
    }

    private static void ActivateExisting(WindowStack stack, Window window)
    {
        if (WindowList?.GetValue(stack) is List<Window> list && InsertWindow != null)
        {
            int prior = list.IndexOf(window);
            if (prior >= 0)
            {
                list.RemoveAt(prior);
                try { InsertWindow.Invoke(stack, new object[] { window }); }
                catch { if (!list.Contains(window)) list.Insert(Math.Min(prior, list.Count), window); throw; }
            }
        }
        else SqueakLog.NativeEntryUnavailable("NativeWindowActivationUnavailable:ExistingWindowRetained");
        // A higher-layer confirmation retains its native key ownership.
        if (stack.GetsInput(window)) stack.Notify_ManuallySetFocus(window);
    }

    internal void RegisterSettingsWindow(Window window)
    {
        settingsWindows.Add(window);
    }

    internal void NotifyClosing(Window window)
    {
        if (!IsOwnedSettingsWindow(window)) return;
        settingsWindows.Remove(window);
        if (ReferenceEquals(activeSettingsWindow, window)) activeSettingsWindow = null;
        persistence.Flush(true, true);
    }
    /// <summary>Accept only our registered dialog or a Dialog_Options instance whose stored Mod is this instance.</summary>
    internal bool IsOwnedSettingsWindow(Window window)
    {
        if (settingsWindows.Contains(window)) return true;
        try
        {
            Type type = window.GetType();
            if (!IsDialogOptionsType(type)) return false;
            if (!optionsOwnerFields.TryGetValue(type, out FieldInfo? ownerField))
            {
                ownerField = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(field => typeof(Mod).IsAssignableFrom(field.FieldType));
                optionsOwnerFields[type] = ownerField;
            }
            return ownerField?.GetValue(window) is Mod owner && ReferenceEquals(owner, mod);
        }
        catch { return false; }
    }

    private static bool IsDialogOptionsType(Type type)
    {
        for (Type? current = type; current != null; current = current.BaseType)
            if (current.FullName == "Verse.Dialog_Options" || current.FullName == "RimWorld.Dialog_Options") return true;
        return false;
    }

}
