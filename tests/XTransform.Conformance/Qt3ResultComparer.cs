// Compares actual QT3 evaluation results against expected result assertions.
// XPath 2.0 §3 and F&O §15 for the semantics of the assertions.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Operators;

namespace XTransform.Conformance;

/// <summary>
/// Dispatches over QT3 &lt;result&gt; / assertion elements and compares them against an actual
/// XdmSequence. Returns a (Passed, Detail) pair.
/// </summary>
internal static class Qt3ResultComparer
{
    private static readonly XNamespace Ns = "http://www.w3.org/2010/09/qt-fots-catalog";

    /// <summary>
    /// Entry point: compare <paramref name="actual"/> (or <paramref name="exceptionCode"/> when
    /// an exception was thrown) against the &lt;result&gt; element.
    /// </summary>
    public static (bool Passed, string? Detail) Compare(
        XdmSequence? actual,
        XElement resultElement,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        // The <result> element wraps a single child assertion (or any-of / all-of).
        XElement? assertionEl = null;
        foreach (XElement child in resultElement.Elements())
        {
            assertionEl = child;
            break;
        }

        if (assertionEl is null)
        {
            return (false, "Empty <result> element.");
        }

        return CompareAssertion(actual, assertionEl, namespaces, exceptionCode);
    }

    private static (bool Passed, string? Detail) CompareAssertion(
        XdmSequence? actual,
        XElement assertion,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        string localName = assertion.Name.LocalName;

        switch (localName)
        {
            case "error":
                return CheckError(assertion, exceptionCode);

            case "assert-eq":
                return CheckAssertEq(actual, assertion, namespaces, exceptionCode);

            case "assert-deep-eq":
                return CheckAssertDeepEq(actual, assertion, namespaces, exceptionCode);

            case "assert-true":
                return CheckAssertEbv(actual, exceptionCode, expected: true);

            case "assert-false":
                return CheckAssertEbv(actual, exceptionCode, expected: false);

            case "assert-empty":
                return CheckAssertEmpty(actual, exceptionCode);

            case "assert-count":
                return CheckAssertCount(actual, assertion, exceptionCode);

            case "assert-type":
                return CheckAssertType(actual, assertion, namespaces, exceptionCode);

            case "assert-string-value":
                return CheckAssertStringValue(actual, assertion, exceptionCode);

            case "assert-permutation":
                return CheckAssertPermutation(actual, assertion, namespaces, exceptionCode);

            case "assert":
                return CheckAssert(actual, assertion, namespaces, exceptionCode);

            case "any-of":
                return CheckAnyOf(actual, assertion, namespaces, exceptionCode);

            case "all-of":
                return CheckAllOf(actual, assertion, namespaces, exceptionCode);

            case "assert-serialization-error":
            case "serialization-matches":
            case "assert-serialization":
            case "assert-xml":
            case "assert-message":
                return (false, $"Skipped: assertion '{localName}' not supported in full-eval mode.");

            default:
                return (false, $"Unknown assertion element: '{localName}'.");
        }
    }

    // ── error ─────────────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckError(XElement assertion, string? exceptionCode)
    {
        if (exceptionCode is null)
        {
            return (false, $"Expected error but evaluation succeeded.");
        }

        string? expectedCode = (string?)assertion.Attribute("code");
        if (expectedCode is null || expectedCode == "*")
        {
            // Any error code is acceptable
            return (true, null);
        }

        // The expected code is just the local part (e.g., "XPST0003")
        // Compare just the local part of the actual code
        int braceIdx = exceptionCode.LastIndexOf('}');
        string actualLocal = braceIdx >= 0
            ? exceptionCode.Substring(braceIdx + 1)
            : exceptionCode;

        if (string.Equals(actualLocal, expectedCode, StringComparison.Ordinal))
        {
            return (true, null);
        }

        // Some tests list multiple acceptable codes via any-of; this handles the single-error case
        return (false, $"Expected error {expectedCode} but got {actualLocal}.");
    }

    // ── assert-eq ────────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssertEq(
        XdmSequence? actual,
        XElement assertion,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected a value but got error: {exceptionCode}.");
        }

        if (actual is null || actual.Count == 0)
        {
            return (false, "Expected a value but got empty sequence.");
        }

        if (actual.Count != 1)
        {
            return (false, $"Expected a single value but got {actual.Count} items.");
        }

        // Evaluate the assertion expression to get the expected value
        string assertExpr = assertion.Value.Trim();
        XdmSequence? expected = EvaluateExpression(assertExpr, namespaces, null, out string? evalError);
        if (evalError is not null)
        {
            return (false, $"Could not evaluate assert-eq expression '{assertExpr}': {evalError}");
        }

        if (expected is null || expected.Count == 0)
        {
            return (false, $"assert-eq expression '{assertExpr}' returned empty.");
        }

        if (expected.Count != 1)
        {
            return (false, $"assert-eq expression returned {expected.Count} items.");
        }

        XdmItem actualItem = actual[0];
        XdmItem expectedItem = expected[0];

        return CompareItems(actualItem, expectedItem);
    }

    // ── assert-deep-eq ────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssertDeepEq(
        XdmSequence? actual,
        XElement assertion,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected a value but got error: {exceptionCode}.");
        }

        actual ??= XdmSequence.Empty;

        string assertExpr = assertion.Value.Trim();
        XdmSequence? expected = EvaluateExpression(assertExpr, namespaces, null, out string? evalError);
        if (evalError is not null)
        {
            return (false, $"Could not evaluate assert-deep-eq expression '{assertExpr}': {evalError}");
        }

        expected ??= XdmSequence.Empty;

        bool deepEq = DeepEqualSeq(actual, expected);
        if (deepEq)
        {
            return (true, null);
        }

        return (false, $"assert-deep-eq failed: actual count={actual.Count}, expected count={expected.Count}.");
    }

    // ── assert-true / assert-false ────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssertEbv(
        XdmSequence? actual,
        string? exceptionCode,
        bool expected)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected {(expected ? "true" : "false")} but got error: {exceptionCode}.");
        }

        if (actual is null || actual.Count == 0)
        {
            bool ebv = false;
            return expected == ebv ? (true, null) : (false, $"Expected {expected} but EBV was {ebv}.");
        }

        bool actualEbv;
        try
        {
            actualEbv = CalculateEbv(actual);
        }
        catch (Exception ex)
        {
            return (false, $"EBV calculation failed: {ex.Message}");
        }

        return expected == actualEbv
            ? (true, null)
            : (false, $"Expected {expected} but got {actualEbv}.");
    }

    // ── assert-empty ─────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssertEmpty(XdmSequence? actual, string? exceptionCode)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected empty sequence but got error: {exceptionCode}.");
        }

        int count = actual?.Count ?? 0;
        return count == 0 ? (true, null) : (false, $"Expected empty sequence but got {count} items.");
    }

    // ── assert-count ──────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssertCount(
        XdmSequence? actual,
        XElement assertion,
        string? exceptionCode)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected a value but got error: {exceptionCode}.");
        }

        int expected;
        if (!int.TryParse(assertion.Value.Trim(), out expected))
        {
            return (false, $"Invalid assert-count value: '{assertion.Value}'.");
        }

        int actual2 = actual?.Count ?? 0;
        return actual2 == expected ? (true, null) : (false, $"Expected {expected} items but got {actual2}.");
    }

    // ── assert-type ──────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssertType(
        XdmSequence? actual,
        XElement assertion,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected a typed value but got error: {exceptionCode}.");
        }

        actual ??= XdmSequence.Empty;
        string typeStr = assertion.Value.Trim();

        // Compile an "instance of" expression to test the type
        string testExpr = "$__result__ instance of " + typeStr;
        XdmSequence? result = EvaluateExpressionWithArg(
            testExpr, namespaces, "$__result__", actual, out string? evalError);

        if (evalError is not null)
        {
            // If we can't parse the type (e.g. schema type), skip
            if (evalError.Contains("XPST0051") || evalError.Contains("unknown") || evalError.Contains("static"))
            {
                return (false, $"Skipped: cannot parse type '{typeStr}': {evalError}");
            }
            return (false, $"assert-type evaluation failed for '{typeStr}': {evalError}");
        }

        bool matches = result?.Count == 1 && result[0] is XdmAtomicValue av && av.AsBoolean();
        return matches ? (true, null) : (false, $"Value does not match type '{typeStr}'.");
    }

    // ── assert-string-value ───────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssertStringValue(
        XdmSequence? actual,
        XElement assertion,
        string? exceptionCode)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected a value but got error: {exceptionCode}.");
        }

        actual ??= XdmSequence.Empty;

        string expectedStr = assertion.Value;
        bool normalizeSpace = string.Equals(
            (string?)assertion.Attribute("normalize-space"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        // Build the string value: atomize each item and concatenate with space
        // F&O §15.1.1: fn:string-join semantics for a sequence of atomics
        StringBuilder sb = new StringBuilder();
        int count = actual.Count;
        for (int i = 0; i < count; i++)
        {
            XdmItem item = actual[i];
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(item.GetStringValue());
        }

        string actualStr = sb.ToString();

        if (normalizeSpace)
        {
            actualStr = NormalizeWhitespace(actualStr);
            expectedStr = NormalizeWhitespace(expectedStr);
        }

        return string.Equals(actualStr, expectedStr, StringComparison.Ordinal)
            ? (true, null)
            : (false, $"String value mismatch: expected '{Truncate(expectedStr)}', got '{Truncate(actualStr)}'.");
    }

    // ── assert-permutation ────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssertPermutation(
        XdmSequence? actual,
        XElement assertion,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected a value but got error: {exceptionCode}.");
        }

        actual ??= XdmSequence.Empty;

        string assertExpr = assertion.Value.Trim();
        XdmSequence? expected = EvaluateExpression(assertExpr, namespaces, null, out string? evalError);
        if (evalError is not null)
        {
            return (false, $"Could not evaluate assert-permutation expression: {evalError}");
        }

        expected ??= XdmSequence.Empty;

        if (actual.Count != expected.Count)
        {
            return (false, $"Permutation count mismatch: got {actual.Count}, expected {expected.Count}.");
        }

        // Multi-set equality: each item in expected must appear in actual (using value-equal)
        List<bool> matched = new List<bool>(actual.Count);
        for (int i = 0; i < actual.Count; i++)
        {
            matched.Add(false);
        }

        int expCount = expected.Count;
        for (int ei = 0; ei < expCount; ei++)
        {
            XdmItem expItem = expected[ei];
            bool found = false;
            int actCount = actual.Count;
            for (int ai = 0; ai < actCount; ai++)
            {
                if (matched[ai])
                {
                    continue;
                }

                try
                {
                    (bool eq, string? _) = CompareItems(actual[ai], expItem);
                    if (eq)
                    {
                        matched[ai] = true;
                        found = true;
                        break;
                    }
                }
                catch
                {
                    // Incomparable items
                }
            }

            if (!found)
            {
                return (false, $"Permutation check: could not find matching item for expected[{ei}].");
            }
        }

        return (true, null);
    }

    // ── assert ────────────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAssert(
        XdmSequence? actual,
        XElement assertion,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        if (exceptionCode is not null)
        {
            return (false, $"Expected a value but got error: {exceptionCode}.");
        }

        actual ??= XdmSequence.Empty;

        // The assertion expression can reference $result
        string assertExpr = assertion.Value.Trim();
        XdmSequence? result = EvaluateExpressionWithArg(
            assertExpr, namespaces, "$result", actual, out string? evalError);

        if (evalError is not null)
        {
            return (false, $"assert expression evaluation failed: {evalError}");
        }

        bool passed;
        try
        {
            passed = result is not null && result.Count > 0 && CalculateEbv(result);
        }
        catch (Exception ex)
        {
            return (false, $"assert EBV failed: {ex.Message}");
        }

        return passed ? (true, null) : (false, $"assert expression was false: '{Truncate(assertExpr)}'.");
    }

    // ── any-of ────────────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAnyOf(
        XdmSequence? actual,
        XElement container,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        List<string> details = new List<string>();
        foreach (XElement child in container.Elements())
        {
            (bool passed, string? detail) = CompareAssertion(actual, child, namespaces, exceptionCode);
            if (passed)
            {
                return (true, null);
            }

            details.Add(detail ?? child.Name.LocalName + " failed");
        }

        return (false, "any-of: none passed: " + string.Join("; ", details));
    }

    // ── all-of ────────────────────────────────────────────────────────────────

    private static (bool Passed, string? Detail) CheckAllOf(
        XdmSequence? actual,
        XElement container,
        DictionaryNamespaceResolver namespaces,
        string? exceptionCode)
    {
        foreach (XElement child in container.Elements())
        {
            (bool passed, string? detail) = CompareAssertion(actual, child, namespaces, exceptionCode);
            if (!passed)
            {
                return (false, detail ?? "all-of: child failed: " + child.Name.LocalName);
            }
        }

        return (true, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates a plain XPath expression with no external variables.
    /// Returns null for empty sequence. evalError is non-null on failure.
    /// </summary>
    private static XdmSequence? EvaluateExpression(
        string expression,
        DictionaryNamespaceResolver namespaces,
        XdmItem? contextItem,
        out string? evalError)
    {
        return EvaluateExpressionCore(expression, namespaces, contextItem,
            variableName: null, variableValue: null, out evalError);
    }

    /// <summary>
    /// Evaluates an XPath expression with one external variable bound to <paramref name="variableValue"/>.
    /// </summary>
    private static XdmSequence? EvaluateExpressionWithArg(
        string expression,
        DictionaryNamespaceResolver namespaces,
        string variableName,
        XdmSequence variableValue,
        out string? evalError)
    {
        return EvaluateExpressionCore(expression, namespaces, contextItem: null,
            variableName: variableName, variableValue: variableValue, out evalError);
    }

    private static XdmSequence? EvaluateExpressionCore(
        string expression,
        DictionaryNamespaceResolver namespaces,
        XdmItem? contextItem,
        string? variableName,
        XdmSequence? variableValue,
        out string? evalError)
    {
        evalError = null;
        try
        {
            Dictionary<QName, SequenceType>? externalVars = null;
            if (variableName is not null && variableValue is not null)
            {
                // Strip leading $
                string varLocalName = variableName.TrimStart('$');
                QName varQName = new QName(string.Empty, string.Empty, varLocalName);
                externalVars = new Dictionary<QName, SequenceType>
                {
                    [varQName] = SequenceType.AnySequence,
                };
            }

            CompiledExpression compiled = XPathCompiler.ParseAndCompile(
                expression,
                SourceSpan.None,
                namespaces,
                BuiltInFunctionLibrary.Instance,
                externalVars);

            RunState run = new RunState();
            DynamicContext ctx = DynamicContext.Root(run).WithFrame(compiled.SlotCount);

            // Bind external variable if provided
            if (variableName is not null && variableValue is not null && compiled.SlotCount > 0)
            {
                // The external variable occupies slot 0 (first allocated)
                ctx.LocalFrame[0] = variableValue;
            }

            if (contextItem is not null)
            {
                ctx = ctx.WithContextItem(contextItem, 1, 1);
            }

            IXdmIterator iter = compiled.Root.Iterate(ctx);
            List<XdmItem> items = new List<XdmItem>();
            XdmItem? item;
            while ((item = iter.Next()) != null)
            {
                items.Add(item);
            }

            return XdmSequence.From(items);
        }
        catch (XTransformException ex)
        {
            evalError = ex.ErrorCode.LocalName + ": " + ex.Message;
            return null;
        }
        catch (Exception ex)
        {
            evalError = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Compares two items for value equality. F&amp;O §10.
    /// </summary>
    private static (bool Passed, string? Detail) CompareItems(XdmItem actualItem, XdmItem expectedItem)
    {
        // Nodes compared by identity is unusual in assert-eq; treat as string comparison
        if (actualItem is XdmNode || expectedItem is XdmNode)
        {
            string a = actualItem.GetStringValue();
            string e = expectedItem.GetStringValue();
            return string.Equals(a, e, StringComparison.Ordinal)
                ? (true, null)
                : (false, $"Node string-value mismatch: '{Truncate(a)}' != '{Truncate(e)}'.");
        }

        XdmAtomicValue av = (XdmAtomicValue)actualItem;
        XdmAtomicValue ev = (XdmAtomicValue)expectedItem;

        // Handle UntypedAtomic on expected side: cast to actual's type for comparison
        if (ev.TypeCode == XdmTypeCode.UntypedAtomic && av.TypeCode != XdmTypeCode.UntypedAtomic)
        {
            // Cast expected to string for comparison when actual is string
            if (av.TypeCode == XdmTypeCode.String)
            {
                return string.Equals(av.GetStringValue(), ev.GetStringValue(), StringComparison.Ordinal)
                    ? (true, null)
                    : (false, $"Mismatch: '{av.GetStringValue()}' != '{ev.GetStringValue()}'.");
            }
        }

        bool? eq = ComparisonOperators.ValueEqual(av, ev);
        if (eq is null)
        {
            return (false, $"Types {av.TypeCode} and {ev.TypeCode} are not comparable.");
        }

        return eq.Value ? (true, null) : (false, $"Value mismatch: '{av.GetStringValue()}' != '{ev.GetStringValue()}'.");
    }

    /// <summary>Structural deep-equality check for sequences. F&amp;O §15.3.1.</summary>
    private static bool DeepEqualSeq(XdmSequence a, XdmSequence b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        int count = a.Count;
        for (int i = 0; i < count; i++)
        {
            if (!DeepEqualItem(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DeepEqualItem(XdmItem a, XdmItem b)
    {
        if (a is XdmAtomicValue av1 && b is XdmAtomicValue av2)
        {
            bool? eq = ComparisonOperators.ValueEqual(av1, av2);
            return eq == true;
        }

        if (a is XdmNode na && b is XdmNode nb)
        {
            return DeepEqualNodes(na, nb);
        }

        return false;
    }

    private static bool DeepEqualNodes(XdmNode a, XdmNode b)
    {
        if (a.NodeKind != b.NodeKind)
        {
            return false;
        }

        return string.Equals(a.GetStringValue(), b.GetStringValue(), StringComparison.Ordinal);
    }

    /// <summary>Calculates EBV for a sequence. F&amp;O §15.1.1.</summary>
    private static bool CalculateEbv(XdmSequence seq)
    {
        if (seq.Count == 0)
        {
            return false;
        }

        XdmItem first = seq[0];
        if (first.IsNode)
        {
            return true;
        }

        if (seq.Count > 1)
        {
            throw new InvalidOperationException("EBV undefined for sequence with multiple atomic items.");
        }

        XdmAtomicValue av = (XdmAtomicValue)first;
        switch (av.TypeCode)
        {
            case XdmTypeCode.Boolean:
                return av.AsBoolean();
            case XdmTypeCode.String:
            case XdmTypeCode.UntypedAtomic:
            case XdmTypeCode.AnyUri:
                return av.GetStringValue().Length > 0;
            case XdmTypeCode.Double:
                double d = av.AsDouble();
                return !double.IsNaN(d) && d != 0.0;
            case XdmTypeCode.Float:
                float f = av.AsFloat();
                return !float.IsNaN(f) && f != 0.0f;
            case XdmTypeCode.Decimal:
                return av.AsDecimal().Value != 0m;
            default:
                return av.AsBigInteger() != System.Numerics.BigInteger.Zero;
        }
    }

    /// <summary>Normalizes whitespace like fn:normalize-space.</summary>
    private static string NormalizeWhitespace(string s)
    {
        StringBuilder sb = new StringBuilder(s.Length);
        bool inWhitespace = false;
        bool leadingSpace = true;

        foreach (char c in s)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!leadingSpace)
                {
                    inWhitespace = true;
                }
            }
            else
            {
                if (inWhitespace)
                {
                    sb.Append(' ');
                }

                sb.Append(c);
                inWhitespace = false;
                leadingSpace = false;
            }
        }

        return sb.ToString();
    }

    private static string Truncate(string s, int max = 80)
    {
        return s.Length > max ? string.Concat(s.AsSpan(0, max), "...") : s;
    }
}
