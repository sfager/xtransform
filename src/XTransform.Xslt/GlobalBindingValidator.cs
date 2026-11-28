// XSLT 2.0 §9.3 — validate global variable / param initialisers by compiling their XPath.
// Any XPath static error (XPST0003, XPST0008, XPST0017) in a top-level select expression
// surfaces here with the correct error code already attached by the XPath compiler.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt;

/// <summary>
/// Compiles every top-level global variable / parameter <c>select</c> expression against a
/// function library and the set of global variables — producing the static diagnostics that
/// would otherwise surface at runtime (undeclared variable → XPST0008, unknown function →
/// XPST0017).
/// </summary>
internal static class GlobalBindingValidator
{
    /// <summary>Runs the global-binding compile pass and appends diagnostics to <paramref name="bag"/>.</summary>
    public static void Validate(DeclarationTable declarations, FunctionLibrary functions, DiagnosticBag bag)
    {
        // External variables map: every global's QName is in scope inside every other global
        // initialiser. We don't yet track the SequenceType precisely — item()* is a safe
        // upper bound for this pass.
        Dictionary<QName, SequenceType> externalVars = new Dictionary<QName, SequenceType>();
        foreach (QName name in declarations.GlobalVariables.Keys)
        {
            externalVars[name] = SequenceType.AnySequence;
        }

        foreach (GlobalVariableDecl decl in declarations.GlobalVariables.Values)
        {
            if (string.IsNullOrEmpty(decl.SelectExpression))
            {
                continue;
            }

            NamespaceBindingsResolver resolver = new NamespaceBindingsResolver(
                decl.Element.InScopeNamespaces,
                defaultElementNamespace: decl.Element.XPathDefaultNamespace,
                defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);

            try
            {
                XPathCompiler.ParseAndCompile(
                    decl.SelectExpression!,
                    decl.Location,
                    resolver,
                    functions,
                    externalVars);
            }
            catch (XTransformException ex)
            {
                bag.Error(ex.ErrorCode, ex.Message, decl.Location);
            }
        }
    }
}
