// XSLT 2.0 §3.11 — xsl:include / xsl:import resolution.
// Walks the include/import graph from a primary stylesheet module, loading each referenced
// module via IResourceResolver and assigning an import precedence (§3.11.3).

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Resolves the <c>xsl:include</c> / <c>xsl:import</c> graph of a primary stylesheet module
/// and assigns import precedences. Cycles raise <c>XTSE0210</c>; missing or unreadable
/// modules raise <c>XTSE0165</c>.
/// </summary>
internal static class StylesheetLinker
{
    /// <summary>
    /// Loads and links the stylesheet at <paramref name="primaryUri"/>, resolving all
    /// <c>xsl:include</c> / <c>xsl:import</c> references via <paramref name="resolver"/>.
    /// </summary>
    public static StylesheetAssembly Link(Uri primaryUri, IResourceResolver resolver)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (primaryUri is null)
        {
            throw new ArgumentNullException(nameof(primaryUri));
        }

        if (resolver is null)
        {
            throw new ArgumentNullException(nameof(resolver));
        }
#pragma warning restore CA1510

        LinkerState state = new LinkerState(resolver);
        return Assemble(state.LoadImportTree(primaryUri, SourceSpan.None, preRead: null));
    }

    /// <summary>
    /// Links a primary module that the caller has already opened as <paramref name="reader"/>.
    /// Relative <c>xsl:include</c> / <c>xsl:import</c> hrefs resolve against
    /// <paramref name="baseUri"/>; without one a relative href raises <c>XTSE0165</c>.
    /// </summary>
    public static StylesheetAssembly Link(XmlReader reader, Uri? baseUri, IResourceResolver resolver)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        if (resolver is null)
        {
            throw new ArgumentNullException(nameof(resolver));
        }
#pragma warning restore CA1510

        StylesheetModule module = StylesheetReader.Read(reader, baseUri);
        Uri identity = baseUri ?? new Uri("urn:xtransform:anonymous-stylesheet");
        LinkerState state = new LinkerState(resolver);
        return Assemble(state.LoadImportTree(identity, SourceSpan.None, module));
    }

    private static StylesheetAssembly Assemble(ModuleNode primary)
    {
        List<StylesheetModuleEntry> flat = new List<StylesheetModuleEntry>();
        int precedence = 0;
        AssignPrecedence(primary, ref precedence, flat, isPrimary: true);

        return new StylesheetAssembly(flat[flat.Count - 1], flat);
    }

    /// <summary>
    /// Post-order DFS of the import tree. Each node is given a strictly higher precedence
    /// number than any of its descendants, and later sibling imports get higher precedence
    /// than earlier siblings (XSLT 2.0 §3.11.3). Included modules share their parent's
    /// precedence — they contribute their imports to the parent's subtree but don't get a
    /// new slot of their own.
    /// </summary>
    private static void AssignPrecedence(
        ModuleNode node,
        ref int precedence,
        List<StylesheetModuleEntry> flat,
        bool isPrimary)
    {
        // Everything this module imports (directly or not) gets a precedence in [subtreeStart, own precedence).
        int subtreeStart = precedence;
        for (int i = 0; i < node.Imports.Count; i++)
        {
            AssignPrecedence(node.Imports[i], ref precedence, flat, isPrimary: false);
        }

        int myPrecedence = precedence++;
        flat.Add(new StylesheetModuleEntry(node.Module, myPrecedence, isPrimary, subtreeStart));

        // Modules included into this one keep the same precedence — append them as entries
        // after this node so later lookups can walk the flat list in precedence order.
        for (int i = 0; i < node.Includes.Count; i++)
        {
            ModuleNode include = node.Includes[i];
            // The included module may itself contain imports; those have already been
            // threaded into node.Imports during loading (see LoadImportTree).
            flat.Add(new StylesheetModuleEntry(include.Module, myPrecedence, isPrimary: false, subtreeStart));
        }
    }

    // ── Linker state ────────────────────────────────────────────────────────

    private sealed class LinkerState
    {
        private readonly IResourceResolver _resolver;
        private readonly HashSet<string> _loading = new HashSet<string>(StringComparer.Ordinal);

        public LinkerState(IResourceResolver resolver)
        {
            _resolver = resolver;
        }

        /// <summary>
        /// Loads <paramref name="uri"/> and its xsl:include / xsl:import graph, returning the
        /// tree node that represents this module.
        /// </summary>
        public ModuleNode LoadImportTree(Uri uri, SourceSpan requestLocation, StylesheetModule? preRead = null, bool requestedByInclude = false)
        {
            string key = uri.OriginalString;

            if (_loading.Contains(key))
            {
                // XSLT 2.0 §3.11.1: a module that includes itself is XTSE0180; §3.11.2: one that imports itself is XTSE0210.
                throw requestedByInclude ? Xtse0180(requestLocation, uri) : Xtse0210(requestLocation, uri);
            }

            _loading.Add(key);
            try
            {
                StylesheetModule module = preRead ?? ReadModule(uri, requestLocation);
                StylesheetModule processed = StylesheetProcessor.Process(module);

                ModuleNode node = new ModuleNode(processed, uri);

                // Walk top-level xsl:include / xsl:import elements in document order. Includes
                // are expanded recursively into the current node's import list (their imports
                // belong to this module), while imports become children in the import tree.
                CollectImportsAndIncludes(processed.Root, uri, node);

                return node;
            }
            finally
            {
                _loading.Remove(key);
            }
        }

        private void CollectImportsAndIncludes(StylesheetElement element, Uri currentBase, ModuleNode node)
        {
            // Only the top-level children of xsl:stylesheet / xsl:transform matter here. For a
            // simplified stylesheet (LRE as root), xsl:include / xsl:import are not allowed
            // anyway.
            if (!element.IsXsltElement)
            {
                return;
            }

            if (!string.Equals(element.Name.LocalName, "stylesheet", StringComparison.Ordinal) &&
                !string.Equals(element.Name.LocalName, "transform", StringComparison.Ordinal))
            {
                return;
            }

            // The top-level elements as they are after includes are expanded: an xsl:include is replaced by the
            // top-level elements of the included module, at its position (XSLT 2.0 §3.11.1), so that declaration
            // order, and with it the choice among rules of equal precedence and priority, follows the text.
            List<StylesheetNode> expanded = new List<StylesheetNode>(element.Children.Count);
            for (int i = 0; i < element.Children.Count; i++)
            {
                StylesheetNode current = element.Children[i];
                if (current is not StylesheetElement child || !child.IsXsltElement)
                {
                    expanded.Add(current);
                    continue;
                }

                string local = child.Name.LocalName;
                if (string.Equals(local, "import", StringComparison.Ordinal))
                {
                    Uri target = ResolveHref(child, currentBase);
                    ModuleNode importedNode = LoadImportTree(target, child.Location);
                    node.Imports.Add(importedNode);
                    expanded.Add(current);
                }
                else if (string.Equals(local, "include", StringComparison.Ordinal))
                {
                    Uri target = ResolveHref(child, currentBase);
                    ModuleNode includedNode = LoadImportTree(target, child.Location, requestedByInclude: true);

                    // Any xsl:import under the included module contributes to this module's import tree.
                    for (int j = 0; j < includedNode.Imports.Count; j++)
                    {
                        node.Imports.Add(includedNode.Imports[j]);
                    }

                    // The included module's own includes were expanded when it was loaded. A simplified stylesheet
                    // module contributes its implicit template (XTSE0150 if its root lacks xsl:version).
                    StylesheetElement includedRoot = includedNode.Module.Root;
                    if (!SimplifiedStylesheet.IsStylesheetElement(includedRoot))
                    {
                        StylesheetElement? implicitTemplate = SimplifiedStylesheet.CreateTemplate(includedRoot);
                        if (implicitTemplate is null)
                        {
                            throw new XsltStaticException(new Diagnostic[]
                            {
                                new Diagnostic(
                                    DiagnosticSeverity.Error,
                                    XsltErrors.XTSE0150,
                                    $"The included module '{target}' is not a stylesheet: its literal result element root has no xsl:version",
                                    child.Location),
                            });
                        }

                        expanded.Add(implicitTemplate);
                    }
                    else
                    {
                        for (int j = 0; j < includedRoot.Children.Count; j++)
                        {
                            expanded.Add(includedRoot.Children[j]);
                        }
                    }
                }
                else
                {
                    expanded.Add(current);
                }

                // xsl:import must come before anything else except other xsl:import; §3.6.
                // Compiler-level ordering checks live in P3-04.
            }

            element.ReplaceChildren(expanded);
        }

        private static Uri ResolveHref(StylesheetElement element, Uri currentBase)
        {
            string? href = null;
            for (int i = 0; i < element.Attributes.Count; i++)
            {
                StylesheetAttribute attr = element.Attributes[i];
                if (string.Equals(attr.Name.LocalName, "href", StringComparison.Ordinal) &&
                    attr.Name.NamespaceUri.Length == 0)
                {
                    href = attr.Value;
                    break;
                }
            }

            if (href is null)
            {
                throw new XsltStaticException(new Diagnostic[]
                {
                    new Diagnostic(
                        DiagnosticSeverity.Error,
                        XsltErrors.XTSE0010,
                        $"xsl:{element.Name.LocalName} requires an href attribute",
                        element.Location),
                });
            }

            try
            {
                return new Uri(currentBase, href);
            }
            catch (UriFormatException)
            {
                // Typically a relative href against a base URI that cannot anchor one.
                throw Xtse0165(element.Location, currentBase, $"cannot resolve href '{href}' against this base URI");
            }
        }

        private StylesheetModule ReadModule(Uri uri, SourceSpan requestLocation)
        {
            Stream? stream;
            try
            {
                stream = _resolver.Open(uri);
            }
            catch (Exception ex)
            {
                throw Xtse0165(requestLocation, uri, ex.Message);
            }

            if (stream is null)
            {
                throw Xtse0165(requestLocation, uri, "resolver returned null");
            }

            using (stream)
            {
                return StylesheetReader.Read(stream, uri);
            }
        }

        private static XsltStaticException Xtse0165(SourceSpan location, Uri uri, string reason)
        {
            return new XsltStaticException(new Diagnostic[]
            {
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    XsltErrors.XTSE0165,
                    $"Cannot retrieve stylesheet module at {uri}: {reason}",
                    location),
            });
        }

        private static XsltStaticException Xtse0180(SourceSpan location, Uri uri)
        {
            return new XsltStaticException(new Diagnostic[]
            {
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    XsltErrors.XTSE0180,
                    $"Circular reference: stylesheet module {uri} includes itself, directly or indirectly",
                    location),
            });
        }

        private static XsltStaticException Xtse0210(SourceSpan location, Uri uri)
        {
            return new XsltStaticException(new Diagnostic[]
            {
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    XsltErrors.XTSE0210,
                    $"Circular reference: stylesheet module {uri} is already being loaded",
                    location),
            });
        }
    }

    /// <summary>Mutable DFS node used while building the import tree.</summary>
    private sealed class ModuleNode
    {
        public ModuleNode(StylesheetModule module, Uri uri)
        {
            Module = module;
            Uri = uri;
        }

        public StylesheetModule Module { get; }
        public Uri Uri { get; }
        public List<ModuleNode> Imports { get; } = new List<ModuleNode>();
        public List<ModuleNode> Includes { get; } = new List<ModuleNode>();
    }
}
