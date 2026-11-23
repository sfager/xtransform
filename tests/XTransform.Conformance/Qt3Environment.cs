using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace XTransform.Conformance;

/// <summary>
/// Represents a resolved QT3 test environment: source documents, namespace bindings, and param values.
/// </summary>
internal sealed class Qt3Environment
{
    private static readonly XNamespace Ns = "http://www.w3.org/2010/09/qt-fots-catalog";

    /// <summary>Environment name (from the catalog), or empty for inline environments.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Path to the source document with role=".", or null.</summary>
    public string? ContextSourceFile { get; init; }

    /// <summary>Source files by their role (not including the "." context role).</summary>
    public Dictionary<string, string> SourceByRole { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Namespace prefix → URI bindings declared in the environment.</summary>
    public Dictionary<string, string> Namespaces { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Variable parameter name → select-expression bindings.</summary>
    public Dictionary<string, string> Params { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Parses an &lt;environment&gt; element relative to <paramref name="baseDir"/>.</summary>
    public static Qt3Environment FromXml(XElement envElement, string? baseDir)
    {
        string envName = (string?)envElement.Attribute("name") ?? string.Empty;
        string? contextSourceFile = null;
        Dictionary<string, string> sourceByRole = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> namespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> paramss = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (XElement child in envElement.Elements())
        {
            string local = child.Name.LocalName;

            if (local == "source")
            {
                string? role = (string?)child.Attribute("role");
                string? file = (string?)child.Attribute("file");
                if (role is null || file is null)
                {
                    continue;
                }

                // Resolve relative to baseDir
                string resolved = baseDir is not null
                    ? Path.GetFullPath(Path.Combine(baseDir, file))
                    : file;

                if (role == ".")
                {
                    contextSourceFile = resolved;
                }
                else
                {
                    sourceByRole[role] = resolved;
                }
            }
            else if (local == "namespace")
            {
                string? prefix = (string?)child.Attribute("prefix");
                string? uri = (string?)child.Attribute("uri");
                if (prefix is not null && uri is not null)
                {
                    namespaces[prefix] = uri;
                }
            }
            else if (local == "param")
            {
                string? name = (string?)child.Attribute("name");
                string? select = (string?)child.Attribute("select");
                if (name is not null && select is not null)
                {
                    paramss[name] = select;
                }
            }
            // schema, description, created, etc. are ignored
        }

        Qt3Environment env = new Qt3Environment
        {
            Name = envName,
            ContextSourceFile = contextSourceFile,
        };

        foreach (KeyValuePair<string, string> kv in sourceByRole)
        {
            env.SourceByRole[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in namespaces)
        {
            env.Namespaces[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in paramss)
        {
            env.Params[kv.Key] = kv.Value;
        }

        return env;
    }

    /// <summary>
    /// Merges set-level and test-case-level environments. Test-case values override set-level values.
    /// Either argument may be null.
    /// </summary>
    public static Qt3Environment Merge(Qt3Environment? setLevel, Qt3Environment? caseLevel)
    {
        if (setLevel is null && caseLevel is null)
        {
            return new Qt3Environment();
        }

        if (setLevel is null)
        {
            return caseLevel!;
        }

        if (caseLevel is null)
        {
            return setLevel;
        }

        Qt3Environment merged = new Qt3Environment
        {
            Name = caseLevel.Name.Length > 0 ? caseLevel.Name : setLevel.Name,
            ContextSourceFile = caseLevel.ContextSourceFile ?? setLevel.ContextSourceFile,
        };

        foreach (KeyValuePair<string, string> kv in setLevel.SourceByRole)
        {
            merged.SourceByRole[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in caseLevel.SourceByRole)
        {
            merged.SourceByRole[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in setLevel.Namespaces)
        {
            merged.Namespaces[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in caseLevel.Namespaces)
        {
            merged.Namespaces[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in setLevel.Params)
        {
            merged.Params[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, string> kv in caseLevel.Params)
        {
            merged.Params[kv.Key] = kv.Value;
        }

        return merged;
    }
}
