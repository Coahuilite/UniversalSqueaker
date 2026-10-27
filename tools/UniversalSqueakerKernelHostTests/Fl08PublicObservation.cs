using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace UniversalSqueaker.KernelHostTests;

// Read actual public trace/diagnostic output, never the carrier's private runtime tables.
internal static class Fl08PublicObservation
{
    // Release has no Dev geometry capture. These existing fixtures locate declared elements from
    // public arranged nodes and scroll state; the native press/release still decides the outcome.
    internal static UiLayoutRect NodeWindowRect(UiHost host, UiNode node)
    {
        var rect = node.Rect ?? throw new InvalidOperationException("No arranged rectangle: " + node.Path);
        UiLayoutPoint offset = host.Session.HostViewport.Position;
        for (UiNode? parent = node.Parent; parent != null; parent = parent.Parent)
            if (parent.Kind == "Scroll") offset -= host.Session.GetScrollPosition(parent);
        return new UiLayoutRect(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height);
    }
    internal static UiLayoutPoint NodeWindowOrigin(UiHost host, UiNode node)
    {
        UiNode? scope = node.Parent;
        while (scope != null && scope.Kind != "Scroll" && scope.Kind != "Clip") scope = scope.Parent;
        if (scope == null) return UiLayoutPoint.Zero;
        UiLayoutPoint origin = NodeWindowRect(host, scope).Position;
        if (scope.Kind == "Scroll") origin -= host.Session.GetScrollPosition(scope);
        return origin;
    }
    internal sealed class PaintedControl
    {
        internal string ElementId = "", Path = "", ControlKey = "";
        internal UiLayoutRect Rect; // Last actual text paint, an interior click point, NOT an input-query rectangle.
    }
    // Forward to the SAME native adapter. Record only the public control-allocation and text-paint
    // outlets used by the existing directory/fallback fixtures, whose US helpers paint before activation.
    internal sealed class PaintedControlExecution : IUiExecution
    {
        private readonly UiHost host;
        private readonly VerseUiExecution native;
        private UiLayoutRect lastText;
        internal readonly List<PaintedControl> Controls = new();
        internal PaintedControlExecution(UiHost host, VerseUiExecution native) { this.host = host; this.native = native; }
        internal void BeginPass() { Controls.Clear(); lastText = default; }
        public void PrepareControl(UiControlKey key)
        {
            var node = host.Session.GetNode(host.Session.ActiveElement)!;
            Controls.Add(new PaintedControl { ElementId = node.ElementId, Path = node.Path, ControlKey = key.Key, Rect = lastText });
            native.PrepareControl(key);
        }
        public void Text(UiLayoutRect rect, string text, UiFont font, UiColour colour, UiTextAnchor anchor, bool wrap)
        {
            var node = host.Session.GetNode(host.Session.ActiveElement)!;
            var origin = NodeWindowOrigin(host, node);
            lastText = new UiLayoutRect(rect.X + origin.X, rect.Y + origin.Y, rect.Width, rect.Height);
            native.Text(rect, text, font, colour, anchor, wrap);
        }
        public bool HasCapture(UiControlKey key) => native.HasCapture(key);
        public bool CaptureAvailable => native.CaptureAvailable;
        public void Capture(UiControlKey key) => native.Capture(key);
        public void Release(UiControlKey key) => native.Release(key);
        public bool HasFocus(UiControlKey key) => native.HasFocus(key);
        public void ReleaseFocus(UiControlKey key) => native.ReleaseFocus(key);
        public UiNativeEditResult Edit(UiNativeEdit request) => native.Edit(request);
        public void ConsumeEvent() => native.ConsumeEvent();
        public void WithState(Action action) => native.WithState(action);
        public void Scroll(UiLayoutRect viewport, UiLayoutRect content, UiLayoutPoint position, bool showScrollbar, Action<UiLayoutPoint> body) => native.Scroll(viewport, content, position, showScrollbar, body);
        public void Warning(string message) => native.Warning(message);
        public void Clip(UiLayoutRect bounds, Action body) => native.Clip(bounds, body);
        public void Fill(UiLayoutRect rect, UiColour colour) => native.Fill(rect, colour);
        public void Line(UiLayoutPoint from, UiLayoutPoint to, UiColour colour, float width) => native.Line(from, to, colour, width);
        public void Image(UiLayoutRect rect, UiImage image, UiColour tint) => native.Image(rect, image, tint);
        public void Tooltip(UiLayoutRect rect, string text) => native.Tooltip(rect, text);
    }
    private sealed class Record
    {
        internal UiHitLayer? Popup;
        internal VerseUiExecution? Execution;
        internal readonly HashSet<string> Recoveries = new();
    }
    private static readonly ConditionalWeakTable<UiSession, Record> records = new();
    private static readonly List<VerseUiExecution> executions = new();
    internal static VerseUiExecution ExecutionFor(UiHost host)
    {
        var record = records.GetOrCreateValue(host.Session);
        if (record.Execution == null) { record.Execution = new VerseUiExecution(); executions.Add(record.Execution); }
        return record.Execution;
    }
    // The existing image fixture supplies its own explicit owner; it disposes that owner itself.
    internal static void UseExecution(UiHost host, VerseUiExecution execution)
        => records.GetOrCreateValue(host.Session).Execution = execution;
    internal static void DisposeExecutions()
    {
        foreach (var execution in executions) execution.Dispose();
        executions.Clear();
    }
    internal static bool Popup(UiSession session, out UiHitLayer layer)
    {
        layer = default;
        if (!records.TryGetValue(session, out var record) || !record.Popup.HasValue || session.OpenPopupId == null) return false;
        layer = record.Popup.Value;
        return layer.PopupOwnerId == session.OpenPopupId;
    }
    internal static string[] Recoveries(UiSession session)
    {
        var record = records.GetOrCreateValue(session);
        // Also detect a deliberate trip or a measure failure before this helper's first draw.
        var observed = UiHost.ObserveLiveHost(session.Identity, new UiObservationBudget(512, 32));
        if (observed.Truncated) throw new InvalidOperationException("Trip observation exceeded its public node budget");
        foreach (var node in observed.Nodes)
        {
            string? id = node.Facts.FirstOrDefault(x => x.Name == "identity")?.Value;
            var actual = id == null ? null : session.GetNodeByElementId(id);
            if (actual != null && session.IsTripped(actual)) {
                session.TryGetTripLog(actual, out string text); record.Recoveries.Add(node.Path + ": " + text);
            }
        }
        return record.Recoveries.ToArray();
    }
    internal static void Draw(UiHost host, Rect viewport)
    {
        var record = records.GetOrCreateValue(host.Session);
        record.Popup = null;
        var prior = host.Session.Controls.Trace;
        host.Session.Controls.Trace = text => {
            prior?.Invoke(text);
            var match = Regex.Match(text, @"^publish id=(.*?) rect=\(?([-0-9.,]+)\)? firstRow=");
            if (!match.Success) return;
            float[] v = match.Groups[2].Value.Split(new[] { ',' }).Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            string id = match.Groups[1].Value;
            // These two US composites declare their dropdowns inside their own widget;
            // ordinary default dropdowns have their own declared element id.
            string ownerId = id.StartsWith("scope-tree-", StringComparison.Ordinal) ? "scope-tree"
                : id == "pack-filter" ? "filter-bar" : id;
            var owner = host.Session.GetNodeByElementId(ownerId);
            if (owner == null) {
                var declarations = BoundDropdowns(host.Manifest.Roots, id).ToArray();
                if (declarations.Length == 1) owner = host.Session.GetNodeByElementId(declarations[0]);
            }
            owner = owner
                ?? throw new InvalidOperationException("No declared US owner for observed popup " + id);
            // US composites now own each popup through their public named child context.
            if (ownerId == "scope-tree" || ownerId == "filter-bar")
                owner = host.CreateContext(viewport.width).WithNode(owner).Child(id).Node;
            record.Popup = new UiHitLayer(owner, new UiLayoutRect(v[0], v[1], v[2], v[3]), true, id);
        };
        var previous = Event.current;
        if (previous == null) Event.current = new Event { type = EventType.Repaint, mousePosition = new Vector2(-1000, -1000) };
        try {
            var execution = ExecutionFor(host);
            host.DrawFrame(VerseUiExecution.Rect(viewport), execution.CaptureFrame(), execution);
        }
        finally {
            host.Session.Controls.Trace = prior;
            Event.current = previous;
        }
    }
    private static IEnumerable<string> BoundDropdowns(IReadOnlyList<UiElementSpec> specs, string binding)
    {
        foreach (var spec in specs) {
            if (spec.Kind == UiKinds.Dropdown && spec.TryGetAttribute("Bind", out string value) && value == binding) yield return spec.Id;
            foreach (var id in BoundDropdowns(spec.Children, binding)) yield return id;
        }
    }
}
