using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace XTransform.Tests.Stylesheets;

/// <summary>
/// Real-world stylesheets (P3-17): each directory under <c>Stylesheets/RealWorld</c> holds
/// <c>style.xsl</c>, <c>input.xml</c> and <c>expected.out</c> (rendered by a reference processor,
/// see <c>eng/generate-realworld-expected.ps1</c>). Output must match byte for byte.
/// Explicit until gate G3: run with <c>eng/check-realworld.sh</c>.
/// </summary>
[TestFixture]
[Explicit("Real-world stylesheets are a gate G3 check; run via eng/check-realworld.sh")]
internal sealed class RealWorldStylesheetTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        yield return Case("docbook-article", "sections_lists_inline_markup_and_xrefs");
        yield return Case("dita-task", "modes_named_templates_and_with_param");
        yield return Case("order-invoice", "keys_params_and_recursive_totals");
        yield return Case("config-sanitizer", "identity_transform_with_overrides");
        yield return Case("catalog-report", "text_output_and_string_functions");
    }

    private static TestCaseData Case(string name, string aspect) =>
        new TestCaseData(name).SetName($"RealWorld_{name}_{aspect}");

    [TestCaseSource(nameof(Cases))]
    public void Transform_RealWorldStylesheet_MatchesReferenceOutputByteForByte(string name)
    {
        // Arrange
        string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "Stylesheets", "RealWorld", name);
        byte[] expected = File.ReadAllBytes(Path.Combine(dir, "expected.out"));

        // Act
        byte[] actual = Render(dir);

        // Assert
        Assert.That(actual, Is.EqualTo(expected), () => "actual output:\n" + System.Text.Encoding.UTF8.GetString(actual));
    }

    /// <summary>Rewrites expected.out in the source tree from this engine. Manual use only.</summary>
    [Test]
    [Category("RegenerateRealWorld")]
    public void Regenerate_ExpectedOutputs_FromThisEngine()
    {
        // Arrange
        string? repoDir = TestContext.CurrentContext.TestDirectory;
        while (repoDir is not null && !Directory.Exists(Path.Combine(repoDir, "tests", "XTransform.Tests", "Stylesheets", "RealWorld")))
        {
            repoDir = Path.GetDirectoryName(repoDir);
        }

        Assert.That(repoDir, Is.Not.Null, "repository root not found");
        string source = Path.Combine(repoDir!, "tests", "XTransform.Tests", "Stylesheets", "RealWorld");

        // Act
        foreach (string dir in Directory.GetDirectories(source))
        {
            File.WriteAllBytes(Path.Combine(dir, "expected.out"), Render(dir));
        }

        // Assert
        Assert.Pass("expected.out files rewritten; review the diff before committing.");
    }

    private static byte[] Render(string dir)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new Uri(Path.Combine(dir, "style.xsl")));
        using FileStream input = File.OpenRead(Path.Combine(dir, "input.xml"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return output.ToArray();
    }
}
