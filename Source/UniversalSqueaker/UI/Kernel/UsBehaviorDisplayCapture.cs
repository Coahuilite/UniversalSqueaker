using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

// Only the concrete production sheet enrolls. Ordinary bindings remain registered and unchanged;
// this is an additional explicitly mapped, detached display input, never a binding-access proxy.
internal sealed class UsBehaviorDisplayCapture
{
    internal readonly struct Stamp : IEquatable<Stamp>
    {
        internal readonly long Data, Results, Sheet;
        private readonly int language;
        private readonly string selection, diagnostic, culture;
        internal Stamp(long data, long results, long sheet, int language, string selection, string diagnostic, string culture)
        { Data = data; Results = results; Sheet = sheet; this.language = language;
            this.selection = selection; this.diagnostic = diagnostic; this.culture = culture; }
        public bool Equals(Stamp other) => Data == other.Data && Results == other.Results && Sheet == other.Sheet
            && language == other.language && selection == other.selection && diagnostic == other.diagnostic && culture == other.culture;
        internal bool SameData(Stamp other) => Data == other.Data && language == other.language && culture == other.culture;
        internal bool SameShape(Stamp other) => Sheet == other.Sheet && selection == other.selection && diagnostic == other.diagnostic;
    }

    private readonly Func<Stamp> observe;
    private readonly Func<bool> active;
    private readonly Func<IReadOnlyList<string>> keys;
    private readonly Dictionary<string, Item> items = new(StringComparer.Ordinal);
    private Stamp accepted;
    private UiRegionSnapshot? snapshot;
    internal readonly UiRegionReadMap Reads = new UiRegionReadMap()
        .Value<IReadOnlyList<string>>("b-action-keys", UiRegionUse.Layout)
        .ItemValue<bool>("b-action-keys", "collapsed", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "context", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "details", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "editable", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "expanded", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "explanation", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "field-effective", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "field-group", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "field-group-first", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "field-help-context", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "field-is-boolean", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "field-is-interaction-seconds", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "field-is-interval", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "field-is-parameter", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "field-is-pitch", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "field-is-volume", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "field-note", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "field-provenance", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "field-title", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "field-topic", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "field-value", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "field-visible", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "footer", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "group", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "group-first", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "has-field-note", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "has-row-result", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "has-scope-hint", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "header", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "not-editable", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "payload", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "preset-anchor", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "row-result", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<BehaviorScope?>("b-action-keys", "scope", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "scope-display", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "scope-effective", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "source", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "source-controls-visible", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "source-status", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "source-technical", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "source-topic", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "source-visible", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "technical-key", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "timing-kind", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "timing-kind-choice", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<string>("b-action-keys", "title", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemValue<bool>("b-action-keys", "visible", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemChoices<string>("b-action-keys", "field-options", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemChoices<BehaviorScope?>("b-action-keys", "scope-options", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemChoices<string>("b-action-keys", "source-options", UiRegionUse.Layout | UiRegionUse.Display)
        .ItemChoices<string>("b-action-keys", "timing-kind-options", UiRegionUse.Layout | UiRegionUse.Display);
    internal UsBehaviorDisplayCapture(Func<Stamp> observe, Func<IReadOnlyList<string>> keys, Func<bool> active)
    { this.observe = observe; this.keys = keys; this.active = active; }

    internal Item Add(string key, bool footer, Func<bool> visible)
    {
        var item = new Item(footer, visible);
        items.Add(key, item);
        return item;
    }

    internal UiRegionSnapshot? CaptureCurrent()
    {
        // The actual enclosing page gate owns this domain. Registration may precede materialization;
        // do not enter the hidden page's business/item getters merely to certify an old packet.
        if (!active()) return null;
        Stamp current = observe(); // Every legal boundary; no leaf reads/factory on this return path.
        if (snapshot != null && current.Equals(accepted)) return snapshot;
        var ordered = keys();
        var packets = new UiRegionSnapshot[ordered.Count];
        for (int i = 0; i < ordered.Count; i++) packets[i] = items[ordered[i]].Capture(current);
        var frozenKeys = Array.AsReadOnly(ordered.ToArray());
        var next = UiRegionSnapshot.Create(values => {
            values.Value<IReadOnlyList<string>>("b-action-keys", frozenKeys);
            for (int i = 0; i < ordered.Count; i++) values.Item("b-action-keys", ordered[i], packets[i]);
        });
        accepted = current; snapshot = next;
        return next;
    }

    private interface ICell
    {
        bool Observe();
        void Write(UiRegionValues values);
        void Commit();
    }
    private sealed class ValueCell<T> : ICell
    {
        private readonly string key;
        private readonly Func<T> read;
        private readonly Func<bool> active;
        private T value = default!, pending = default!;
        private bool included, pendingIncluded, initialized;
        internal ValueCell(string key, Func<T> read, Func<bool> active)
        { this.key = key; this.read = read; this.active = active; }
        public bool Observe()
        {
            pendingIncluded = active();
            if (!pendingIncluded) return included;
            pending = read();
            return !included || !initialized || !EqualityComparer<T>.Default.Equals(value, pending);
        }
        public void Write(UiRegionValues values) { if (pendingIncluded) values.Value(key, pending); }
        public void Commit()
        {
            included = pendingIncluded;
            if (included) { value = pending; initialized = true; }
        }
    }
    private sealed class ChoiceCell<T> : ICell
    {
        private readonly string key;
        private readonly Func<IReadOnlyList<UiChoice<T>>> read;
        private readonly Func<bool> active;
        private bool included, pendingIncluded;
        private IReadOnlyList<UiChoice<T>>? value, pending;
        internal ChoiceCell(string key, Func<IReadOnlyList<UiChoice<T>>> read, Func<bool> active)
        { this.key = key; this.read = read; this.active = active; }
        public bool Observe()
        {
            pendingIncluded = active();
            if (!pendingIncluded) return included;
            var next = read();
            bool changed = value == null || !value.SequenceEqual(next);
            pending = changed ? Array.AsReadOnly(next.ToArray()) : value;
            return !included || changed;
        }
        public void Write(UiRegionValues values) { if (pendingIncluded) values.Choices(key, pending!); }
        public void Commit() { included = pendingIncluded; if (included) value = pending; }
    }
    internal sealed class Item
    {
        private readonly bool footer;
        private readonly Func<bool> visible;
        private readonly List<ICell> cells = new();
        private Stamp accepted;
        private bool wasVisible;
        private UiRegionSnapshot? snapshot;
        internal Item(bool footer, Func<bool> visible)
        { this.footer = footer; this.visible = visible; }
        internal void Value<T>(string key, Func<T> read, Func<bool> active)
        {
            cells.Add(new ValueCell<T>(key, read, active));
        }
        internal void Choices<T>(string key, Func<IReadOnlyList<UiChoice<T>>> read, Func<bool> active)
        {
            cells.Add(new ChoiceCell<T>(key, read, active));
        }
        internal UiRegionSnapshot Capture(Stamp current)
        {
            if (snapshot != null && current.SameData(accepted) && current.SameShape(accepted)
                && (!footer || current.Results == accepted.Results)) return snapshot;
            bool currentVisible = visible(); // The real parent gate, before any child read.
            bool changed = snapshot == null || currentVisible != wasVisible;
            if (currentVisible)
                foreach (var cell in cells) changed |= cell.Observe();
            if (changed)
                snapshot = UiRegionSnapshot.Create(values => {
                    if (currentVisible) foreach (var cell in cells) cell.Write(values);
                    else values.Value("visible", false);
                });
            if (currentVisible) foreach (var cell in cells) cell.Commit();
            accepted = current; wasVisible = currentVisible;
            return snapshot!;
        }
    }
}
