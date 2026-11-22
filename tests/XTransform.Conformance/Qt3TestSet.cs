using System.Collections.Generic;
using System.Xml.Linq;

namespace XTransform.Conformance;

/// <summary>A parsed QT3 test-set file.</summary>
internal sealed class Qt3TestSet
{
    private static readonly XNamespace Ns = "http://www.w3.org/2010/09/qt-fots-catalog";

    /// <summary>The name of this test set.</summary>
    public string Name { get; }

    /// <summary>The absolute file path of this test-set file.</summary>
    public string FilePath { get; }

    /// <summary>Dependencies declared at the test-set level (apply to all test cases unless overridden).</summary>
    public IReadOnlyList<Qt3Dependency> SetLevelDependencies { get; }

    /// <summary>All test cases in this test set.</summary>
    public IReadOnlyList<Qt3TestCase> TestCases { get; }

    private Qt3TestSet(
        string name,
        string filePath,
        IReadOnlyList<Qt3Dependency> setLevelDependencies,
        IReadOnlyList<Qt3TestCase> testCases)
    {
        Name = name;
        FilePath = filePath;
        SetLevelDependencies = setLevelDependencies;
        TestCases = testCases;
    }

    /// <summary>Loads and parses a test-set file.</summary>
    public static Qt3TestSet Load(string filePath)
    {
        XDocument doc = XDocument.Load(filePath);
        XElement root = doc.Root!;

        string name = (string?)root.Attribute("name") ?? string.Empty;

        // Set-level dependencies: direct <dependency> children and children of <dependencies>
        List<Qt3Dependency> setDeps = ReadDependencies(root);

        List<Qt3TestCase> cases = new();
        foreach (XElement tcEl in root.Elements(Ns + "test-case"))
        {
            Qt3TestCase? tc = ParseTestCase(tcEl);
            if (tc is not null)
            {
                cases.Add(tc);
            }
        }

        return new Qt3TestSet(name, filePath, setDeps, cases);
    }

    private static Qt3TestCase? ParseTestCase(XElement el)
    {
        string? name = (string?)el.Attribute("name");
        if (name is null)
        {
            return null;
        }

        // Test expression — may be CDATA or text
        XElement? testEl = el.Element(Ns + "test");
        string testExpression = testEl?.Value ?? string.Empty;

        // Case-level dependencies
        List<Qt3Dependency> deps = ReadDependencies(el);

        // Result
        XElement? resultEl = el.Element(Ns + "result");
        Qt3Result result = resultEl is not null
            ? ParseResult(resultEl)
            : new Qt3Result(System.Array.Empty<string>(), true);

        return new Qt3TestCase(name, testExpression, deps, result);
    }

    /// <summary>
    /// Reads dependency elements from direct <c>&lt;dependency&gt;</c> children and
    /// from within <c>&lt;dependencies&gt;</c> wrapper elements (both occur in QT3).
    /// </summary>
    private static List<Qt3Dependency> ReadDependencies(XElement parent)
    {
        List<Qt3Dependency> deps = new();

        // Direct <dependency> children
        foreach (XElement dep in parent.Elements(Ns + "dependency"))
        {
            Qt3Dependency? d = ParseDependency(dep);
            if (d is not null)
            {
                deps.Add(d);
            }
        }

        // <dependencies> wrapper
        foreach (XElement wrapper in parent.Elements(Ns + "dependencies"))
        {
            foreach (XElement dep in wrapper.Elements(Ns + "dependency"))
            {
                Qt3Dependency? d = ParseDependency(dep);
                if (d is not null)
                {
                    deps.Add(d);
                }
            }
        }

        return deps;
    }

    private static Qt3Dependency? ParseDependency(XElement el)
    {
        string? type = (string?)el.Attribute("type");
        string? value = (string?)el.Attribute("value");
        if (type is null || value is null)
        {
            return null;
        }

        string? satisfiedAttr = (string?)el.Attribute("satisfied");
        bool satisfied = satisfiedAttr is null ||
                         !string.Equals(satisfiedAttr, "false", System.StringComparison.OrdinalIgnoreCase);

        return new Qt3Dependency(type, value, satisfied);
    }

    /// <summary>
    /// Recursively scans a result element for <c>&lt;error&gt;</c> codes and presence of
    /// assertion elements. Handles <c>&lt;any-of&gt;</c> and <c>&lt;all-of&gt;</c> containers.
    /// </summary>
    private static Qt3Result ParseResult(XElement resultEl)
    {
        List<string> errorCodes = new();
        bool acceptsAny = false;

        CollectResultInfo(resultEl, errorCodes, ref acceptsAny);

        return new Qt3Result(errorCodes, acceptsAny);
    }

    private static void CollectResultInfo(XElement el, List<string> errorCodes, ref bool acceptsAny)
    {
        string localName = el.Name.LocalName;

        switch (localName)
        {
            case "error":
                string? code = (string?)el.Attribute("code");
                if (code is not null && !errorCodes.Contains(code))
                {
                    errorCodes.Add(code);
                }
                break;

            case "result":
            case "any-of":
            case "all-of":
                // Recurse into containers
                foreach (XElement child in el.Elements())
                {
                    CollectResultInfo(child, errorCodes, ref acceptsAny);
                }
                break;

            default:
                // Any assertion other than <error> means we cannot judge by parse-only
                // (we still mark acceptsAny = true for bookkeeping, but parse-only logic
                // treats non-error results as "expect success")
                acceptsAny = true;
                break;
        }
    }
}

/// <summary>A dependency declaration on a test set or test case.</summary>
internal sealed record Qt3Dependency(string Type, string Value, bool Satisfied);

/// <summary>A single QT3 test case.</summary>
internal sealed record Qt3TestCase(
    string Name,
    string TestExpression,
    IReadOnlyList<Qt3Dependency> Dependencies,
    Qt3Result Result);

/// <summary>
/// The expected result of a QT3 test case, reduced to what is relevant for parse-only mode.
/// </summary>
internal sealed record Qt3Result(IReadOnlyList<string> ExpectedErrorCodes, bool AcceptsAnyResult);
