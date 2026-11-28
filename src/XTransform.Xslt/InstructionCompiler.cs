// Compiles the body of an xsl:template / xsl:function / etc. to an XsltInstruction tree.
// P3-07 landed control-flow + output instructions; P3-08 threads the XsltCompileScope
// through compilation so XPath expressions see xsl:variable / xsl:param / global bindings.

// Compile methods intentionally return the abstract XsltInstruction (or null) so the
// per-node dispatch in CompileNode composes cleanly, even though individual methods
// construct one specific concrete type. Narrowing the return type would churn callers.
#pragma warning disable CA1859

using System;
using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xslt.Instructions;
using XTransform.XPath;

namespace XTransform.Xslt;

/// <summary>Compiles XSLT instruction bodies.</summary>
internal static class InstructionCompiler
{
    /// <summary>
    /// Compiles one <see cref="TemplateDecl"/> into a <see cref="CompiledTemplate"/>, returning
    /// <see langword="null"/> when the template body references constructs that this phase of
    /// the compiler doesn't yet support (reported as diagnostics).
    /// </summary>
    /// <summary>Thread-local function library used for the current compilation.</summary>
    [System.ThreadStatic]
    private static FunctionLibrary? s_functions;

    /// <summary>Thread-local table receiving the compiled predicates of patterns met while compiling.</summary>
    [System.ThreadStatic]
    private static PatternPredicateTable? s_patterns;

    public static CompiledTemplate? CompileTemplate(
        TemplateDecl decl,
        DeclarationTable declarations,
        FunctionLibrary functions,
        PatternPredicateTable patterns,
        DiagnosticBag bag)
    {
        s_functions = functions;
        s_patterns = patterns;
        try
        {
            return CompileTemplateInner(decl, declarations, bag);
        }
        finally
        {
            s_functions = null;
            s_patterns = null;
        }
    }

    /// <summary>
    /// Compiles an <c>xsl:function</c> body into a <see cref="UserFunctionBody"/>. Params are
    /// pushed onto the variable scope stack at invocation time; the body's returned items are
    /// collected via <see cref="ItemCollectorReceiver"/>.
    /// </summary>
    public static UserFunctionBody? CompileUserFunction(
        FunctionDecl decl,
        DeclarationTable declarations,
        FunctionLibrary functions,
        PatternPredicateTable patterns,
        DiagnosticBag bag)
    {
        s_functions = functions;
        s_patterns = patterns;
        try
        {
            XsltCompileScope scope = XsltCompileScope.WithGlobals(declarations.GlobalVariables.Keys);
            List<QName> paramNames = new List<QName>();
            int firstNonParam = 0;
            for (int i = 0; i < decl.Element.Children.Count; i++)
            {
                if (decl.Element.Children[i] is not StylesheetElement child)
                {
                    // Literal text starts the body: the params (if any) are behind us.
                    firstNonParam = i;
                    break;
                }

                if (!(child.IsXsltElement && string.Equals(child.Name.LocalName, "param", StringComparison.Ordinal)))
                {
                    firstNonParam = i;
                    break;
                }

                string? nameAttr = GetAttribute(child, "name");
                if (nameAttr is null)
                {
                    bag.Error(XsltErrors.XTSE0010, "xsl:param requires a name attribute", child.Location);
                    return null;
                }

                if (!LexicalQNameParser.TryResolve(nameAttr, child.InScopeNamespaces, out QName name, out string? why))
                {
                    bag.Error(XsltErrors.XTSE0010, $"Invalid param name '{nameAttr}': {why}", child.Location);
                    return null;
                }

                paramNames.Add(name);
                scope.Declare(name, SequenceType.AnySequence);
                firstNonParam = i + 1;
            }

            List<XsltInstruction> bodyInstructions = new List<XsltInstruction>();
            for (int i = firstNonParam; i < decl.Element.Children.Count; i++)
            {
                XsltInstruction? compiled = CompileNode(decl.Element.Children[i], scope, bag);
                if (compiled is not null)
                {
                    bodyInstructions.Add(compiled);
                }
            }

            XsltInstruction body = bodyInstructions.Count == 1
                ? bodyInstructions[0]
                : new SequenceInstruction(bodyInstructions, decl.Location);

            return new UserFunctionBody(paramNames, body);
        }
        finally
        {
            s_functions = null;
            s_patterns = null;
        }
    }

    private static CompiledTemplate? CompileTemplateInner(
        TemplateDecl decl,
        DeclarationTable declarations,
        DiagnosticBag bag)
    {
        XsltCompileScope scope = XsltCompileScope.WithGlobals(declarations.GlobalVariables.Keys);

        // Pass 1: collect xsl:param children at the start of the template body. XSLT 2.0
        // §9.5 — params must precede every other sibling.
        List<ParamSlot> paramSlots = new List<ParamSlot>();
        int firstNonParam = 0;
        for (int i = 0; i < decl.Element.Children.Count; i++)
        {
            if (decl.Element.Children[i] is not StylesheetElement child)
            {
                // Literal text starts the body: the params (if any) are behind us.
                firstNonParam = i;
                break;
            }

            if (!(child.IsXsltElement && string.Equals(child.Name.LocalName, "param", StringComparison.Ordinal)))
            {
                firstNonParam = i;
                break;
            }

            if (!TryCompileParam(child, scope, bag, out ParamSlot? slot))
            {
                return null;
            }

            paramSlots.Add(slot!);
            scope.Declare(slot!.Name, SequenceType.AnySequence);
            firstNonParam = i + 1;
        }

        // Pass 2: compile the remaining children as the body.
        List<XsltInstruction> bodyInstructions = new List<XsltInstruction>();
        for (int i = firstNonParam; i < decl.Element.Children.Count; i++)
        {
            XsltInstruction? compiled = CompileNode(decl.Element.Children[i], scope, bag);
            if (compiled is not null)
            {
                bodyInstructions.Add(compiled);
            }
        }

        XsltInstruction body = bodyInstructions.Count == 1
            ? bodyInstructions[0]
            : new SequenceInstruction(bodyInstructions, decl.Location);

        return new CompiledTemplate(decl, paramSlots, body);
    }

    private static XsltInstruction? CompileNode(
        StylesheetNode node,
        XsltCompileScope scope,
        DiagnosticBag bag)
    {
        if (node is StylesheetText text)
        {
            // Bare text nodes inside a template body (xml:space="preserve" or survivors of
            // whitespace stripping) become literal text instructions.
            return text.Text.Length == 0 ? null : new TextInstruction(text.Text, text.Location);
        }

        if (node is not StylesheetElement element)
        {
            return null;
        }

        if (!element.IsXsltElement)
        {
            return CompileLiteralResultElement(element, scope, bag);
        }

        return element.Name.LocalName switch
        {
            "if" => CompileIf(element, scope, bag),
            "choose" => CompileChoose(element, scope, bag),
            "for-each" => CompileForEach(element, scope, bag),
            "apply-templates" => CompileApplyTemplates(element, scope, bag),
            "call-template" => CompileCallTemplate(element, scope, bag),
            "value-of" => CompileValueOf(element, scope, bag),
            "text" => CompileText(element, bag),
            "variable" => CompileLocalVariable(element, scope, bag),
            "sequence" => CompileXslSequence(element, scope, bag),
            "copy" => CompileCopy(element, scope, bag),
            "copy-of" => CompileCopyOf(element, scope, bag),
            "element" => CompileXslElement(element, scope, bag),
            "attribute" => CompileXslAttribute(element, scope, bag),
            "comment" => CompileXslComment(element, scope, bag),
            "processing-instruction" => CompileXslPi(element, scope, bag),
            "document" => CompileXslDocument(element, scope, bag),
            "number" => CompileNumber(element, scope, bag),
            "analyze-string" => CompileAnalyzeString(element, scope, bag),
            "perform-sort" => CompilePerformSort(element, scope, bag),
            // Deferred to later tasks:
            "message" or "result-document" or
            "for-each-group" or "next-match" or "fallback" or
            "apply-imports" => null,
            _ => null,
        };
    }

    /// <summary>
    /// Compiles the content of every global variable and parameter that is defined by a sequence
    /// constructor (no <c>select</c>): XSLT 2.0 §9.3. Their scope is the global variables only.
    /// </summary>
    public static IReadOnlyDictionary<GlobalVariableDecl, VariableValueBody> CompileGlobalBodies(
        DeclarationTable declarations,
        FunctionLibrary functions,
        PatternPredicateTable patterns,
        DiagnosticBag bag)
    {
        Dictionary<GlobalVariableDecl, VariableValueBody> bodies = new Dictionary<GlobalVariableDecl, VariableValueBody>();
        s_functions = functions;
        s_patterns = patterns;
        try
        {
            foreach (GlobalVariableDecl decl in declarations.GlobalVariables.Values)
            {
                if (!string.IsNullOrEmpty(decl.SelectExpression) || decl.Element.Children.Count == 0)
                {
                    continue;
                }

                XsltCompileScope scope = XsltCompileScope.WithGlobals(declarations.GlobalVariables.Keys);
                bodies[decl] = new VariableValueBody(
                    CompileChildren(decl.Element, scope, bag),
                    buildsDocument: decl.AsType is null);
            }
        }
        finally
        {
            s_functions = null;
            s_patterns = null;
        }

        return bodies;
    }

    // ── xsl:analyze-string (XSLT 2.0 §15.1) ─────────────────────────────────────

    private static XsltInstruction? CompileAnalyzeString(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? selectAttr = GetAttribute(element, "select");
        string? regexAttr = GetAttribute(element, "regex");
        if (selectAttr is null || regexAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:analyze-string requires select and regex attributes", element.Location);
            return null;
        }

        // Content model: xsl:matching-substring?, xsl:non-matching-substring?, xsl:fallback* — in that order.
        StylesheetElement? matchingEl = null;
        StylesheetElement? nonMatchingEl = null;
        bool ok = true;
        foreach (StylesheetNode child in element.Children)
        {
            if (child is not StylesheetElement childEl || !childEl.IsXsltElement)
            {
                bag.Error(XsltErrors.XTSE0010, "xsl:analyze-string may contain only xsl:matching-substring, xsl:non-matching-substring and xsl:fallback", element.Location);
                ok = false;
                continue;
            }

            switch (childEl.Name.LocalName)
            {
                case "matching-substring":
                    if (matchingEl is not null || nonMatchingEl is not null)
                    {
                        bag.Error(XsltErrors.XTSE0010, "xsl:matching-substring must appear once, before xsl:non-matching-substring", childEl.Location);
                        ok = false;
                    }

                    matchingEl ??= childEl;
                    break;
                case "non-matching-substring":
                    if (nonMatchingEl is not null)
                    {
                        bag.Error(XsltErrors.XTSE0010, "xsl:non-matching-substring must appear at most once", childEl.Location);
                        ok = false;
                    }

                    nonMatchingEl ??= childEl;
                    break;
                case "fallback":
                    break;
                default:
                    bag.Error(XsltErrors.XTSE0010, $"xsl:{childEl.Name.LocalName} is not allowed inside xsl:analyze-string", childEl.Location);
                    ok = false;
                    break;
            }
        }

        if (matchingEl is null && nonMatchingEl is null && ok)
        {
            bag.Error(XsltErrors.XTSE1130, "xsl:analyze-string needs an xsl:matching-substring or an xsl:non-matching-substring", element.Location);
            ok = false;
        }

        if (!ok ||
            !TryCompileXPath(selectAttr, element, scope, bag, out BoundExpression? select))
        {
            return null;
        }

        CompiledAvt? regex = CompileAvtString(regexAttr, element, scope, bag);
        CompiledAvt? flags = CompileOptionalAvt(element, "flags", scope, bag, out bool flagsOk);
        if (regex is null || !flagsOk)
        {
            return null;
        }

        // A regex written without expressions can be checked now (XTDE1140 / XTDE1145 as static errors).
        string? literalRegex = LiteralValue(regex);
        string? literalFlags = flags is null ? string.Empty : LiteralValue(flags);
        if (literalRegex is not null && literalFlags is not null)
        {
            try
            {
                AnalyzeStringInstruction.CompileRegex(literalRegex, literalFlags, element.Location);
            }
            catch (XsltDynamicException ex)
            {
                bag.Error(ex.ErrorCode, ex.Message, element.Location);
                return null;
            }
        }

        XsltInstruction? matching = matchingEl is null ? null : CompileChildren(matchingEl, scope, bag);
        XsltInstruction? nonMatching = nonMatchingEl is null ? null : CompileChildren(nonMatchingEl, scope, bag);
        return new AnalyzeStringInstruction(select!, regex, flags, matching, nonMatching, element.Location);
    }

    // The text of an AVT that contains no expressions, or null when it has any.
    private static string? LiteralValue(CompiledAvt avt)
    {
        if (avt.Parts.Count == 0)
        {
            return string.Empty;
        }

        return avt.Parts.Count == 1 && avt.Parts[0].Literal is string text ? text : null;
    }

    // ── xsl:number (XSLT 2.0 §12) ──────────────────────────────────────────────

    private static XsltInstruction? CompileNumber(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? valueAttr = GetAttribute(element, "value");
        string? selectAttr = GetAttribute(element, "select");
        string? levelAttr = GetAttribute(element, "level");
        string? countAttr = GetAttribute(element, "count");
        string? fromAttr = GetAttribute(element, "from");

        // XTSE0975: value is allowed only when select, level, count and from are all absent.
        if (valueAttr is not null && (selectAttr is not null || levelAttr is not null || countAttr is not null || fromAttr is not null))
        {
            bag.Error(
                XsltErrors.XTSE0975,
                "xsl:number: the value attribute cannot be combined with select, level, count or from",
                element.Location);
            return null;
        }

        NumberLevel level = NumberLevel.Single;
        if (levelAttr is not null)
        {
            switch (levelAttr.Trim())
            {
                case "single": level = NumberLevel.Single; break;
                case "multiple": level = NumberLevel.Multiple; break;
                case "any": level = NumberLevel.Any; break;
                default:
                    bag.Error(XsltErrors.XTSE0020, $"xsl:number: invalid level '{levelAttr}'", element.Location);
                    return null;
            }
        }

        BoundExpression? value = null;
        if (valueAttr is not null && !TryCompileXPath(valueAttr, element, scope, bag, out value))
        {
            return null;
        }

        BoundExpression? select = null;
        if (selectAttr is not null && !TryCompileXPath(selectAttr, element, scope, bag, out select))
        {
            return null;
        }

        if (!TryParsePattern(countAttr, element, bag, out XTransform.XPath.Ast.PatternAst? count) ||
            !TryParsePattern(fromAttr, element, bag, out XTransform.XPath.Ast.PatternAst? from))
        {
            return null;
        }

        CompiledAvt? format = CompileOptionalAvt(element, "format", scope, bag, out bool ok1);
        CompiledAvt? lang = CompileOptionalAvt(element, "lang", scope, bag, out bool ok2);
        CompiledAvt? letterValue = CompileOptionalAvt(element, "letter-value", scope, bag, out bool ok3);
        CompiledAvt? ordinal = CompileOptionalAvt(element, "ordinal", scope, bag, out bool ok4);
        CompiledAvt? groupSep = CompileOptionalAvt(element, "grouping-separator", scope, bag, out bool ok5);
        CompiledAvt? groupSize = CompileOptionalAvt(element, "grouping-size", scope, bag, out bool ok6);
        if (!(ok1 && ok2 && ok3 && ok4 && ok5 && ok6))
        {
            return null;
        }

        return new NumberInstruction(
            value, select, level, count, from, format, lang, letterValue, ordinal, groupSep, groupSize, element.Location);
    }

    private static CompiledAvt? CompileOptionalAvt(
        StylesheetElement element, string name, XsltCompileScope scope, DiagnosticBag bag, out bool ok)
    {
        string? raw = GetAttribute(element, name);
        if (raw is null)
        {
            ok = true;
            return null;
        }

        CompiledAvt? avt = CompileAvtString(raw, element, scope, bag);
        ok = avt is not null;
        return avt;
    }

    private static bool TryParsePattern(
        string? text, StylesheetElement element, DiagnosticBag bag, out XTransform.XPath.Ast.PatternAst? pattern)
    {
        pattern = null;
        if (text is null)
        {
            return true;
        }

        try
        {
            NamespaceBindingsResolver resolver = new NamespaceBindingsResolver(
                element.InScopeNamespaces,
                defaultElementNamespace: string.Empty,
                defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);
            pattern = XPathParser.ParsePattern(text, element.Location, resolver);
            s_patterns?.Register(pattern, resolver, s_functions ?? BuiltInFunctionLibrary.Instance, element.Location, bag);
            return true;
        }
        catch (XTransformException ex)
        {
            bag.Error(ex.ErrorCode, ex.Message, element.Location);
            return false;
        }
    }

    // ── xsl:element / xsl:attribute / xsl:comment / xsl:processing-instruction / xsl:document ──

    private static XsltInstruction? CompileXslElement(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:element requires a name attribute", element.Location);
            return null;
        }

        CompiledAvt? nameAvt = CompileAvtString(nameAttr, element, scope, bag);
        if (nameAvt is null) { return null; }

        CompiledAvt? nsAvt = null;
        string? nsAttr = GetAttribute(element, "namespace");
        if (nsAttr is not null)
        {
            nsAvt = CompileAvtString(nsAttr, element, scope, bag);
            if (nsAvt is null) { return null; }
        }

        string defaultElementNs = element.InScopeNamespaces.Resolve(string.Empty) ?? string.Empty;
        XsltInstruction body = CompileChildren(element, scope, bag);
        return new ElementInstruction(nameAvt, nsAvt, element.InScopeNamespaces, defaultElementNs, body, element.Location);
    }

    private static XsltInstruction? CompileXslAttribute(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:attribute requires a name attribute", element.Location);
            return null;
        }

        CompiledAvt? nameAvt = CompileAvtString(nameAttr, element, scope, bag);
        if (nameAvt is null) { return null; }

        CompiledAvt? nsAvt = null;
        string? nsAttr = GetAttribute(element, "namespace");
        if (nsAttr is not null)
        {
            nsAvt = CompileAvtString(nsAttr, element, scope, bag);
            if (nsAvt is null) { return null; }
        }

        string? selectExpr = GetAttribute(element, "select");
        BoundExpression? select = null;
        XsltInstruction? body = null;
        if (!string.IsNullOrEmpty(selectExpr))
        {
            if (!TryCompileXPath(selectExpr!, element, scope, bag, out select))
            {
                return null;
            }
        }
        else
        {
            body = CompileChildren(element, scope, bag);
        }

        return new AttributeInstruction(nameAvt, nsAvt, element.InScopeNamespaces, select, body, element.Location);
    }

    private static XsltInstruction? CompileXslComment(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? selectExpr = GetAttribute(element, "select");
        BoundExpression? select = null;
        XsltInstruction? body = null;
        if (!string.IsNullOrEmpty(selectExpr))
        {
            if (!TryCompileXPath(selectExpr!, element, scope, bag, out select))
            {
                return null;
            }
        }
        else
        {
            body = CompileChildren(element, scope, bag);
        }

        return new CommentInstruction(select, body, element.Location);
    }

    private static XsltInstruction? CompileXslPi(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:processing-instruction requires a name attribute", element.Location);
            return null;
        }

        CompiledAvt? nameAvt = CompileAvtString(nameAttr, element, scope, bag);
        if (nameAvt is null) { return null; }

        string? selectExpr = GetAttribute(element, "select");
        BoundExpression? select = null;
        XsltInstruction? body = null;
        if (!string.IsNullOrEmpty(selectExpr))
        {
            if (!TryCompileXPath(selectExpr!, element, scope, bag, out select))
            {
                return null;
            }
        }
        else
        {
            body = CompileChildren(element, scope, bag);
        }

        return new ProcessingInstructionInstruction(nameAvt, select, body, element.Location);
    }

    private static XsltInstruction? CompileXslDocument(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        XsltInstruction body = CompileChildren(element, scope, bag);
        return new DocumentInstruction(body, element.Location);
    }

    /// <summary>Parses + compiles an AVT given its raw string value.</summary>
    private static CompiledAvt? CompileAvtString(string value, StylesheetElement scopeElement, XsltCompileScope scope, DiagnosticBag bag)
    {
        AttributeValueTemplate avt;
        try
        {
            avt = AttributeValueTemplate.Parse(value, scopeElement.Location);
        }
        catch (XsltStaticException ex)
        {
            foreach (Diagnostic d in ex.Errors)
            {
                bag.Add(d);
            }

            return null;
        }

        List<CompiledAvtPart> parts = new List<CompiledAvtPart>(avt.Parts.Count);
        foreach (AvtPart part in avt.Parts)
        {
            switch (part)
            {
                case AvtLiteral literal:
                    parts.Add(CompiledAvtPart.Literal_(literal.Text));
                    break;

                case AvtExpression expr:
                    if (!TryCompileXPath(expr.ExpressionText, scopeElement, scope, bag, out BoundExpression? compiled))
                    {
                        return null;
                    }

                    parts.Add(CompiledAvtPart.Expression_(compiled!));
                    break;
            }
        }

        return new CompiledAvt(parts.ToArray());
    }

    // ── xsl:sequence / xsl:copy / xsl:copy-of ──────────────────────────────

    private static XsltInstruction? CompileXslSequence(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? selectAttr = GetAttribute(element, "select");
        if (selectAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:sequence requires a select attribute (body form is deferred)", element.Location);
            return null;
        }

        if (!TryCompileXPath(selectAttr, element, scope, bag, out BoundExpression? select))
        {
            return null;
        }

        return new XslSequenceInstruction(select!, element.Location);
    }

    private static XsltInstruction? CompileCopy(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        XsltInstruction body = CompileChildren(element, scope, bag);
        return new CopyInstruction(body, element.Location);
    }

    private static XsltInstruction? CompileCopyOf(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? selectAttr = GetAttribute(element, "select");
        if (selectAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:copy-of requires a select attribute", element.Location);
            return null;
        }

        if (!TryCompileXPath(selectAttr, element, scope, bag, out BoundExpression? select))
        {
            return null;
        }

        return new CopyOfInstruction(select!, element.Location);
    }

    // ── Content-producing instructions ─────────────────────────────────────

    private static XsltInstruction? CompileValueOf(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? selectAttr = GetAttribute(element, "select");
        if (selectAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:value-of requires a select attribute (body form is deferred)", element.Location);
            return null;
        }

        if (!TryCompileXPath(selectAttr, element, scope, bag, out BoundExpression? select))
        {
            return null;
        }

        string separator = GetAttribute(element, "separator") ?? " ";
        return new ValueOfInstruction(select!, separator, element.Location);
    }

    private static XsltInstruction? CompileText(StylesheetElement element, DiagnosticBag bag)
    {
        string literal = ConcatTextChildren(element);

        // XSLT 2.0 §12.1 — @disable-output-escaping. We don't honour it in v1; warn when it
        // is set to "yes" so users know their intent is being ignored.
        string? doe = GetAttribute(element, "disable-output-escaping");
        if (string.Equals(doe, "yes", StringComparison.Ordinal))
        {
            bag.Warning(
                XsltErrors.XTSE0150,
                "xsl:text @disable-output-escaping is not honoured by this processor; output will be escaped normally",
                element.Location);
        }

        return new TextInstruction(literal, element.Location);
    }

    private static string ConcatTextChildren(StylesheetElement element)
    {
        if (element.Children.Count == 0)
        {
            return string.Empty;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < element.Children.Count; i++)
        {
            if (element.Children[i] is StylesheetText t)
            {
                sb.Append(t.Text);
            }
        }

        return sb.ToString();
    }

    // ── xsl:variable (local) ───────────────────────────────────────────────

    private static XsltInstruction? CompileLocalVariable(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:variable requires a name attribute", element.Location);
            return null;
        }

        if (!LexicalQNameParser.TryResolve(nameAttr, element.InScopeNamespaces, out QName name, out string? why))
        {
            bag.Error(XsltErrors.XTSE0010, $"Invalid variable name '{nameAttr}': {why}", element.Location);
            return null;
        }

        string? selectExpr = GetAttribute(element, "select");
        BoundExpression? select = null;
        if (!string.IsNullOrEmpty(selectExpr))
        {
            if (!TryCompileXPath(selectExpr!, element, scope, bag, out select))
            {
                return null;
            }
        }

        // XSLT 2.0 §9.3: without select the value is the content — a document node when there is no
        // as attribute, else the sequence the content constructs; an empty untyped variable is "".
        // The content is compiled before the variable is declared, so it cannot see itself.
        VariableValueBody? body = null;
        if (select is null && string.IsNullOrEmpty(selectExpr))
        {
            bool hasAs = GetAttribute(element, "as") is not null;
            if (element.Children.Count > 0)
            {
                body = new VariableValueBody(CompileChildren(element, scope, bag), buildsDocument: !hasAs);
            }
            else if (!hasAs && !TryCompileXPath("''", element, scope, bag, out select))
            {
                return null;
            }
        }

        // Add to scope so subsequent siblings see $name. Lexical extent is the enclosing
        // SequenceInstruction (restored by its finally block at runtime).
        scope.Declare(name, SequenceType.AnySequence);
        return new VariableInstruction(name, select, body, element.Location);
    }

    // ── Literal result elements ────────────────────────────────────────────

    private static XsltInstruction? CompileLiteralResultElement(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        List<LiteralAttributeInstruction> attributes = new List<LiteralAttributeInstruction>();
        for (int i = 0; i < element.Attributes.Count; i++)
        {
            StylesheetAttribute attr = element.Attributes[i];
            if (string.Equals(attr.Name.NamespaceUri, XsltNamespace.Uri, StringComparison.Ordinal))
            {
                continue;
            }

            CompiledAvt? avt = CompileAvt(attr, element, scope, bag);
            if (avt is null)
            {
                continue;
            }

            attributes.Add(new LiteralAttributeInstruction(attr.Name, avt, attr.Location));
        }

        XsltInstruction body = CompileChildren(element, scope, bag);
        return new LiteralResultElementInstruction(element.Name, attributes, body, element.Location);
    }

    private static CompiledAvt? CompileAvt(StylesheetAttribute attr, StylesheetElement scopeElement, XsltCompileScope scope, DiagnosticBag bag)
    {
        AttributeValueTemplate? avt = attr.ParsedTemplate ?? AttributeValueTemplate.Parse(attr.Value, attr.Location);
        List<CompiledAvtPart> parts = new List<CompiledAvtPart>(avt.Parts.Count);
        foreach (AvtPart part in avt.Parts)
        {
            switch (part)
            {
                case AvtLiteral literal:
                    parts.Add(CompiledAvtPart.Literal_(literal.Text));
                    break;

                case AvtExpression expr:
                    if (!TryCompileXPath(expr.ExpressionText, scopeElement, scope, bag, out BoundExpression? compiled))
                    {
                        return null;
                    }

                    parts.Add(CompiledAvtPart.Expression_(compiled!));
                    break;
            }
        }

        return new CompiledAvt(parts.ToArray());
    }

    // ── Params ─────────────────────────────────────────────────────────────

    private static bool TryCompileParam(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag, out ParamSlot? slot)
    {
        slot = null;
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:param requires a name attribute", element.Location);
            return false;
        }

        if (!LexicalQNameParser.TryResolve(nameAttr, element.InScopeNamespaces, out QName name, out string? why))
        {
            bag.Error(XsltErrors.XTSE0010, $"Invalid param name '{nameAttr}': {why}", element.Location);
            return false;
        }

        bool required = string.Equals(GetAttribute(element, "required"), "yes", StringComparison.Ordinal);
        string? selectExpr = GetAttribute(element, "select");
        BoundExpression? defaultSelect = null;
        if (!string.IsNullOrEmpty(selectExpr))
        {
            if (!TryCompileXPath(selectExpr!, element, scope, bag, out defaultSelect))
            {
                return false;
            }
        }

        slot = new ParamSlot(name, defaultSelect, required, element.Location);
        return true;
    }

    // ── Control flow ───────────────────────────────────────────────────────

    private static XsltInstruction? CompileIf(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? testAttr = GetAttribute(element, "test");
        if (testAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:if requires a test attribute", element.Location);
            return null;
        }

        if (!TryCompileXPath(testAttr, element, scope, bag, out BoundExpression? test))
        {
            return null;
        }

        XsltInstruction body = CompileChildren(element, scope, bag);
        return new IfInstruction(test!, body, element.Location);
    }

    private static XsltInstruction? CompileChoose(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        List<ChooseBranch> whens = new List<ChooseBranch>();
        XsltInstruction? otherwise = null;

        for (int i = 0; i < element.Children.Count; i++)
        {
            if (element.Children[i] is not StylesheetElement child || !child.IsXsltElement)
            {
                continue;
            }

            switch (child.Name.LocalName)
            {
                case "when":
                    string? testAttr = GetAttribute(child, "test");
                    if (testAttr is null)
                    {
                        bag.Error(XsltErrors.XTSE0010, "xsl:when requires a test attribute", child.Location);
                        continue;
                    }

                    if (!TryCompileXPath(testAttr, child, scope, bag, out BoundExpression? test))
                    {
                        continue;
                    }

                    whens.Add(new ChooseBranch(test!, CompileChildren(child, scope, bag)));
                    break;

                case "otherwise":
                    otherwise = CompileChildren(child, scope, bag);
                    break;
            }
        }

        return new ChooseInstruction(whens, otherwise, element.Location);
    }

    private static XsltInstruction? CompileForEach(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? selectAttr = GetAttribute(element, "select");
        if (selectAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:for-each requires a select attribute", element.Location);
            return null;
        }

        if (!TryCompileXPath(selectAttr, element, scope, bag, out BoundExpression? select))
        {
            return null;
        }

        XsltInstruction body = CompileChildren(element, scope, bag);
        return new ForEachInstruction(select!, body, element.Location, CompileSortKeys(element, scope, bag));
    }

    // ── xsl:sort / xsl:perform-sort (XSLT 2.0 §13) ─────────────────────────────

    // The xsl:sort children of an instruction, in order.
    private static List<SortKey> CompileSortKeys(StylesheetElement parent, XsltCompileScope scope, DiagnosticBag bag)
    {
        List<SortKey> keys = new List<SortKey>();
        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (parent.Children[i] is StylesheetElement child &&
                child.IsXsltElement &&
                string.Equals(child.Name.LocalName, "sort", StringComparison.Ordinal))
            {
                SortKey? key = CompileSortKey(child, scope, bag);
                if (key is not null)
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    private static SortKey? CompileSortKey(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? selectAttr = GetAttribute(element, "select");
        BoundExpression? select = null;
        VariableValueBody? content = null;
        if (selectAttr is not null)
        {
            if (element.Children.Count > 0)
            {
                bag.Error(XsltErrors.XTSE1015, "xsl:sort has both a select attribute and content", element.Location);
                return null;
            }

            if (!TryCompileXPath(selectAttr, element, scope, bag, out select))
            {
                return null;
            }
        }
        else if (element.Children.Count > 0)
        {
            content = new VariableValueBody(CompileChildren(element, scope, bag), buildsDocument: false);
        }
        else if (!TryCompileXPath(".", element, scope, bag, out select))
        {
            return null;
        }

        CompiledAvt? order = CompileOptionalAvt(element, "order", scope, bag, out bool ok1);
        CompiledAvt? dataType = CompileOptionalAvt(element, "data-type", scope, bag, out bool ok2);
        CompiledAvt? caseOrder = CompileOptionalAvt(element, "case-order", scope, bag, out bool ok3);
        CompiledAvt? lang = CompileOptionalAvt(element, "lang", scope, bag, out bool ok4);
        CompiledAvt? collation = CompileOptionalAvt(element, "collation", scope, bag, out bool ok5);
        CompiledAvt? stable = CompileOptionalAvt(element, "stable", scope, bag, out bool ok6);
        if (!(ok1 && ok2 && ok3 && ok4 && ok5 && ok6))
        {
            return null;
        }

        // A literal value that is not allowed is a static error; one computed at run time is XTDE0030.
        if (!IsValidLiteral(order, "ascending", "descending") ||
            !IsValidLiteral(caseOrder, "upper-first", "lower-first") ||
            !IsValidLiteral(stable, "yes", "no") ||
            !IsValidDataType(dataType) ||
            !IsValidLanguageLiteral(lang))
        {
            bag.Error(XsltErrors.XTSE0020, "xsl:sort has an attribute with an invalid value", element.Location);
            return null;
        }

        return new SortKey(select, content, order, dataType, caseOrder, lang, collation, stable, element.Location);
    }

    private static bool IsValidLiteral(CompiledAvt? avt, string a, string b)
    {
        string? text = avt is null ? null : LiteralValue(avt);
        if (text is null)
        {
            return true;
        }

        text = text.Trim();
        return string.Equals(text, a, StringComparison.Ordinal) || string.Equals(text, b, StringComparison.Ordinal);
    }

    private static bool IsValidLanguageLiteral(CompiledAvt? avt)
    {
        string? text = avt is null ? null : LiteralValue(avt);
        return text is null || ItemSorter.IsValidLanguage(text.Trim());
    }

    private static bool IsValidDataType(CompiledAvt? avt)
    {
        string? text = avt is null ? null : LiteralValue(avt);
        if (text is null)
        {
            return true;
        }

        text = text.Trim();
        return text == "text" || text == "number" || text.IndexOf(':') > 0;
    }

    private static XsltInstruction? CompilePerformSort(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        List<SortKey> keys = CompileSortKeys(element, scope, bag);
        if (keys.Count == 0)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:perform-sort requires at least one xsl:sort", element.Location);
            return null;
        }

        string? selectAttr = GetAttribute(element, "select");
        if (selectAttr is not null)
        {
            if (!TryCompileXPath(selectAttr, element, scope, bag, out BoundExpression? select))
            {
                return null;
            }

            return new PerformSortInstruction(select, null, keys, element.Location);
        }

        // The content, other than the xsl:sort elements (which compile to nothing), builds the sequence.
        VariableValueBody content = new VariableValueBody(CompileChildren(element, scope, bag), buildsDocument: false);
        return new PerformSortInstruction(null, content, keys, element.Location);
    }

    // ── apply-templates / call-template ────────────────────────────────────

    private static XsltInstruction? CompileApplyTemplates(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string selectAttr = GetAttribute(element, "select") ?? "child::node()";
        if (!TryCompileXPath(selectAttr, element, scope, bag, out BoundExpression? select))
        {
            return null;
        }

        string? modeAttr = GetAttribute(element, "mode");
        ApplyTemplatesMode modeSpec;
        QName mode = DeclarationCollector.DefaultMode;
        if (modeAttr is null || string.Equals(modeAttr, "#default", StringComparison.Ordinal))
        {
            modeSpec = ApplyTemplatesMode.Default;
        }
        else if (string.Equals(modeAttr, "#current", StringComparison.Ordinal))
        {
            modeSpec = ApplyTemplatesMode.Current;
        }
        else if (!LexicalQNameParser.TryResolve(modeAttr, element.InScopeNamespaces, out mode, out string? why))
        {
            bag.Error(XsltErrors.XTSE0010, $"Invalid mode '{modeAttr}' on xsl:apply-templates: {why}", element.Location);
            return null;
        }
        else
        {
            modeSpec = ApplyTemplatesMode.Named;
        }

        List<WithParamInstruction> withParams = CollectWithParams(element, scope, bag);
        return new ApplyTemplatesInstruction(select!, modeSpec, mode, withParams, element.Location, CompileSortKeys(element, scope, bag));
    }

    private static XsltInstruction? CompileCallTemplate(StylesheetElement element, XsltCompileScope scope, DiagnosticBag bag)
    {
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:call-template requires a name attribute", element.Location);
            return null;
        }

        if (!LexicalQNameParser.TryResolve(nameAttr, element.InScopeNamespaces, out QName name, out string? why))
        {
            bag.Error(XsltErrors.XTSE0010, $"Invalid template name '{nameAttr}': {why}", element.Location);
            return null;
        }

        List<WithParamInstruction> withParams = CollectWithParams(element, scope, bag);
        return new CallTemplateInstruction(name, withParams, element.Location);
    }

    private static List<WithParamInstruction> CollectWithParams(StylesheetElement parent, XsltCompileScope scope, DiagnosticBag bag)
    {
        List<WithParamInstruction> list = new List<WithParamInstruction>();
        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (parent.Children[i] is not StylesheetElement child || !child.IsXsltElement)
            {
                continue;
            }

            if (!string.Equals(child.Name.LocalName, "with-param", StringComparison.Ordinal))
            {
                continue;
            }

            string? nameAttr = GetAttribute(child, "name");
            if (nameAttr is null)
            {
                bag.Error(XsltErrors.XTSE0010, "xsl:with-param requires a name attribute", child.Location);
                continue;
            }

            if (!LexicalQNameParser.TryResolve(nameAttr, child.InScopeNamespaces, out QName name, out string? why))
            {
                bag.Error(XsltErrors.XTSE0010, $"Invalid param name '{nameAttr}': {why}", child.Location);
                continue;
            }

            string? select = GetAttribute(child, "select");
            BoundExpression? expr = null;
            if (!string.IsNullOrEmpty(select))
            {
                if (!TryCompileXPath(select!, child, scope, bag, out expr))
                {
                    continue;
                }
            }

            list.Add(new WithParamInstruction(name, expr, child.Location));
        }

        return list;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static XsltInstruction CompileChildren(StylesheetElement element, XsltCompileScope parentScope, DiagnosticBag bag)
    {
        // Nested bodies get their own scope so xsl:variable declarations don't leak upward.
        XsltCompileScope scope = parentScope.Enter();
        List<XsltInstruction> children = new List<XsltInstruction>();
        for (int i = 0; i < element.Children.Count; i++)
        {
            XsltInstruction? instr = CompileNode(element.Children[i], scope, bag);
            if (instr is not null)
            {
                children.Add(instr);
            }
        }

        return children.Count switch
        {
            0 => SequenceInstruction.Empty,
            1 => new SequenceInstruction(new XsltInstruction[] { children[0] }, element.Location),
            _ => new SequenceInstruction(children, element.Location),
        };
    }

    internal static bool TryCompileXPath(
        string expression,
        StylesheetElement scopeElement,
        XsltCompileScope scope,
        DiagnosticBag bag,
        out BoundExpression? compiled)
    {
        try
        {
            NamespaceBindingsResolver resolver = new NamespaceBindingsResolver(
                scopeElement.InScopeNamespaces,
                defaultElementNamespace: string.Empty,
                defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);

            Dictionary<QName, SequenceType> externals = scope.AsExternalVariables();
            CompiledExpression ce = XPathCompiler.ParseAndCompile(
                expression, scopeElement.Location, resolver, s_functions ?? BuiltInFunctionLibrary.Instance, externals);
            compiled = new BoundExpression(ce.Root, ce.SlotCount, new List<QName>(scope.Names));
            return true;
        }
        catch (XTransformException ex)
        {
            bag.Error(ex.ErrorCode, ex.Message, scopeElement.Location);
            compiled = null;
            return false;
        }
    }

    private static string? GetAttribute(StylesheetElement element, string localName)
    {
        for (int i = 0; i < element.Attributes.Count; i++)
        {
            StylesheetAttribute attr = element.Attributes[i];
            if (attr.Name.NamespaceUri.Length == 0 &&
                string.Equals(attr.Name.LocalName, localName, StringComparison.Ordinal))
            {
                return attr.Value;
            }
        }

        return null;
    }
}
