// XSLT 2.0 §6.2, §6.4 — compile each xsl:template into per-mode TemplateRules and
// produce one ModeTable per mode. Union patterns are split so every rule has a single,
// non-union pattern. Priority is either the declared @priority (xs:double) or the pattern's
// §6.4 default. Buckets are sorted; a tie between rules of equal precedence and priority is decided at
// run time (the later rule wins; XTRE0540 is recoverable), since patterns cannot be compared statically.

using System;
using System.Collections.Generic;
using System.Globalization;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

namespace XTransform.Xslt;

/// <summary>
/// Compiles mode tables from the declaration-table's <see cref="TemplateDecl"/> list.
/// </summary>
internal static class ModeCompiler
{
    /// <summary>Entry point.</summary>
    public static IReadOnlyDictionary<QName, ModeTable> Compile(
        DeclarationTable declarations,
        FunctionLibrary functions,
        PatternPredicateTable patterns,
        DiagnosticBag bag)
    {
        Dictionary<QName, ModeTable> modes = new Dictionary<QName, ModeTable>();
        int ordinal = 0;
        List<TemplateRule> allRules = new List<TemplateRule>();

        foreach (TemplateDecl decl in declarations.Templates)
        {
            if (decl.MatchPattern is null)
            {
                // Named-only templates have no pattern; they are registered in
                // DeclarationTable.NamedTemplates and don't participate in the mode tables.
                continue;
            }

            IReadOnlyList<QName> declaredModes = decl.Modes.Count > 0
                ? decl.Modes
                : new[] { DeclarationCollector.DefaultMode };

            PatternAst parsed;
            try
            {
                NamespaceBindingsResolver resolver = new NamespaceBindingsResolver(
                    decl.Element.InScopeNamespaces,
                    defaultElementNamespace: decl.Element.XPathDefaultNamespace,
                    defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);
                parsed = XPathParser.ParsePattern(decl.MatchPattern, decl.Location, resolver);

                // Compile the step predicates now: matching evaluates them at run time (XSLT 2.0 §5.5.3).
                patterns.Register(parsed, resolver, functions, decl.Location, bag);
            }
            catch (XTransformException ex)
            {
                bag.Error(ex.ErrorCode, ex.Message, decl.Location);
                continue;
            }

            // Split union patterns into independent rules per alternative.
            IReadOnlyList<PatternAst> alternatives = SplitUnion(parsed);

            // Determine the effective priority: declared value (parsed as xs:double) wins,
            // otherwise the pattern's §6.4 default. Each alternative keeps its own default.
            double? declaredPriority = ParseDeclaredPriority(decl.Priority, decl.Location, bag);

            // Rules for mode="#all" belong to every mode: they are added to each mode's table once all tables exist.
            List<TemplateRule>? allModeRules = null;
            foreach (QName mode in declaredModes)
            {
                if (mode.Equals(DeclarationCollector.AllModes))
                {
                    allModeRules = new List<TemplateRule>();
                    foreach (PatternAst alt in alternatives)
                    {
                        allModeRules.Add(new TemplateRule(
                            decl,
                            alt,
                            declaredPriority ?? alt.DefaultPriority,
                            decl.ImportPrecedence,
                            ordinal++,
                            mode));
                    }

                    continue;
                }

                if (!modes.TryGetValue(mode, out ModeTable? table))
                {
                    table = new ModeTable(mode);
                    modes[mode] = table;
                }

                foreach (PatternAst alt in alternatives)
                {
                    double priority = declaredPriority ?? alt.DefaultPriority;
                    TemplateRule rule = new TemplateRule(
                        decl,
                        alt,
                        priority,
                        decl.ImportPrecedence,
                        ordinal++,
                        mode);
                    RegisterRule(table, alt, rule);
                }
            }

            if (allModeRules is not null)
            {
                allRules.AddRange(allModeRules);
            }
        }

        // mode="#all": the rules go into the table of every mode that has one, and into a table of their own
        // (under DeclarationCollector.AllModes) that serves any mode without a table of its own.
        if (allRules.Count > 0)
        {
            if (!modes.ContainsKey(DeclarationCollector.DefaultMode))
            {
                modes[DeclarationCollector.DefaultMode] = new ModeTable(DeclarationCollector.DefaultMode);
            }

            ModeTable allTable = new ModeTable(DeclarationCollector.AllModes);
            modes[DeclarationCollector.AllModes] = allTable;
            foreach (TemplateRule rule in allRules)
            {
                RegisterRule(allTable, rule.Pattern, rule);
            }

            foreach (KeyValuePair<QName, ModeTable> entry in modes)
            {
                if (entry.Key.Equals(DeclarationCollector.AllModes))
                {
                    continue;
                }

                foreach (TemplateRule rule in allRules)
                {
                    RegisterRule(
                        entry.Value,
                        rule.Pattern,
                        new TemplateRule(rule.Source, rule.Pattern, rule.Priority, rule.ImportPrecedence, rule.DocumentOrderOrdinal, entry.Key));
                }
            }
        }

        foreach (ModeTable table in modes.Values)
        {
            table.SortBuckets();
        }

        return modes;
    }

    // ── Pattern classification ──────────────────────────────────────────────

    /// <summary>
    /// Puts <paramref name="rule"/> into the appropriate bucket(s) of <paramref name="table"/>
    /// based on <paramref name="pattern"/>'s final step's node test.
    /// </summary>
    private static void RegisterRule(ModeTable table, PatternAst pattern, TemplateRule rule)
    {
        // id(...)/steps and key(...)/steps are classified by their last step, as a path pattern is.
        if (pattern is IdKeyWithPathAst { TailSteps.IsEmpty: false } withPath)
        {
            StepPatternAst tailEnd = withPath.TailSteps[withPath.TailSteps.Length - 1];
            ClassifyByFinalTest(table, tailEnd.Test, tailEnd.Axis == Axis.Attribute, rule);
            return;
        }

        // id() and key() patterns match element nodes.
        if (pattern is IdPatternAst or KeyPatternAst or IdKeyWithPathAst)
        {
            table.AddElementWildcard(rule);
            return;
        }

        if (pattern is PathPatternAst path)
        {
            StepPatternAst? finalStep = path.Steps.Length > 0 ? path.Steps[path.Steps.Length - 1] : null;
            if (finalStep is null)
            {
                // Bare "/" — a document-node match.
                table.AddByKind(XdmNodeKind.Document, rule);
                return;
            }

            bool isAttribute = finalStep.Axis == Axis.Attribute;
            ClassifyByFinalTest(table, finalStep.Test, isAttribute, rule);
            return;
        }

        // Fallback: treat unknown shape as matching any node.
        table.AddAnyNode(rule);
    }

    private static void ClassifyByFinalTest(ModeTable table, NodeTestAst test, bool isAttribute, TemplateRule rule)
    {
        switch (test)
        {
            case NameTestAst nameTest:
                HandleNameTest(table, nameTest, isAttribute, rule);
                return;

            case KindTestAst kindTest:
                HandleKindTest(table, kindTest, isAttribute, rule);
                return;

            case AnyKindTestAst:
                // node() — matches any kind of node.
                table.AddAnyNode(rule);
                return;
        }
    }

    private static void HandleNameTest(ModeTable table, NameTestAst nameTest, bool isAttribute, TemplateRule rule)
    {
        switch (nameTest.Kind)
        {
            case NameTestKind.QualifiedName:
                QName q = NormaliseForLookup(nameTest.Name!.Value);
                if (isAttribute) { table.AddAttributeByName(q, rule); }
                else { table.AddElementByName(q, rule); }
                return;

            case NameTestKind.AnyName:
            case NameTestKind.PrefixWildcard:
            case NameTestKind.LocalWildcard:
                if (isAttribute) { table.AddAttributeWildcard(rule); }
                else { table.AddElementWildcard(rule); }
                return;
        }
    }

    private static void HandleKindTest(ModeTable table, KindTestAst kt, bool isAttribute, TemplateRule rule)
    {
        switch (kt.Kind)
        {
            case XdmNodeKind.Element:
                if (kt.NameArg.HasValue &&
                    !string.Equals(kt.NameArg.Value.LocalName, "*", StringComparison.Ordinal))
                {
                    table.AddElementByName(NormaliseForLookup(kt.NameArg.Value), rule);
                }
                else
                {
                    table.AddElementWildcard(rule);
                }
                return;

            case XdmNodeKind.Attribute:
                if (kt.NameArg.HasValue &&
                    !string.Equals(kt.NameArg.Value.LocalName, "*", StringComparison.Ordinal))
                {
                    table.AddAttributeByName(NormaliseForLookup(kt.NameArg.Value), rule);
                }
                else
                {
                    table.AddAttributeWildcard(rule);
                }
                return;

            case XdmNodeKind.Document:
            case XdmNodeKind.Text:
            case XdmNodeKind.Comment:
            case XdmNodeKind.ProcessingInstruction:
            case XdmNodeKind.Namespace:
                table.AddByKind(kt.Kind, rule);
                return;
        }

        // Unknown kind: fall back to any-node.
        table.AddAnyNode(rule);
    }

    // Lookup equality for QName ignores prefix; strip it to keep bucket keys canonical.
    private static QName NormaliseForLookup(QName name) =>
        new QName(string.Empty, name.NamespaceUri, name.LocalName);

    // ── Conflict detection ─────────────────────────────────────────────────

    // ── Helpers ────────────────────────────────────────────────────────────

    private static PatternAst[] SplitUnion(PatternAst parsed)
    {
        if (parsed is UnionPatternAst union)
        {
            PatternAst[] result = new PatternAst[union.Alternatives.Length];
            for (int i = 0; i < union.Alternatives.Length; i++)
            {
                result[i] = union.Alternatives[i];
            }

            return result;
        }

        return new[] { parsed };
    }

    private static double? ParseDeclaredPriority(string? priorityAttr, SourceSpan location, DiagnosticBag bag)
    {
        if (string.IsNullOrEmpty(priorityAttr))
        {
            return null;
        }

        if (double.TryParse(priorityAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return value;
        }

        bag.Error(
            XsltErrors.XTSE0010,
            $"xsl:template @priority must be an xs:decimal, got '{priorityAttr}'",
            location);
        return null;
    }
}
