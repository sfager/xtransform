using System;
using System.Collections.Generic;

namespace XTransform.Xdm;

/// <summary>Severity of a compiler diagnostic.</summary>
public enum DiagnosticSeverity
{
    /// <summary>A non-fatal advisory.</summary>
    Warning,
    /// <summary>A fatal compilation error.</summary>
    Error,
}

/// <summary>A single compiler diagnostic: a location, severity, error code, and message.</summary>
public sealed class Diagnostic
{
    /// <summary>Initializes a diagnostic with the given fields.</summary>
    public Diagnostic(DiagnosticSeverity severity, QName code, string message, SourceSpan location)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }
#pragma warning restore CA1510

        Severity = severity;
        Code = code;
        Message = message;
        Location = location;
    }

    /// <summary>Whether this is a warning or an error.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>The W3C error QName, e.g. {http://www.w3.org/2005/xqt-errors}XTSE0010.</summary>
    public QName Code { get; }

    /// <summary>Human-readable description. Starts with a capital; does not end with a period.</summary>
    public string Message { get; }

    /// <summary>Source location where the diagnostic was raised.</summary>
    public SourceSpan Location { get; }

    /// <summary>Formats as "style.xsl(12,5): error XTSE0010: message text".</summary>
    public override string ToString()
    {
        string sev = Severity == DiagnosticSeverity.Error ? "error" : "warning";
        return Location.ToString() + ": " + sev + " " + Code.LocalName + ": " + Message;
    }
}

/// <summary>Accumulates diagnostics during compilation. Not thread-safe.</summary>
internal sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();
    private bool _hasErrors;

    /// <summary>Adds a pre-built diagnostic.</summary>
    public void Add(Diagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
        if (diagnostic.Severity == DiagnosticSeverity.Error)
        {
            _hasErrors = true;
        }
    }

    /// <summary>Adds an error diagnostic.</summary>
    public void Error(QName code, string message, SourceSpan location)
    {
        Add(new Diagnostic(DiagnosticSeverity.Error, code, message, location));
    }

    /// <summary>Adds a warning diagnostic.</summary>
    public void Warning(QName code, string message, SourceSpan location)
    {
        Add(new Diagnostic(DiagnosticSeverity.Warning, code, message, location));
    }

    /// <summary>True if any error-severity diagnostic has been added.</summary>
    public bool HasErrors => _hasErrors;

    /// <summary>Returns a snapshot of all diagnostics in insertion order.</summary>
    public IReadOnlyList<Diagnostic> ToList() => _diagnostics.ToArray();
}
