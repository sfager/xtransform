using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace XTransform.Conformance;

/// <summary>
/// XSLT conformance tests over the pinned xslt30-test catalog, filtered per decisions.md D2.
/// Tagged [Category("Conformance")]; skipped when the suite directory is absent.
/// </summary>
[TestFixture]
[Category("Conformance")]
internal sealed class XsltConformanceTests
{
    private static readonly Lazy<DiscoveryState> s_state = new(Discover);
    private static readonly ConcurrentDictionary<string, XsltAreaStats> s_areas = new(StringComparer.Ordinal);

    private sealed record DiscoveryState(string? Root, IReadOnlyList<XsltTestInstance> Cases, KnownFailures KnownFailures);

    private static DiscoveryState Discover()
    {
        string? root = FindXsltRoot();
        if (root is null)
        {
            return new DiscoveryState(null, Array.Empty<XsltTestInstance>(), new KnownFailures(null));
        }

        KnownFailures known = new KnownFailures(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "known-failures", "xslt-g3.txt"));

        List<XsltTestInstance> cases = new List<XsltTestInstance>();
        // XTRANSFORM_XSLT_SETS=a,b runs only those test sets; XTRANSFORM_XSLT_SKIP_SETS=c,d skips them.
        HashSet<string> only = SplitEnv("XTRANSFORM_XSLT_SETS");
        HashSet<string> skip = SplitEnv("XTRANSFORM_XSLT_SKIP_SETS");

        foreach (XsltTestSetRef setRef in XsltTestSet.LoadCatalog(root))
        {
            if ((only.Count > 0 && !only.Contains(setRef.Name)) || skip.Contains(setRef.Name))
            {
                continue;
            }

            string path = Path.Combine(root, setRef.File);
            if (!File.Exists(path))
            {
                continue;
            }

            XsltTestSet set = XsltTestSet.Load(path);
            string area = DeriveArea(setRef.File);
            foreach (XsltTestCase tc in set.Cases)
            {
                if (XsltFilter.IsInScope(tc.Dependencies, set.Dependencies))
                {
                    cases.Add(new XsltTestInstance(setRef.Name, tc, area, set));
                }
            }
        }

        return new DiscoveryState(root, cases, known);
    }

    private static HashSet<string> SplitEnv(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
    }

    public static IEnumerable<TestCaseData> DiscoverCases()
    {
        DiscoveryState state = s_state.Value;
        if (state.Root is null)
        {
            yield return new TestCaseData((XsltTestInstance?)null)
                .SetName("xslt30-missing")
                .Ignore("xslt30-test is not present. Run eng/fetch-test-suites.sh (or pwsh eng/fetch-test-suites.ps1).");
            yield break;
        }

        foreach (XsltTestInstance instance in state.Cases)
        {
            yield return new TestCaseData(instance).SetName(instance.FullName);
        }
    }

    [TestCaseSource(nameof(DiscoverCases))]
    public void Evaluate(XsltTestInstance? instance)
    {
        if (instance is null)
        {
            Assert.Ignore();
            return;
        }

        XsltAreaStats stats = s_areas.GetOrAdd(instance.Area, _ => new XsltAreaStats());
        if (s_state.Value.KnownFailures.IsKnownFailure(instance.FullName, out string? reason))
        {
            stats.Add(XsltOutcome.Skip, XsltFailureStage.None, known: true);
            Assert.Ignore($"known failure: {reason}");
            return;
        }

        XsltTestResult result;
        try
        {
            result = XsltTestRunner.Run(instance.Case, instance.Set);
        }
        catch (Exception ex)
        {
            result = new XsltTestResult(XsltOutcome.Fail, XsltFailureStage.Run, "harness exception: " + ex.GetType().Name + ": " + ex.Message);
        }

        stats.Add(result.Outcome, result.Stage, known: false);
        switch (result.Outcome)
        {
            case XsltOutcome.Pass:
                break;
            case XsltOutcome.Skip:
                Assert.Ignore(result.Detail ?? "unsupported");
                break;
            default:
                Assert.Fail($"[{result.Stage}] {result.Detail}");
                break;
        }
    }

    [OneTimeTearDown]
    public void WriteSummary()
    {
        DiscoveryState state = s_state.Value;
        string? repoRoot = Qt3TestSuiteLocator.FindRepoRoot();
        if (state.Root is null || repoRoot is null || s_areas.IsEmpty)
        {
            return;
        }

        string dir = Path.Combine(repoRoot, "artifacts", "conformance");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "xslt-summary.md");

        long passed = s_areas.Values.Sum(a => a.Passed);
        long failed = s_areas.Values.Sum(a => a.Failed);
        long unsupported = s_areas.Values.Sum(a => a.Unsupported);
        long known = s_areas.Values.Sum(a => a.Known);
        long compileFails = s_areas.Values.Sum(a => a.CompileFailures);
        long runFails = s_areas.Values.Sum(a => a.RunFailures);
        long compareFails = s_areas.Values.Sum(a => a.CompareFailures);
        long executed = passed + failed;

        using StreamWriter w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("# XSLT conformance (xslt30-test, XSLT 2.0 filter, gate G3)");
        w.WriteLine();
        w.WriteLine($"- Generated: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        w.WriteLine($"- In-scope cases discovered: {state.Cases.Count:N0}");
        w.WriteLine();
        w.WriteLine("## Totals");
        w.WriteLine();
        w.WriteLine("| Metric | Count |");
        w.WriteLine("| --- | ---: |");
        w.WriteLine($"| Executed (passed + failed) | {executed:N0} |");
        w.WriteLine($"| Passed | {passed:N0} |");
        w.WriteLine($"| Failed | {failed:N0} |");
        w.WriteLine($"| Unsupported by the harness / engine (skipped) | {unsupported:N0} |");
        w.WriteLine($"| Known failures (skipped) | {known:N0} |");
        w.WriteLine();
        w.WriteLine($"Pass rate (of executed): {(executed > 0 ? (double)passed / executed * 100 : 0):F1}%");
        w.WriteLine();
        w.WriteLine("## Failures by stage");
        w.WriteLine();
        w.WriteLine("Compile-stage misses are static-error disagreements (parse / compile only); run-stage misses are runtime errors; compare-stage misses are output mismatches.");
        w.WriteLine();
        w.WriteLine("| Stage | Failures |");
        w.WriteLine("| --- | ---: |");
        w.WriteLine($"| Compile (parse-only) | {compileFails:N0} |");
        w.WriteLine($"| Run | {runFails:N0} |");
        w.WriteLine($"| Output comparison | {compareFails:N0} |");
        w.WriteLine();
        w.WriteLine("## Per area");
        w.WriteLine();
        w.WriteLine("| Area | Executed | Passed | Failed | Compile | Run | Compare | Skipped | Pass-rate |");
        w.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (KeyValuePair<string, XsltAreaStats> kv in s_areas.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            XsltAreaStats s = kv.Value;
            long ex = s.Passed + s.Failed;
            w.WriteLine($"| {kv.Key} | {ex:N0} | {s.Passed:N0} | {s.Failed:N0} | {s.CompileFailures:N0} | {s.RunFailures:N0} | {s.CompareFailures:N0} | {s.Unsupported + s.Known:N0} | {(ex > 0 ? (double)s.Passed / ex * 100 : 0):F1}% |");
        }

        // Machine-readable summary for eng/check-conformance-baseline.sh; one file per shard
        // (XTRANSFORM_XSLT_SHARD, default "full") so core and unicode-90 runs can be combined.
        string shard = Environment.GetEnvironmentVariable("XTRANSFORM_XSLT_SHARD") ?? "full";
        string jsonPath = Path.Combine(dir, $"xslt-summary-{shard}.json");
        double rate = state.Cases.Count > 0 ? (double)passed / state.Cases.Count : 0;
        File.WriteAllText(
            jsonPath,
            "{\n" +
            $"  \"gate\": \"G3\",\n  \"suite\": \"xslt30-test\",\n  \"shard\": \"{shard}\",\n" +
            $"  \"generatedUtc\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",\n" +
            $"  \"inScopeDiscovered\": {state.Cases.Count},\n  \"executed\": {executed},\n" +
            $"  \"passed\": {passed},\n  \"failed\": {failed},\n  \"skipped\": {unsupported + known},\n" +
            $"  \"knownFailures\": {known},\n  \"passRate\": {rate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}\n" +
            "}\n",
            new UTF8Encoding(false));

        TestContext.Out.WriteLine($"[xslt-summary] Executed {executed}, passed {passed}, failed {failed}, skipped {unsupported + known}. Report: {path}");
    }

    /// <summary><c>tests/insn/value-of/x.xml</c> → <c>insn</c>.</summary>
    private static string DeriveArea(string file)
    {
        string[] parts = file.Replace('\\', '/').Split('/');
        return parts.Length >= 2 && parts[0] == "tests" ? parts[1] : parts[0];
    }

    private static string? FindXsltRoot()
    {
        string? dir = TestContext.CurrentContext.TestDirectory;
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            string candidate = Path.Combine(dir, ".testsuites", "xslt30-test", "catalog.xml");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate);
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}

internal sealed record XsltTestInstance(string SetName, XsltTestCase Case, string Area, XsltTestSet Set)
{
    public string FullName => $"{SetName}/{Case.Name}";
}

internal sealed class XsltAreaStats
{
    private long _passed;
    private long _failed;
    private long _unsupported;
    private long _known;
    private long _compile;
    private long _run;
    private long _compare;

    public long Passed => System.Threading.Interlocked.Read(ref _passed);
    public long Failed => System.Threading.Interlocked.Read(ref _failed);
    public long Unsupported => System.Threading.Interlocked.Read(ref _unsupported);
    public long Known => System.Threading.Interlocked.Read(ref _known);
    public long CompileFailures => System.Threading.Interlocked.Read(ref _compile);
    public long RunFailures => System.Threading.Interlocked.Read(ref _run);
    public long CompareFailures => System.Threading.Interlocked.Read(ref _compare);

    public void Add(XsltOutcome outcome, XsltFailureStage stage, bool known)
    {
        if (known)
        {
            System.Threading.Interlocked.Increment(ref _known);
            return;
        }

        switch (outcome)
        {
            case XsltOutcome.Pass:
                System.Threading.Interlocked.Increment(ref _passed);
                break;
            case XsltOutcome.Skip:
                System.Threading.Interlocked.Increment(ref _unsupported);
                break;
            default:
                System.Threading.Interlocked.Increment(ref _failed);
                switch (stage)
                {
                    case XsltFailureStage.Compile:
                        System.Threading.Interlocked.Increment(ref _compile);
                        break;
                    case XsltFailureStage.Run:
                        System.Threading.Interlocked.Increment(ref _run);
                        break;
                    default:
                        System.Threading.Interlocked.Increment(ref _compare);
                        break;
                }

                break;
        }
    }
}
