using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace FerriteLib.UiKit;

/// <summary>
/// Parsed UI layout manifest. Parsing is strict and safe: root must be
/// <c>&lt;UiPage Schema="1" Source="..."&gt;</c>, external entities are prohibited, and every
/// Widget Kind is validated against <see cref="WidgetRegistry"/>. Nested container elements
/// (<c>Block</c>, <c>Section</c>, <c>Column</c>) are supported and are parsed recursively.
/// </summary>
public sealed class LayoutManifest
{
    private const int MaxDepth = 8;
    private const int MaxNodeCount = 256;

    public string Source { get; }

    public string SchemaVersion { get; }

    public IReadOnlyList<UiElementSpec> Roots { get; }

    private LayoutManifest(string source, string schemaVersion, IReadOnlyList<UiElementSpec> roots)
    {
        Source = source;
        SchemaVersion = schemaVersion;
        Roots = roots;
    }

    public static LayoutManifest Parse(string xml)
    {
        if (xml == null) throw new ArgumentNullException(nameof(xml));

        try
        {
            return ParseCore(xml);
        }
        catch (XmlException ex)
        {
            throw new FormatException(
                $"Invalid UI layout XML at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}",
                ex);
        }
    }

    public static LayoutManifest ParseFile(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        return Parse(File.ReadAllText(path));
    }

    private static LayoutManifest ParseCore(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        using var textReader = new StringReader(xml);
        using XmlReader reader = XmlReader.Create(textReader, settings);
        IXmlLineInfo lineInfo = reader as IXmlLineInfo ?? NullLineInfo.Instance;

        int nodeCount = 0;
        string source = "";
        string schema = "";

        // Locate the root <UiPage> element.
        while (reader.Read())
        {
            nodeCount = BumpNode(nodeCount, lineInfo);
            if (reader.NodeType != XmlNodeType.Element) continue;

            if (reader.Depth > MaxDepth)
                throw ParseError(lineInfo, $"UI layout exceeds the maximum depth of {MaxDepth}.");

            if (!string.Equals(reader.Name, "UiPage", StringComparison.Ordinal))
                throw ParseError(lineInfo, $"Expected root element <UiPage> but found <{reader.Name}>.");

            schema = reader.GetAttribute("Schema") ?? "";
            source = reader.GetAttribute("Source") ?? "";

            if (schema.Length == 0)
                throw ParseError(lineInfo, "Root <UiPage> is missing the required Schema attribute.");
            if (!string.Equals(schema, "1", StringComparison.Ordinal))
                throw ParseError(lineInfo, $"Unsupported UiPage Schema '{schema}'; expected '1'.");
            if (source.Length == 0)
                throw ParseError(lineInfo, "Root <UiPage> is missing the required Source attribute.");

            if (reader.IsEmptyElement)
                return new LayoutManifest(source, schema, Array.Empty<UiElementSpec>());
            break;
        }

        if (reader.EOF)
            throw new FormatException("UI layout XML is empty; expected <UiPage Schema=\"1\" Source=\"...\">.");

        var roots = new List<UiElementSpec>();

        while (reader.Read())
        {
            nodeCount = BumpNode(nodeCount, lineInfo);

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (reader.Depth > MaxDepth)
                        throw ParseError(lineInfo, $"UI layout exceeds the maximum depth of {MaxDepth}.");
                    if (reader.Depth != 1)
                        throw ParseError(lineInfo,
                            $"Unexpected nested element <{reader.Name}> at depth {reader.Depth}; expected a root-level element.");
                    if (!IsSupportedElementName(reader.Name))
                        throw ParseError(lineInfo,
                            $"Expected <Widget>, <Block>, <Section>, or <Column> but found <{reader.Name}>.");

                    roots.Add(ReadElement(reader, source, lineInfo, ref nodeCount));
                    break;

                case XmlNodeType.EndElement:
                    if (string.Equals(reader.Name, "UiPage", StringComparison.Ordinal))
                        return new LayoutManifest(source, schema, roots);
                    throw ParseError(lineInfo, $"Unexpected end element </{reader.Name}>.");

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    throw ParseError(lineInfo, "Text content is not allowed inside <UiPage>; expected layout elements only.");

                default:
                    continue;
            }
        }

        throw new FormatException("UI layout XML ended before the <UiPage> element was closed.");
    }

    private static UiElementSpec ReadElement(
        XmlReader reader,
        string source,
        IXmlLineInfo lineInfo,
        ref int nodeCount)
    {
        string elementName = reader.Name;
        int elementLine = lineInfo.LineNumber;
        string id = reader.GetAttribute("id") ?? reader.GetAttribute("Id") ?? "";
        string kind = elementName;

        if (string.Equals(elementName, "Widget", StringComparison.Ordinal))
        {
            kind = reader.GetAttribute("Kind") ?? "";
            if (kind.Length == 0)
                throw ParseError(lineInfo, $"<Widget id=\"{id}\"> is missing the required Kind attribute.");

            // Validate resolvability before consuming children so an unknown Kind fails fast.
            try
            {
                _ = WidgetRegistry.Resolve(source, kind);
            }
            catch (UnknownWidgetKindException ex)
            {
                throw new UnknownWidgetKindException(ex.Scope, ex.Kind, $"Widget id=\"{id}\" at line {elementLine}.");
            }
        }
        else if (!IsSupportedElementName(elementName))
        {
            throw ParseError(lineInfo,
                $"Expected <Widget>, <Block>, <Section>, or <Column> but found <{elementName}>.");
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (reader.HasAttributes)
        {
            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                attributes[reader.Name] = reader.Value;
            }
            reader.MoveToElement();
        }

        if (reader.IsEmptyElement)
            return new UiElementSpec(id, kind, attributes, Array.Empty<UiElementSpec>());

        var children = new List<UiElementSpec>();

        while (reader.Read())
        {
            nodeCount = BumpNode(nodeCount, lineInfo);

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (reader.Depth > MaxDepth)
                        throw ParseError(lineInfo, $"UI layout exceeds the maximum depth of {MaxDepth}.");

                    if (string.Equals(elementName, "Widget", StringComparison.Ordinal))
                        throw ParseError(lineInfo,
                            $"<Widget id=\"{id}\" Kind=\"{kind}\"> must not contain nested elements; found <{reader.Name}> at line {lineInfo.LineNumber}.");

                    if (!IsSupportedElementName(reader.Name))
                        throw ParseError(lineInfo,
                            $"Expected <Widget>, <Block>, <Section>, or <Column> inside <{elementName} id=\"{id}\"> but found <{reader.Name}>.");

                    children.Add(ReadElement(reader, source, lineInfo, ref nodeCount));
                    break;

                case XmlNodeType.EndElement:
                    if (!string.Equals(reader.Name, elementName, StringComparison.Ordinal))
                        throw ParseError(lineInfo, $"Unexpected end element </{reader.Name}> inside <{elementName} id=\"{id}\">.");
                    return new UiElementSpec(id, kind, attributes, children);

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    throw ParseError(lineInfo, $"<{elementName} id=\"{id}\" Kind=\"{kind}\"> must not contain text content.");

                default:
                    continue;
            }
        }

        throw new FormatException($"UI layout XML ended inside <{elementName} id=\"{id}\" Kind=\"{kind}\">.");
    }

    private static bool IsSupportedElementName(string name)
    {
        return string.Equals(name, "Widget", StringComparison.Ordinal)
            || string.Equals(name, "Block", StringComparison.Ordinal)
            || string.Equals(name, "Section", StringComparison.Ordinal)
            || string.Equals(name, "Column", StringComparison.Ordinal);
    }

    private static int BumpNode(int count, IXmlLineInfo lineInfo)
    {
        count++;
        if (count > MaxNodeCount)
            throw ParseError(lineInfo, $"UI layout exceeds the maximum node count of {MaxNodeCount}.");
        return count;
    }

    private static FormatException ParseError(IXmlLineInfo lineInfo, string message)
    {
        return new FormatException(
            $"Invalid UI layout XML at line {lineInfo.LineNumber}, position {lineInfo.LinePosition}: {message}");
    }

    private sealed class NullLineInfo : IXmlLineInfo
    {
        internal static readonly NullLineInfo Instance = new();

        public int LineNumber => 0;

        public int LinePosition => 0;

        public bool HasLineInfo() => false;
    }
}
