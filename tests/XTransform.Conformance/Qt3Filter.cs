using System.Collections.Generic;

namespace XTransform.Conformance;

/// <summary>Applies the D2 QT3 filter to determine whether a test case is in scope.</summary>
/// <remarks>decisions.md D2</remarks>
internal static class Qt3Filter
{
    // Excluded feature tokens — decisions.md D2
    private static readonly HashSet<string> s_excludedFeatures = new(System.StringComparer.Ordinal)
    {
        "schemaImport",
        "schemaValidation",
        "staticTyping",
        "namespace-axis",
        "moduleImport",
    };

    // Included spec tokens — decisions.md D2
    private static readonly HashSet<string> s_includedSpecTokens = new(System.StringComparer.Ordinal)
    {
        "XP20",
        "XP20+",
        "XP10+",
    };

    /// <summary>
    /// Returns <see langword="true"/> when the test case is in scope for the QT3 XPath 2.0
    /// parse-only gate (gate G1).
    /// </summary>
    /// <param name="caseLevel">Dependencies declared directly on the test case.</param>
    /// <param name="setLevel">Dependencies declared at the test-set level.</param>
    public static bool IsInScope(
        IReadOnlyList<Qt3Dependency> caseLevel,
        IReadOnlyList<Qt3Dependency> setLevel)
    {
        // decisions.md D2 — feature exclusions apply at both case and set level.
        // A feature dependency with satisfied=true (the default) means the feature is required;
        // exclude the test when that feature is in our excluded set.
        // A feature dependency with satisfied=false means the test only applies when the feature
        // is absent — that is actually fine for us, so we do NOT exclude it.
        foreach (Qt3Dependency dep in caseLevel)
        {
            if (IsExcludedFeature(dep))
            {
                return false;
            }
        }

        foreach (Qt3Dependency dep in setLevel)
        {
            if (IsExcludedFeature(dep))
            {
                return false;
            }
        }

        // decisions.md D2 — spec filter.
        // Effective spec = case-level spec if present, else set-level spec.
        // If no spec dependency exists at all: in scope (XPath 2.0 is the default).
        Qt3Dependency? effectiveSpec = FindSpec(caseLevel) ?? FindSpec(setLevel);

        if (effectiveSpec is null)
        {
            // No spec constraint — in scope.
            return true;
        }

        // A spec dependency with satisfied=false means the test only applies when the spec
        // feature is absent. For parse-only purposes, treat this as always in scope.
        if (!effectiveSpec.Satisfied)
        {
            return true;
        }

        // Check that the value tokens include at least one of XP20, XP20+, XP10+.
        return HasIncludedSpecToken(effectiveSpec.Value);
    }

    private static bool IsExcludedFeature(Qt3Dependency dep)
    {
        // Only exclude when the feature is positively required (satisfied=true)
        if (!string.Equals(dep.Type, "feature", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!dep.Satisfied)
        {
            // satisfied=false means "test requires feature to be absent" — not a problem
            return false;
        }

        // The value may be a single token (each <dependency> has one value in QT3)
        return s_excludedFeatures.Contains(dep.Value.Trim());
    }

    private static Qt3Dependency? FindSpec(IReadOnlyList<Qt3Dependency> deps)
    {
        foreach (Qt3Dependency dep in deps)
        {
            if (string.Equals(dep.Type, "spec", System.StringComparison.Ordinal))
            {
                return dep;
            }
        }

        return null;
    }

    private static bool HasIncludedSpecToken(string value)
    {
        // The value is space-separated tokens, e.g. "XP20+ XQ10+"
        // decisions.md D2: match by token, not substring.
        string[] tokens = value.Split(' ');
        foreach (string token in tokens)
        {
            if (s_includedSpecTokens.Contains(token.Trim()))
            {
                return true;
            }
        }

        return false;
    }
}
