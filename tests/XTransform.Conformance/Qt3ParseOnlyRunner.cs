using System;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Conformance;

/// <summary>
/// Executes a single QT3 test case in parse-only mode: attempts to parse the XPath expression
/// and checks the result against the expected outcome.
/// </summary>
internal static class Qt3ParseOnlyRunner
{
    public enum Outcome
    {
        Pass,
        Fail,
        Skipped,
    }

    public readonly record struct Result(Outcome Outcome, string? Detail);

    /// <summary>
    /// Runs the parse-only check for one test expression.
    /// </summary>
    /// <param name="testExpression">The XPath expression text from the test case.</param>
    /// <param name="expectsXpst0003">
    ///   <see langword="true"/> when the test case expects an <c>XPST0003</c> error.
    ///   Per phase-1-tasks.md P1-14, parse-only agreement is "parses vs expects XPST0003".
    /// </param>
    /// <returns>The <see cref="Result"/> of the parse attempt.</returns>
    public static Result Run(string testExpression, bool expectsXpst0003)
    {
        try
        {
            XPathParser.ParseExpression(
                testExpression,
                SourceSpan.None,
                DictionaryNamespaceResolver.Default);

            // Parse succeeded — pass iff XPST0003 was not expected.
            return expectsXpst0003
                ? new Result(Outcome.Fail, "Expected XPST0003 but parse succeeded.")
                : new Result(Outcome.Pass, null);
        }
        catch (XPathStaticException)
        {
            // Any static exception satisfies an expected XPST0003.
            return expectsXpst0003
                ? new Result(Outcome.Pass, null)
                : new Result(Outcome.Fail, "Parse threw a static exception but XPST0003 was not expected.");
        }
        catch (Exception ex)
        {
            // Unexpected exception type — always a failure.
            return new Result(Outcome.Fail, $"Unexpected exception {ex.GetType().Name}: {ex.Message}");
        }
    }
}
