using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.DynamicErrorCases;

/// <summary>Batch 3 of P4-24: type and conversion errors raised while an expression is evaluated.</summary>
[TestFixture]
public sealed class DynamicErrorExpressionTests
{
    // XPath 2.0 §3.5.2: a general comparison of values that cannot be compared is a type error.
    [TestCase("3 = '2'", "XPTY0004", TestName = "GeneralComparisonIntegerWithString")]
    [TestCase("'a' != 1", "XPTY0004", TestName = "GeneralInequalityStringWithInteger")]
    [TestCase("(1, '2') = ('1.0', '3.0')", "XPTY0004", TestName = "GeneralComparisonOfMixedSequences")]
    [TestCase("(1, '2') > ('1.0', '3.0')", "XPTY0004", TestName = "GeneralOrderingOfMixedSequences")]
    [TestCase("xs:date('2001-01-01') = 5", "XPTY0004", TestName = "GeneralComparisonDateWithNumber")]
    // XPath 2.0 §3.10.5: a failed treat is a dynamic error of its own.
    [TestCase("(23.5) treat as xs:integer", "XPDY0050", TestName = "TreatDecimalAsInteger")]
    [TestCase("'a' treat as xs:decimal", "XPDY0050", TestName = "TreatStringAsDecimal")]
    [TestCase("(1, 2) treat as xs:integer", "XPDY0050", TestName = "TreatTwoItemsAsOne")]
    [TestCase("(1, 'x') treat as xs:decimal*", "XPDY0050", TestName = "TreatMixedSequenceAsDecimals")]
    // F&O §15: aggregates over values that cannot be combined, and an effective boolean value that is not defined.
    [TestCase("sum(('a', 'b'))", "FORG0006", TestName = "SumOfStrings")]
    [TestCase("sum((1, 2, 'fred'))", "FORG0006", TestName = "SumOfANumberAndAString")]
    [TestCase("min((xs:dateTime('1996-12-23T13:13:00'), 23, 'brian'))", "FORG0006", TestName = "MinOfMixedTypes")]
    [TestCase("max(('a', 1))", "FORG0006", TestName = "MaxOfAStringAndANumber")]
    [TestCase("boolean((1, 2))", "FORG0006", TestName = "BooleanOfTwoNumbers")]
    [TestCase("if (xs:QName('a')) then 1 else 2", "FORG0006", TestName = "ConditionOnAQName")]
    // F&O §6.4.1: the context item of name(), local-name() and namespace-uri() must be a node.
    [TestCase("(1 to 5)[name() = 'a']", "XPTY0004", TestName = "NameOfANumber")]
    [TestCase("(1 to 3)[local-name() = 'a']", "XPTY0004", TestName = "LocalNameOfANumber")]
    [TestCase("(1 to 3)[namespace-uri() = '']", "XPTY0004", TestName = "NamespaceUriOfANumber")]
    // XML Schema Part 2 §3.2.7: the day must exist in the month.
    [TestCase("xs:dateTime('2000-02-31T12:00:00+02:30')", "FORG0001", TestName = "February31InADateTime")]
    [TestCase("xs:date('2001-02-29')", "FORG0001", TestName = "February29InANonLeapYear")]
    [TestCase("xs:date('2000-04-31')", "FORG0001", TestName = "April31")]
    [TestCase("xs:gMonthDay('--02-30')", "FORG0001", TestName = "February30InAGMonthDay")]
    [TestCase("xs:date('1900-02-29')", "FORG0001", TestName = "February29InACenturyThatIsNotALeapYear")]
    // XSLT 2.0 §16.6: names passed to the availability functions must be lexical QNames.
    [TestCase("system-property('c#')", "XTDE1390", TestName = "SystemPropertyNotAQName")]
    [TestCase("function-available('1234')", "XTDE1400", TestName = "FunctionAvailableNotAQName")]
    [TestCase("element-available('c#')", "XTDE1440", TestName = "ElementAvailableNotAQName")]
    [TestCase("type-available('c#')", "XTDE1425", TestName = "TypeAvailableNotAQName")]
    [TestCase("system-property('nope:x')", "XTDE1390", TestName = "SystemPropertyPrefixNotBound")]
    public void Evaluate_ExpressionThatFails_RaisesTheErrorCode(string expression, string code)
    {
        XTransformException ex = Assert.Catch<XTransformException>(() => Evaluate(expression))!;

        Assert.That(ex.ErrorCode.LocalName, Is.EqualTo(code));
    }

    [TestCase("string(3 = 3.0)", "true", TestName = "NumbersOfDifferentTypesCompare")]
    [TestCase("string('a' = 'a')", "true", TestName = "StringsCompare")]
    [TestCase("string(xs:untypedAtomic('3') = 3)", "true", TestName = "UntypedAtomicIsConvertedToTheOtherType")]
    [TestCase("string(() = 1)", "false", TestName = "EmptySequenceIsFalse")]
    [TestCase("string((1, 2) = (2, 3))", "true", TestName = "ExistentialComparison")]
    [TestCase("string((23) treat as xs:decimal)", "23", TestName = "TreatIntegerAsDecimal")]
    [TestCase("string(sum((1, 2.5)))", "3.5", TestName = "SumOfNumbers")]
    [TestCase("string(boolean(()))", "false", TestName = "BooleanOfTheEmptySequence")]
    [TestCase("string(xs:date('2000-02-29'))", "2000-02-29", TestName = "February29InALeapYear")]
    [TestCase("string(xs:date('2400-02-29'))", "2400-02-29", TestName = "February29InACenturyThatIsALeapYear")]
    [TestCase("string(xs:gMonthDay('--02-29'))", "--02-29", TestName = "FebruaryTwentyNinthWithNoYear")]
    [TestCase("string(xs:dateTime('2001-12-31T23:59:59'))", "2001-12-31T23:59:59", TestName = "December31")]
    [TestCase("string(name(/d))", "d", TestName = "NameOfANode")]
    [TestCase("string(system-property('xsl:version'))", "2.0", TestName = "SystemPropertyWithABoundPrefix")]
    [TestCase("string(function-available('fn:count'))", "true", TestName = "FunctionAvailableWithAPrefix")]
    public void Evaluate_ValidExpression_Succeeds(string expression, string expected)
    {
        Assert.That(Evaluate(expression), Is.EqualTo(expected));
    }

    [Test]
    public void Transform_ApplyTemplatesSelectingANumber_RaisesXtte0520()
    {
        // XSLT 2.0 §6.1: a select that returns a non-node is XTTE0520; XTTE0510 is for a context item that is not a node.
        XTransformException ex = Assert.Catch<XTransformException>(
            () => Transform("<xsl:template match='/'><xsl:apply-templates select='3'/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE0520));
    }

    [Test]
    public void Transform_ApplyTemplatesWithoutSelectOnANumber_RaisesXtte0510()
    {
        XTransformException ex = Assert.Catch<XTransformException>(
            () => Transform("<xsl:template match='/'><xsl:for-each select='1 to 2'><xsl:apply-templates/></xsl:for-each></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE0510));
    }

    private static string Evaluate(string expression) =>
        Transform($"<xsl:template match='/'><xsl:value-of select=\"{expression.Replace("\"", "'")}\"/></xsl:template>");

    private static string Transform(string body)
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:fn='http://www.w3.org/2005/xpath-functions'>" +
            "<xsl:output method='text'/>" + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d/>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
