using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

// Existing Program.cs R4-B case and its helpers, moved into the current public-Execution runner.
// Business assertions are retained; only the native/neutral adapter and image-owner plumbing migrate.
internal static partial class Program
{
    private static void PresetRowsComposeThroughTheSharedBand()
    {
        var viewport = new Rect(0f, 0f, 1024f, 768f);

        // --- (1) the icon path: the band reserves the picture's height, so the card grows.
        // The picture is deliberately far taller than a text band, so the clause tests "the picture's own
        // height was reserved" and not a coincidence of this viewport's wording.
        using var imageExecution = new VerseUiExecution();
        var texture = new Texture2D(64, 200);
        var withIcon = new RecordingSettingsSource { RichData = true, XenotypeIcon = imageExecution.Images.Add(texture) };
        using (UiHost host = UsKernelSettingsHost.Create(withIcon, new Program.StubMetrics()))
        {
            Fl08PublicObservation.UseExecution(host, imageExecution);
            host.Bindings.Invoke("set-tab", "Presets");
            var drawnTextures = (IList)(typeof(GUI).GetField("DrawTextureRects")?.GetValue(null)
                ?? throw new InvalidOperationException("the texture draw recorder is unavailable"));
            drawnTextures.Clear();
            host.DrawChecked(viewport);
            float withIconHeight = PresetCardHeight(host, viewport);
            Assert(drawnTextures.Count == 1 && drawnTextures[0] is Rect imageRect
                && Math.Abs(imageRect.height - 200f) < 0.1f,
                "the 200px fixture image must use its reserved draw height, not the old text-row height");

            // The same model with no icon: the null fallback must draw, and must not reserve a picture.
            withIcon.XenotypeIcon = default;
            host.Bindings.BindingOwner.NotifyChanged("baseline-presets");
            // Direct fixture mutation bypasses the production write boundary's revision bump.
            host.Session.BumpContentRevision();
            host.DrawChecked(viewport);
            float noIconHeight = PresetCardHeight(host, viewport);
            Assert(noIconHeight < withIconHeight - 1f,
                "a row carrying a picture must reserve the picture's own height through the shared band"
                + " (with icon " + withIconHeight + ", without " + noIconHeight + "): a null icon is the"
                + " band's documented fallback, not a smaller band");
        }

        // --- (2) the composition's checkbox is the card's own right-end slot, and it is a PART target.
        var driven = new RecordingSettingsSource { RichData = true };
        using (UiHost host = UsKernelSettingsHost.Create(driven, new Program.StubMetrics()))
        {
            host.Bindings.Invoke("set-tab", "Presets");
            ClearDrawBoxSolidCalls();
            host.DrawChecked(viewport);
            Rect card = PresetCard(host, viewport);
            UiLayoutRect scroll = host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(viewport.width, viewport.height))).RectById["content-scroll"];

            float boxX = card.xMax - scroll.X - UsCardLayout.Padding - (UsKernelDraw.ControlColumnRightInset + UsKernelDraw.CheckboxVisual);
            Rect? box = DrawnBoxAtX(boxX);
            Assert(box.HasValue,
                "the composed row must paint its checkbox in the card's right-end control column (expected x "
                + boxX + "); without it the migration lost the checkbox");

            driven.LastBaselineRace = null;
            Rect windowBox = box!.Value;
            windowBox.x += scroll.X;
            windowBox.y += scroll.Y;
            PressAt(host, viewport, windowBox);

            Assert(driven.LastBaselineRace == "human" && driven.LastBaselineRaceSelected == false,
                "a press on the drawn checkbox must write the row's own typed toggle (got race '"
                + (driven.LastBaselineRace ?? "(none)") + "', selected " + driven.LastBaselineRaceSelected + ")");
            Assert(driven.LastBaselinePresetToggle == null,
                "and it must be the BOX part, not the row body: expansion must not move with selection");
        }

        // --- (3) expansion reserves descendants, and collapsing reserves none.
        var expandable = new RecordingSettingsSource
        {
            RichData = true,
            PresetToggleFlipsExpandedState = true
        };
        using (UiHost host = UsKernelSettingsHost.Create(expandable, new Program.StubMetrics()))
        {
            host.Bindings.Invoke("set-tab", "Presets");
            host.DrawChecked(viewport);
            float expandedHeight = PresetCardHeight(host, viewport);
            Assert(expandable.PresetExpanded, "the fixture starts expanded");

            host.Bindings.Invoke("toggle-baseline-preset", "us.preset1");
            host.Bindings.BindingOwner.NotifyChanged("baseline-presets");
            host.DrawChecked(viewport);
            float collapsedHeight = PresetCardHeight(host, viewport);
            Assert(!expandable.PresetExpanded, "the toggle write flipped the model's expanded answer");
            Assert(collapsedHeight < expandedHeight - 1f,
                "hidden descendants must take NO layout: expanded " + expandedHeight + " vs collapsed "
                + collapsedHeight);
        }

        // --- (4) per host: the expanded answer belongs to the host's own model instance.
        var first = new RecordingSettingsSource { RichData = true, PresetToggleFlipsExpandedState = true };
        var second = new RecordingSettingsSource { RichData = true, PresetToggleFlipsExpandedState = true };
        using (UiHost hostA = UsKernelSettingsHost.Create(first, new Program.StubMetrics()))
        using (UiHost hostB = UsKernelSettingsHost.Create(second, new Program.StubMetrics()))
        {
            hostA.Bindings.Invoke("set-tab", "Presets");
            hostB.Bindings.Invoke("set-tab", "Presets");
            hostA.DrawChecked(viewport);
            hostB.DrawChecked(viewport);
            float beforeA = PresetCardHeight(hostA, viewport);

            hostA.Bindings.Invoke("toggle-baseline-preset", "us.preset1");
            hostA.Bindings.BindingOwner.NotifyChanged("baseline-presets");
            hostA.DrawChecked(viewport);
            hostB.DrawChecked(viewport);
            float afterA = PresetCardHeight(hostA, viewport);
            float afterB = PresetCardHeight(hostB, viewport);

            Assert(!first.PresetExpanded && second.PresetExpanded,
                "the collapse landed on host A's own model only");
            Assert(afterA < beforeA - 1f && Math.Abs(afterB - beforeA) < 0.01f,
                "host B must keep its expanded layout when host A collapses (A " + beforeA + " -> " + afterA
                + ", B " + afterB + ")");
        }
    }

    private static Rect PresetCard(UiHost host, Rect viewport)
    {
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(VerseUiExecution.Point(new Vector2(viewport.width, viewport.height)));
        Assert(snapshot.RectById.TryGetValue("preset-list", out UiLayoutRect card),
            "the Presets workspace must place the preset-list card");
        return VerseUiExecution.Rect(card);
    }

    private static float PresetCardHeight(UiHost host, Rect viewport)
    {
        return PresetCard(host, viewport).height;
    }

    private static Rect? DrawnBoxAtX(float x)
    {
        IList rects = Recorded("DrawBoxSolidRects");
        Rect? found = null;
        foreach (object? entry in rects)
        {
            if (entry is not Rect rect) continue;
            if (Math.Abs(rect.width - UsKernelDraw.CheckboxVisual) > 0.1f
                || Math.Abs(rect.height - UsKernelDraw.CheckboxVisual) > 0.1f) continue;
            if (Math.Abs(rect.x - x) > 0.5f) continue;
            if (!found.HasValue || rect.y < found.Value.y) found = rect;
        }

        return found;
    }

    private static void PressAt(UiHost host, Rect viewport, Rect target)
    {
        try
        {
            foreach (EventType phase in new[] { EventType.MouseDown, EventType.MouseUp })
            {
                Event raised = Event.KeyboardEvent("");
                raised.type = phase;
                raised.button = 0;
                raised.mousePosition = new Vector2(target.x + target.width / 2f, target.y + target.height / 2f);
                Event.current = raised;
                host.DrawChecked(viewport);
            }
        }
        finally
        {
            Event.current = null;
        }
    }

    private static IList Recorded(string fieldName)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException("the runtime stub does not record '" + fieldName + "'");
        }

        return field.GetValue(null) as IList
            ?? throw new InvalidOperationException("the stub's '" + fieldName + "' recorder is not a list");
    }

    private static void ClearDrawBoxSolidCalls()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod(
            "ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null) throw new InvalidOperationException("the runtime stub does not expose ClearDrawBoxSolidCalls");
        clear.Invoke(null, null);
    }
    private static void Assert(bool condition, string message) => Check(condition, message);
}
