using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Tests for <see cref="AttributeValueTemplate.Parse"/> (XSLT 2.0 §5.6.1).
/// </summary>
[TestFixture]
internal sealed class AttributeValueTemplateTests
{
    private static readonly SourceSpan Loc = new SourceSpan("file:///t.xsl", 1, 1);

    [Test]
    public void Parse_PlainText_OneLiteralPart()
    {
        // Arrange + Act
        AttributeValueTemplate avt = AttributeValueTemplate.Parse("plain text", Loc);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(avt.IsLiteral, Is.True);
            Assert.That(avt.LiteralValue, Is.EqualTo("plain text"));
            Assert.That(avt.Parts.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void Parse_ExprAndLiteralsInterleaved_SplitsIntoThreeParts()
    {
        // Arrange + Act
        AttributeValueTemplate avt = AttributeValueTemplate.Parse("{$base}/{$path}", Loc);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(avt.Parts.Count, Is.EqualTo(3));
            Assert.That(avt.Parts[0], Is.InstanceOf<AvtExpression>());
            Assert.That(((AvtExpression)avt.Parts[0]).ExpressionText, Is.EqualTo("$base"));
            Assert.That(avt.Parts[1], Is.InstanceOf<AvtLiteral>());
            Assert.That(((AvtLiteral)avt.Parts[1]).Text, Is.EqualTo("/"));
            Assert.That(avt.Parts[2], Is.InstanceOf<AvtExpression>());
            Assert.That(((AvtExpression)avt.Parts[2]).ExpressionText, Is.EqualTo("$path"));
        });
    }

    [Test]
    public void Parse_DoubledBraces_BecomeLiteralBrace()
    {
        // Arrange + Act
        AttributeValueTemplate avt = AttributeValueTemplate.Parse("{{literal}}", Loc);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(avt.IsLiteral, Is.True);
            Assert.That(avt.LiteralValue, Is.EqualTo("{literal}"));
        });
    }

    [Test]
    public void Parse_UnmatchedOpenBrace_ThrowsXtse0370()
    {
        // Arrange + Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => AttributeValueTemplate.Parse("abc{def", Loc))!;
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0370));
    }

    [Test]
    public void Parse_UnmatchedCloseBrace_ThrowsXtse0370()
    {
        // Arrange + Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => AttributeValueTemplate.Parse("abc}def", Loc))!;
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0370));
    }

    [Test]
    public void Parse_CloseBraceInsideStringLiteral_IsNotTreatedAsClose()
    {
        // Arrange — the '}' is inside a quoted XPath string; the AVT ends at the real '}'.
        AttributeValueTemplate avt = AttributeValueTemplate.Parse("{string-join(('a}b'), '')}", Loc);

        // Assert
        AvtExpression expr = (AvtExpression)avt.Parts.Single();
        Assert.That(expr.ExpressionText, Is.EqualTo("string-join(('a}b'), '')"));
    }

    [Test]
    public void Parse_EmptyExpression_AllowedAsEmptyExpressionPart()
    {
        // Arrange + Act — "{}" is syntactically allowed by the AVT split; XPath parse error
        // (XPST0003) will surface later when the expression text is compiled.
        AttributeValueTemplate avt = AttributeValueTemplate.Parse("{}", Loc);

        // Assert
        Assert.That(((AvtExpression)avt.Parts.Single()).ExpressionText, Is.Empty);
    }

    [Test]
    public void Parse_LeadingAndTrailingLiterals_PreservedAroundExpression()
    {
        // Arrange + Act
        AttributeValueTemplate avt = AttributeValueTemplate.Parse("prefix-{$x}-suffix", Loc);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(((AvtLiteral)avt.Parts[0]).Text, Is.EqualTo("prefix-"));
            Assert.That(((AvtExpression)avt.Parts[1]).ExpressionText, Is.EqualTo("$x"));
            Assert.That(((AvtLiteral)avt.Parts[2]).Text, Is.EqualTo("-suffix"));
        });
    }
}
