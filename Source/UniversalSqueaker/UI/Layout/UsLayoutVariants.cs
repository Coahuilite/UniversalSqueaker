using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Consumer-side layout variants for the Schema=2 settings page, composed on the tree the carrier
/// hands back. This is the library's prescribed consumer path for composition ("compose it on the
/// tree"): the page declares one tree, and a variant is that same tree with one element omitted.
///
/// Why a variant instead of a binding: the carrier's declarative engine has no visible/width binding.
/// <c>UiLayoutEngine.IsHidden</c> reads a static <c>Hidden</c> attribute plus <c>Tab</c> compared to
/// <c>UiBindings.ActiveTabKey</c> - and the brief forbids using active-tab for help visibility -
/// while <c>ResolveColumnWidths</c> reads only the static <c>Width</c>/<c>Auto</c>/<c>MinWidth</c>/
/// <c>MaxWidth</c>. So the only honest way for US to retract the help column is to hand the engine a
/// different root list.
///
/// Keeping the drawer a REAL tree element in both variants is the point: the omitted/installed
/// <c>help-scroll</c> Scroll keeps its node (identity is element Id/kind/declared index), so its
/// scroll position, the fit audit and the hover-claim machine all keep working across a toggle.
/// </summary>
internal static class UsLayoutVariants
{
    /// <summary>
    /// Declared <c>Id</c> of the help drawer's Scroll element in <c>Layout.Schema2.xml</c>. The OPEN
    /// tree is the manifest as shipped; the CLOSED tree is that tree with this element omitted.
    /// </summary>
    internal const string HelpColumnId = "help-scroll";

    // One warning per process is the contract: a manifest whose root list cannot be swapped is a
    // programmer/version error, and repeating the line every revision would be the noise this
    // library-side audit surface exists to avoid.
    private static bool warnedUnsupportedRootList;

    /// <summary>
    /// Recursive clone of <paramref name="root"/> with the spec whose <c>Id</c> equals
    /// <paramref name="elementId"/> omitted. Id, Kind, attributes and the declared order of every
    /// surviving node are preserved: <see cref="UiElementSpec"/> is immutable, so a variant is a new
    /// spec tree, never a mutation of the shipped one. A root that IS the named element comes back
    /// unchanged, because a root list cannot express an empty variant that way.
    /// </summary>
    internal static UiElementSpec WithoutElement(UiElementSpec root, string elementId)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (string.IsNullOrEmpty(elementId)) return root;

        UiElementSpec? filtered = Filter(root, elementId);
        return filtered ?? root;
    }

    /// <summary>
    /// Installs <paramref name="roots"/> into the manifest's own root list. The property is typed
    /// <see cref="IReadOnlyList{T}"/> but <c>UiLayoutManifest.ParseCore</c> builds a
    /// <c>List&lt;UiElementSpec&gt;</c>; this refuses anything else rather than guessing an
    /// installation path that cannot exist. On refusal the caller keeps the shipped tree (drawer
    /// open) and exactly one warning is logged - fail-soft, never a throw.
    /// </summary>
    internal static bool TryReplaceRoots(
        UiLayoutManifest manifest,
        IReadOnlyList<UiElementSpec> roots,
        out string diagnostic)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        if (roots == null) throw new ArgumentNullException(nameof(roots));

        if (manifest.Roots is not List<UiElementSpec> list || list.Count != roots.Count)
        {
            diagnostic = "the manifest root list is not the mutable, same-length List<UiElementSpec> a"
                + " variant swap requires (runtime type " + manifest.Roots.GetType().Name + ", "
                + manifest.Roots.Count + " roots, variant " + roots.Count + " roots)";
            WarnOnce(diagnostic);
            return false;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            list[i] = roots[i];
        }

        diagnostic = "";
        return true;
    }

    /// <summary>
    /// Null means "this spec is the one the variant omits". A subtree with no omission anywhere comes
    /// back as the SAME immutable instance; otherwise the surviving children are re-wrapped in declared
    /// order. The change flag is propagated, not inferred from the top level: a nested omission (the
    /// drawer is two levels down, inside page-root/body-row) must rebuild every ancestor on its path.
    /// </summary>
    private static UiElementSpec? Filter(UiElementSpec spec, string elementId)
    {
        if (spec.Id.Length > 0 && string.Equals(spec.Id, elementId, StringComparison.Ordinal))
        {
            return null;
        }

        if (spec.Children.Count == 0)
        {
            return spec;
        }

        var children = new List<UiElementSpec>(spec.Children.Count);
        bool changed = false;
        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            UiElementSpec? filtered = Filter(child, elementId);
            if (filtered == null)
            {
                changed = true;
                continue;
            }

            if (!ReferenceEquals(filtered, child))
            {
                changed = true;
            }

            children.Add(filtered);
        }

        if (!changed)
        {
            return spec;
        }

        return new UiElementSpec(spec.Id, spec.Kind, spec.Attributes, children);
    }

    private static void WarnOnce(string diagnostic)
    {
        if (warnedUnsupportedRootList) return;
        warnedUnsupportedRootList = true;
        // SqueakLog is a closed event facade with no generic warning outlet, and it is outside this
        // task's ownership; this is the same Verse.Log.Warning convention the rest of the US UI code
        // uses for a recoverable, non-protocol defect (UsKernelOverlayController).
        Log.Warning(
            "[UniversalSqueaker] help drawer layout variant could not be installed; the shipped tree"
            + " (drawer open) is kept: " + diagnostic);
    }
}
