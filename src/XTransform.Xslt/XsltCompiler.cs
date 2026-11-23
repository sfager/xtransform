// XSLT 2.0 compiler — orchestrates declaration collection, name binding, mode compilation
// and instruction compilation. Later passes (full static type inference, optimisation,
// IL code gen) attach to CompiledStylesheet as separate tasks.

using System;
using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xslt.Instructions;

namespace XTransform.Xslt;

/// <summary>The XSLT 2.0 compiler.</summary>
internal static class XsltCompiler
{
    /// <summary>
    /// Runs every compiler pass over <paramref name="assembly"/>. Static errors are collected
    /// into a <see cref="DiagnosticBag"/> and raised as a single <see cref="XsltStaticException"/>.
    /// </summary>
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
        IReadOnlyDictionary<QName, ModeTable> modes = ModeCompiler.Compile(declarations, bag);

        // Build the function library: register user function signatures first so inter-function
        // references resolve; bodies are populated in pass 2 via the slot.
        XsltFunctionLibrary functions = new XsltFunctionLibrary();
        Dictionary<FunctionKey, UserFunctionSlot> userSlots = new Dictionary<FunctionKey, UserFunctionSlot>();
        foreach (KeyValuePair<FunctionKey, FunctionDecl> kv in declarations.Functions)
        {
            UserFunctionSlot slot = functions.RegisterUser(kv.Value.Name, kv.Value.Arity);
            userSlots[kv.Key] = slot;
        }

        // Pass 2: compile every template and function body.
        Dictionary<TemplateDecl, CompiledTemplate> compiledTemplates =
            new Dictionary<TemplateDecl, CompiledTemplate>();
        foreach (TemplateDecl decl in declarations.Templates)
        {
            CompiledTemplate? compiled = InstructionCompiler.CompileTemplate(decl, declarations, functions, bag);
            if (compiled is not null)
            {
                compiledTemplates[decl] = compiled;
            }
        }

        foreach (TemplateDecl decl in declarations.NamedTemplates.Values)
        {
            if (compiledTemplates.ContainsKey(decl))
            {
                continue;
            }

            CompiledTemplate? compiled = InstructionCompiler.CompileTemplate(decl, declarations, functions, bag);
            if (compiled is not null)
            {
                compiledTemplates[decl] = compiled;
            }
        }

        // Compile xsl:function bodies and bind their slots.
        foreach (KeyValuePair<FunctionKey, FunctionDecl> kv in declarations.Functions)
        {
            UserFunctionBody? body = InstructionCompiler.CompileUserFunction(kv.Value, declarations, functions, bag);
            if (body is not null)
            {
                userSlots[kv.Key].SetBody(body);
            }
        }

        if (bag.HasErrors)
        {
            throw new XsltStaticException(bag.ToList());
        }

        foreach (Diagnostic d in bag.ToList())
        {
            if (d.Severity == DiagnosticSeverity.Warning)
            {
                warnings.Add(d);
            }
        }

        return new CompiledStylesheet(
            assembly,
            declarations,
            modes,
            compiledTemplates,
            declarations.Keys,
            functions,
            warnings);
    }
}
