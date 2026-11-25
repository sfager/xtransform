// Runs one xslt30-test case through XsltCompiler + XsltTransformer and checks the result.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using XTransform.XPath;
using XTransform.XPath.Operators;
using XTransform.Xdm;

namespace XTransform.Conformance;

internal enum XsltOutcome
{
    Pass,
    Fail,
    Skip,
}

/// <summary>Which pipeline stage a failure came from.</summary>
internal enum XsltFailureStage
{
    None,
    Compile,
    Run,
    Compare,
}

internal sealed record XsltTestResult(XsltOutcome Outcome, XsltFailureStage Stage, string? Detail);

internal static class XsltTestRunner
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(8);

    private static readonly Dictionary<string, string> s_defaultPrefixes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["xs"] = "http://www.w3.org/2001/XMLSchema",
        ["fn"] = "http://www.w3.org/2005/xpath-functions",
        ["xsl"] = "http://www.w3.org/1999/XSL/Transform",
        ["err"] = "http://www.w3.org/2005/xqt-errors",
        ["math"] = "http://www.w3.org/2005/xpath-functions/math",
    };

    /// <summary>Runs one case under a watchdog: compile, run and comparison all count towards the limit.</summary>
    public static XsltTestResult Run(XsltTestCase tc, XsltTestSet set)
    {
        XsltTestResult? result = null;
        Exception? crash = null;
        Thread worker = new Thread(
            () =>
            {
                try
                {
                    result = RunUnguarded(tc, set);
                }
                catch (Exception ex)
                {
                    crash = ex;
                }
            },
            256 * 1024 * 1024)
        {
            IsBackground = true,
        };
        worker.Start();
        if (!worker.Join(s_timeout + TimeSpan.FromSeconds(20)))
        {
            return new XsltTestResult(XsltOutcome.Fail, XsltFailureStage.Run, "hang: compile, run or comparison did not finish");
        }

        if (crash is not null)
        {
            return new XsltTestResult(XsltOutcome.Fail, XsltFailureStage.Run, "harness exception: " + crash.GetType().Name + ": " + crash.Message);
        }

        return result!;
    }

    private static XsltTestResult RunUnguarded(XsltTestCase tc, XsltTestSet set)
    {
        if (tc.UsesPackages)
        {
            return Skip("packages are XSLT 3.0");
        }

        XsltTestEnvironment? env = tc.InlineEnvironment;
        if (tc.EnvironmentRef is not null && !set.Environments.TryGetValue(tc.EnvironmentRef, out env))
        {
            return Skip($"unknown environment '{tc.EnvironmentRef}'");
        }

        // The principal stylesheet comes from the test, or else from its environment.
        string? stylesheetFile = tc.StylesheetFile ?? env?.StylesheetFile;
        if (stylesheetFile is null || tc.Result is null)
        {
            return Skip("no principal stylesheet file or result");
        }

        List<XsltTestParam> allParams = new List<XsltTestParam>();
        if (env is not null)
        {
            allParams.AddRange(env.Params);
        }

        allParams.AddRange(tc.Params);
        if (allParams.Any(p => p.IsStatic))
        {
            return Skip("static parameters are not supported by the harness");
        }

        // ── Compile ──────────────────────────────────────────────────────────
        XsltExecutable? exe = null;
        List<string> errorCodes = new List<string>();
        string? errorDetail = null;
        bool isErrorStage = false;
        try
        {
            exe = new XsltCompiler().Compile(new Uri(stylesheetFile));
        }
        catch (Exception ex)
        {
            Capture(ex, errorCodes, out errorDetail);
            isErrorStage = true;
            if (ex is NotSupportedException)
            {
                return Skip(errorDetail);
            }
        }

        // ── Run ──────────────────────────────────────────────────────────────
        string? output = null;
        if (exe is not null)
        {
            try
            {
                output = Execute(exe, tc, env, allParams);
            }
            catch (SkipException sk)
            {
                return Skip(sk.Message);
            }
            catch (NotSupportedException ex)
            {
                return Skip(ex.Message);
            }
            catch (Exception ex)
            {
                Capture(ex, errorCodes, out errorDetail);
            }
        }

        XsltFailureStage failStage = exe is null ? XsltFailureStage.Compile : XsltFailureStage.Run;
        _ = isErrorStage;

        // ── Compare ──────────────────────────────────────────────────────────
        XsltActual actual = new XsltActual(output, errorCodes, errorDetail, set.Directory);
        (bool passed, string? detail) = XsltResultComparer.Check(tc.Result.Elements().FirstOrDefault(), actual);
        if (passed)
        {
            return new XsltTestResult(XsltOutcome.Pass, XsltFailureStage.None, null);
        }

        XsltFailureStage stage = actual.ErrorCodes.Count > 0 ? failStage : XsltFailureStage.Compare;
        if (detail is not null && detail.StartsWith("unsupported:", StringComparison.Ordinal))
        {
            return Skip(detail);
        }

        return new XsltTestResult(XsltOutcome.Fail, stage, detail);
    }

    private static XsltTestResult Skip(string? why) => new XsltTestResult(XsltOutcome.Skip, XsltFailureStage.None, why);

    private static void Capture(Exception ex, List<string> codes, out string detail)
    {
        if (ex is XsltStaticException sx)
        {
            codes.AddRange(sx.Errors.Select(e => e.Code.LocalName));
            detail = string.Join("; ", sx.Errors.Select(e => e.Code.LocalName + ": " + e.Message));
        }
        else if (ex is XTransformException tx)
        {
            codes.Add(tx.ErrorCode.LocalName);
            detail = tx.ErrorCode.LocalName + ": " + tx.Message;
        }
        else
        {
            detail = "internal " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static string Execute(XsltExecutable exe, XsltTestCase tc, XsltTestEnvironment? env, List<XsltTestParam> parameters)
    {
        XsltTransformer transformer = exe.CreateTransformer();

        if (tc.InitialTemplate is not null)
        {
            transformer.InitialTemplate = ResolveName(tc.InitialTemplate, "name");
        }

        if (tc.InitialMode is not null)
        {
            transformer.InitialMode = ResolveName(tc.InitialMode, "name");
        }

        foreach (XsltTestParam p in parameters)
        {
            if (p.Select is null)
            {
                throw new SkipException("parameter without select attribute");
            }

            XdmSequence value = EvaluateSelect(p.Select, p.Element);
            try
            {
                transformer.SetParameter(ResolveName(p.Element, "name"), value);
            }
            catch (ArgumentException)
            {
                // Undeclared parameters are ignored (XSLT 2.0 §9.5).
            }
        }

        XsltTestSource? source = env?.Sources.FirstOrDefault(s => s.Role == ".");
        Stream? input = null;
        if (source is not null)
        {
            input = source.File is not null
                ? new MemoryStream(File.ReadAllBytes(source.File))
                : new MemoryStream(Encoding.UTF8.GetBytes(source.Content ?? string.Empty));
        }

        if (input is null && transformer.InitialTemplate is null)
        {
            throw new SkipException("no source document and no initial template");
        }

        using (input)
        using (MemoryStream output = new MemoryStream())
        {
            // A dedicated thread with a large stack: deeply recursive stylesheets must reach the
            // engine's own depth limit (XTDE0810) rather than overflow the thread-pool stack.
            Exception? failure = null;
            using CancellationTokenSource cts = new CancellationTokenSource(s_timeout);
            Thread worker = new Thread(
                () =>
                {
                    try
                    {
                        transformer.Transform(input, output, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        failure = new TimeoutException("transformation timed out");
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                },
                256 * 1024 * 1024)
            {
                IsBackground = true,
            };
            worker.Start();

            // Cooperative cancellation stops the run at the next loop back-edge; the extra grace
            // period only covers code that never checks the token.
            if (!worker.Join(s_timeout + TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("transformation timed out and did not respond to cancellation");
            }

            if (failure is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return new UTF8Encoding(false).GetString(output.ToArray());
        }
    }

    private static QName ResolveName(XElement el, string attribute)
    {
        string raw = ((string?)el.Attribute(attribute) ?? string.Empty).Trim();
        if (raw.StartsWith("Q{", StringComparison.Ordinal))
        {
            int close = raw.IndexOf('}', StringComparison.Ordinal);
            return new QName(raw.Substring(2, close - 2), raw.Substring(close + 1));
        }

        int colon = raw.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return new QName(raw);
        }

        string prefix = raw.Substring(0, colon);
        XNamespace ns = el.GetNamespaceOfPrefix(prefix) ?? XNamespace.None;
        return new QName(prefix, ns.NamespaceName, raw.Substring(colon + 1));
    }

    internal static DictionaryNamespaceResolver NamespacesOf(XElement el)
    {
        Dictionary<string, string> prefixes = new Dictionary<string, string>(s_defaultPrefixes, StringComparer.Ordinal);
        foreach (XElement e in el.AncestorsAndSelf().Reverse())
        {
            foreach (XAttribute a in e.Attributes().Where(a => a.IsNamespaceDeclaration))
            {
                prefixes[a.Name.Namespace == XNamespace.None ? string.Empty : a.Name.LocalName] = a.Value;
            }
        }

        prefixes.Remove(string.Empty);
        return new DictionaryNamespaceResolver(prefixes);
    }

    private static XdmSequence EvaluateSelect(string select, XElement context)
    {
        CompiledExpression compiled = XPathCompiler.ParseAndCompile(
            select,
            SourceSpan.None,
            NamespacesOf(context),
            BuiltInFunctionLibrary.Instance);

        RunState run = new RunState();
        DynamicContext ctx = DynamicContext.Root(run).WithFrame(compiled.SlotCount);
        IXdmIterator iter = compiled.Root.Iterate(ctx);
        List<XdmItem> items = new List<XdmItem>();
        XdmItem? item;
        while ((item = iter.Next()) != null)
        {
            items.Add(item);
        }

        return XdmSequence.From(items);
    }

    private sealed class SkipException : Exception
    {
        public SkipException(string message)
            : base(message)
        {
        }
    }
}

/// <summary>What a run produced: serialized output, or error codes.</summary>
internal sealed class XsltActual
{
    public XsltActual(string? output, IReadOnlyList<string> errorCodes, string? errorDetail, string setDirectory)
    {
        Output = output;
        ErrorCodes = errorCodes;
        ErrorDetail = errorDetail;
        SetDirectory = setDirectory;
    }

    public string? Output { get; }
    public IReadOnlyList<string> ErrorCodes { get; }
    public string? ErrorDetail { get; }
    public string SetDirectory { get; }
}

/// <summary>
/// Checks XSLT result assertions. The QT3 comparer works on XdmSequence results of an XPath
/// expression; XSLT results are serialized documents, so the XSLT-specific assertions
/// (assert-xml, assert, assert-string-value, serialization-matches) are handled here and
/// error / any-of / all-of follow the same shape as Qt3ResultComparer.
/// </summary>
internal static class XsltResultComparer
{
    public static (bool Passed, string? Detail) Check(XElement? assertion, XsltActual actual)
    {
        if (assertion is null)
        {
            return (false, "empty <result>");
        }

        switch (assertion.Name.LocalName)
        {
            case "error":
            {
                string expected = (string?)assertion.Attribute("code") ?? "*";
                if (actual.ErrorCodes.Count == 0)
                {
                    return (false, $"expected error {expected} but the transformation succeeded");
                }

                return expected == "*" || actual.ErrorCodes.Contains(expected, StringComparer.Ordinal)
                    ? (true, null)
                    : (false, $"expected error {expected}, got {actual.ErrorDetail}");
            }

            case "all-of":
                foreach (XElement child in assertion.Elements())
                {
                    (bool ok, string? why) = Check(child, actual);
                    if (!ok)
                    {
                        return (false, why);
                    }
                }

                return (true, null);

            case "any-of":
            {
                List<string> whys = new List<string>();
                foreach (XElement child in assertion.Elements())
                {
                    (bool ok, string? why) = Check(child, actual);
                    if (ok)
                    {
                        return (true, null);
                    }

                    whys.Add(why ?? child.Name.LocalName);
                }

                return (false, "any-of: " + string.Join(" | ", whys));
            }

            case "assert-message":
            case "assert-result-document":
                return (false, $"unsupported: {assertion.Name.LocalName} (xsl:message / xsl:result-document arrive in phase 4)");
        }

        if (actual.Output is null)
        {
            return (false, "expected a result but got " + (actual.ErrorDetail ?? "an error"));
        }

        switch (assertion.Name.LocalName)
        {
            case "assert-xml":
                return AssertXml(assertion, actual);

            case "assert-string-value":
                return AssertStringValue(assertion, actual);

            case "assert":
                return AssertXPath(assertion, actual);

            case "serialization-matches":
                return SerializationMatches(assertion, actual);

            case "assert-serialization":
            case "assert-serialization-error":
                return (false, $"unsupported: {assertion.Name.LocalName}");

            default:
                return (false, $"unsupported: assertion '{assertion.Name.LocalName}'");
        }
    }

    // ── assert-xml ───────────────────────────────────────────────────────────

    private static (bool, string?) AssertXml(XElement assertion, XsltActual actual)
    {
        string? file = (string?)assertion.Attribute("file");
        string expected = file is not null
            ? File.ReadAllText(Path.GetFullPath(Path.Combine(actual.SetDirectory, file)))
            : assertion.Value;

        XElement? e = ParseFragment(expected);
        XElement? a = ParseFragment(actual.Output!);
        if (e is not null && a is not null)
        {
            return Canonical(e, a) ? (true, null) : (false, $"XML differs. Expected: {Trunc(expected)} Actual: {Trunc(actual.Output!)}");
        }

        return string.Equals(expected.Trim(), actual.Output!.Trim(), StringComparison.Ordinal)
            ? (true, null)
            : (false, $"text differs. Expected: {Trunc(expected)} Actual: {Trunc(actual.Output!)}");
    }

    private static XElement? ParseFragment(string text)
    {
        string stripped = Regex.Replace(text, @"^\s*<\?xml[^>]*\?>", string.Empty);
        stripped = Regex.Replace(stripped, @"<!DOCTYPE[^>\[]*(\[[^\]]*\])?>", string.Empty);
        stripped = stripped.Trim();
        try
        {
            return XElement.Parse("<wrapper>" + stripped + "</wrapper>", LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static bool Canonical(XElement a, XElement b)
    {
        if (a.Name != b.Name)
        {
            return false;
        }

        List<XAttribute> aa = a.Attributes().Where(x => !x.IsNamespaceDeclaration).OrderBy(x => x.Name.ToString(), StringComparer.Ordinal).ToList();
        List<XAttribute> ba = b.Attributes().Where(x => !x.IsNamespaceDeclaration).OrderBy(x => x.Name.ToString(), StringComparer.Ordinal).ToList();
        if (aa.Count != ba.Count)
        {
            return false;
        }

        for (int i = 0; i < aa.Count; i++)
        {
            if (aa[i].Name != ba[i].Name || aa[i].Value != ba[i].Value)
            {
                return false;
            }
        }

        List<XNode> an = a.Nodes().ToList();
        List<XNode> bn = b.Nodes().ToList();
        if (an.Count != bn.Count)
        {
            return false;
        }

        for (int i = 0; i < an.Count; i++)
        {
            switch (an[i])
            {
                case XElement ae when bn[i] is XElement be:
                    if (!Canonical(ae, be))
                    {
                        return false;
                    }

                    break;
                case XText at when bn[i] is XText bt:
                    if (at.Value != bt.Value)
                    {
                        return false;
                    }

                    break;
                case XComment ac when bn[i] is XComment bc:
                    if (ac.Value != bc.Value)
                    {
                        return false;
                    }

                    break;
                case XProcessingInstruction ap when bn[i] is XProcessingInstruction bp:
                    if (ap.Target != bp.Target || ap.Data != bp.Data)
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    // ── assert-string-value ──────────────────────────────────────────────────

    private static (bool, string?) AssertStringValue(XElement assertion, XsltActual actual)
    {
        string text = StringValueOf(actual.Output!);
        string expected = assertion.Value;
        if (string.Equals((string?)assertion.Attribute("normalize-space"), "true", StringComparison.Ordinal))
        {
            text = Regex.Replace(text.Trim(), @"\s+", " ");
            expected = Regex.Replace(expected.Trim(), @"\s+", " ");
        }

        return text == expected ? (true, null) : (false, $"string value differs. Expected: {Trunc(expected)} Actual: {Trunc(text)}");
    }

    private static string StringValueOf(string output)
    {
        XElement? wrapped = ParseFragment(output);
        return wrapped is null ? output : wrapped.Value;
    }

    // ── assert (XPath over the result document) ──────────────────────────────

    private static (bool, string?) AssertXPath(XElement assertion, XsltActual actual)
    {
        string expr = assertion.Value.Trim();
        NamePool pool = new NamePool();
        XdmNode doc;
        try
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                ConformanceLevel = ConformanceLevel.Fragment,
            };
            using StringReader sr = new StringReader(actual.Output!);
            using XmlReader reader = XmlReader.Create(sr, settings);
            doc = XTransform.Xslt.XsltInput.Load(reader, pool);
        }
        catch (Exception ex)
        {
            return (false, "result is not XML for assert: " + ex.Message);
        }

        try
        {
            QName resultVar = new QName("result");
            CompiledExpression compiled = XPathCompiler.ParseAndCompile(
                "boolean(" + expr + ")",
                SourceSpan.None,
                XsltTestRunner.NamespacesOf(assertion),
                BuiltInFunctionLibrary.Instance,
                new Dictionary<QName, SequenceType> { [resultVar] = SequenceType.AnySequence });

            RunState run = new RunState();
            DynamicContext ctx = DynamicContext.Root(run).WithFrame(compiled.SlotCount);
            if (compiled.SlotCount > 0)
            {
                ctx.LocalFrame[0] = XdmSequence.Of(doc);
            }

            ctx = ctx.WithContextItem(doc, 1, 1);
            XdmItem? item = compiled.Root.Iterate(ctx).Next();
            return item is not null && item.GetStringValue() == "true"
                ? (true, null)
                : (false, $"assert was false: {Trunc(expr)}");
        }
        catch (XTransformException ex)
        {
            return (false, $"assert evaluation failed: {ex.ErrorCode.LocalName}: {ex.Message}");
        }
    }

    // ── serialization-matches ────────────────────────────────────────────────

    private static (bool, string?) SerializationMatches(XElement assertion, XsltActual actual)
    {
        string? file = (string?)assertion.Attribute("file");
        string pattern = file is not null
            ? File.ReadAllText(Path.GetFullPath(Path.Combine(actual.SetDirectory, file)))
            : assertion.Value;
        string flags = (string?)assertion.Attribute("flags") ?? string.Empty;
        RegexOptions opts = RegexOptions.None;
        if (flags.Contains('i', StringComparison.Ordinal))
        {
            opts |= RegexOptions.IgnoreCase;
        }

        if (flags.Contains('s', StringComparison.Ordinal))
        {
            opts |= RegexOptions.Singleline;
        }

        if (flags.Contains('m', StringComparison.Ordinal))
        {
            opts |= RegexOptions.Multiline;
        }

        if (flags.Contains('x', StringComparison.Ordinal))
        {
            opts |= RegexOptions.IgnorePatternWhitespace;
        }

        try
        {
            return Regex.IsMatch(actual.Output!, pattern, opts)
                ? (true, null)
                : (false, $"output does not match /{Trunc(pattern)}/: {Trunc(actual.Output!)}");
        }
        catch (ArgumentException ex)
        {
            return (false, "unsupported: regex not translatable: " + ex.Message);
        }
    }

    private static string Trunc(string s) => s.Length <= 160 ? s : string.Concat(s.AsSpan(0, 160), "…");
}
