using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

namespace XTransform.Tests.XPath;

/// <summary>Golden-file driven parser tests.</summary>
[TestFixture]
internal sealed class XPathParserTests
{
    [TestCaseSource(nameof(GoldenCases))]
    public void ParseExpression_MatchesExpectedTree(string label, string expression, string expectedTree)
    {
        // Arrange / Act
        XPathAst ast = XPathParser.ParseExpression(expression, SourceSpan.None, DictionaryNamespaceResolver.Default);

        // Assert
        string actual = ast.ToDebugString().Replace("\r\n", "\n").TrimEnd();
        Assert.That(actual, Is.EqualTo(expectedTree.TrimEnd()), $"Case: {label}");
    }

    private static IEnumerable<TestCaseData> GoldenCases()
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "XPath", "ParserCases.txt");
        string text = File.ReadAllText(path);
        string[] lines = text.Replace("\r\n", "\n").Split('\n');

        string? label = null;
        string? expression = null;
        var treeLines = new List<string>();
        int state = 0; // 0=expect label/expr, 1=reading tree

        foreach (string raw in lines)
        {
            string line = raw;

            if (state == 0)
            {
                if (line.Length > 0 && line[0] == '#')
                {
                    label = line.Substring(1).Trim();
                }
                else if (line.Length > 0)
                {
                    expression = line;
                    state = 1;
                }
                // blank line in state 0 — skip
            }
            else // state == 1
            {
                if (line.Length == 0)
                {
                    // end of block
                    if (expression != null && treeLines.Count > 0)
                    {
                        string tree = string.Join("\n", treeLines);
                        yield return new TestCaseData(label ?? expression, expression, tree)
                            .SetName($"Parser_{label ?? expression}");
                    }
                    label = null;
                    expression = null;
                    treeLines.Clear();
                    state = 0;
                }
                else
                {
                    treeLines.Add(line);
                }
            }
        }

        // Last block (no trailing blank line)
        if (expression != null && treeLines.Count > 0)
        {
            string tree = string.Join("\n", treeLines);
            yield return new TestCaseData(label ?? expression, expression, tree)
                .SetName($"Parser_{label ?? expression}");
        }
    }
}
