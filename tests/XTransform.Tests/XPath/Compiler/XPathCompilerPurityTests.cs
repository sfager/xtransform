// Tests for XPathCompiler purity and thread safety.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Compiler;

/// <summary>
/// Verifies that the XPath compiler is stateless across calls and safe to invoke concurrently.
/// </summary>
[TestFixture]
internal sealed class XPathCompilerPurityTests
{
    private static readonly IReadOnlyDictionary<QName, SequenceType> s_testVariables =
        new Dictionary<QName, SequenceType>
        {
            [new QName("x")] = SequenceType.AnySequence,
        };

    // ── Determinism ───────────────────────────────────────────────────────────

    [TestCase("42")]
    [TestCase("1 + 2 * 3")]
    [TestCase("for $x in (1, 2) return $x + 1")]
    [TestCase("if ($x) then \"yes\" else \"no\"")]
    [TestCase("/root/child::*[1]")]
    public void Compile_SameExpressionTwice_ProducesEquivalentTrees(string expression)
    {
        // Arrange
        IReadOnlyDictionary<QName, SequenceType> vars = expression.Contains("$x")
            ? s_testVariables
            : null!;

        // Act
        CompiledExpression first = XPathCompiler.ParseAndCompile(
            expression, SourceSpan.None, DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance, vars);

        CompiledExpression second = XPathCompiler.ParseAndCompile(
            expression, SourceSpan.None, DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance, vars);

        // Assert — deterministic output
        Assert.That(second.Root.ToDebugString(),
            Is.EqualTo(first.Root.ToDebugString()),
            "Second compile of the same expression produced a different tree.");

        Assert.That(second.SlotCount, Is.EqualTo(first.SlotCount),
            "Second compile produced a different slot count.");
    }

    // ── Parallel safety ───────────────────────────────────────────────────────

    [Test]
    public void Compile_ParallelInvocations_AllProduceEquivalentTrees()
    {
        // Arrange
        const string expression = "1 + 2 * 3";
        const int threadCount = 8;

        string[] results = new string[threadCount];
        string[] errors = new string[threadCount];

        // Act — compile the same expression from 8 concurrent threads
        Parallel.For(0, threadCount, i =>
        {
            try
            {
                CompiledExpression result = XPathCompiler.ParseAndCompile(
                    expression,
                    SourceSpan.None,
                    DictionaryNamespaceResolver.Default,
                    EmptyFunctionLibrary.Instance);
                results[i] = result.Root.ToDebugString();
            }
#pragma warning disable CA1031 // Catch only specific exceptions — intentional for test harness
            catch (System.Exception ex)
#pragma warning restore CA1031
            {
                errors[i] = ex.Message;
            }
        });

        // Assert — no exceptions
        for (int i = 0; i < threadCount; i++)
        {
            Assert.That(errors[i], Is.Null,
                $"Thread {i} threw an exception: {errors[i]}");
        }

        // Assert — all results are equal to thread 0's result
        string expected = results[0];
        for (int i = 1; i < threadCount; i++)
        {
            Assert.That(results[i], Is.EqualTo(expected),
                $"Thread {i} produced a different tree than thread 0.");
        }
    }

    [Test]
    public void Compile_ParallelWithDifferentExpressions_NoCrossContamination()
    {
        // Arrange
        string[] expressions =
        {
            "42",
            "\"hello\"",
            "1 + 2",
            "(1, 2, 3)",
            ".",
            "()",
            "a/b",
            "/root",
        };

        string[] referenceResults = new string[expressions.Length];
        for (int i = 0; i < expressions.Length; i++)
        {
            referenceResults[i] = XPathCompiler.ParseAndCompile(
                expressions[i],
                SourceSpan.None,
                DictionaryNamespaceResolver.Default,
                EmptyFunctionLibrary.Instance).Root.ToDebugString();
        }

        string[][] parallelResults = new string[8][];
        for (int t = 0; t < 8; t++)
        {
            parallelResults[t] = new string[expressions.Length];
        }

        // Act — each thread compiles all expressions
        Parallel.For(0, 8, t =>
        {
            for (int e = 0; e < expressions.Length; e++)
            {
                CompiledExpression result = XPathCompiler.ParseAndCompile(
                    expressions[e],
                    SourceSpan.None,
                    DictionaryNamespaceResolver.Default,
                    EmptyFunctionLibrary.Instance);
                parallelResults[t][e] = result.Root.ToDebugString();
            }
        });

        // Assert — every thread's results match the reference results
        for (int t = 0; t < 8; t++)
        {
            for (int e = 0; e < expressions.Length; e++)
            {
                Assert.That(parallelResults[t][e], Is.EqualTo(referenceResults[e]),
                    $"Thread {t}, expression {e} ('{expressions[e]}') produced wrong tree.");
            }
        }
    }
}
