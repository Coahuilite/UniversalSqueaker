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
///   1. The settings window keeps the new all-frame-failure model (pageUnavailable +
///      noticeDueNextFrame + DrawUnavailableNotice) and can never reintroduce a legacy
///      second-page fallback.
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
        VerifyLocalizationContract(root);
    }

    // 1. Settings window: new failure model present, legacy whole-page fallback symbols absent.
    private static void VerifySettingsWindowFailureModel(string root)
    {
        string path = SettingsWindowPath(root);
        CheckSourceContains(
            path,
            new[]
            {
                "private bool pageUnavailable;",
                "private bool noticeDueNextFrame;",
                "private void DrawUnavailableNotice(",
                "DrawUnavailableNotice(contentRect);",
                "pageUnavailable = true;",
                "noticeDueNextFrame = true;",
                "UiThemeDraw.Surface(",
            },
            "the settings window must keep the Schema2 failure model (deferred notice -> "
            + "pageUnavailable error region drawn through the shared UiTheme surface)");

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

    // 2. Second event authority / second palette must not resurrect anywhere under UI/.
    private static void VerifyNoSecondEventAuthorityOrPalette(string root)
    {
        string uiDir = Path.Combine(root, "Source", "UniversalSqueaker", "UI");
        string[] forbiddenTypes = { "UiInteract", "Palette", "SurfaceFrame", "UiText", "UiValueStore" };

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

        // The 15 settings us/* kinds + the 1 overlay readout kind are the shipped surface; pinning
        // the cardinality makes an accidental silent drop (registration removed AND manifest line
        // deleted together) visible.
        Assert(registeredKinds.Count == 16,
            "the registered us/* kind set must have 16 members (15 settings + 1 overlay), got "
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
            Assert(UsHelpCatalog.TryGetSection(key, out _),
                "manifest HelpKey '" + key + "' has no UsHelpCatalog section (hover would show an empty panel)");
        }

        foreach (string key in mapKeys)
        {
            Assert(UsHelpCatalog.TryGetSection(key, out _),
                "SectionHelpKeyOf target '" + key + "' has no UsHelpCatalog section (nav selection would empty the panel)");
        }

        foreach (string key in catalogSections)
        {
            Assert(manifestHelpKeys.Contains(key) || mapKeys.Contains(key),
                "catalog section '" + key + "' is unreachable: neither a manifest HelpKey nor a "
                + "SectionHelpKeyOf value (dead catalog content)");
        }

        Assert(mapKeys.Contains("us/page-title"),
            "the section help map must keep a page-title fallback entry");

        foreach (KeyValuePair<string, int> pair in catalogItems)
        {
            Assert(pair.Value > 0,
                "catalog section '" + pair.Key + "' must expose at least one hoverable item");
        }
    }

    // 5b. Panel: the height formula and the drawn row list both key off section.Items, and the
    // hover/select contract runs through the Host-bound actions (never a private command bridge).
    private static void VerifyHelpPanelWiringAndHeightFormula(string root)
    {
        string panel = Path.Combine(root, "Source", "UniversalSqueaker", "UI", "Kernel", "UsHelpPanelWidget.cs");
        CheckSourceContains(panel, new[]
        {
            "listHeight = ItemHeight + (ItemHeight + ItemGap) * section.Items.Count;",
            "foreach (HelpItem item in section.Items)",
            "ValidateValue<string>(\"help-section-key\"",
            "ValidateAction<string>(\"set-help-hover\"",
            "ValidateAction<string>(\"set-help-selection\"",
            "ctx.Bindings.Invoke(\"set-help-hover\"",
            "ctx.Bindings.Invoke(\"set-help-selection\"",
        },
        "the help panel must render one row per section.Items entry and size its list height from the same count");

        string host = File.ReadAllText(
            Path.Combine(root, "Source", "UniversalSqueaker", "UI", "UsKernelSettingsHost.cs"));
        Assert(host.Contains("BindReadOnly<string>(\"help-section-key\"")
               && host.Contains("BindAction<string>(\"set-help-hover\"")
               && host.Contains("BindAction<string>(\"set-help-selection\"")
               && host.Contains("BindAction<string>(\"scroll-to\""),
            "the Host owns the help-section-key/hover/selection/scroll-to wiring (single event authority)");
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
    /// in the shipped manifests. Literals ending in `.` are excluded on purpose: those are concatenation
    /// prefixes (for example `"US.Mood." + mood`) whose full key names cannot be read statically.
    /// </summary>
    private static HashSet<string> CollectKeyedReferences(string root)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string sourceRoot = Path.Combine(root, "Source");

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).Equals("AssemblyInfo.cs", StringComparison.Ordinal)) continue;
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(file), "\"(US\\.[A-Za-z0-9_]*)\""))
            {
                string key = match.Groups[1].Value;
                if (key.Length > 3 && !key.EndsWith(".", StringComparison.Ordinal)) keys.Add(key);
            }
        }

        foreach (string manifest in ManifestPaths(root))
        {
            foreach (string attribute in new[] { "TitleKey", "CaptionKey", "TextKey", "LabelKey", "HelpKey" })
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
