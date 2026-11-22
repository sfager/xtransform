// Tests for XPathCompiler: tree shape via ToDebugString, all parser cases compile.

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Compiler;

/// <summary>
/// Verifies that the XPath compiler produces the expected ExpressionNode tree shapes
/// as reported by <c>ToDebugString()</c>. The golden parser cases are compiled without
/// throwing — which proves the pipeline handles all 205+ expressions.
/// </summary>
[TestFixture]
internal sealed class XPathCompilerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// All variables that appear as free variables in ParserCases.txt, pre-bound
    /// to xs:anyAtomicType? (most general type) so the compiler can resolve them.
    /// </summary>
    private static readonly IReadOnlyDictionary<QName, SequenceType> s_testVariables =
        new Dictionary<QName, SequenceType>
        {
            [new QName("x")] = SequenceType.AnySequence,
            [new QName("http://www.w3.org/2005/xpath-functions", "x")] = SequenceType.AnySequence,
            [new QName("fn", "http://www.w3.org/2005/xpath-functions", "x")] = SequenceType.AnySequence,
            [new QName("i")] = SequenceType.AnySequence,
            [new QName("j")] = SequenceType.AnySequence,
            [new QName("items")] = SequenceType.AnySequence,
            [new QName("debug")] = SequenceType.AnySequence,
        };

    /// <summary>
    /// Compiles using AcceptAllFunctionLibrary and pre-seeded external variables.
    /// Used for the all-parser-cases coverage test.
    /// </summary>
    private static CompiledExpression CompileExpression(string expression)
    {
        return XPathCompiler.ParseAndCompile(
            expression,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            AcceptAllFunctionLibrary.Instance,
            s_testVariables);
    }

    /// <summary>
    /// Compiles without any external variables or functions (clean slot numbering).
    /// Used for explicit shape tests where slot indices must be predictable.
    /// Variable-free and function-free expressions only.
    /// </summary>
    private static CompiledExpression CompileFresh(string expression)
    {
        return XPathCompiler.ParseAndCompile(
            expression,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            AcceptAllFunctionLibrary.Instance);
    }

    /// <summary>
    /// Compiles with specific external variable bindings and no functions (clean slots).
    /// </summary>
    private static CompiledExpression CompileWithVars(
        string expression,
        IReadOnlyDictionary<QName, SequenceType> vars)
    {
        return XPathCompiler.ParseAndCompile(
            expression,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            AcceptAllFunctionLibrary.Instance,
            vars);
    }

    // ── All 205 parser cases compile without throwing ─────────────────────────

    [TestCaseSource(nameof(ParserCases))]
    public void Compile_AllParserCases_ProducesNonNullRoot(
        string label, string expression)
    {
        // Act
        CompiledExpression result = CompileExpression(expression);

        // Assert
        Assert.That(result, Is.Not.Null, $"Compile returned null for case: {label}");
        Assert.That(result.Root, Is.Not.Null, $"Root is null for case: {label}");
        Assert.That(result.Root.ToDebugString(), Is.Not.Null.And.Not.Empty,
            $"ToDebugString empty for case: {label}");
    }

    private static IEnumerable<TestCaseData> ParserCases()
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "XPath", "ParserCases.txt");
        string text = File.ReadAllText(path);
        string[] lines = text.Replace("\r\n", "\n").Split('\n');

        string? label = null;
        string? expression = null;
        int state = 0;

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
            }
            else
            {
                if (line.Length == 0)
                {
                    if (expression != null)
                    {
                        yield return new TestCaseData(label ?? expression, expression)
                            .SetName("Compile_" + (label ?? expression));
                    }

                    label = null;
                    expression = null;
                    state = 0;
                }
            }
        }

        // Last block (no trailing blank line)
        if (expression != null)
        {
            yield return new TestCaseData(label ?? expression, expression)
                .SetName("Compile_" + (label ?? expression));
        }
    }

    // ── Explicit shape tests ───────────────────────────────────────────────────

    [Test]
    public void Compile_IntegerLiteral_ProducesLiteralNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("42");

        // Assert
        Assert.That(result.Root.ToDebugString(), Is.EqualTo("LiteralNode(xs:integer, 42)"));
    }

    [Test]
    public void Compile_IntegerLiteral_StaticTypeIsExactInteger()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("42");

        // Assert
        Assert.That(result.Root.StaticType.ItemType.ToString(), Is.EqualTo("xs:integer"));
        Assert.That(result.Root.StaticType.Occurrence, Is.EqualTo(Occurrence.ExactlyOne));
    }

    [Test]
    public void Compile_EmptyParens_ProducesEmptySequenceNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("()");

        // Assert
        Assert.That(result.Root.ToDebugString(), Is.EqualTo("EmptySequenceNode"));
        Assert.That(result.Root.StaticType, Is.EqualTo(SequenceType.EmptySequence));
    }

    [Test]
    public void Compile_ContextItem_ProducesContextItemNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh(".");

        // Assert
        Assert.That(result.Root.ToDebugString(), Is.EqualTo("ContextItemNode"));
    }

    [Test]
    public void Compile_ParenthesizedContextItem_UnwrapsToContextItemNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("(.)");

        // Assert — parens are transparent; inner node is exposed.
        Assert.That(result.Root.ToDebugString(), Is.EqualTo("ContextItemNode"));
    }

    [Test]
    public void Compile_SequenceThreeItems_ProducesSequenceConcatNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("(1, 2, 3)");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("SequenceConcatNode(3 items)"));
        Assert.That(debug, Does.Contain("LiteralNode(xs:integer, 1)"));
        Assert.That(debug, Does.Contain("LiteralNode(xs:integer, 2)"));
        Assert.That(debug, Does.Contain("LiteralNode(xs:integer, 3)"));
    }

    [Test]
    public void Compile_ForExpression_ProducesForNodeWithSlot()
    {
        // Arrange / Act — no external variables: $x gets slot 0
        CompiledExpression result = CompileFresh("for $x in (1, 2) return $x");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("ForNode([$x@slot0])"));
        Assert.That(debug, Does.Contain("VariableRefNode(x@slot0)"));
        Assert.That(result.SlotCount, Is.EqualTo(1));
    }

    [Test]
    public void Compile_IfExpression_ProducesIfNodeWithThreeChildren()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("if (1) then 2 else 3");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("IfNode"));
        Assert.That(debug, Does.Contain("LiteralNode(xs:integer, 1)"));
        Assert.That(debug, Does.Contain("LiteralNode(xs:integer, 2)"));
        Assert.That(debug, Does.Contain("LiteralNode(xs:integer, 3)"));
    }

    [Test]
    public void Compile_ArithmeticPrecedence_ProducesCorrectTree()
    {
        // Arrange / Act — 1 + 2 * 3 → Add(1, Multiply(2, 3))
        CompiledExpression result = CompileFresh("1 + 2 * 3");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("ArithmeticNode(+)"));
        Assert.That(debug, Does.Contain("ArithmeticNode(*)"));
        Assert.That(debug, Does.Contain("LiteralNode(xs:integer, 1)"));
    }

    [Test]
    public void Compile_RelativePath_ProducesPathNodeWithSteps()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("a/b/c");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("PathNode(relative)"));
        Assert.That(debug, Does.Contain("AxisStepNode(child)"));
        Assert.That(debug, Does.Contain("NameTest(a)"));
        Assert.That(debug, Does.Contain("NameTest(b)"));
        Assert.That(debug, Does.Contain("NameTest(c)"));
    }

    [Test]
    public void Compile_AbsoluteDoubleSlash_ProducesAbsoluteDoubleSlashPath()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("//foo");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("PathNode(//)"));
        Assert.That(debug, Does.Contain("AxisStepNode(child)"));
        Assert.That(debug, Does.Contain("NameTest(foo)"));
    }

    [Test]
    public void Compile_InstanceOf_ProducesInstanceOfNode()
    {
        // Arrange / Act — $x pre-bound as single external variable at slot 0
        IReadOnlyDictionary<QName, SequenceType> vars = new Dictionary<QName, SequenceType>
        {
            [new QName("x")] = SequenceType.AnySequence,
        };
        CompiledExpression result = CompileWithVars("$x instance of element()", vars);

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("InstanceOfNode(element())"));
    }

    [Test]
    public void Compile_StringLiteral_StaticTypeIsExactString()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("\"hello\"");

        // Assert
        Assert.That(result.Root.StaticType.ItemType.ToString(), Is.EqualTo("xs:string"));
        Assert.That(result.Root.StaticType.Occurrence, Is.EqualTo(Occurrence.ExactlyOne));
    }

    [Test]
    public void Compile_DecimalLiteral_StaticTypeIsExactDecimal()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("3.14");

        // Assert
        Assert.That(result.Root.StaticType.ItemType.ToString(), Is.EqualTo("xs:decimal"));
        Assert.That(result.Root.StaticType.Occurrence, Is.EqualTo(Occurrence.ExactlyOne));
    }

    [Test]
    public void Compile_DoubleLiteral_StaticTypeIsExactDouble()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("1.5e10");

        // Assert
        Assert.That(result.Root.StaticType.ItemType.ToString(), Is.EqualTo("xs:double"));
        Assert.That(result.Root.StaticType.Occurrence, Is.EqualTo(Occurrence.ExactlyOne));
    }

    [Test]
    public void Compile_ForWithTwoClauses_AllocatesTwoSlots()
    {
        // Arrange / Act — no external variables: $i gets slot 0, $j gets slot 1
        CompiledExpression result = CompileFresh("for $i in a, $j in b return $i + $j");

        // Assert
        Assert.That(result.SlotCount, Is.EqualTo(2));
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.Contain("$i@slot0"));
        Assert.That(debug, Does.Contain("$j@slot1"));
    }

    [Test]
    public void Compile_SomeQuantified_ProducesQuantifiedNode()
    {
        // Arrange / Act — no external variables: $i gets slot 0
        CompiledExpression result = CompileFresh("some $i in (1, 2) satisfies $i > 0");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("QuantifiedNode(some,"));
        Assert.That(debug, Does.Contain("$i@slot0"));
    }

    [Test]
    public void Compile_CastAs_ProducesCastNode()
    {
        // Arrange / Act
        IReadOnlyDictionary<QName, SequenceType> vars = new Dictionary<QName, SequenceType>
        {
            [new QName("x")] = SequenceType.AnySequence,
        };
        CompiledExpression result = CompileWithVars("$x cast as xs:integer", vars);

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("CastNode(xs:integer)"));
    }

    [Test]
    public void Compile_OrExpression_ProducesOrNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("a or b");

        // Assert
        Assert.That(result.Root.ToDebugString(), Does.StartWith("OrNode"));
    }

    [Test]
    public void Compile_AndExpression_ProducesAndNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("a and b");

        // Assert
        Assert.That(result.Root.ToDebugString(), Does.StartWith("AndNode"));
    }

    [Test]
    public void Compile_RangeExpression_ProducesRangeNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("1 to 10");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("RangeNode(to)"));
        Assert.That(result.Root.StaticType.ItemType.ToString(), Is.EqualTo("xs:integer"));
    }

    [Test]
    public void Compile_UnionSet_ProducesSetOpNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("a | b");

        // Assert
        Assert.That(result.Root.ToDebugString(), Does.StartWith("SetOpNode(union)"));
    }

    [Test]
    public void Compile_FilterExpr_ProducesFilterNode()
    {
        // Arrange / Act — $items as single external at slot 0
        IReadOnlyDictionary<QName, SequenceType> vars = new Dictionary<QName, SequenceType>
        {
            [new QName("items")] = SequenceType.AnySequence,
        };
        CompiledExpression result = CompileWithVars("$items[1]", vars);

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("FilterNode"));
        Assert.That(debug, Does.Contain("VariableRefNode(items@slot0)"));
    }

    [Test]
    public void Compile_SlashAlone_ProducesEmptyPathNode()
    {
        // Arrange / Act
        CompiledExpression result = CompileFresh("/");

        // Assert
        string debug = result.Root.ToDebugString();
        Assert.That(debug, Does.StartWith("PathNode(/)"));
    }
}
