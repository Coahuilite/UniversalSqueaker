using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniversalSqueaker.UI;
using System.IO;
using System.Reflection;
using System.Xml;

namespace UniversalSqueaker.UiLogicTests;

/// <summary>
/// Zero-Verse source-level invariants for the production Schema2 UI path. The old widget tree
/// (per-page legacy classes, the neutral SelectionButton/Palette/SurfaceFrame/UiText helpers and
/// the UiInteract command authority) was deleted together with its production types; this gate
/// now pins what must stay true on the surviving Schema2 path:
///   1. The settings window stays a thin UiWindowHost consumer (FL P2): no chrome and no second
///      failure state machine may re-grow on this side, and no legacy second-page fallback symbol
///      may return.
///   2. No file under UI/ (widgets included) revives the deleted second event authority or the
///      second palette/surface/text helper set.
///   3. The widget kinds UsKernelWidgetRegistrar registers are exactly the us/* kinds the two
///      Schema2 manifests reference (both directions, parsed — not substring-guessed).
///   4. The only embedded UI manifests are the two Schema2 files.
///   5. The help catalog, the manifests' HelpKey attributes, the section help-key map and the
///      panel's item-count height formula agree with each other (no manifest/catalog drift in
///      either direction, no unreachable or empty catalog entries).
///   6. Localization contract: the two shipped Keyed tables carry the identical key set with
///      identical `{n}` placeholder multisets, every Keyed reference made by the production UI
///      resolves in BOTH languages, and the shipped manifests never pass literal English prose
///      through Title/Caption/Text attributes.
/// These are NARROW STRUCTURAL GUARDS: they fail loudly when a pinned contract disappears, but
/// behavioural proof lives in the UiKit/kernel-host harnesses.
/// </summary>
internal static class UiSourceInvariantTests
{
    public static void RunAll()
    {
        string root = RepoRoot();
        VerifySettingsWindowFailureModel(root);
        VerifyNoSecondEventAuthorityOrPalette(root);
        VerifyRegistrarKindsMatchManifests(root);
        VerifyEmbeddedManifestsAreSchema2Only(root);
        VerifyHelpCatalogManifestConsistency(root);
        VerifyHelpPanelWiringAndHeightFormula(root);
        VerifyHoverClaimsMatchCatalogItems(root);
        VerifyLocalizationContract(root);
        VerifyPrerequisiteDesyncIsNamed(root);
        VerifyViewCacheSharesLayoutClock(root);
        VerifyWriteBindingsGoThroughTheRegistry(root);
        VerifyHelpDrawerIsIndependentState(root);
    }

    // 8. Prerequisite desync is named, not a draw-time TypeLoadException (the 2026-09-04 incident):
    //    the Mod constructor records the Require verdict and the settings window short-circuits on it.
    private static void VerifyPrerequisiteDesyncIsNamed(string root)
    {
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "Mod.cs"),
            new[] { "PrerequisiteVerified = FerriteLibVersion.Require(" },
            "Mod must record the Require verdict in PrerequisiteVerified");
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UniversalSqueakerSettingsWindow.cs"),
            new[] { "UniversalSqueakerMod.PrerequisiteVerified" },
            "the settings window must short-circuit on an unverified prerequisite");
    }

    // 9. The production view cache must key on the session content revision - the same clock the
    // layout cache invalidates on. A Time.frameCount-keyed cache let a popup-pass write arrange
    // against the stale view and draw the fresh view into the stale snapshot until the next bump
    // (the 2026-09-04 filter misalignment that only a workspace switch healed).
    private static void VerifyViewCacheSharesLayoutClock(string root)
    {
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UsKernelSettingsSource.cs"),
            new[] { "AttachRevisionSource", "cachedRevision" },
            "the production view cache must key on the session content revision");
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UsKernelSettingsHost.cs"),
            new[] { "AttachRevisionSource(() => host.Session.ContentRevision)" },
            "the host must wire the view cache to the session revision");
    }

    // 10. Write registrations go through the ONE funnel (adoption plan section 9 item 6 / P3-2a).
    //     IUiBindings exposes no full write-key enumeration, so the settings page registers every write key
    //     through UsWriteBindings, which records them and lets the kernel-host revision-clock lane
    //     enumerate the whole write set - as a hand-list that lane covered 21 of the 45 registration sites.
    //     This guard is the other half: a RAW .BindValue/.BindAction/.BindCommand under UI/ is the bypass
    //     that would register a write key no lane can see, so it fails here by file and count. The
    //     exemption below is a NAME plus a REASON plus a pinned count, never a relaxed assertion, and both
    //     directions are checked: a vanished funnel file and a vanished exemption file each fail the guard.
    private static void VerifyWriteBindingsGoThroughTheRegistry(string root)
    {
        string ui = Path.Combine(root, "Source", "UniversalSqueaker", "UI");
        string funnel = Path.Combine(ui, "Kernel", "UsWriteBindings.cs");
        string panelExemption = Path.Combine(ui, "Diagnostics", "UsDiagnosticsHost.cs");

        Assert(File.Exists(funnel),
            "the write-binding funnel UI/Kernel/UsWriteBindings.cs is missing: every settings-page write "
            + "registration is supposed to go through it, and without it no lane can enumerate the write set");
        Assert(File.Exists(panelExemption),
            "the named write-registration exemption UI/Diagnostics/UsDiagnosticsHost.cs is missing; a vanished "
            + "exemption file must fail this guard instead of silently widening it");

        // EXEMPT, by name and with its reason: the diagnostics panel owns a SECOND host and its own
        // DiagRevisionBumper clock, so its registrations are not settings-page write keys, and folding them
        // into the settings funnel is the next adopter's work. The count is pinned so a new registration
        // there is a deliberate act (re-cut this number in that batch) rather than an invisible write key.
        int exempt = CountWriteRegistrations(File.ReadAllText(panelExemption));
        Assert(exempt == 12,
            "UI/Diagnostics/UsDiagnosticsHost.cs is expected to carry 12 write registrations (its own host and "
            + "its own revision clock, exempt from the settings-page funnel), got " + exempt
            + " - re-cut this pin deliberately in the batch that changes the panel's write surface");

        int funnelOperations = CountWriteRegistrations(File.ReadAllText(funnel));
        Assert(funnelOperations == 5,
            "the funnel registers through exactly five IUiBindings operations (Value, Action, Command, "
            + "ItemValue, ItemAction), got " + funnelOperations
            + " - re-cut this pin in the batch that adds a sixth");

        var offenders = new List<string>();
        foreach (string file in Directory.EnumerateFiles(ui, "*.cs", SearchOption.AllDirectories))
        {
            if (string.Equals(file, funnel, StringComparison.OrdinalIgnoreCase)
                || string.Equals(file, panelExemption, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int hits = CountWriteRegistrations(File.ReadAllText(file));
            if (hits > 0)
            {
                offenders.Add(file.Substring(ui.Length + 1).Replace('\\', '/') + " (" + hits + ")");
            }
        }

        Assert(offenders.Count == 0,
            "write registrations must go through UsWriteBindings: a raw IUiBindings write call is a write key "
            + "no lane can enumerate, so the revision-clock contract would silently stop covering it. "
            + "Offenders: " + string.Join(", ", offenders));
    }

    private static int CountWriteRegistrations(string text)
    {
        return CountOccurrences(text, ".BindValue")
            + CountOccurrences(text, ".BindAction")
            + CountOccurrences(text, ".BindCommand");
    }

    private static int CountOccurrences(string text, string fragment)
    {
        int count = 0;
        int at = 0;
        while (true)
        {
            int hit = text.IndexOf(fragment, at, StringComparison.Ordinal);
            if (hit < 0)
            {
                return count;
            }

            count++;
            at = hit + fragment.Length;
        }
    }

    // 1. Settings window: since FL P2 the chrome and the whole-frame failure state machine belong to
    //    the library shell, so the consumer-side contract is the INVERSE - the window must stay a thin
    //    UiWindowHost subclass and must not re-grow chrome or a second failure model. A re-added
    //    DrawBackground/DrawCloseButton/pageUnavailable here is exactly the regression this pins.
    private static void VerifySettingsWindowFailureModel(string root)
    {
        string path = SettingsWindowPath(root);
        CheckSourceContains(
            path,
            new[]
            {
                "public sealed class UniversalSqueakerSettingsWindow : UiWindowHost",
                "protected override UiHost CreateHost()",
                "protected override void DrawNotice(Rect rect, UiWindowNotice notice)",
                "protected override bool PrerequisiteVerified => UniversalSqueakerMod.PrerequisiteVerified;",
                "protected override Func<Vector2>? InitialSizePolicy",
                "UiThemeDraw.Surface(",
            },
            "the settings window must be a thin UiWindowHost consumer: it supplies the page, the "
            + "notices and its own size policy, and routes the prerequisite desync through the shell");

        foreach (string shellState in new[]
        {
            "private bool pageUnavailable", "private bool noticeDueNextFrame", "DrawBackground(",
            "DrawTitleBar(", "DrawCloseButton(", "Widgets.ButtonInvisible", "Mouse.IsOver",
        })
        {
            CheckSourceDoesNotContain(path, shellState,
                "chrome and the next-frame trip are the shell's (FL P2 conditions a-d); the window "
                + "must not re-grow them: " + shellState);
        }

        string[] forbiddenLegacySymbols =
        {
            "FerriteVoicePacksPage",
            "VanillaVoicePacksPage",
            "DrawLegacyPage",
            "UsesLegacySettingsSession",
            "BeginSettingsSession",
            "EndSettingsSession",
            "RequestXenotypeTabOnNextDraw",
            "ClearXenotypeTabRequest",
        };
        foreach (string symbol in forbiddenLegacySymbols)
        {
            CheckSourceDoesNotContain(path, symbol,
                "the legacy whole-page fallback was deleted; it must not return in the settings window");
        }

        // The legacy select-xenotype-tab request path was deleted from both sides of the entry
        // point: OpenSettings() takes no parameters and the settings class keeps no tab request.
        string modPath = Path.Combine(root, "Source", "UniversalSqueaker", "Mod.cs");
        CheckSourceContains(modPath, new[] { "public void OpenSettings()" },
            "the settings entry point is the parameterless OpenSettings()");
        CheckSourceDoesNotContain(modPath, "selectXenotypeTab",
            "the deleted xenotype-tab request parameter must not return");
        string settingsClass = Path.Combine(root, "Source", "UniversalSqueaker", "Settings", "UniversalSqueakerSettings.cs");
        CheckSourceDoesNotContain(settingsClass, "RequestXenotypeTabOnNextDraw",
            "the settings class must not regain a xenotype-tab request API");
        CheckSourceDoesNotContain(settingsClass, "ClearXenotypeTabRequest",
            "the settings class must not regain a xenotype-tab clear API");
    }

    // 2. Second event authority / second palette must not resurrect anywhere under UI/. The list is the
    // one MEMORY's "Naming/harness bans" claims: `UiPanel` was reported as prose-only in FL→US round 2
    // (it was a real deleted type in the old chain - `Widgets/UiPanel.cs` - but sat in no source list),
    // so the name joined the scan rather than leaving the rule. Over-banning a dead name is the safe
    // direction; a memory that promises a check the check does not make is not.
    private static void VerifyNoSecondEventAuthorityOrPalette(string root)
    {
        string uiDir = Path.Combine(root, "Source", "UniversalSqueaker", "UI");
        string[] forbiddenTypes = { "UiInteract", "Palette", "SurfaceFrame", "UiText", "UiValueStore", "UiPanel" };

        foreach (string file in Directory.EnumerateFiles(uiDir, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            foreach (string forbidden in forbiddenTypes)
            {
                // Word-boundary-ish check: "UiTextElement"/"PaletteToken" would also be a revival,
                // so a plain ordinal hit is the correct (stricter) semantics here.
                if (text.IndexOf(forbidden, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException(
                        "deleted UiKit type '" + forbidden + "' reappears in production UI source: " + file);
                }
            }
        }
    }

    // 3. Registrar kind set == us/* kinds parsed from the two Schema2 manifests.
    private static void VerifyRegistrarKindsMatchManifests(string root)
    {
        string uiKernel = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel");
        string registrar = File.ReadAllText(Path.Combine(uiKernel, "UsKernelWidgetRegistrar.cs"));

        var registeredKinds = new HashSet<string>(StringComparer.Ordinal);
        int search = 0;
        const string marker = ".Register()";
        while (true)
        {
            int hit = registrar.IndexOf(marker, search, StringComparison.Ordinal);
            if (hit < 0) break;

            int start = hit;
            while (start > 0 && (char.IsLetterOrDigit(registrar[start - 1]) || registrar[start - 1] == '_'))
            {
                start--;
            }

            string className = registrar.Substring(start, hit - start);
            registeredKinds.Add(ReadKindConstant(uiKernel, className));
            search = hit + marker.Length;
        }

        var manifestKinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string kind in EnumerateManifestKinds(
                     Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml")))
        {
            if (kind.StartsWith("us/", StringComparison.Ordinal)) manifestKinds.Add(kind);
        }

        foreach (string kind in EnumerateManifestKinds(
                     Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Overlay.Schema2.xml")))
        {
            if (kind.StartsWith("us/", StringComparison.Ordinal)) manifestKinds.Add(kind);
        }

        Assert(registeredKinds.Count == manifestKinds.Count && IsSameSet(registeredKinds, manifestKinds),
            "UsKernelWidgetRegistrar must register exactly the us/* kinds the Schema2 manifests use "
            + "(registered=" + registeredKinds.Count + ", manifest=" + manifestKinds.Count + "; "
            + "missing=[" + Join(registeredKinds, manifestKinds) + "], orphan=[" + Join(manifestKinds, registeredKinds) + "])");

        // The 10 settings us/* kinds + the 1 overlay readout kind are the shipped surface; pinning
        // the cardinality makes an accidental silent drop (registration removed AND manifest line
        // deleted together) visible. S4-1 shrank it 18 -> 15 (us/global-volume, us/basic-tuning,
        // us/camera-indicator); S4-2 shrank it 15 -> 13 (us/race-layer, us/xenotype-layer);
        // S4-3a shrank it 13 -> 12 (us/timing); S4-3b shrank it 12 -> 11 (us/attenuation-editor);
        // S6-3 grew it 11 -> 12 with us/section-header and 12 -> 13 with us/square-toggle. Those are the
        // FIRST GROWTHS on this pin: neither is a dissolved composite returning, both are new US surfaces
        // the declarative vocabulary cannot express (a vertical 3px rail - chrome/rule paints horizontal
        // lines only - and a track+knob whose geometry follows a bound bool). The parity assertion above is
        // what keeps the growth honest: a kind exists only because a manifest element uses it, and it
        // disappears the day none does.
        //
        // This pin lives in the UI-LOGIC project, not in the kernel-host harness, and that is worth
        // remembering: while gate 6 fails (an expected red), verify-local stops there and EVERY gate
        // after it is unrun - which is how the literals below stayed at 13 through S4-3a. When a gate
        // is expected red, run it AND run what follows it separately.
        Assert(registeredKinds.Count == 13,
            "the registered us/* kind set must have 13 members (12 settings + 1 overlay), got "
            + registeredKinds.Count);
    }

    private static string ReadKindConstant(string uiKernel, string className)
    {
        foreach (string file in Directory.EnumerateFiles(uiKernel, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            if (text.IndexOf("class " + className + " ", StringComparison.Ordinal) < 0
                && text.IndexOf("class " + className + "\n", StringComparison.Ordinal) < 0
                && text.IndexOf("class " + className + "\r", StringComparison.Ordinal) < 0)
            {
                continue;
            }

            // Section-family widgets declare KindName (the IUiWidget.Kind string is provided by
            // UsSectionWidgetBase); plain widgets declare Kind directly.
            string value = ReadConstString(text, "const string Kind = \"");
            if (value.Length > 0) return value;
            value = ReadConstString(text, "const string KindName = \"");
            if (value.Length > 0) return value;
        }

        throw new InvalidOperationException(
            "registrar references " + className + ".Register() but no Kind constant was found for it");
    }

    private static string ReadConstString(string text, string marker)
    {
        int at = text.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return "";
        int begin = at + marker.Length;
        int end = text.IndexOf('"', begin);
        return end > begin ? text.Substring(begin, end - begin) : "";
    }

    private static IEnumerable<string> EnumerateManifestKinds(string path)
    {
        if (!File.Exists(path))
        {
            throw new Exception("Schema2 manifest missing: " + path);
        }

        var document = new XmlDocument();
        document.XmlResolver = null;
        document.Load(path);

        foreach (XmlNode node in document.SelectNodes("//*[@Kind]")!)
        {
            yield return ((XmlElement)node).GetAttribute("Kind");
        }
    }

    // 4. Only the two Schema2 manifests are embedded (and exist on disk).
    private static void VerifyEmbeddedManifestsAreSchema2Only(string root)
    {
        string proj = File.ReadAllText(
            Path.Combine(root, "Source", "UniversalSqueaker", "UniversalSqueaker.csproj"));

        var embedded = new List<string>();
        int search = 0;
        while (true)
        {
            int hit = proj.IndexOf("<EmbeddedResource Include=\"", search, StringComparison.Ordinal);
            if (hit < 0) break;
            int begin = hit + "<EmbeddedResource Include=\"".Length;
            int end = proj.IndexOf('"', begin);
            embedded.Add(Path.GetFileName(proj.Substring(begin, end - begin)));
            search = end;
        }

        Assert(embedded.Count == 2,
            "exactly two UI manifests are embedded, found " + embedded.Count + " [" + string.Join(",", embedded) + "]");
        Assert(embedded.Contains("Layout.Schema2.xml") && embedded.Contains("Layout.Overlay.Schema2.xml"),
            "the embedded manifests must be Layout.Schema2.xml and Layout.Overlay.Schema2.xml, got ["
            + string.Join(",", embedded) + "]");
        Assert(!proj.Contains("\\Layout.xml") && !proj.Contains("/Layout.xml"),
            "the legacy UI/Layout.xml must not be embedded or referenced");

        string uiDir = Path.Combine(root, "Source", "UniversalSqueaker", "UI");
        foreach (string xml in Directory.EnumerateFiles(uiDir, "*.xml", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(xml);
            Assert(name == "Layout.Schema2.xml" || name == "Layout.Overlay.Schema2.xml",
                "the UI tree may only carry Schema2 manifests, found " + xml);
        }
    }

    // 5a. Help catalog <-> manifests <-> section-map drift protection (positive direction only:
    // every referenced key must exist; every catalog section must be reachable somewhere).
    private static void VerifyHelpCatalogManifestConsistency(string root)
    {
        var catalogSections = ReadCatalogSectionKeys();
        var catalogItems = ReadCatalogItemCounts();

        var manifestHelpKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in EnumerateManifestAttributes(
                     Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml"), "HelpKey"))
        {
            manifestHelpKeys.Add(key);
        }

        var mapKeys = ReadSectionHelpKeyMap(root);

        foreach (string key in manifestHelpKeys)
        {
            // A manifest HelpKey is a hover CLAIM identity, and the panel resolves a claim against the whole
            // catalog: the item key first, then the active section's overview (UsHelpPanelLogic.Resolve ->
            // UsHelpCatalog.TryFindItem). Both shapes are therefore valid declarations - step B is the first
            // manifest that carries ITEM keys, because the engine's element-level HelpKey is what let the
            // checklist's search field and row template keep their entries without a line of C#. What must
            // not happen is a key the catalog cannot answer at all, which is what would show an empty panel.
            Assert(UsHelpCatalog.TryFindItem(key, out _, out _) || UsHelpCatalog.TryGetSection(key, out _),
                "manifest HelpKey '" + key + "' is neither a catalog item nor a catalog section "
                + "(hover would show an empty panel)");
        }

        foreach (string key in mapKeys)
        {
            Assert(UsHelpCatalog.TryGetSection(key, out _),
                "SectionHelpKeyOf target '" + key + "' has no UsHelpCatalog section (nav selection would empty the panel)");
        }

        foreach (string key in catalogSections)
        {
            Assert(manifestHelpKeys.Contains(key)
                || manifestHelpKeys.Any(help => help.StartsWith(key + "/", StringComparison.Ordinal))
                || mapKeys.Contains(key),
                "catalog section '" + key + "' is unreachable: neither a manifest HelpKey (itself or one of "
                + "its items) nor a SectionHelpKeyOf value (dead catalog content)");
        }

        Assert(mapKeys.Contains("us/page-title"),
            "the section help map must keep a page-title fallback entry");

        foreach (KeyValuePair<string, int> pair in catalogItems)
        {
            Assert(pair.Value > 0,
                "catalog section '" + pair.Key + "' must expose at least one hoverable item");
        }
    }

    // 5b. Panel: a pure read surface (D2 ruling - the index list and pinned selection are gone), and
    // since FL P3 the hover CLAIM is session state: the panel sizes its text band from the
    // hover-invariant catalog maxima, resolves through the pure logic seam, and reads
    // UiSession.HoverClaim. Claims arrive only from the mid-column controls through the single outlet,
    // UsKernelDraw.HelpHover -> Session.ClaimHover. The consumer-side grace machine that used to own
    // this must stay deleted - re-growing it would put two owners on one clock.
    private static void VerifyHelpPanelWiringAndHeightFormula(string root)
    {
        string panel = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsHelpPanelWidget.cs");
        CheckSourceContains(panel, new[]
        {
            "ValidateValue<string>(\"help-section-key\"",
            "MaxBodyBand(ctx, textWidth)",
            "ctx.Session.HoverClaim ?? \"\"",
            "TranslationSeam(ctx)",
        },
        "the help panel must validate its one read key, size its text band from the hover-invariant "
        + "catalog maxima (MaxBodyBand), take the claim from the session, and resolve its display "
        + "through the pure logic seam with the Host translation seam applied in one pass");
        CheckSourceDoesNotContain(panel, "ctx.Bindings.Invoke",
            "the help panel is read-only: no write channel survives the D2 index-list cut");
        CheckSourceDoesNotContain(panel, "help-hover",
            "the hover claim is session state since FL P3; the help-hover binding stays dead");

        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsKernelDraw.cs"),
            new[] { "ctx.Session.ClaimHover(itemKey)" },
            "HelpHover is the single claim outlet and it claims on the session, not through a binding");
        CheckSourceDoesNotContain(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsSectionWidgetBase.cs"),
            "help-hover",
            "the accent border reads the session claim; the binding must not return as a second read path");

        // The retired machine: gone from the window, the model, the state and the source boundary.
        string[] retiredMachine = { "BeginHelpHoverFrame", "SetHelpHover", "HelpHoverKey", "HelpHoverGraceLeft" };
        foreach (string file in new[]
        {
            "UI/UniversalSqueakerSettingsWindow.cs",
            "UI/Model/VoicePacksPageModel.cs",
            "UI/Model/VoicePacksPageState.cs",
            "UI/UsKernelSettingsSource.cs",
            "UI/IUsKernelSettingsSource.cs",
        })
        {
            string text = File.ReadAllText(Path.Combine(root, "Source", "UniversalSqueaker", file));
            foreach (string symbol in retiredMachine)
            {
                // net472: no string.Contains(string, StringComparison).
                if (text.IndexOf(symbol, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException(
                        "the consumer-side hover-claim machine moved to UiSession (FL P3); '"
                        + symbol + "' must not live in " + file);
                }
            }
        }

        string host = File.ReadAllText(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UsKernelSettingsHost.cs"));
        // Re-cut 2026-09-24 (T3-1): this clause pinned the raw `BindAction<string>("scroll-to"` text, which
        // the write-registration funnel renamed to `writes.Action<string>(...)`. Its INTENT is "the Host owns
        // the scroll-to action wiring" - that is what is asserted now, receiver-agnostic - while the "a write
        // registration must go through the funnel" half is owned by
        // VerifyWriteBindingsGoThroughTheRegistry, so the receiver is deliberately not pinned here.
        Assert(host.Contains("BindReadOnly<string>(\"help-section-key\"")
               && host.Contains("host.Session.HoverGraceFrames = HelpHoverGracePasses")
               && !host.Contains("set-help-hover")
               && host.Contains("Action<string>(\"scroll-to\""),
            "the Host owns help-section-key/scroll-to wiring, sets the grace length on the session, "
            + "and the retired hover/selection channels stay dead (single event authority)");
    }


    // 5c. The C+A wiring table, both directions: every two-segment "us/<section>/<item>" literal in
    // the widget tree must be a real catalog item (a claim that resolves to nothing is a silent
    // dead hover), and every catalog item must be claimed by some control (an entry no control
    // owns is dead catalog content the panel index would show forever). This is the drift pin for
    // "feature exists, wiring lost": adding a help entry without wiring it, or rewiring a control
    // onto a renamed key, fails here at build-gate time, not in someone's screenshot.
    private static void VerifyHoverClaimsMatchCatalogItems(string root)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        string kernelDir = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel");
        foreach (string file in Directory.EnumerateFiles(kernelDir, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            int at = 0;
            while ((at = text.IndexOf("\"us/", at, StringComparison.Ordinal)) >= 0)
            {
                int close = text.IndexOf('"', at + 1);
                if (close < 0) break;
                string literal = text.Substring(at + 1, close - at - 1);
                // Two segments below "us" = an item key; one segment = a kind/section reference.
                string[] parts = literal.Split('/');
                if (parts.Length == 3 && parts[0] == "us" && parts[1].Length > 0 && parts[2].Length > 0)
                {
                    claimed.Add(literal);
                }

                at = close + 1;
            }
        }

        // Step B widened the claim SOURCES, not the rule: since the engine claims a hovered element's
        // HelpKey, a claim can be DECLARED in the manifest - that is the hook that let the checklist's
        // search field and row template drop their imperative UsKernelDraw.HelpHover sites without losing
        // help coverage. A manifest HelpKey that names an item is therefore a claim like any other, and the
        // bidirectionality below (every claim resolves, every item is claimed) covers both sources.
        foreach (string key in EnumerateManifestAttributes(
                     Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Layout.Schema2.xml"), "HelpKey"))
        {
            string[] parts = key.Split('/');
            if (parts.Length == 3 && parts[0] == "us" && parts[1].Length > 0 && parts[2].Length > 0)
            {
                claimed.Add(key);
            }
        }

        Assert(claimed.Count > 0, "the widget tree claims no help item keys at all; the scan is broken");

        var catalogItems = ReadCatalogItemKeys();
        foreach (string key in claimed)
        {
            Assert(catalogItems.Contains(key),
                "widget hover claim '" + key + "' is not a catalog item (dead hover; "
                + "add the entry or fix the key)");
        }

        foreach (string key in catalogItems)
        {
            Assert(claimed.Contains(key),
                "catalog item '" + key + "' is claimed by no control (dead catalog content; "
                + "wire the control or remove the entry)");
        }
    }

    private static HashSet<string> ReadCatalogItemKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        FieldInfo? field = typeof(UsHelpCatalog).GetField("Sections", BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException("UsHelpCatalog.Sections field is gone; re-point this guard deliberately.");
        }

        var sections = (System.Collections.IDictionary)field.GetValue(null)!;
        foreach (DictionaryEntry entry in sections)
        {
            HelpSection section = (HelpSection)entry.Value!;
            foreach (HelpItem item in section.Items)
            {
                Assert(keys.Add(item.Key), "catalog item key collides across sections: " + item.Key);
            }
        }

        return keys;
    }

    private static HashSet<string> ReadCatalogSectionKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        FieldInfo? field = typeof(UsHelpCatalog).GetField("Sections", BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException("UsHelpCatalog.Sections field is gone; re-point this guard deliberately.");
        }

        var sections = (System.Collections.IDictionary)field.GetValue(null)!;
        foreach (DictionaryEntry entry in sections)
        {
            keys.Add((string)entry.Key!);
        }

        return keys;
    }

    private static Dictionary<string, int> ReadCatalogItemCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        FieldInfo? field = typeof(UsHelpCatalog).GetField("Sections", BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            throw new InvalidOperationException("UsHelpCatalog.Sections field is gone; re-point this guard deliberately.");
        }

        var sections = (System.Collections.IDictionary)field.GetValue(null)!;
        foreach (DictionaryEntry entry in sections)
        {
            HelpSection section = (HelpSection)entry.Value!;
            counts[section.Key] = section.Items.Count;
        }

        return counts;
    }

    private static HashSet<string> ReadSectionHelpKeyMap(string root)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string model = File.ReadAllText(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Model", "VoicePacksPageModel.cs"));

        int start = model.IndexOf("SectionHelpKeyOf", StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("VoicePacksPageModel.SectionHelpKeyOf is gone; re-point this guard deliberately.");
        }

        int end = model.IndexOf("SetTuningLayer", start, StringComparison.Ordinal);
        if (end < 0) end = model.Length;
        string body = model.Substring(start, end - start);

        foreach (string fragment in body.Split('\n'))
        {
            int quote = fragment.IndexOf("\"us/", StringComparison.Ordinal);
            while (quote >= 0)
            {
                int close = fragment.IndexOf('"', quote + 1);
                if (close < 0) break;
                keys.Add(fragment.Substring(quote + 1, close - quote - 1));
                quote = fragment.IndexOf("\"us/", close, StringComparison.Ordinal);
            }
        }

        return keys;
    }

    private static IEnumerable<string> EnumerateManifestAttributes(string path, string attribute)
    {
        if (!File.Exists(path))
        {
            throw new Exception("Schema2 manifest missing: " + path);
        }

        var document = new XmlDocument();
        document.XmlResolver = null;
        document.Load(path);

        foreach (XmlNode node in document.SelectNodes("//*[@" + attribute + "]")!)
        {
            yield return ((XmlElement)node).GetAttribute(attribute);
        }
    }

    // 6. Localization contract. RimWorld answers an unknown key by returning the key itself (and, in
    // dev mode, a pseudo-translated variant of it), so a half-migrated string never fails loudly at
    // runtime — it just shows a raw key or accented garbage to the player. That makes key existence a
    // build-time property, not a runtime one, and it is exactly what this guard pins.
    private static void VerifyLocalizationContract(string root)
    {
        Dictionary<string, string> english = ReadKeyedTable(Path.Combine(root, "1.6", "Languages", "English", "Keyed", "UniversalSqueaker.xml"));
        Dictionary<string, string> chinese = ReadKeyedTable(Path.Combine(root, "1.6", "Languages", "ChineseSimplified", "Keyed", "UniversalSqueaker.xml"));

        Assert(english.Count >= 100 && chinese.Count >= 100,
            "both Keyed tables must be populated (english=" + english.Count + ", chinese=" + chinese.Count + ")");

        var englishKeys = new HashSet<string>(english.Keys, StringComparer.Ordinal);
        var chineseKeys = new HashSet<string>(chinese.Keys, StringComparer.Ordinal);
        Assert(IsSameSet(englishKeys, chineseKeys),
            "the two Keyed tables must carry the identical key set; only-in-english={"
            + Join(englishKeys, chineseKeys) + "} only-in-chinese={" + Join(chineseKeys, englishKeys) + "}");

        foreach (KeyValuePair<string, string> entry in english)
        {
            Assert(entry.Value.Trim().Length > 0, "English Keyed entry has no text: " + entry.Key);
            string chineseText = chinese[entry.Key];
            Assert(chineseText.Trim().Length > 0, "Chinese Keyed entry has no text: " + entry.Key);

            // A translation that drops or renumbers a placeholder silently breaks the formatted line,
            // because the substituted argument lands in the wrong slot or disappears.
            Assert(SamePlaceholders(entry.Value, chineseText),
                "Keyed entry's {n} placeholders differ between languages: " + entry.Key
                + " english={" + entry.Value + "} chinese={" + chineseText + "}");
        }

        foreach (string key in CollectKeyedReferences(root))
        {
            Assert(englishKeys.Contains(key),
                "the UI references a Keyed entry that has no English text: " + key);
            Assert(chineseKeys.Contains(key),
                "the UI references a Keyed entry that has no Chinese text: " + key);
        }

        foreach (string manifest in ManifestPaths(root))
        {
            var document = new XmlDocument();
            document.Load(manifest);
            XmlNode? offenders = document.SelectSingleNode("//Widget[@Title or @Caption or @Text]");
            Assert(offenders == null,
                "shipped manifests must pass translatable text as *Key attributes, never as a literal;"
                + " offending element: " + (offenders?.Name ?? "") + " in " + manifest);
        }

        // Text-surface closure (F4, 2026-09-06): the window chrome is drawn through the Keyed seam
        // only, and the footer's closed save-status token set reports drift instead of silently
        // rendering an unknown state. Restoring a chrome literal or dropping the guard fails here.
        string windowChrome = Path.Combine(
            root, "Source", "UniversalSqueaker", "UI", "UniversalSqueakerSettingsWindow.cs");
        CheckSourceDoesNotContain(windowChrome, "\"Close\"",
            "the close button must translate US.Settings.Window.Close, not ship an English literal");
        CheckSourceDoesNotContain(windowChrome, "VoicePack Routing",
            "the window subtitle must come from the Keyed table, not a source literal");
        CheckSourceContains(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsFooterWidget.cs"),
            new[] { "ReportedStatusTokens.Add" },
            "an unknown footer save-status token must be reported once per value (drift guard)");
    }

    // 10. The retractable help drawer is INDEPENDENT state (brief: "Help visibility is independent
    //     state. Do not reuse active-tab"). The engine compares the Tab attribute against
    //     UiBindings.ActiveTabKey - so a drawer that rode active-tab would leak into workspace switching
    //     and leave a reserved column whenever the workspace happened to match. The declarative switch
    //     for this page is VisibleKey="help-open": a bool value binding that is this page's own state.
    //     This guard is three-sided: the manifest's drawer element declares VisibleKey="help-open" and
    //     no Tab; the consumer drives visibility through its own state/binding names; and the retired
    //     root-list variant does not come back. A regression that re-binds help visibility to the
    //     workspace, or that hides the drawer by REMOVING it from the definition, fails here.
    private static void VerifyHelpDrawerIsIndependentState(string root)
    {
        string ui = Path.Combine(root, "Source", "UniversalSqueaker", "UI");
        string manifest = Path.Combine(ui, "Layout.Schema2.xml");
        var document = new XmlDocument();
        document.XmlResolver = null;
        document.Load(manifest);
        XmlNode? drawerElement = document.SelectSingleNode("//*[@Id='help-scroll']");
        Assert(drawerElement != null,
            "Layout.Schema2.xml must keep the Id='help-scroll' drawer element; without it help has no home");
        Assert(!((XmlElement)drawerElement!).HasAttribute("Tab"),
            "the help drawer element must not carry a Tab attribute: Tab is the engine's active-tab switch, "
            + "and help visibility is independent state");

        string hostPath = Path.Combine(ui, "UsKernelSettingsHost.cs");
        CheckSourceContains(hostPath, new[]
        {
            "\"help-open\"",
            "SetHelpDrawerOpen",
        },
        "the Host must own help visibility as its own value binding and write the drawer state through the source boundary");

        string statePath = Path.Combine(ui, "Model", "VoicePacksPageState.cs");
        CheckSourceContains(statePath, new[] { "HelpDrawerOpen" },
            "help visibility must live in the per-window page state");
        Assert(File.ReadAllText(statePath).IndexOf("public bool HelpDrawerOpen = false;", StringComparison.Ordinal) >= 0,
            "HelpDrawerOpen must default to false: the shipped window opens narrow (vanilla-like) with the"
            + " help drawer retracted, and only widens when the player expands it");

        // (乙1): there are TWO declared presentations of the help panel now, and neither is gated by the raw
        // player intent. Each is hidden DECLARATIVELY - the elements stay in the definition, which is what
        // lets their nodes and scroll positions survive a close/open - and the host derives which one is
        // true from the screen. Asserting both keys (and that neither element is Tab-gated) is what keeps a
        // future edit from re-pointing one of them at help-open and making the two appear together.
        Assert(((XmlElement)drawerElement!).HasAttribute("VisibleKey")
            && string.Equals(((XmlElement)drawerElement!).GetAttribute("VisibleKey"), "help-open-wide", StringComparison.Ordinal),
            "the WIDE help column must be hidden DECLARATIVELY through VisibleKey=\"help-open-wide\"");
        XmlNode? narrowElement = document.SelectSingleNode("//*[@Id='help-band']");
        Assert(narrowElement != null,
            "Layout.Schema2.xml must declare the Id='help-band' narrow-screen help presentation (乙1)");
        Assert(!((XmlElement)narrowElement!).HasAttribute("Tab"),
            "the narrow help band must not carry a Tab attribute either: help visibility is independent state");
        Assert(((XmlElement)narrowElement!).HasAttribute("VisibleKey")
            && string.Equals(((XmlElement)narrowElement!).GetAttribute("VisibleKey"), "help-open-narrow", StringComparison.Ordinal),
            "the NARROW help band must be hidden DECLARATIVELY through VisibleKey=\"help-open-narrow\"");
        Assert(!string.Equals(((XmlElement)drawerElement!).GetAttribute("VisibleKey"),
                ((XmlElement)narrowElement!).GetAttribute("VisibleKey"), StringComparison.Ordinal),
            "the two help presentations must NOT share a visibility key, or they would be drawn at the same"
            + " time - which is exactly the mutual exclusion (乙1) is built on");

        // The REPLACING shape (2026-09-21b), both halves. On the manifest: the narrow band takes its height
        // from the slot the body frees (Fill="true" and NO Height - a declared Height makes the engine's
        // flexible-fill rule refuse the slot and the footer then leaves the page, measured at 660.7 natural
        // against a 530 viewport), and the body row yields that slot through its OWN derived key, hidden and
        // never removed - removing it is what releases its node and its scroll position. On the host: the key
        // is derived there, so there is no second writable truth about which presentation is showing.
        XmlNode? bodyElement = document.SelectSingleNode("//*[@Id='body-row']");
        Assert(bodyElement != null, "Layout.Schema2.xml must declare the Id='body-row' page body row");
        Assert(((XmlElement)bodyElement!).HasAttribute("VisibleKey")
            && string.Equals(((XmlElement)bodyElement!).GetAttribute("VisibleKey"), "body-visible", StringComparison.Ordinal),
            "the body row must yield its slot DECLARATIVELY through VisibleKey=\"body-visible\" while the"
            + " narrow band replaces it");
        Assert(!((XmlElement)bodyElement!).HasAttribute("Tab"),
            "the body row must not be Tab-gated: it yields to the help presentation, not to a workspace");
        Assert(!string.Equals(((XmlElement)bodyElement!).GetAttribute("VisibleKey"),
                ((XmlElement)narrowElement!).GetAttribute("VisibleKey"), StringComparison.Ordinal),
            "the body row must not share the narrow band's visibility key, or the two could never swap");
        Assert(string.Equals(((XmlElement)narrowElement!).GetAttribute("Fill"), "true", StringComparison.Ordinal)
            && !((XmlElement)narrowElement!).HasAttribute("Height"),
            "the narrow band must take its height from the freed slot (Fill=\"true\" and NO Height): a"
            + " declared Height makes the engine refuse the flexible slot and pushes the footer off the page");
        CheckSourceContains(hostPath, new[] { "\"body-visible\"" },
            "the Host must derive the body's visibility key: the player never writes it, and a writable copy"
            + " would be a second truth about which presentation is showing");

        // The retired mechanism must not come back in any form - not as the file, and not inlined into the
        // Host. Rebuilding the manifest root list removes the element from the definition, and the engine
        // releases a removed element's node together with its scroll position (0.4 -> 0.6 semantics).
        Assert(!File.Exists(Path.Combine(ui, "Layout", "UsLayoutVariants.cs")),
            "UsLayoutVariants must not come back: a rebuilt root list omits the element, and the engine"
            + " releases an omitted element's node and scroll position");
        CheckSourceDoesNotContain(hostPath, "UsLayoutVariants",
            "the Host must not reference the retired root-list variant");
        CheckSourceDoesNotContain(hostPath, "TryReplaceRoots",
            "the Host must not install a rebuilt root list by hand");
        CheckSourceDoesNotContain(hostPath, "manifest.Roots",
            "the Host must not mutate the manifest root list: that is the removed-element path, and visibility"
            + " is declarative now");

        // The Help toggle's Keyed caption moved with the control (S3-2a): it is the manifest's header
        // button that carries TextKey, and the page-title widget must no longer own either. Both halves are
        // asserted, because "the caption exists" is not "the page exposes it" and not "the widget lost it".
        CheckSourceContains(Path.Combine(ui, "Layout.Schema2.xml"),
            new[] { "\"US.Help.Drawer.Toggle\"" },
            "the page header must expose the discoverable Help toggle through the Keyed table: the caption"
            + " belongs to the manifest's header button now");
        CheckSourceDoesNotContain(Path.Combine(ui, "Kernel", "UsPageTitleWidget.cs"), "US.Help.Drawer.Toggle",
            "the page-title widget must not carry the Help toggle's caption any more: the switch is declared"
            + " in the manifest, and a caption without a control is the drift this pair exists to catch");
    }
    private static Dictionary<string, string> ReadKeyedTable(string path)
    {
        Assert(File.Exists(path), "Keyed table is missing: " + path);
        var document = new XmlDocument();
        document.Load(path);

        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XmlNode node in document.SelectNodes("/LanguageData/*")!)
        {
            Assert(node.NodeType == XmlNodeType.Element, "unexpected node in " + path);
            table[node.Name] = node.InnerText;
        }

        return table;
    }

    /// <summary>
    /// Every complete `US.*` string literal in the production source plus every `*Key` attribute value
    /// in the shipped manifests. Dots inside the key must be part of the character class or every
    /// key with three or more segments (US.Help.ScopeTree.ActionScope.Text) silently escapes the
    /// scan - and a reference that is never collected can never be asserted to exist. Literals
    /// ending in `.` are excluded on purpose: those are concatenation prefixes (for example
    /// `"US.Mood." + mood`) whose full key names cannot be read statically.
    /// </summary>
    private static HashSet<string> CollectKeyedReferences(string root)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string sourceRoot = Path.Combine(root, "Source");

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).Equals("AssemblyInfo.cs", StringComparison.Ordinal)) continue;
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(file), "\"(US\\.[A-Za-z0-9_.]*)\""))
            {
                string key = match.Groups[1].Value;
                if (key.Length > 3 && !key.EndsWith(".", StringComparison.Ordinal)) keys.Add(key);
            }
        }

        foreach (string manifest in ManifestPaths(root))
        {
            // PlaceholderKey joined the list with step B: the checklist's search hint moved from a C#
            // literal into the input/text-field element, and a key the scanner does not collect is a
            // reference that can never be asserted to exist.
            foreach (string attribute in new[] { "TitleKey", "CaptionKey", "TextKey", "LabelKey", "HelpKey", "PlaceholderKey" })
            {
                foreach (string value in EnumerateManifestAttributes(manifest, attribute))
                {
                    if (value.StartsWith("US.", StringComparison.Ordinal)) keys.Add(value.Trim());
                }
            }
        }

        return keys;
    }

    private static IEnumerable<string> ManifestPaths(string root)
    {
        string ui = Path.Combine(root, "Source", "UniversalSqueaker", "UI");
        yield return Path.Combine(ui, "Layout.Schema2.xml");
        yield return Path.Combine(ui, "Layout.Overlay.Schema2.xml");
    }

    private static bool SamePlaceholders(string english, string chinese)
    {
        var left = new List<string>(System.Text.RegularExpressions.Regex.Matches(english, "\\{\\d+\\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value));
        var right = new List<string>(System.Text.RegularExpressions.Regex.Matches(chinese, "\\{\\d+\\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value));
        left.Sort(StringComparer.Ordinal);
        right.Sort(StringComparer.Ordinal);
        return left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static string SettingsWindowPath(string root) =>
        Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UniversalSqueakerSettingsWindow.cs");

    private static bool IsSameSet(HashSet<string> a, HashSet<string> b)
    {
        foreach (string item in a)
        {
            if (!b.Contains(item)) return false;
        }

        foreach (string item in b)
        {
            if (!a.Contains(item)) return false;
        }

        return true;
    }

    private static string Join(HashSet<string> source, HashSet<string> exclude)
    {
        var missing = new List<string>();
        foreach (string item in source)
        {
            if (!exclude.Contains(item)) missing.Add(item);
        }

        return string.Join(" ", missing);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void CheckSourceContains(string path, string[] requiredFragments, string message)
    {
        string text = File.ReadAllText(path);
        foreach (string fragment in requiredFragments)
        {
            if (text.IndexOf(fragment, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    message + "; missing fragment: " + fragment + " in " + path);
            }
        }
    }

    private static void CheckSourceDoesNotContain(string path, string fragment, string message)
    {
        string text = File.ReadAllText(path);
        if (text.IndexOf(fragment, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                message + "; forbidden fragment: " + fragment + " in " + path);
        }
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "verify-local.ps1")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate repository root from " + AppDomain.CurrentDomain.BaseDirectory);
    }
}
