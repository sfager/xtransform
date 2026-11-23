// XPath 2.0 §2.1.1 — the dynamic context includes the current dateTime and implicit timezone;
// RunState carries these stable, run-wide values.

using System;
using System.Threading;
using XTransform.Xdm;

namespace XTransform.XPath;

/// <summary>
/// Run-wide mutable state shared by every child <see cref="DynamicContext"/>.
/// One instance per transformation; thread-affine (not thread-safe).
/// </summary>
internal sealed class RunState
{
    private readonly XsdDateTime _currentDateTime;
    private readonly int _implicitTimezoneMinutes;

    /// <summary>
    /// Initializes a new <see cref="RunState"/> with the given cancellation token, current dateTime,
    /// and implicit timezone.
    /// </summary>
    /// <param name="cancellation">Token that signals the run should be cancelled.</param>
    /// <param name="currentDateTime">
    /// Stable value for fn:current-dateTime(). When <see langword="null"/>, the current UTC time is captured.
    /// </param>
    /// <param name="implicitTimezoneMinutes">
    /// Offset in minutes for fn:implicit-timezone(). Zero means UTC.
    /// </param>
    // CA1068: CancellationToken is first because the other parameters are optional — placing
    // an optional CancellationToken last would require callers to name it explicitly.
#pragma warning disable CA1068 // CancellationToken parameters must come last — optional parameters make last-position worse here
    public RunState(
        CancellationToken cancellation = default,
        XsdDateTime? currentDateTime = null,
        int implicitTimezoneMinutes = 0)
#pragma warning restore CA1068
    {
        Cancellation = cancellation;
        _currentDateTime = currentDateTime ?? CaptureNow();
        _implicitTimezoneMinutes = implicitTimezoneMinutes;
    }

    /// <summary>Cancellation token for this run.</summary>
    public CancellationToken Cancellation { get; }

    /// <summary>fn:current-dateTime value — stable for the whole run.</summary>
    public XsdDateTime CurrentDateTime => _currentDateTime;

    /// <summary>fn:implicit-timezone offset in minutes.</summary>
    public int ImplicitTimezoneMinutes => _implicitTimezoneMinutes;

    /// <summary>
    /// Host-opaque tag used by higher layers (XSLT) to attach their own run-wide state to a
    /// shared <see cref="RunState"/>. XPath itself never inspects this value.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Captures UTC now as an <see cref="XsdDateTime"/> with UTC timezone (offset = 0).
    /// Never uses <see cref="System.DateTime"/> for storage; raw components are stored instead.
    /// </summary>
    private static XsdDateTime CaptureNow()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        XsdDecimal second = new XsdDecimal(now.Second + now.Millisecond / 1000m);
        return new XsdDateTime(
            XsdDateTimeKind.DateTime,
            now.Year, now.Month, now.Day,
            now.Hour, now.Minute, second,
            timezoneMinutes: 0);
    }
}
