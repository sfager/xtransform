using System;
using System.Collections.Generic;

// Binary serialization is not a requirement for this engine; exceptions do not cross
// app-domain boundaries in the supported scenarios.
#pragma warning disable CA2237 // Mark ISerializable types with SerializableAttribute
#pragma warning disable CA2229 // Implement serialization constructors

namespace XTransform.Xdm;

/// <summary>Base for all XTransform exceptions. Carries a W3C error QName and source location.</summary>
public abstract class XTransformException : Exception
{
    /// <summary>Initializes a new exception with a W3C error code, message, and source location.</summary>
    protected XTransformException(QName errorCode, string message, SourceSpan location,
                                   Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Location = location;
    }

    /// <summary>The W3C error QName, e.g. {http://www.w3.org/2005/xqt-errors}XPTY0004.</summary>
    public QName ErrorCode { get; }

    /// <summary>Source location where the error was detected; <see cref="SourceSpan.None"/> if unknown.</summary>
    public SourceSpan Location { get; }
}

/// <summary>Static (compile-time) error in a single XPath expression.</summary>
public sealed class XPathStaticException : XTransformException
{
    /// <summary>Initializes a static XPath exception.</summary>
    public XPathStaticException(QName errorCode, string message, SourceSpan location)
        : base(errorCode, message, location) { }
}

/// <summary>Dynamic error raised while evaluating XPath, including fn:error.</summary>
public class XPathDynamicException : XTransformException
{
    /// <summary>Initializes a dynamic XPath exception.</summary>
    public XPathDynamicException(QName errorCode, string message, SourceSpan location)
        : base(errorCode, message, location) { }

    /// <summary>The error object passed to fn:error, if any.</summary>
    public XdmSequence? ErrorObject { get; init; }
}

/// <summary>Dynamic error with an XSLT-specific code; carries the template call stack.</summary>
public sealed class XsltDynamicException : XPathDynamicException
{
    /// <summary>Initializes a dynamic XSLT exception with an empty template stack.</summary>
    public XsltDynamicException(QName errorCode, string message, SourceSpan location)
        : base(errorCode, message, location)
    {
        TemplateStack = Array.Empty<SourceSpan>();
    }

    /// <summary>XSLT template call stack at the point of failure, innermost last.</summary>
    public IReadOnlyList<SourceSpan> TemplateStack { get; init; }
}

/// <summary>Thrown once by XsltCompiler.Compile with every static error collected.</summary>
public sealed class XsltStaticException : XTransformException
{
    /// <summary>Initializes from a non-empty list of diagnostics; the first error supplies ErrorCode and Location.</summary>
    public XsltStaticException(IReadOnlyList<Diagnostic> errors)
        : base(GetFirst(errors).Code, GetFirst(errors).Message, GetFirst(errors).Location)
    {
        Errors = errors;
    }

    /// <summary>All static errors; the first one supplies <see cref="XTransformException.ErrorCode"/> and <see cref="XTransformException.Location"/>.</summary>
    public IReadOnlyList<Diagnostic> Errors { get; }

    private static Diagnostic GetFirst(IReadOnlyList<Diagnostic> errors)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (errors is null)
        {
            throw new ArgumentNullException(nameof(errors));
        }
#pragma warning restore CA1510
        if (errors.Count == 0)
        {
            throw new ArgumentException("At least one diagnostic is required.", nameof(errors));
        }
        return errors[0];
    }
}

#pragma warning restore CA2237
#pragma warning restore CA2229
