using System;
using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class ErrorsTests
{
    private static readonly SourceSpan TestSpan = new SourceSpan("style.xsl", 12, 5);

    // --- XTransformException (via XPathStaticException) ---

    [Test]
    public void XPathStaticException_ErrorCode_IsInXqtErrorsNamespace()
    {
        // Arrange / Act
        var ex = new XPathStaticException(XPathErrors.XPST0003, "Syntax error", TestSpan);

        // Assert
        Assert.That(ex.ErrorCode.NamespaceUri, Is.EqualTo(XPathErrors.Namespace));
        Assert.That(ex.ErrorCode.LocalName, Is.EqualTo("XPST0003"));
    }

    [Test]
    public void XPathStaticException_Message_MatchesConstructorArg()
    {
        // Arrange / Act
        var ex = new XPathStaticException(XPathErrors.XPST0003, "Syntax error", TestSpan);

        // Assert
        Assert.That(ex.Message, Is.EqualTo("Syntax error"));
    }

    [Test]
    public void XPathStaticException_Location_MatchesConstructorArg()
    {
        // Arrange / Act
        var ex = new XPathStaticException(XPathErrors.XPST0003, "Syntax error", TestSpan);

        // Assert
        Assert.That(ex.Location.ModuleUri, Is.EqualTo("style.xsl"));
        Assert.That(ex.Location.Line, Is.EqualTo(12));
    }

    // --- XPathDynamicException ---

    [Test]
    public void XPathDynamicException_ErrorCode_MatchesCode()
    {
        // Arrange / Act
        var ex = new XPathDynamicException(XPathErrors.XPTY0004, "Type error", TestSpan);

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    [Test]
    public void XPathDynamicException_ErrorObject_DefaultsToNull()
    {
        // Arrange / Act
        var ex = new XPathDynamicException(XPathErrors.XPTY0004, "Type error", TestSpan);

        // Assert
        Assert.That(ex.ErrorObject, Is.Null);
    }

    // --- XsltDynamicException ---

    [Test]
    public void XsltDynamicException_TemplateStack_DefaultsToEmpty()
    {
        // Arrange / Act
        var ex = new XsltDynamicException(XsltErrors.XTDE0640, "Circular variable", TestSpan);

        // Assert
        Assert.That(ex.TemplateStack, Is.Empty);
    }

    [Test]
    public void XsltDynamicException_TemplateStack_CanBeSetViaInit()
    {
        // Arrange
        var stack = new List<SourceSpan> { TestSpan };

        // Act
        var ex = new XsltDynamicException(XsltErrors.XTDE0640, "Circular variable", TestSpan)
        {
            TemplateStack = stack,
        };

        // Assert
        Assert.That(ex.TemplateStack, Has.Count.EqualTo(1));
        Assert.That(ex.TemplateStack[0].Line, Is.EqualTo(12));
    }

    // --- XsltStaticException ---

    [Test]
    public void XsltStaticException_ErrorCode_IsFromFirstDiagnostic()
    {
        // Arrange
        var d1 = new Diagnostic(DiagnosticSeverity.Error, XsltErrors.XTSE0010, "First error", TestSpan);
        var d2 = new Diagnostic(DiagnosticSeverity.Error, XPathErrors.XPST0003, "Second error",
            new SourceSpan("other.xsl", 1, 1));
        var errors = new List<Diagnostic> { d1, d2 };

        // Act
        var ex = new XsltStaticException(errors);

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0010));
        Assert.That(ex.Location.Line, Is.EqualTo(12));
        Assert.That(ex.Errors, Has.Count.EqualTo(2));
    }

    [Test]
    public void XsltStaticException_NullErrors_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => _ = new XsltStaticException(null!));
    }

    [Test]
    public void XsltStaticException_EmptyErrors_ThrowsArgumentException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => _ = new XsltStaticException(new List<Diagnostic>()));
    }

    // --- Diagnostic ---

    [TestCase(DiagnosticSeverity.Error, "XTSE0010", "Bad element", "style.xsl(12,5): error XTSE0010: Bad element")]
    [TestCase(DiagnosticSeverity.Warning, "XPST0003", "Suspect syntax", "style.xsl(12,5): warning XPST0003: Suspect syntax")]
    public void Diagnostic_ToString_FormatsCorrectly(
        DiagnosticSeverity severity, string localName, string message, string expected)
    {
        // Arrange
        var code = new QName(XPathErrors.Namespace, localName);
        var d = new Diagnostic(severity, code, message, TestSpan);

        // Act / Assert
        Assert.That(d.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Diagnostic_ToString_UnknownLocation_ShowsUnknown()
    {
        // Arrange
        var d = new Diagnostic(DiagnosticSeverity.Error, XsltErrors.XTSE0010, "Bad element", SourceSpan.None);

        // Act / Assert
        Assert.That(d.ToString(), Does.StartWith("(unknown): error XTSE0010:"));
    }

    // --- DiagnosticBag ---

    [Test]
    public void DiagnosticBag_HasErrors_FalseWhenEmpty()
    {
        // Arrange / Act
        var bag = new DiagnosticBag();

        // Assert
        Assert.That(bag.HasErrors, Is.False);
    }

    [Test]
    public void DiagnosticBag_HasErrors_TrueAfterError()
    {
        // Arrange
        var bag = new DiagnosticBag();

        // Act
        bag.Error(XPathErrors.XPST0003, "Syntax error", TestSpan);

        // Assert
        Assert.That(bag.HasErrors, Is.True);
    }

    [Test]
    public void DiagnosticBag_HasErrors_FalseAfterWarningOnly()
    {
        // Arrange
        var bag = new DiagnosticBag();

        // Act
        bag.Warning(XPathErrors.XPST0003, "Suspect construct", TestSpan);

        // Assert
        Assert.That(bag.HasErrors, Is.False);
    }

    [Test]
    public void DiagnosticBag_ToList_ReturnsSnapshotInInsertionOrder()
    {
        // Arrange
        var bag = new DiagnosticBag();
        bag.Error(XPathErrors.XPST0003, "First", TestSpan);
        bag.Warning(XsltErrors.XTSE0010, "Second", TestSpan);

        // Act
        IReadOnlyList<Diagnostic> list = bag.ToList();

        // Assert
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list[0].Code, Is.EqualTo(XPathErrors.XPST0003));
        Assert.That(list[1].Code, Is.EqualTo(XsltErrors.XTSE0010));
    }

    [Test]
    public void DiagnosticBag_ToList_IsSnapshot_MutationDoesNotAffectPriorResult()
    {
        // Arrange
        var bag = new DiagnosticBag();
        bag.Error(XPathErrors.XPST0003, "First", TestSpan);
        IReadOnlyList<Diagnostic> snapshot = bag.ToList();

        // Act — add more after taking the snapshot
        bag.Error(XPathErrors.XPTY0004, "Second", TestSpan);

        // Assert — snapshot is unchanged
        Assert.That(snapshot, Has.Count.EqualTo(1));
    }

    // --- XPathErrors constants ---

    [TestCase("XPST0003")]
    [TestCase("XPST0008")]
    [TestCase("XPST0017")]
    [TestCase("XPST0051")]
    [TestCase("XPST0081")]
    [TestCase("XPTY0004")]
    [TestCase("XPDY0002")]
    [TestCase("FORG0001")]
    [TestCase("FOAR0001")]
    [TestCase("FOCA0002")]
    public void XPathErrors_AllCodes_InCorrectNamespace(string localName)
    {
        // Arrange — look up the static field by name via reflection
        var field = typeof(XPathErrors).GetField(localName);
        Assert.That(field, Is.Not.Null, $"Field {localName} not found");

        // Act
        var code = (QName)field!.GetValue(null)!;

        // Assert
        Assert.That(code.NamespaceUri, Is.EqualTo(XPathErrors.Namespace));
        Assert.That(code.LocalName, Is.EqualTo(localName));
    }

    [TestCase("XTSE0010")]
    [TestCase("XTDE0640")]
    [TestCase("XTMM9000")]
    public void XsltErrors_AllCodes_InCorrectNamespace(string localName)
    {
        // Arrange
        var field = typeof(XsltErrors).GetField(localName);
        Assert.That(field, Is.Not.Null, $"Field {localName} not found");

        // Act
        var code = (QName)field!.GetValue(null)!;

        // Assert
        Assert.That(code.NamespaceUri, Is.EqualTo(XPathErrors.Namespace));
        Assert.That(code.LocalName, Is.EqualTo(localName));
    }
}
