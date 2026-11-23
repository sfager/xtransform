using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace XTransform.Conformance;

/// <summary>
/// QT3 conformance tests.
/// Default mode: full evaluation (gate G2).
/// Set XTRANSFORM_CONFORMANCE_MODE=parse to run parse-only (gate G1).
/// All tests in this class are tagged [Category("Conformance")] and are skipped when the
/// QT3 test suite directory is absent.
/// </summary>
[TestFixture]
[Category("Conformance")]
internal sealed class Qt3ConformanceTests
{
    // ── Mode ──────────────────────────────────────────────────────────────────

    private static readonly string s_mode =
        Environment.GetEnvironmentVariable("XTRANSFORM_CONFORMANCE_MODE") ?? "full";

    private static bool IsFullEvalMode =>
        !string.Equals(s_mode, "parse", StringComparison.OrdinalIgnoreCase);

    // ── Static discovery (must run before TestCaseSource is evaluated) ────────

    private static readonly Lazy<DiscoveryState> s_state = new(DiscoverLazy);

    private sealed record DiscoveryState(
        string? Qt3Root,
        Qt3Catalog? Catalog,
        IReadOnlyList<Qt3TestInstance> Cases,
        IReadOnlyList<Qt3TestSet> TestSets,
        KnownFailures KnownFailures);

    private static DiscoveryState DiscoverLazy()
    {
        string? root = Qt3TestSuiteLocator.FindQt3Root();
        if (root is null)
        {
            return new DiscoveryState(null, null, Array.Empty<Qt3TestInstance>(),
                Array.Empty<Qt3TestSet>(), new KnownFailures(null));
        }

        Qt3Catalog catalog = Qt3Catalog.Load(root);

        // Known failures are loaded relative to the test output dir; during discovery
        // TestContext.CurrentContext.TestDirectory is available.
        KnownFailures known = IsFullEvalMode
            ? KnownFailures.LoadForG2(TestContext.CurrentContext.TestDirectory)
            : KnownFailures.LoadForG1(TestContext.CurrentContext.TestDirectory);

        List<Qt3TestInstance> cases = new();
        List<Qt3TestSet> sets = new();

        foreach (Qt3TestSetRef setRef in catalog.TestSetRefs)
        {
            string setFilePath = Path.Combine(root, setRef.File);
            if (!File.Exists(setFilePath))
            {
                continue;
            }

            Qt3TestSet set = Qt3TestSet.Load(setFilePath);
            sets.Add(set);
            string area = DeriveArea(setRef.File);

            foreach (Qt3TestCase tc in set.TestCases)
            {
                if (!Qt3Filter.IsInScope(tc.Dependencies, set.SetLevelDependencies))
                {
                    continue;
                }

                cases.Add(new Qt3TestInstance(setRef.Name, tc, area, set));
            }
        }

        return new DiscoveryState(root, catalog, cases, sets, known);
    }

    // ── Per-test result tracking for the summary report ───────────────────────

    private static readonly ConcurrentDictionary<string, AreaStats> s_areaStats = new(StringComparer.Ordinal);

    // ── NUnit test-case source ────────────────────────────────────────────────

    public static IEnumerable<TestCaseData> DiscoverCases()
    {
        DiscoveryState state = s_state.Value;
        if (state.Qt3Root is null)
        {
            yield return new TestCaseData((Qt3TestInstance?)null)
                .SetName("qt3-missing")
                .Ignore(Qt3TestSuiteLocator.MissingSuiteMessage);
            yield break;
        }

        foreach (Qt3TestInstance instance in state.Cases)
        {
            yield return new TestCaseData(instance).SetName(instance.FullName);
        }
    }

    // ── Test method ───────────────────────────────────────────────────────────

    [TestCaseSource(nameof(DiscoverCases))]
    public void Evaluate(Qt3TestInstance? instance)
    {
        if (instance is null)
        {
            Assert.Ignore();
            return;
        }

        DiscoveryState state = s_state.Value;

        if (state.KnownFailures.IsKnownFailure(instance.FullName, out string? reason))
        {
            RecordOutcome(instance.Area, Qt3ParseOnlyRunner.Outcome.Skipped);
            Assert.Ignore($"known failure: {reason}");
            return;
        }

        if (!IsFullEvalMode)
        {
            // Parse-only agreement: phase-1-tasks.md P1-14 says "checks only parses vs
            // expects XPST0003". We are strict: parser must throw iff XPST0003 is one of the
            // expected error codes. Other XPST*/XPTY*/XPDY*/FORG*/etc. codes are runtime
            // concerns and the parser should accept the expression for parse-only purposes.
            bool expectsXpst0003 = instance.Case.Result.ExpectedErrorCodes.Contains(
                "XPST0003", StringComparer.Ordinal);
            Qt3ParseOnlyRunner.Result parseResult = Qt3ParseOnlyRunner.Run(
                instance.Case.TestExpression, expectsXpst0003);

            RecordOutcome(instance.Area, parseResult.Outcome);

            if (parseResult.Outcome == Qt3ParseOnlyRunner.Outcome.Fail)
            {
                Assert.Fail(parseResult.Detail ?? "Parse-only check failed.");
            }

            return;
        }

        // Full evaluation mode (G2)
        Qt3EvaluationResult result = Qt3FullEvaluationRunner.Run(
            instance, state.Catalog!, instance.TestSet);

        switch (result.Outcome)
        {
            case EvaluationOutcome.Pass:
                RecordOutcome(instance.Area, Qt3ParseOnlyRunner.Outcome.Pass);
                break;

            case EvaluationOutcome.Skip:
                RecordOutcome(instance.Area, Qt3ParseOnlyRunner.Outcome.Skipped);
                Assert.Ignore(result.Detail ?? "Skipped.");
                break;

            case EvaluationOutcome.Fail:
            default:
                RecordOutcome(instance.Area, Qt3ParseOnlyRunner.Outcome.Fail);
                Assert.Fail(result.Detail ?? "Full-evaluation check failed.");
                break;
        }
    }

    // ── Summary report ────────────────────────────────────────────────────────

    [OneTimeTearDown]
    public void WriteSummary()
    {
        DiscoveryState state = s_state.Value;
        if (state.Qt3Root is null)
        {
            return;
        }

        string? repoRoot = Qt3TestSuiteLocator.FindRepoRoot();
        if (repoRoot is null)
        {
            return;
        }

        string artifactsDir = Path.Combine(repoRoot, "artifacts", "conformance");
        Directory.CreateDirectory(artifactsDir);
        string summaryPath = Path.Combine(artifactsDir, "qt3-summary.md");

        // Compute totals
        long totalInScope = 0;
        long totalPassed = 0;
        long totalFailed = 0;
        long totalSkipped = 0;

        foreach (AreaStats stats in s_areaStats.Values)
        {
            totalInScope += stats.InScope;
            totalPassed += stats.Passed;
            totalFailed += stats.Failed;
            totalSkipped += stats.Skipped;
        }

        // Any in-scope cases that were never executed (suite discovered but test not run)
        // count as in-scope without results — ensure we reflect total discovery count.
        long discoveredInScope = state.Cases.Count;

        string modeLabel = IsFullEvalMode ? "full-evaluation (gate G2)" : "parse-only (gate G1)";

        using StreamWriter w = new(summaryPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        w.WriteLine($"# QT3 conformance — {modeLabel}");
        w.WriteLine();
        w.WriteLine("- Pinned commit: `201a6e466940cdfc727f4babfedcde5332b9f578`");
        w.WriteLine($"- Generated: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        w.WriteLine($"- Mode: {modeLabel}");
        w.WriteLine();
        w.WriteLine("## Totals");
        w.WriteLine();
        w.WriteLine("| Metric | Count |");
        w.WriteLine("| --- | ---: |");
        w.WriteLine($"| In-scope cases (discovered) | {discoveredInScope:N0} |");
        w.WriteLine($"| In-scope cases (executed) | {totalInScope:N0} |");
        w.WriteLine($"| Passed | {totalPassed:N0} |");
        w.WriteLine($"| Failed | {totalFailed:N0} |");
        w.WriteLine($"| Known-failure (skipped) | {totalSkipped:N0} |");
        w.WriteLine();

        double passRate = totalInScope > 0
            ? (double)totalPassed / totalInScope * 100.0
            : 0.0;
        w.WriteLine($"Pass rate: {passRate:F1}%");
        w.WriteLine();
        w.WriteLine("## Per area");
        w.WriteLine();
        w.WriteLine("| Area | In-scope | Passed | Failed | Skipped | Pass-rate |");
        w.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: |");

        foreach (KeyValuePair<string, AreaStats> kvp in SortedAreas())
        {
            AreaStats s = kvp.Value;
            double areaRate = s.InScope > 0
                ? (double)s.Passed / s.InScope * 100.0
                : 0.0;
            w.WriteLine(
                $"| {kvp.Key} | {s.InScope:N0} | {s.Passed:N0} | {s.Failed:N0} | {s.Skipped:N0} | {areaRate:F1}% |");
        }

        // Also emit a machine-readable JSON summary for CI baseline checks.
        string jsonPath = Path.Combine(artifactsDir, "qt3-summary.json");
        using (StreamWriter jw = new(jsonPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            string gate = IsFullEvalMode ? "G2" : "G1";
            string modeName = IsFullEvalMode ? "full-evaluation" : "parse-only";
            jw.WriteLine("{");
            jw.WriteLine($"  \"gate\": \"{gate}\",");
            jw.WriteLine("  \"suite\": \"QT3\",");
            jw.WriteLine($"  \"mode\": \"{modeName}\",");
            jw.WriteLine("  \"qt3Commit\": \"201a6e466940cdfc727f4babfedcde5332b9f578\",");
            jw.WriteLine($"  \"generatedUtc\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",");
            jw.WriteLine($"  \"inScopeDiscovered\": {discoveredInScope},");
            jw.WriteLine($"  \"inScopeExecuted\": {totalInScope},");
            jw.WriteLine($"  \"passed\": {totalPassed},");
            jw.WriteLine($"  \"failed\": {totalFailed},");
            jw.WriteLine($"  \"skipped\": {totalSkipped},");
            jw.WriteLine($"  \"passRate\": {passRate / 100.0:F4}");
            jw.WriteLine("}");
        }

        TestContext.Out.WriteLine($"[qt3-summary] In-scope discovered: {discoveredInScope}  Executed: {totalInScope}  Passed: {totalPassed}  Failed: {totalFailed}  Skipped: {totalSkipped}  Pass-rate: {passRate:F1}%");
        TestContext.Out.WriteLine($"[qt3-summary] Report: {summaryPath}");
        TestContext.Out.WriteLine($"[qt3-summary] JSON: {jsonPath}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void RecordOutcome(string area, Qt3ParseOnlyRunner.Outcome outcome)
    {
        AreaStats stats = s_areaStats.GetOrAdd(area, _ => new AreaStats());
        switch (outcome)
        {
            case Qt3ParseOnlyRunner.Outcome.Pass:
                stats.IncrementInScope();
                stats.IncrementPassed();
                break;
            case Qt3ParseOnlyRunner.Outcome.Fail:
                stats.IncrementInScope();
                stats.IncrementFailed();
                break;
            case Qt3ParseOnlyRunner.Outcome.Skipped:
                stats.IncrementInScope();
                stats.IncrementSkipped();
                break;
        }
    }

    private static List<KeyValuePair<string, AreaStats>> SortedAreas()
    {
        List<KeyValuePair<string, AreaStats>> list = new(s_areaStats);
        list.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
        return list;
    }

    /// <summary>
    /// Derives an area name from the test-set file path, e.g. <c>prod/Literal.xml</c> → <c>prod</c>.
    /// </summary>
    private static string DeriveArea(string file)
    {
        // Normalise to forward slashes
        string normalised = file.Replace('\\', '/');
        int slash = normalised.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? normalised.Substring(0, slash) : normalised;
    }
}

/// <summary>A fully-resolved test instance ready for execution.</summary>
internal sealed record Qt3TestInstance(string TestSetName, Qt3TestCase Case, string Area, Qt3TestSet TestSet)
{
    /// <summary>The short key used in the known-failures file: <c>setName/caseName</c>.</summary>
    public string FullName => $"{TestSetName}/{Case.Name}";
}

/// <summary>Thread-safe per-area statistics accumulator.</summary>
internal sealed class AreaStats
{
    private long _inScope;
    private long _passed;
    private long _failed;
    private long _skipped;

    public long InScope => System.Threading.Interlocked.Read(ref _inScope);
    public long Passed => System.Threading.Interlocked.Read(ref _passed);
    public long Failed => System.Threading.Interlocked.Read(ref _failed);
    public long Skipped => System.Threading.Interlocked.Read(ref _skipped);

    public void IncrementInScope() => System.Threading.Interlocked.Increment(ref _inScope);
    public void IncrementPassed() => System.Threading.Interlocked.Increment(ref _passed);
    public void IncrementFailed() => System.Threading.Interlocked.Increment(ref _failed);
    public void IncrementSkipped() => System.Threading.Interlocked.Increment(ref _skipped);
}
