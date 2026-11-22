using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for <see cref="RunState"/>: stable dateTime, implicit timezone and cancellation token.
/// </summary>
[TestFixture]
internal sealed class RunStateTests
{
    // ── CurrentDateTime ───────────────────────────────────────────────────────

    [Test]
    public void CurrentDateTime_IsStableAcrossReads()
    {
        // Arrange
        RunState run = new RunState();

        // Act
        XsdDateTime first = run.CurrentDateTime;
        XsdDateTime second = run.CurrentDateTime;

        // Assert — same field value returned both times
        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void CurrentDateTime_IsCaptured_AtConstruction()
    {
        // Arrange — capture one RunState, then wait at least 50 ms, capture another
        RunState run1 = new RunState();
        Thread.Sleep(100); // ensure at least one millisecond has elapsed
        RunState run2 = new RunState();

        // Act
        XsdDateTime dt1 = run1.CurrentDateTime;
        XsdDateTime dt2 = run2.CurrentDateTime;

        // Assert — the two dateTimes must differ (dt2 is later than dt1)
        // Compare by converting the second component to a comparable form.
        // Two datetimes created at least 100ms apart must differ somewhere.
        Assert.That(dt1, Is.Not.EqualTo(dt2),
            "RunState constructed 100 ms apart must have different CurrentDateTime values");
    }

    // ── ImplicitTimezoneMinutes ───────────────────────────────────────────────

    [Test]
    public void ImplicitTimezoneMinutes_DefaultsToZero()
    {
        // Arrange / Act
        RunState run = new RunState();

        // Assert
        Assert.That(run.ImplicitTimezoneMinutes, Is.EqualTo(0));
    }

    [Test]
    public void ImplicitTimezoneMinutes_UsesProvidedValue()
    {
        // Arrange / Act
        RunState run = new RunState(implicitTimezoneMinutes: -300);

        // Assert
        Assert.That(run.ImplicitTimezoneMinutes, Is.EqualTo(-300));
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Test]
    public void Cancellation_DefaultsToNone()
    {
        // Arrange / Act
        RunState run = new RunState();

        // Assert — a default token is never cancellable
        Assert.That(run.Cancellation.CanBeCanceled, Is.False);
    }

    [Test]
    public void Cancellation_PropagatesProvidedToken()
    {
        // Arrange
        using CancellationTokenSource cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        // Act
        RunState run = new RunState(token);

        // Assert — same token is stored
        Assert.That(run.Cancellation, Is.EqualTo(token));
    }
}
