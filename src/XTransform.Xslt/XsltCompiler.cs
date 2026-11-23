// XSLT 2.0 compiler — pass 1 (declaration collection and name binding).
// Later passes (static type inference, template rule compilation, optimisation, code gen)
// land in P3-05+ and attach additional state to the returned CompiledStylesheet.

using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// The XSLT 2.0 compiler. P3-04 implements declaration collection and the first wave of
/// name-binding diagnostics; subsequent tasks bolt instruction compilation onto the result.
/// </summary>
internal static class XsltCompiler
{
    /// <summary>
    /// Runs the P3-04 compiler passes over <paramref name="assembly"/>. Every static error
    /// detected during a pass is collected into a <see cref="DiagnosticBag"/>; on completion,
    /// the compiler throws a single <see cref="XsltStaticException"/> listing them all.
    /// </summary>
    /// <exception cref="XsltStaticException">Thrown when any static error was recorded.</exception>
    public static CompiledStylesheet Compile(StylesheetAssembly assembly)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }
#pragma warning restore CA1510

        DiagnosticBag bag = new DiagnosticBag();

        // Preserve any warnings carried from the linker / per-module reader / processor.
        List<Diagnostic> warnings = new List<Diagnostic>();
        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            foreach (Diagnostic d in assembly.Modules[i].Module.Diagnostics)
            {
                if (d.Severity == DiagnosticSeverity.Warning)
                {
                    warnings.Add(d);
                }
                else
                {
                    bag.Add(d);
                }
            }
        }

        DeclarationTable declarations = DeclarationCollector.Collect(assembly, bag);
        NameBindingValidator.Validate(declarations, bag);
        GlobalBindingValidator.Validate(declarations, bag);

        if (bag.HasErrors)
        {
            throw new XsltStaticException(bag.ToList());
        }

        return new CompiledStylesheet(assembly, declarations, warnings);
    }
}
