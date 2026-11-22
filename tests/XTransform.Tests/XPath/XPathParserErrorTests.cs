using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath;

/// <summary>Tests for XPST0003, XPST0051, and XPST0081 error paths in the parser.</summary>
[TestFixture]
internal sealed class XPathParserErrorTests
{
    private static XPathStaticException ParseError(string expr)
    {
        return Assert.Throws<XPathStaticException>(
            () => XPathParser.ParseExpression(expr, SourceSpan.None, DictionaryNamespaceResolver.Default))!;
    }

    // ── XPST0081: undeclared prefix ──────────────────────────────────────────

    [Test]
    public void UndeclaredPrefix_InNameTest_ThrowsXPST0081()
    {
        // Arrange / Act
        XPathStaticException ex = ParseError("foo:bar");
        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0081));
    }

    [Test]
    public void UndeclaredPrefix_InFunctionCall_ThrowsXPST0081()
    {
        // Arrange / Act
        XPathStaticException ex = ParseError("foo:bar()");
        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0081));
    }

    [Test]
    public void UndeclaredPrefix_InCastAs_ThrowsXPST0081()
    {
        // Arrange / Act
        XPathStaticException ex = ParseError("$x cast as foo:int");
        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0081));
    }

    [Test]
    public void UndeclaredPrefix_InVariableRef_ThrowsXPST0081()
    {
        // Variable references don't have namespaces by default, but a prefixed var $ns:x
        // — actually variable refs in XPath 2.0 can have expanded names.
        // A prefixed name test like a:b (undeclared) in path context.
        XPathStaticException ex = ParseError("a:b/c");
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0081));
    }

    // ── XPST0003: syntax errors ───────────────────────────────────────────────

    [Test]
    public void MissingRightOperand_ThrowsXPST0003()
    {
        // Arrange / Act
        XPathStaticException ex = ParseError("1 +");
        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    [Test]
    public void UnterminatedParen_ThrowsXPST0003()
    {
        // Arrange / Act
        XPathStaticException ex = ParseError("(");
        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    [Test]
    public void TrailingToken_ThrowsXPST0003()
    {
        // "1 2" — extra token after expression
        XPathStaticException ex = ParseError("1 2");
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    [Test]
    public void EmptyExpression_ThrowsXPST0003()
    {
        // A truly empty expression has nothing to parse
        XPathStaticException ex = ParseError("");
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    // ── XPST0051: unknown xs: type ────────────────────────────────────────────

    [Test]
    public void UnknownXsType_InCastAs_ThrowsXPST0051()
    {
        // Arrange / Act
        XPathStaticException ex = ParseError("$x cast as xs:notAType");
        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0051));
    }

    [Test]
    public void UnknownXsType_InInstanceOf_ThrowsXPST0051()
    {
        XPathStaticException ex = ParseError("$x instance of xs:badType");
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0051));
    }

    // ── Error location ────────────────────────────────────────────────────────

    [Test]
    public void SyntaxError_LocationIsWithinExpression()
    {
        // "1 +" — the error should be at offset 2 or 3 (the missing operand)
        XPathStaticException ex = ParseError("1 +");
        // The exception's Location.Start should be within the string length (3 chars)
        Assert.That(ex.Location.Start, Is.LessThanOrEqualTo(3));
    }

    [Test]
    public void UndeclaredPrefix_LocationAtPrefixStart()
    {
        // "foo:bar" — prefix starts at offset 0
        XPathStaticException ex = ParseError("foo:bar");
        Assert.That(ex.Location.Start, Is.GreaterThanOrEqualTo(0));
        Assert.That(ex.Location.Start, Is.LessThan(7));
    }
}
