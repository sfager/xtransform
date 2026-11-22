namespace XTransform.Xdm;

/// <summary>
/// A location in a stylesheet module or XPath expression. Offsets are within the
/// expression text (0-based, UTF-16 units); Line/Column locate the containing element or
/// attribute in the module (1-based, 0 when unknown).
/// </summary>
public readonly struct SourceSpan
{
    /// <summary>
    /// Initializes a source span with a module URI, line/column, and optional character range.
    /// </summary>
    public SourceSpan(string? moduleUri, int line, int column, int start = 0, int length = 0)
    {
        ModuleUri = moduleUri;
        Line = line;
        Column = column;
        Start = start;
        Length = length;
    }

    /// <summary>The URI of the stylesheet module, or null when unavailable.</summary>
    public string? ModuleUri { get; }

    /// <summary>1-based line number; 0 when unknown.</summary>
    public int Line { get; }

    /// <summary>1-based column number; 0 when unknown.</summary>
    public int Column { get; }

    /// <summary>0-based start offset within the expression text (UTF-16 units).</summary>
    public int Start { get; }

    /// <summary>Length in UTF-16 units within the expression text.</summary>
    public int Length { get; }

    /// <summary>Sentinel for "no location available".</summary>
#pragma warning disable CA1805 // explicit default communicates intent for a "no-location" sentinel
    public static SourceSpan None { get; } = default;
#pragma warning restore CA1805

    /// <summary>Returns a copy with the given character-range fields replaced.</summary>
    public SourceSpan WithRange(int start, int length) =>
        new SourceSpan(ModuleUri, Line, Column, start, length);

    /// <summary>"style.xsl(12,5)" when location is known, "(unknown)" otherwise.</summary>
    public override string ToString()
    {
        if (ModuleUri is null || Line == 0)
        {
            return "(unknown)";
        }

        string name = ExtractFileName(ModuleUri);
        return name + "(" + Line + "," + Column + ")";
    }

    private static string ExtractFileName(string uri)
    {
        int slash = uri.LastIndexOf('/');
        return slash >= 0 ? uri.Substring(slash + 1) : uri;
    }
}
