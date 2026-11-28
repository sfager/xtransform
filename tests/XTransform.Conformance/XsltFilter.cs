using System;
using System.Collections.Generic;
using System.Linq;

namespace XTransform.Conformance;

/// <summary>Applies the XSLT filter from decisions.md D2.</summary>
internal static class XsltFilter
{
    // The year_component_values capabilities this engine provides (see D2 in docs/decisions.md).
    private static readonly HashSet<string> s_supportedYearComponents = new HashSet<string>(StringComparer.Ordinal)
    {
        "support year above 9999",
        "support negative year",
    };

    private static readonly HashSet<string> s_excludedFeatures = new HashSet<string>(StringComparer.Ordinal)
    {
        "schema_aware",
        "XSD_1.1",
        "streaming",
        "higher_order_functions",
        "dynamic_evaluation",
    };

    private static readonly HashSet<string> s_includedSpecs = new HashSet<string>(StringComparer.Ordinal)
    {
        "XSLT20",
        "XSLT20+",
        "XSLT10+",
    };

    /// <summary>
    /// A test is in scope when its effective <c>spec</c> dependency (the case's, else the set's)
    /// names XSLT20, XSLT20+ or XSLT10+ — or there is none — and it does not positively require an
    /// excluded feature. <c>satisfied="false"</c> inverts a dependency.
    /// </summary>
    public static bool IsInScope(IReadOnlyList<XsltDependency> caseLevel, IReadOnlyList<XsltDependency> setLevel)
    {
        foreach (XsltDependency dep in caseLevel.Concat(setLevel))
        {
            // A year_component_values dependency with satisfied="false" describes a processor that lacks the capability: the test
            // expects an overflow error there. XTransform supports years above 9999 and negative years, so it does not apply.
            if (!dep.Satisfied && dep.Type == "year_component_values" && s_supportedYearComponents.Contains(dep.Value.Trim()))
            {
                return false;
            }

            // on-multiple-match="error" describes a processor configured to raise XTRE0540. XTransform recovers (the last rule wins;
            // docs/implementation-defined.md), so those tests do not apply; the matching "recover" tests do.
            if (dep.Satisfied && dep.Type == "on-multiple-match" && dep.Value.Trim() == "error")
            {
                return false;
            }

            if (dep.Type == "feature" && dep.Satisfied && s_excludedFeatures.Contains(dep.Value.Trim()))
            {
                return false;
            }
        }

        XsltDependency? spec = caseLevel.FirstOrDefault(d => d.Type == "spec") ?? setLevel.FirstOrDefault(d => d.Type == "spec");
        if (spec is null || !spec.Satisfied)
        {
            return true;
        }

        return spec.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(s_includedSpecs.Contains);
    }
}
