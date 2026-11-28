using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>P4-15: xsl:attribute-set and use-attribute-sets (XSLT 2.0 §10.2), the separator of xsl:attribute, and xsl:copy with sets.</summary>
[TestFixture]
public sealed class XsltAttributeSetTests
{
    private const string Xsl = "http://www.w3.org/1999/XSL/Transform";

    [Test]
    public void UseAttributeSets_OnALiteralResultElement_AddsTheAttributes()
    {
        string result = Run(
            "<xsl:attribute-set name='s'><xsl:attribute name='a'>1</xsl:attribute><xsl:attribute name='b'>2</xsl:attribute></xsl:attribute-set>" +
            "<xsl:template match='/'><e xsl:use-attribute-sets='s'/></xsl:template>");

        Assert.That(result, Is.EqualTo("<e a=\"1\" b=\"2\" />"));
    }

    [Test]
    public void UseAttributeSets_OnXslElement_AddsTheAttributes()
    {
        string result = Run(
            "<xsl:attribute-set name='s'><xsl:attribute name='a'>1</xsl:attribute></xsl:attribute-set>" +
            "<xsl:template match='/'><xsl:element name='e' use-attribute-sets='s'/></xsl:template>");

        Assert.That(result, Is.EqualTo("<e a=\"1\" />"));
    }

    [Test]
    public void UseAttributeSets_OnXslCopy_AddsTheAttributesToACopiedElementOnly()
    {
        string result = Run(
            "<xsl:attribute-set name='s'><xsl:attribute name='a'>1</xsl:attribute></xsl:attribute-set>" +
            "<xsl:template match='/'><r><xsl:apply-templates select='/*/@x, /*, /*/text()'/></r></xsl:template>" +
            "<xsl:template match='*'><xsl:copy use-attribute-sets='s'/></xsl:template>" +
            "<xsl:template match='@* | text()'><xsl:copy use-attribute-sets='s'/></xsl:template>",
            input: "<d x='v'>t</d>");

        Assert.That(result, Is.EqualTo("<r x=\"v\"><d a=\"1\" />t</r>"));
    }

    [Test]
    public void AttributeSet_UsingAnotherSet_AddsThatSetsAttributesFirst()
    {
        string result = Run(
            "<xsl:attribute-set name='a' use-attribute-sets='b'><xsl:attribute name='x'>own</xsl:attribute></xsl:attribute-set>" +
            "<xsl:attribute-set name='b'><xsl:attribute name='y'>used</xsl:attribute></xsl:attribute-set>" +
            "<xsl:template match='/'><e xsl:use-attribute-sets='a'/></xsl:template>");

        Assert.That(result, Is.EqualTo("<e y=\"used\" x=\"own\" />"));
    }

    [Test]
    public void UseAttributeSets_AttributesOfTheElementOverrideTheSet()
    {
        string result = Run(
            "<xsl:attribute-set name='s'><xsl:attribute name='a'>set</xsl:attribute></xsl:attribute-set>" +
            "<xsl:template match='/'><e a='literal' xsl:use-attribute-sets='s'/><f xsl:use-attribute-sets='s'><xsl:attribute name='a'>instruction</xsl:attribute></f></xsl:template>");

        Assert.That(result, Is.EqualTo("<e a=\"literal\" /><f a=\"instruction\" />"));
    }

    [Test]
    public void AttributeSet_DeclaredTwice_IsMergedAndTheLaterDeclarationWins()
    {
        string result = Run(
            "<xsl:attribute-set name='s'><xsl:attribute name='a'>first</xsl:attribute><xsl:attribute name='b'>b</xsl:attribute></xsl:attribute-set>" +
            "<xsl:attribute-set name='s'><xsl:attribute name='a'>second</xsl:attribute></xsl:attribute-set>" +
            "<xsl:template match='/'><e xsl:use-attribute-sets='s'/></xsl:template>");

        Assert.That(result, Is.EqualTo("<e a=\"second\" b=\"b\" />"));
    }

    [Test]
    public void AttributeSet_SeesGlobalVariablesButNotTheLocalOnesOfTheUser()
    {
        string result = Run(
            "<xsl:variable name='v' select=\"'global'\"/>" +
            "<xsl:attribute-set name='s'><xsl:attribute name='a' select='$v'/></xsl:attribute-set>" +
            "<xsl:template match='/'><xsl:variable name='v' select=\"'local'\"/><e xsl:use-attribute-sets='s'/></xsl:template>");

        Assert.That(result, Is.EqualTo("<e a=\"global\" />"));
    }

    [Test]
    public void AttributeSet_IsEvaluatedForEachUse()
    {
        string result = Run(
            "<xsl:attribute-set name='s'><xsl:attribute name='n' select='position()'/></xsl:attribute-set>" +
            "<xsl:template match='/'><xsl:for-each select='1 to 3'><e xsl:use-attribute-sets='s'/></xsl:for-each></xsl:template>");

        Assert.That(result, Is.EqualTo("<e n=\"1\" /><e n=\"2\" /><e n=\"3\" />"));
    }

    [TestCase("<xsl:attribute name='a' select='1, 2, 3'/>", "a=\"1 2 3\"", TestName = "SelectDefaultsToASpace")]
    [TestCase("<xsl:attribute name='a' select='1, 2, 3' separator='-'/>", "a=\"1-2-3\"", TestName = "SelectWithSeparator")]
    [TestCase("<xsl:attribute name='a'><xsl:sequence select='1, 2, 3'/></xsl:attribute>", "a=\"123\"", TestName = "ContentDefaultsToNothing")]
    [TestCase("<xsl:attribute name='a' separator='-'><xsl:sequence select='1, 2, 3'/></xsl:attribute>", "a=\"1-2-3\"", TestName = "ContentWithSeparator")]
    [TestCase("<xsl:attribute name='a' separator=''  select='1, 2, 3'/>", "a=\"123\"", TestName = "EmptySeparator")]
    [TestCase("<xsl:attribute name='a' separator='{string-join((\"-\",\"-\"), \"\")}' select='1, 2'/>", "a=\"1--2\"", TestName = "SeparatorIsAnAvt")]
    public void Attribute_Separator_JoinsTheItems(string instruction, string expected)
    {
        string result = Run("<xsl:template match='/'><e>" + instruction + "</e></xsl:template>");

        Assert.That(result, Is.EqualTo("<e " + expected + " />"));
    }

    [Test]
    public void ParameterValues_AreEvaluatedInTheCallersScope()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:call-template name='t'><xsl:with-param name='n' select='2'/><xsl:with-param name='m'><xsl:value-of select='$n'/></xsl:with-param></xsl:call-template></xsl:template>" +
            "<xsl:template name='t'><xsl:param name='n'/><xsl:param name='m'/><xsl:value-of select='$n, $m' separator='|'/></xsl:template>",
            "<xsl:param name='n' select='1'/>",
            text: true);

        Assert.That(result, Is.EqualTo("2|1"));
    }

    [Test]
    public void Copy_WithNoContextItem_RaisesXtte0945()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(
            "<xsl:function name='f:g' xmlns:f='urn:f'><xsl:copy><t/></xsl:copy></xsl:function>" +
            "<xsl:template match='/'><xsl:value-of select='f:g()' xmlns:f='urn:f'/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE0945));
    }

    private static string Run(string body, string globals = "", bool text = false, string input = "<d/>")
    {
        string xsl = "<xsl:stylesheet version='2.0' xmlns:xsl='" + Xsl + "'><xsl:output method='" + (text ? "text" : "xml") + "' omit-xml-declaration='yes'/>" + globals + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(inputStream, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
