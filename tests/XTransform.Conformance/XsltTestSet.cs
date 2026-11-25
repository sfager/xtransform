// Reader for the W3C XSLT test catalog (namespace http://www.w3.org/2012/10/xslt-test-catalog).
// Element names are matched by local name so the reader is independent of the namespace.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace XTransform.Conformance;

/// <summary>A <c>&lt;spec&gt;</c> or <c>&lt;feature&gt;</c> dependency.</summary>
internal sealed record XsltDependency(string Type, string Value, bool Satisfied);

/// <summary>A stylesheet parameter supplied by an environment or a test case.</summary>
internal sealed record XsltTestParam(string Name, string? Select, bool IsStatic, XElement Element);

/// <summary>A source document: either a file (absolute path) or inline content.</summary>
internal sealed record XsltTestSource(string Role, string? File, string? Content);

/// <summary>An environment: source documents, parameters and resources.</summary>
internal sealed class XsltTestEnvironment
{
    public string Name { get; init; } = string.Empty;
    public List<XsltTestSource> Sources { get; } = new List<XsltTestSource>();
    public List<XsltTestParam> Params { get; } = new List<XsltTestParam>();
}

/// <summary>A reference to a test-set file in the catalog.</summary>
internal sealed record XsltTestSetRef(string Name, string File);

/// <summary>One XSLT test case.</summary>
internal sealed class XsltTestCase
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<XsltDependency> Dependencies { get; init; } = Array.Empty<XsltDependency>();
    public string? EnvironmentRef { get; init; }
    public XsltTestEnvironment? InlineEnvironment { get; init; }
    public string? StylesheetFile { get; init; }
    public XElement? InitialTemplate { get; init; }
    public XElement? InitialMode { get; init; }
    public IReadOnlyList<XsltTestParam> Params { get; init; } = Array.Empty<XsltTestParam>();
    public XElement? Result { get; init; }
    public bool UsesPackages { get; init; }
}

/// <summary>A parsed test-set file.</summary>
internal sealed class XsltTestSet
{
    public string Name { get; init; } = string.Empty;
    public string Directory { get; init; } = string.Empty;
    public IReadOnlyList<XsltDependency> Dependencies { get; init; } = Array.Empty<XsltDependency>();
    public IReadOnlyDictionary<string, XsltTestEnvironment> Environments { get; init; } =
        new Dictionary<string, XsltTestEnvironment>();
    public IReadOnlyList<XsltTestCase> Cases { get; init; } = Array.Empty<XsltTestCase>();

    /// <summary>Reads the catalog and returns the referenced test-set files.</summary>
    public static IReadOnlyList<XsltTestSetRef> LoadCatalog(string root)
    {
        XDocument doc = XDocument.Load(Path.Combine(root, "catalog.xml"));
        return doc.Root!.Elements()
            .Where(e => e.Name.LocalName == "test-set")
            .Select(e => new XsltTestSetRef((string?)e.Attribute("name") ?? string.Empty, (string?)e.Attribute("file") ?? string.Empty))
            .ToList();
    }

    /// <summary>Loads and parses one test-set file.</summary>
    public static XsltTestSet Load(string filePath)
    {
        XElement root = XDocument.Load(filePath).Root!;
        string dir = Path.GetDirectoryName(filePath) ?? string.Empty;

        Dictionary<string, XsltTestEnvironment> envs = new Dictionary<string, XsltTestEnvironment>(StringComparer.Ordinal);
        foreach (XElement envEl in root.Elements().Where(e => e.Name.LocalName == "environment"))
        {
            XsltTestEnvironment env = ParseEnvironment(envEl, dir);
            if (env.Name.Length > 0)
            {
                envs[env.Name] = env;
            }
        }

        List<XsltTestCase> cases = new List<XsltTestCase>();
        foreach (XElement tc in root.Elements().Where(e => e.Name.LocalName == "test-case"))
        {
            XsltTestCase? parsed = ParseCase(tc, dir);
            if (parsed is not null)
            {
                cases.Add(parsed);
            }
        }

        return new XsltTestSet
        {
            Name = (string?)root.Attribute("name") ?? string.Empty,
            Directory = dir,
            Dependencies = ParseDependencies(root),
            Environments = envs,
            Cases = cases,
        };
    }

    private static XsltTestCase? ParseCase(XElement tc, string dir)
    {
        string? name = (string?)tc.Attribute("name");
        XElement? test = tc.Elements().FirstOrDefault(e => e.Name.LocalName == "test");
        if (name is null || test is null)
        {
            return null;
        }

        XElement? envEl = tc.Elements().FirstOrDefault(e => e.Name.LocalName == "environment");
        string? envRef = (string?)envEl?.Attribute("ref");
        XsltTestEnvironment? inline = envEl is not null && envRef is null ? ParseEnvironment(envEl, dir) : null;

        XElement? stylesheet = test.Elements().FirstOrDefault(e =>
            e.Name.LocalName == "stylesheet" &&
            ((string?)e.Attribute("role") is null or "principal"));
        string? file = (string?)stylesheet?.Attribute("file");

        return new XsltTestCase
        {
            Name = name,
            Dependencies = ParseDependencies(tc),
            EnvironmentRef = envRef,
            InlineEnvironment = inline,
            StylesheetFile = file is null ? null : Path.GetFullPath(Path.Combine(dir, file)),
            InitialTemplate = test.Elements().FirstOrDefault(e => e.Name.LocalName == "initial-template"),
            InitialMode = test.Elements().FirstOrDefault(e => e.Name.LocalName == "initial-mode"),
            Params = ParseParams(test),
            Result = tc.Elements().FirstOrDefault(e => e.Name.LocalName == "result"),
            UsesPackages = test.Elements().Any(e => e.Name.LocalName == "package"),
        };
    }

    private static XsltTestEnvironment ParseEnvironment(XElement el, string dir)
    {
        XsltTestEnvironment env = new XsltTestEnvironment { Name = (string?)el.Attribute("name") ?? string.Empty };
        foreach (XElement child in el.Elements())
        {
            if (child.Name.LocalName == "source")
            {
                string role = (string?)child.Attribute("role") ?? ".";
                string? file = (string?)child.Attribute("file");
                XElement? content = child.Elements().FirstOrDefault(e => e.Name.LocalName == "content");
                env.Sources.Add(new XsltTestSource(
                    role,
                    file is null ? null : Path.GetFullPath(Path.Combine(dir, file)),
                    content?.Value));
            }
        }

        env.Params.AddRange(ParseParams(el));
        return env;
    }

    private static List<XsltTestParam> ParseParams(XElement parent) =>
        parent.Elements()
            .Where(e => e.Name.LocalName == "param")
            .Select(e => new XsltTestParam(
                (string?)e.Attribute("name") ?? string.Empty,
                (string?)e.Attribute("select"),
                string.Equals((string?)e.Attribute("static"), "yes", StringComparison.Ordinal),
                e))
            .ToList();

    private static List<XsltDependency> ParseDependencies(XElement parent)
    {
        List<XsltDependency> list = new List<XsltDependency>();
        foreach (XElement deps in parent.Elements().Where(e => e.Name.LocalName == "dependencies"))
        {
            foreach (XElement dep in deps.Elements())
            {
                string? value = (string?)dep.Attribute("value");
                if (value is null)
                {
                    continue;
                }

                bool satisfied = !string.Equals((string?)dep.Attribute("satisfied"), "false", StringComparison.OrdinalIgnoreCase);
                list.Add(new XsltDependency(dep.Name.LocalName, value, satisfied));
            }
        }

        return list;
    }
}
