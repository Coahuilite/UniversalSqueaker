using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace UniversalSqueaker.UI;

/// <summary>
/// The <see cref="IUiBindings"/> operation a registered write key came through.
/// </summary>
public enum UsWriteBindingKind
{
    /// <summary><c>BindValue</c>: a typed value the page writes back.</summary>
    Value = 0,

    /// <summary><c>BindAction</c>: a payload-carrying action.</summary>
    Action = 1,

    /// <summary><c>BindCommand</c>: a payload-free command.</summary>
    Command = 2,
}

/// <summary>
/// One write binding exactly as the settings host registered it. See <see cref="UsWriteBindings"/>.
/// </summary>
public sealed class UsWriteBinding
{
    internal UsWriteBinding(string key, UsWriteBindingKind kind, bool itemScoped)
    {
        Key = key;
        Kind = kind;
        ItemScoped = itemScoped;
    }

    /// <summary>
    /// The registered key. An item-scoped family is recorded ONCE, as its template
    /// (<c>&lt;items&gt;.&lt;item&gt;.&lt;leaf&gt;</c>), because its concrete keys are a runtime projection
    /// of the catalog and no creation-time table can enumerate them.
    /// </summary>
    public string Key { get; }

    /// <summary>Which <see cref="IUiBindings"/> operation registered the key.</summary>
    public UsWriteBindingKind Kind { get; }

    /// <summary>True when <see cref="Key"/> is the template of a per-row family rather than a concrete key.</summary>
    public bool ItemScoped { get; }

    public override string ToString() => Key;
}

/// <summary>
/// The one funnel every write registration on the SETTINGS PAGE goes through, and the registry the
/// D1/D6 contract lane enumerates.
/// <para>
/// Why it exists: <see cref="IUiBindings"/> exposes no "enumerate every write key" surface, so before this
/// funnel the revision-clock lane hand-listed its keys - measured 2026-09-24 at 21 of the settings page's
/// 45 write registrations, which is a coverage number nothing could keep honest. Registration now records
/// itself, so "a new write key without a display-write probe" is a red lane instead of a silent gap.
/// </para>
/// <para>
/// MEASURED SCOPE (2026-09-24, HEAD 5e88904), with the units, because two different counts are both right:
/// the settings host makes <b>45 write registration call sites</b>, which resolve to <b>43 distinct literal
/// keys</b> plus <b>3 item-scoped templates</b> (<c>race-rows.&lt;item&gt;.select-domain</c>,
/// <c>xenotype-rows.&lt;item&gt;.select-domain</c>, <c>checklist-pack-keys.&lt;item&gt;.enabled</c>) =
/// <b>46 distinct registry keys</b>. Three templates rather than two because the single
/// <c>LayerRowBindings</c> registration site is executed by BOTH row families, each with its own items key.
/// A consumer of this registry therefore counts 46 keys where the source has 45 call sites. "Central" means
/// THE SETTINGS PAGE WRITE SURFACE: <c>UI/Diagnostics/UsDiagnosticsHost.cs</c> has its own 12 write registrations on its own host
/// and its own revision clock and is NOT in this funnel; it is the named next adopter, and the source guard
/// in <c>UiSourceInvariantTests</c> carries it as an explicit exemption with that reason rather than a
/// relaxed assertion.
/// </para>
/// <para>
/// This type is public because the kernel-host harness reads it out of the production assembly (the same
/// precedent as <c>UsTheme</c>) - it is the harness's read surface, NOT a consumer-facing API, and no
/// consumer outside this repository should bind to it.
/// </para>
/// </summary>
public sealed class UsWriteBindings
{
    private readonly IUiBindings bindings;
    private readonly List<UsWriteBinding> bound = new();
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);

    internal UsWriteBindings(IUiBindings bindings)
    {
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }

    /// <summary>The bindings surface the page is built on - what <c>UiHost</c> is constructed with.</summary>
    public IUiBindings Bindings => bindings;

    /// <summary>
    /// Every write key registered through this funnel, in registration order and one entry per key. An
    /// item-scoped family contributes its template once, not one entry per materialized row.
    /// </summary>
    public IReadOnlyList<UsWriteBinding> Bound => bound;

    /// <summary>Registers one writable value binding and records its key.</summary>
    public void Value<T>(string key, Func<T> get, Action<T> set)
    {
        bindings.BindValue(key, get, set);
        Record(key, UsWriteBindingKind.Value, false);
    }

    /// <summary>Registers one payload-carrying action binding and records its key.</summary>
    public void Action<T>(string key, Action<T> action)
    {
        bindings.BindAction(key, action);
        Record(key, UsWriteBindingKind.Action, false);
    }

    /// <summary>Registers one command binding and records its key.</summary>
    public void Command(string key, Action action)
    {
        bindings.BindCommand(key, action);
        Record(key, UsWriteBindingKind.Command, false);
    }

    /// <summary>Registers one item-scoped writable value binding and records its family template once.</summary>
    public void ItemValue<T>(string itemsKey, string itemKey, string leaf, Func<T> get, Action<T> set)
    {
        bindings.BindValue(ItemKey(itemsKey, itemKey, leaf), get, set);
        Record(ItemTemplate(itemsKey, leaf), UsWriteBindingKind.Value, true);
    }

    /// <summary>Registers one item-scoped action binding and records its family template once.</summary>
    public void ItemAction<T>(string itemsKey, string itemKey, string leaf, Action<T> action)
    {
        bindings.BindAction(ItemKey(itemsKey, itemKey, leaf), action);
        Record(ItemTemplate(itemsKey, leaf), UsWriteBindingKind.Action, true);
    }

    /// <summary>The concrete key of one item-scoped write, e.g. <c>race-rows.human.select-domain</c>.</summary>
    public static string ItemKey(string itemsKey, string itemKey, string leaf)
        => itemsKey + "." + itemKey + "." + leaf;

    /// <summary>The template one item-scoped family is recorded under, e.g. <c>race-rows.&lt;item&gt;.select-domain</c>.</summary>
    public static string ItemTemplate(string itemsKey, string leaf)
        => itemsKey + ".<item>." + leaf;

    private void Record(string key, UsWriteBindingKind kind, bool itemScoped)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("a write binding needs a key", nameof(key));
        }

        // One key, one entry: the engine refuses a duplicate registration itself, and an item-scoped family
        // registers once per materialized row while its template must appear exactly once.
        if (!seen.Add(key))
        {
            return;
        }

        bound.Add(new UsWriteBinding(key, kind, itemScoped));
    }
}
