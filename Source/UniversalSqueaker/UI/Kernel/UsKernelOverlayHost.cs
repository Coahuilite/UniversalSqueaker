using System;
using System.IO;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>
/// Production overlay Host adapter — the mod's second UiKit host, fully independent of the
/// Settings Window. It reads the real embedded Schema=2 overlay resource, resolves the
/// <c>us/camera-readout</c> kind through the shared <see cref="UiWidgetRegistry"/> (same US scope,
/// same <see cref="UsKernelWidgetRegistrar"/> as the settings host), builds the typed binding table
/// against the narrow <see cref="IUsKernelOverlaySource"/> boundary, and uses the same
/// <see cref="UiTheme.DarkGold"/> theme, <see cref="VerseFerriteTextMetrics"/> metrics and
/// translation mechanism as the settings host. Any schema/kind/binding error fails here at
/// creation time; <see cref="UsKernelOverlayController"/> treats that as a permanent fallback to
/// the legacy pure-Verse readout for the game session.
/// </summary>
public static class UsKernelOverlayHost
{
    private const string Source = "coahuilite.universalsqueaker";
    private const string ManifestResourceName = "UniversalSqueaker.UI.Layout.Overlay.Schema2.xml";

    public static UiHost Create(IUsKernelOverlaySource source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        UsKernelWidgetRegistrar.EnsureRegistered();

        UiLayoutManifest manifest = UiLayoutManifest.Parse(ReadManifest());

        var bindings = new UiBindings();
        bindings.BindReadOnly<string>("camera-readout", () => source.ReadoutText);

        return new UiHost(
            Source,
            manifest,
            bindings,
            UiTheme.DarkGold,
            VerseFerriteTextMetrics.Instance,
            new UsKernelTranslation());
    }

    private static string ReadManifest()
    {
        using Stream? stream = typeof(UsKernelOverlayHost).Assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded Schema=2 overlay resource '{ManifestResourceName}' was not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>Shared RimWorld translation seam for both kernel hosts (settings + overlay).</summary>
internal sealed class UsKernelTranslation : IUiTranslation
{
    public string Translate(string key)
    {
        return key.Translate();
    }
}
