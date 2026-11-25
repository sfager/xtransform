using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>
/// XSLT 2.0 §9.3 — a variable or parameter defined by content: a document node (temporary tree) when there is
/// no as attribute, otherwise the constructed sequence; a variable with neither select nor content is "".
/// </summary>
[TestFixture]
internal sealed class XsltVariableBodyTests
{
    [Test]
    public void LocalVariable_WithElementContent_IsADocumentNodeThatCanBeNavigated()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='/'><xsl:variable name='v'><a><b>x</b><b>y</b></a></xsl:variable>" +
            "<xsl:value-of select=\"concat(count($v), ':', $v instance of document-node(), ':', count($v//b), ':', $v/a/b[2])\"/></xsl:template>";

        // Act
        string output = Run(xsl, "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("1:true:2:y"));
    }

    [Test]
    public void LocalVariable_WithTextContent_IsOneTextNodeDocument()
    {
        // Arrange — two value-of instructions merge into one text node.
        const string xsl =
            "<xsl:template match='/'><xsl:variable name='v'><xsl:value-of select=\"'a'\"/><xsl:value-of select=\"'b'\"/></xsl:variable>" +
            "<xsl:value-of select=\"concat(count($v), ':', $v)\"/></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("1:ab"));
    }

    [Test]
    public void LocalVariable_AdjacentAtomicValues_AreSeparatedBySpaces()
    {
        const string xsl =
            "<xsl:template match='/'><xsl:variable name='v'><xsl:sequence select='(1, 2, 3)'/></xsl:variable>" +
            "<xsl:value-of select='$v'/></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("1 2 3"));
    }

    [Test]
    public void LocalVariable_WithAsAttribute_IsTheConstructedSequence()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='/'><xsl:variable name='v' as='item()*'><xsl:sequence select='1 to 3'/></xsl:variable>" +
            "<xsl:value-of select=\"concat(count($v), ':', sum($v))\"/></xsl:template>";

        // Act / Assert
        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("3:6"));
    }

    [Test]
    public void LocalVariable_WithNeitherSelectNorContent_IsTheEmptyString()
    {
        const string xsl =
            "<xsl:template match='/'><xsl:variable name='v'/><xsl:value-of select=\"concat(count($v), ':', string-length($v), ':', $v instance of xs:string)\" xmlns:xs='http://www.w3.org/2001/XMLSchema'/></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("1:0:true"));
    }

    [Test]
    public void LocalVariable_Content_SeesTheContextItem()
    {
        const string xsl =
            "<xsl:template match='doc'><xsl:variable name='v'><xsl:value-of select='name()'/></xsl:variable><xsl:value-of select='$v'/></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("doc"));
    }

    [Test]
    public void GlobalVariable_WithContent_IsADocumentNode()
    {
        // Arrange
        const string xsl =
            "<xsl:variable name='g'><r><x/><x/></r></xsl:variable>" +
            "<xsl:template match='/'><xsl:value-of select=\"concat(count($g//x), ':', $g instance of document-node())\"/></xsl:template>";

        // Act / Assert
        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("2:true"));
    }

    [Test]
    public void GlobalVariable_ContentReferencingAnEarlierGlobal_SeesItsValue()
    {
        const string xsl =
            "<xsl:variable name='k' select=\"'v'\"/>" +
            "<xsl:variable name='g'><xsl:value-of select='$k'/>!</xsl:variable>" +
            "<xsl:template match='/'><xsl:value-of select='$g'/></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("v!"));
    }

    [Test]
    public void GlobalVariable_ContentReferencingALaterGlobal_ForcesItsEvaluation()
    {
        const string xsl =
            "<xsl:variable name='g'><xsl:value-of select='$k'/></xsl:variable>" +
            "<xsl:variable name='k' select=\"'later'\"/>" +
            "<xsl:template match='/'><xsl:value-of select='$g'/></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("later"));
    }

    [Test]
    public void GlobalParameter_DefaultGivenAsContent_IsUsedWhenNoValueIsSupplied()
    {
        const string xsl =
            "<xsl:param name='p'><xsl:text>default</xsl:text></xsl:param>" +
            "<xsl:template match='/'><xsl:value-of select='$p'/></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("default"));
    }

    [Test]
    public void GlobalVariable_WithAsAndNoContent_IsTheEmptySequence()
    {
        const string xsl =
            "<xsl:variable name='g' as='item()*'/>" +
            "<xsl:template match='/'><xsl:value-of select='count($g)'/></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("0"));
    }

    [Test]
    public void Variable_ContentUsedAsAnalyzeStringInput_IsAtomizedToItsStringValue()
    {
        // The content variable is a document node; select must still produce a single string.
        const string xsl =
            "<xsl:template match='/'><xsl:variable name='v'>a-b</xsl:variable>" +
            "<xsl:analyze-string select='$v' regex='-'><xsl:matching-substring>[<xsl:value-of select='.'/>]</xsl:matching-substring>" +
            "<xsl:non-matching-substring><xsl:value-of select='.'/></xsl:non-matching-substring></xsl:analyze-string></xsl:template>";

        Assert.That(Run(xsl, "<doc/>"), Is.EqualTo("a[-]b"));
    }

    private static string Run(string body, string input)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='text'/>" + body + "</xsl:stylesheet>"));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
