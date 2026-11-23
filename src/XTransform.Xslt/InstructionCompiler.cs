// Compiles the body of an xsl:template / xsl:function / etc. to an XsltInstruction tree.
// P3-07 implements control-flow + apply-templates/call-template; content instructions
// (xsl:value-of / xsl:text / LRE / xsl:element / …) land in P3-07b and P3-09+.

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
    public static CompiledTemplate? CompileTemplate(TemplateDecl decl, DiagnosticBag bag)
    {
        // Pass 1: collect xsl:param children at the start of the template body and assign
        // slot indices. Instructions appear after the last param (XSLT 2.0 §9.5).
        List<ParamSlot> paramSlots = new List<ParamSlot>();
        int firstNonParam = 0;
        for (int i = 0; i < decl.Element.Children.Count; i++)
        {
            if (decl.Element.Children[i] is not StylesheetElement child)
            {
                continue;
            }

            if (!(child.IsXsltElement && string.Equals(child.Name.LocalName, "param", StringComparison.Ordinal)))
            {
                firstNonParam = i;
                break;
            }

            if (!TryCompileParam(child, paramSlots.Count, bag, out ParamSlot? slot))
            {
                return null;
            }

            paramSlots.Add(slot!);
            firstNonParam = i + 1;
        }

        // Pass 2: compile the remaining children as the body.
        List<XsltInstruction> bodyInstructions = new List<XsltInstruction>();
        for (int i = firstNonParam; i < decl.Element.Children.Count; i++)
        {
            XsltInstruction? compiled = CompileNode(decl.Element.Children[i], bag);
            if (compiled is not null)
            {
                bodyInstructions.Add(compiled);
            }
        }

        XsltInstruction body = bodyInstructions.Count == 1
            ? bodyInstructions[0]
            : new SequenceInstruction(bodyInstructions, decl.Location);

        // P3-07 scope: the slot count equals the parameter count. Local variables (P3-08) and
        // XPath-internal slots (currently allocated per-expression in fresh frames) don't
        // contribute to the shared template frame yet.
        int slotCount = paramSlots.Count;
        return new CompiledTemplate(decl, paramSlots, slotCount, body);
    }

    private static XsltInstruction? CompileNode(StylesheetNode node, DiagnosticBag bag)
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
            return CompileLiteralResultElement(element, bag);
        }

        return element.Name.LocalName switch
        {
            "if" => CompileIf(element, bag),
            "choose" => CompileChoose(element, bag),
            "for-each" => CompileForEach(element, bag),
            "apply-templates" => CompileApplyTemplates(element, bag),
            "call-template" => CompileCallTemplate(element, bag),
            "value-of" => CompileValueOf(element, bag),
            "text" => CompileText(element, bag),
            // Deferred to later tasks:
            "variable" or "element" or "attribute" or
            "copy" or "copy-of" or "sequence" or "comment" or "processing-instruction" or
            "document" or "message" or "number" or "result-document" or
            "analyze-string" or "for-each-group" or "next-match" or "fallback" or
            "apply-imports" or "perform-sort" => null,
            _ => null,
        };
    }

    // ── Content-producing instructions ─────────────────────────────────────

    private static XsltInstruction? CompileValueOf(StylesheetElement element, DiagnosticBag bag)
    {
        string? selectAttr = GetAttribute(element, "select");
        if (selectAttr is null)
        {
            // XSLT 2.0 allows a body providing the value too; P3-07b scope only supports @select.
            bag.Error(XsltErrors.XTSE0010, "xsl:value-of requires a select attribute (body form is deferred)", element.Location);
            return null;
        }

        if (!TryCompileXPath(selectAttr, element, bag, out ExpressionNode? select))
        {
            return null;
        }

        string separator = GetAttribute(element, "separator") ?? " ";
        return new ValueOfInstruction(select!, separator, element.Location);
    }

    private static XsltInstruction? CompileText(StylesheetElement element, DiagnosticBag bag)
    {
        // xsl:text content is the concatenated text of its children (child text nodes only).
        string literal = ConcatTextChildren(element);
        _ = bag;
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

    // ── Literal result elements ────────────────────────────────────────────

    private static XsltInstruction? CompileLiteralResultElement(StylesheetElement element, DiagnosticBag bag)
    {
        List<LiteralAttributeInstruction> attributes = new List<LiteralAttributeInstruction>();
        for (int i = 0; i < element.Attributes.Count; i++)
        {
            StylesheetAttribute attr = element.Attributes[i];
            // Skip xsl: control attributes on LREs (xsl:version, xsl:use-attribute-sets, etc.).
            if (string.Equals(attr.Name.NamespaceUri, XsltNamespace.Uri, StringComparison.Ordinal))
            {
                continue;
            }

            CompiledAvt? avt = CompileAvt(attr, element, bag);
            if (avt is null)
            {
                continue;
            }

            attributes.Add(new LiteralAttributeInstruction(attr.Name, avt, attr.Location));
        }

        XsltInstruction body = CompileChildren(element, bag);
        return new LiteralResultElementInstruction(element.Name, attributes, body, element.Location);
    }

    private static CompiledAvt? CompileAvt(StylesheetAttribute attr, StylesheetElement scope, DiagnosticBag bag)
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
                    if (!TryCompileXPath(expr.ExpressionText, scope, bag, out ExpressionNode? compiled))
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

    private static bool TryCompileParam(StylesheetElement element, int slotIndex, DiagnosticBag bag, out ParamSlot? slot)
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
        ExpressionNode? defaultSelect = null;
        if (!string.IsNullOrEmpty(selectExpr))
        {
            if (!TryCompileXPath(selectExpr!, element, bag, out defaultSelect))
            {
                return false;
            }
        }

        slot = new ParamSlot(name, slotIndex, defaultSelect, required, element.Location);
        return true;
    }

    // ── Control flow ───────────────────────────────────────────────────────

    private static XsltInstruction? CompileIf(StylesheetElement element, DiagnosticBag bag)
    {
        string? testAttr = GetAttribute(element, "test");
        if (testAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:if requires a test attribute", element.Location);
            return null;
        }

        if (!TryCompileXPath(testAttr, element, bag, out ExpressionNode? test))
        {
            return null;
        }

        XsltInstruction body = CompileChildren(element, bag);
        return new IfInstruction(test!, body, element.Location);
    }

    private static XsltInstruction? CompileChoose(StylesheetElement element, DiagnosticBag bag)
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

                    if (!TryCompileXPath(testAttr, child, bag, out ExpressionNode? test))
                    {
                        continue;
                    }

                    whens.Add(new ChooseBranch(test!, CompileChildren(child, bag)));
                    break;

                case "otherwise":
                    otherwise = CompileChildren(child, bag);
                    break;
            }
        }

        return new ChooseInstruction(whens, otherwise, element.Location);
    }

    private static XsltInstruction? CompileForEach(StylesheetElement element, DiagnosticBag bag)
    {
        string? selectAttr = GetAttribute(element, "select");
        if (selectAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:for-each requires a select attribute", element.Location);
            return null;
        }

        if (!TryCompileXPath(selectAttr, element, bag, out ExpressionNode? select))
        {
            return null;
        }

        XsltInstruction body = CompileChildren(element, bag);
        return new ForEachInstruction(select!, body, element.Location);
    }

    // ── apply-templates / call-template ────────────────────────────────────

    private static XsltInstruction? CompileApplyTemplates(StylesheetElement element, DiagnosticBag bag)
    {
        string selectAttr = GetAttribute(element, "select") ?? "child::node()";
        if (!TryCompileXPath(selectAttr, element, bag, out ExpressionNode? select))
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

        List<WithParamInstruction> withParams = CollectWithParams(element, bag);
        return new ApplyTemplatesInstruction(select!, modeSpec, mode, withParams, element.Location);
    }

    private static XsltInstruction? CompileCallTemplate(StylesheetElement element, DiagnosticBag bag)
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

        List<WithParamInstruction> withParams = CollectWithParams(element, bag);
        return new CallTemplateInstruction(name, withParams, element.Location);
    }

    private static List<WithParamInstruction> CollectWithParams(StylesheetElement parent, DiagnosticBag bag)
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
            ExpressionNode? expr = null;
            if (!string.IsNullOrEmpty(select))
            {
                if (!TryCompileXPath(select!, child, bag, out expr))
                {
                    continue;
                }
            }

            list.Add(new WithParamInstruction(name, expr, child.Location));
        }

        return list;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static XsltInstruction CompileChildren(StylesheetElement element, DiagnosticBag bag)
    {
        List<XsltInstruction> children = new List<XsltInstruction>();
        for (int i = 0; i < element.Children.Count; i++)
        {
            XsltInstruction? instr = CompileNode(element.Children[i], bag);
            if (instr is not null)
            {
                children.Add(instr);
            }
        }

        return children.Count switch
        {
            0 => SequenceInstruction.Empty,
            1 => children[0],
            _ => new SequenceInstruction(children, element.Location),
        };
    }

    private static bool TryCompileXPath(
        string expression,
        StylesheetElement scope,
        DiagnosticBag bag,
        out ExpressionNode? compiled)
    {
        try
        {
            NamespaceBindingsResolver resolver = new NamespaceBindingsResolver(
                scope.InScopeNamespaces,
                defaultElementNamespace: string.Empty,
                defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);

            // P3-07 scope: no external variables are visible to the compiled expression; the
            // globals + xsl:param plumbing lands in P3-08.
            CompiledExpression ce = XPathCompiler.ParseAndCompile(
                expression, scope.Location, resolver, BuiltInFunctionLibrary.Instance);
            compiled = ce.Root;
            return true;
        }
        catch (XTransformException ex)
        {
            bag.Error(ex.ErrorCode, ex.Message, scope.Location);
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
