// Full XPath 2.0 evaluation runner for QT3 test cases.
// XPath 2.0 §2.1.2 — dynamic context.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Conformance;

/// <summary>
/// Outcome of a full-evaluation QT3 test run.
/// </summary>
internal enum EvaluationOutcome
{
    Pass,
    Fail,
    Skip,
}

/// <summary>Result returned from <see cref="Qt3FullEvaluationRunner.Run"/>.</summary>
internal sealed record Qt3EvaluationResult(EvaluationOutcome Outcome, string? Detail);

/// <summary>
/// Compiles and evaluates the XPath expression in a QT3 test case and compares the result
/// against the expected assertion.
/// </summary>
internal static class Qt3FullEvaluationRunner
{
    // ── Document cache ────────────────────────────────────────────────────────

    // Reuse loaded trees across test cases to avoid repeated file I/O.
    private static readonly Dictionary<string, TinyTree> s_docCache =
        new Dictionary<string, TinyTree>(StringComparer.OrdinalIgnoreCase);

    private static readonly NamePool s_sharedPool = new NamePool();

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the given test case in full-evaluation mode.
    /// </summary>
    public static Qt3EvaluationResult Run(
        Qt3TestInstance instance,
        Qt3Catalog catalog,
        Qt3TestSet testSet)
    {
        Qt3TestCase testCase = instance.Case;

        // ── 1. Resolve the environment ─────────────────────────────────────────

        Qt3Environment env = ResolveEnvironment(testCase, testSet, catalog);

        // ── 2. Build the namespace resolver ───────────────────────────────────

        DictionaryNamespaceResolver namespaces = BuildNamespaceResolver(env);

        // ── 3. Compile param variables ────────────────────────────────────────

        Dictionary<QName, XdmSequence> paramValues = new Dictionary<QName, XdmSequence>();
        Dictionary<QName, SequenceType> externalVarDecls = new Dictionary<QName, SequenceType>();

        foreach (KeyValuePair<string, string> param in env.Params)
        {
            QName paramQName = new QName(string.Empty, string.Empty, param.Key);
            externalVarDecls[paramQName] = SequenceType.AnySequence;

            XdmSequence paramValue = EvalParam(param.Value, namespaces);
            paramValues[paramQName] = paramValue;
        }

        // ── 4. Compile the test expression ────────────────────────────────────

        CompiledExpression compiled;
        try
        {
            compiled = XPathCompiler.ParseAndCompile(
                testCase.TestExpression,
                SourceSpan.None,
                namespaces,
                BuiltInFunctionLibrary.Instance,
                externalVarDecls.Count > 0 ? externalVarDecls : null);
        }
        catch (XPathStaticException ex)
        {
            string code = ex.ErrorCode.LocalName;
            return HandleException(code, ex.Message, testCase);
        }
        catch (Exception ex)
        {
            return new Qt3EvaluationResult(EvaluationOutcome.Fail,
                $"Unexpected compile exception: {ex.GetType().Name}: {ex.Message}");
        }

        // ── 5. Build the dynamic context ──────────────────────────────────────

        // P3-12 wired CheckCancellation() into the hot iterators; the token stops a
        // runaway stylesheet without needing a background-thread watchdog. We keep the
        // 5-second cap as a hard ceiling for test runtime.
        using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        RunState run = new RunState(timeoutCts.Token);
        DynamicContext ctx = DynamicContext.Root(run).WithFrame(compiled.SlotCount);

        // Bind param variables to their compile-time slots
        // The slots are allocated in the order the externalVarDecls were added.
        int slotIndex = 0;
        foreach (KeyValuePair<QName, SequenceType> kv in externalVarDecls)
        {
            if (slotIndex < compiled.SlotCount && paramValues.TryGetValue(kv.Key, out XdmSequence? pv))
            {
                ctx.LocalFrame[slotIndex] = pv;
            }

            slotIndex++;
        }

        // Load context item if the environment specifies a source document
        if (env.ContextSourceFile is not null)
        {
            TinyTree? tree = LoadDocument(env.ContextSourceFile);
            if (tree is not null)
            {
                // Document node is always at index 0 in the TinyTree.
                XdmNode docNode = new XdmNode(NodeHandle.ForNode(tree, 0));
                ctx = ctx.WithContextItem(docNode, 1, 1);
            }
            // If we can't load the doc, context item remains null (may cause XPDY0002)
        }

        // ── 6. Evaluate ────────────────────────────────────────────────────────

        XdmSequence? actual = null;
        string? exceptionCode = null;

        try
        {
            IXdmIterator iter = compiled.Root.Iterate(ctx);
            List<XdmItem> items = new List<XdmItem>();
            XdmItem? item;
            while ((item = iter.Next()) != null)
            {
                items.Add(item);
            }

            actual = XdmSequence.From(items);
        }
        catch (OperationCanceledException)
        {
            return new Qt3EvaluationResult(EvaluationOutcome.Fail, "Evaluation exceeded 5s cancellation deadline.");
        }
        catch (XTransformException ex)
        {
            exceptionCode = ex.ErrorCode.LocalName;
        }
        catch (Exception ex)
        {
            exceptionCode = "UNEXPECTED:" + ex.GetType().Name;
        }

        // ── 7. Compare result ─────────────────────────────────────────────────

        if (testCase.Result.ResultElement is null)
        {
            // No result element — anything goes
            return new Qt3EvaluationResult(EvaluationOutcome.Pass, null);
        }

        try
        {
            (bool passed, string? detail) = Qt3ResultComparer.Compare(
                actual,
                testCase.Result.ResultElement,
                namespaces,
                exceptionCode);

            // A "Skipped" result from an unsupported assertion type
            if (!passed && detail is not null && detail.StartsWith("Skipped:", StringComparison.Ordinal))
            {
                return new Qt3EvaluationResult(EvaluationOutcome.Skip, detail);
            }

            return new Qt3EvaluationResult(
                passed ? EvaluationOutcome.Pass : EvaluationOutcome.Fail,
                detail);
        }
        catch (Exception ex)
        {
            return new Qt3EvaluationResult(EvaluationOutcome.Fail,
                $"Result comparison threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── Environment resolution ─────────────────────────────────────────────────

    private static Qt3Environment ResolveEnvironment(
        Qt3TestCase testCase,
        Qt3TestSet testSet,
        Qt3Catalog catalog)
    {
        // The environment is either:
        //  (a) an inline environment on the test case
        //  (b) a ref to a local (test-set-level) environment
        //  (c) a ref to a global (catalog-level) environment
        //  (d) none

        Qt3Environment? resolved = null;

        if (testCase.InlineEnvironment is not null)
        {
            resolved = testCase.InlineEnvironment;
        }
        else if (testCase.EnvironmentRef is not null)
        {
            string envRef = testCase.EnvironmentRef;

            // Check test-set-local environments first
            if (testSet.LocalEnvironments.TryGetValue(envRef, out Qt3Environment? localEnv))
            {
                resolved = localEnv;
            }
            else if (catalog.GlobalEnvironments.TryGetValue(envRef, out Qt3Environment? globalEnv))
            {
                resolved = globalEnv;
            }
            // "empty" environment means no context item — resolved remains null which is fine
        }

        return resolved ?? new Qt3Environment();
    }

    // ── Namespace resolver ────────────────────────────────────────────────────

    private static DictionaryNamespaceResolver BuildNamespaceResolver(Qt3Environment env)
    {
        Dictionary<string, string> prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["xs"] = "http://www.w3.org/2001/XMLSchema",
            ["fn"] = "http://www.w3.org/2005/xpath-functions",
            ["xdt"] = "http://www.w3.org/2005/xpath-datatypes",
            ["err"] = "http://www.w3.org/2005/xqt-errors",
            ["math"] = "http://www.w3.org/2005/xpath-functions/math",
        };

        foreach (KeyValuePair<string, string> kv in env.Namespaces)
        {
            prefixes[kv.Key] = kv.Value;
        }

        return new DictionaryNamespaceResolver(
            prefixes,
            defaultElementNamespace: string.Empty,
            defaultFunctionNamespace: "http://www.w3.org/2005/xpath-functions");
    }

    // ── Document loading ──────────────────────────────────────────────────────

    private static TinyTree? LoadDocument(string filePath)
    {
        lock (s_docCache)
        {
            if (s_docCache.TryGetValue(filePath, out TinyTree? cached))
            {
                return cached;
            }
        }

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
            };

            using XmlReader reader = XmlReader.Create(filePath, settings);
            TinyTree tree = TinyTreeBuilder.Load(reader, s_sharedPool);

            lock (s_docCache)
            {
                s_docCache[filePath] = tree;
            }

            return tree;
        }
        catch
        {
            // Can't load — return null; context item will be absent
            return null;
        }
    }

    // ── Param evaluation ──────────────────────────────────────────────────────

    private static XdmSequence EvalParam(string selectExpression, DictionaryNamespaceResolver namespaces)
    {
        try
        {
            CompiledExpression compiled = XPathCompiler.ParseAndCompile(
                selectExpression,
                SourceSpan.None,
                namespaces,
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
        catch
        {
            return XdmSequence.Empty;
        }
    }

    // ── Exception routing ─────────────────────────────────────────────────────

    private static Qt3EvaluationResult HandleException(
        string errorCode,
        string message,
        Qt3TestCase testCase)
    {
        // Check if the test expects this error
        if (testCase.Result.ResultElement is null)
        {
            return new Qt3EvaluationResult(EvaluationOutcome.Fail,
                $"Static error {errorCode}: {message}");
        }

        (bool passed, string? detail) = Qt3ResultComparer.Compare(
            actual: null,
            testCase.Result.ResultElement,
            new DictionaryNamespaceResolver(),
            exceptionCode: errorCode);

        return new Qt3EvaluationResult(
            passed ? EvaluationOutcome.Pass : EvaluationOutcome.Fail,
            detail ?? $"Static error {errorCode}: {message}");
    }
}
