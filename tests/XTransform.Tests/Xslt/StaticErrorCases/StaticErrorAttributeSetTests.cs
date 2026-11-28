using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 8 of P4-23: circular references between attribute sets (XSLT 2.0 §10.2; XTSE0720).</summary>
[TestFixture]
public sealed class StaticErrorAttributeSetTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:attribute-set name='a' use-attribute-sets='a'/>", 1, TestName = "SelfReference")]
    [TestCase("<xsl:attribute-set name='a' use-attribute-sets='b'/><xsl:attribute-set name='b' use-attribute-sets='a'/>", 2, TestName = "TwoSetCycle")]
    [TestCase("<xsl:attribute-set name='a' use-attribute-sets='b'/><xsl:attribute-set name='b' use-attribute-sets='c'/><xsl:attribute-set name='c' use-attribute-sets='a'/>", 3, TestName = "ThreeSetCycle")]
    [TestCase("<xsl:attribute-set name='x' use-attribute-sets='a'/><xsl:attribute-set name='a' use-attribute-sets='b'/><xsl:attribute-set name='b' use-attribute-sets='a'/>", 2, TestName = "CycleReachedFromAnotherSet")]
    [TestCase("<xsl:attribute-set name='a' use-attribute-sets='ok  a'/><xsl:attribute-set name='ok'/>", 1, TestName = "SelfReferenceInAList")]
    [TestCase("<xsl:attribute-set name='a'/><xsl:attribute-set name='a' use-attribute-sets='a'/>", 1, TestName = "SameNameDeclaredTwice")]
    [TestCase("<xsl:attribute-set name='p:a' use-attribute-sets='p:b'/><xsl:attribute-set name='p:b' use-attribute-sets='p:a'/>", 2, TestName = "QualifiedNames")]
    [TestCase("<xsl:attribute-set name='a'><xsl:attribute name='x'><xsl:variable name='v'><e xsl:use-attribute-sets='a'/></xsl:variable></xsl:attribute></xsl:attribute-set>", 1, TestName = "LiteralElementInTheBodyUsesTheSet")]
    [TestCase("<xsl:attribute-set name='a'><xsl:attribute name='x'><xsl:element name='e' use-attribute-sets='b'/></xsl:attribute></xsl:attribute-set><xsl:attribute-set name='b' use-attribute-sets='a'/>", 2, TestName = "BodyReferenceClosesACycle")]
    public void Compile_AttributeSetReferencesItself_RaisesXtse0720(string body, int sets)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert: every set on the cycle is reported, once.
        Assert.That(ex.Errors.Count(e => e.Code.Equals(XsltErrors.XTSE0720)), Is.EqualTo(sets));
    }

    [TestCase("<xsl:attribute-set name='a' use-attribute-sets='b'/><xsl:attribute-set name='b'/>", TestName = "Chain")]
    [TestCase("<xsl:attribute-set name='a' use-attribute-sets='b c'/><xsl:attribute-set name='b' use-attribute-sets='c'/><xsl:attribute-set name='c'/>", TestName = "Diamond")]
    [TestCase("<xsl:attribute-set name='p:a'/><xsl:attribute-set name='q:a' use-attribute-sets='p:a'/>", TestName = "SameLocalNameDifferentNamespace")]
    [TestCase("<xsl:attribute-set name='a'><xsl:attribute name='x'><xsl:element name='e' use-attribute-sets='b'/></xsl:attribute></xsl:attribute-set><xsl:attribute-set name='b'/>", TestName = "BodyUsesAnotherSet")]
    public void Compile_AttributeSetsWithoutACycle_Compiles(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}' xmlns:p='urn:p' xmlns:q='urn:q'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));
}
