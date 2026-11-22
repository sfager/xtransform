// Tests for the F&O §17.1 cast matrix — one test case per CSV row.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
[Category("Cast")]
public sealed class CastTableMatrixTests
{
    // -------------------------------------------------------------------------
    // Load CSV rows as test cases
    // -------------------------------------------------------------------------

    private static IEnumerable<TestCaseData> CastMatrixRows()
    {
        string csvPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "CastMatrix.csv");
        string[] lines = File.ReadAllLines(csvPath);

        int rowIndex = 0;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] parts = line.Split(',');
            if (parts.Length < 3)
            {
                continue;
            }

            string sourceName = parts[0].Trim();
            string targetName = parts[1].Trim();
            string disposition = parts[2].Trim();
            string sampleValue = parts.Length > 3 ? parts[3].Trim() : string.Empty;
            string expectedOutput = parts.Length > 4 ? parts[4].Trim() : string.Empty;

            // Include row index to make each test name unique even when source/target/disposition match
            yield return new TestCaseData(sourceName, targetName, disposition, sampleValue, expectedOutput)
                .SetName($"Row{rowIndex:D3}_{sourceName}_To_{targetName}_{disposition}");
            rowIndex++;
        }
    }

    // -------------------------------------------------------------------------
    // Helper: type code lookup by enum name (CSV uses XdmTypeCode enum member names)
    // -------------------------------------------------------------------------

    private static bool TryGetTypeCodeByEnumName(string name, out XdmTypeCode typeCode)
    {
#if NET6_0_OR_GREATER
        return System.Enum.TryParse(name, false, out typeCode);
#else
        try
        {
            typeCode = (XdmTypeCode)System.Enum.Parse(typeof(XdmTypeCode), name, false);
            return true;
        }
        catch
        {
            typeCode = default;
            return false;
        }
#endif
    }

    // -------------------------------------------------------------------------
    // Matrix test: IsCastAllowed
    // -------------------------------------------------------------------------

    [TestCaseSource(nameof(CastMatrixRows))]
    public void IsCastAllowed_MatrixCell_MatchesExpectedDisposition(
        string sourceName, string targetName, string disposition, string sampleValue, string expectedOutput)
    {
        // Arrange
        bool sourceFound = TryGetTypeCodeByEnumName(sourceName, out XdmTypeCode sourceCode);
        bool targetFound = TryGetTypeCodeByEnumName(targetName, out XdmTypeCode targetCode);

        if (!sourceFound || !targetFound)
        {
            Assert.Ignore($"Unknown type name: {sourceName} or {targetName}");
            return;
        }

        bool expectedAllowed = disposition != "N";

        // Act
        bool allowed = CastTable.IsCastAllowed(sourceCode, targetCode);

        // Assert
        Assert.That(allowed, Is.EqualTo(expectedAllowed),
            $"IsCastAllowed({sourceName},{targetName}) expected {expectedAllowed} for disposition {disposition}");
    }

    // -------------------------------------------------------------------------
    // Matrix test: Cast result / error
    // -------------------------------------------------------------------------

    [TestCaseSource(nameof(CastMatrixRows))]
    public void Cast_MatrixCell_ProducesCorrectResultOrError(
        string sourceName, string targetName, string disposition, string sampleValue, string expectedOutput)
    {
        // Arrange
        bool sourceFound = TryGetTypeCodeByEnumName(sourceName, out XdmTypeCode sourceCode);
        bool targetFound = TryGetTypeCodeByEnumName(targetName, out XdmTypeCode targetCode);

        if (!sourceFound || !targetFound)
        {
            Assert.Ignore($"Unknown type name: {sourceName} or {targetName}");
            return;
        }

        switch (disposition)
        {
            case "N":
            {
                // N cell — Cast must throw XPTY0004
                // Create a minimal value with the source type to test the N cell
                XdmAtomicValue? sourceValue = CreateMinimalValue(sourceCode);
                if (sourceValue is null)
                {
                    Assert.Ignore($"Cannot create minimal value for {sourceName}");
                    return;
                }

                // Act / Assert
                XPathDynamicException? ex = Assert.Throws<XPathDynamicException>(
                    () => CastTable.Cast(sourceValue, targetCode, SourceSpan.None));
                Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
                break;
            }

            case "Y":
            case "M-success":
            {
                if (string.IsNullOrEmpty(sampleValue))
                {
                    Assert.Ignore($"No sample value provided for {sourceName} → {targetName}");
                    return;
                }

                // Parse sample as source type
                XdmAtomicValue? sourceValue = TryParseAs(sampleValue, sourceCode);
                if (sourceValue is null)
                {
                    Assert.Ignore($"Cannot parse '{sampleValue}' as {sourceName}");
                    return;
                }

                // Act
                XdmAtomicValue result = CastTable.Cast(sourceValue, targetCode, SourceSpan.None);

                // Assert
                Assert.That(result.GetStringValue(), Is.EqualTo(expectedOutput),
                    $"Cast({sourceName}('{sampleValue}'), {targetName})");
                break;
            }

            case "M-fail":
            {
                if (string.IsNullOrEmpty(sampleValue))
                {
                    Assert.Ignore($"No sample value provided for M-fail {sourceName} → {targetName}");
                    return;
                }

                // Parse sample as source type (the source parse itself should succeed)
                XdmAtomicValue? sourceValue = TryParseAs(sampleValue, sourceCode);
                if (sourceValue is null)
                {
                    Assert.Ignore($"Cannot parse '{sampleValue}' as {sourceName}");
                    return;
                }

                // Act / Assert — cast must throw FORG0001
                XPathDynamicException? ex = Assert.Throws<XPathDynamicException>(
                    () => CastTable.Cast(sourceValue, targetCode, SourceSpan.None));
                Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
                break;
            }

            default:
                Assert.Ignore($"Unknown disposition: {disposition}");
                break;
        }
    }

    // -------------------------------------------------------------------------
    // TryCast: N cells return false without throwing
    // -------------------------------------------------------------------------

    [TestCaseSource(nameof(CastMatrixRows))]
    public void TryCast_NCells_ReturnFalse(
        string sourceName, string targetName, string disposition, string sampleValue, string expectedOutput)
    {
        if (disposition != "N")
        {
            Assert.Ignore("Not an N cell");
            return;
        }

        // Arrange
        bool sourceFound = TryGetTypeCodeByEnumName(sourceName, out XdmTypeCode sourceCode);
        bool targetFound = TryGetTypeCodeByEnumName(targetName, out XdmTypeCode targetCode);

        if (!sourceFound || !targetFound)
        {
            Assert.Ignore($"Unknown type name: {sourceName} or {targetName}");
            return;
        }

        XdmAtomicValue? sourceValue = CreateMinimalValue(sourceCode);
        if (sourceValue is null)
        {
            Assert.Ignore($"Cannot create minimal value for {sourceName}");
            return;
        }

        // Act
        bool ok = CastTable.TryCast(sourceValue, targetCode, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.False);
        Assert.That(result, Is.Null);
    }

    // -------------------------------------------------------------------------
    // Helper: create a minimal value for a type (used for N-cell tests)
    // -------------------------------------------------------------------------

    private static XdmAtomicValue? CreateMinimalValue(XdmTypeCode code)
    {
        return TryParseAs(GetMinimalLexical(code), code);
    }

    private static string GetMinimalLexical(XdmTypeCode code)
    {
        switch (code)
        {
            case XdmTypeCode.UntypedAtomic:
            case XdmTypeCode.String:
            case XdmTypeCode.NormalizedString:
            case XdmTypeCode.Token:
            case XdmTypeCode.Language:
                return "en";
            case XdmTypeCode.NmToken:
                return "foo";
            case XdmTypeCode.Name:
            case XdmTypeCode.NcName:
            case XdmTypeCode.Id:
            case XdmTypeCode.IdRef:
            case XdmTypeCode.Entity:
                return "foo";
            case XdmTypeCode.Boolean:
                return "true";
            case XdmTypeCode.Integer:
            case XdmTypeCode.NonPositiveInteger:
            case XdmTypeCode.NegativeInteger:
                return "-1";
            case XdmTypeCode.Long:
            case XdmTypeCode.Int:
            case XdmTypeCode.Short:
            case XdmTypeCode.Byte:
                return "1";
            case XdmTypeCode.NonNegativeInteger:
            case XdmTypeCode.UnsignedLong:
            case XdmTypeCode.UnsignedInt:
            case XdmTypeCode.UnsignedShort:
            case XdmTypeCode.UnsignedByte:
            case XdmTypeCode.PositiveInteger:
                return "1";
            case XdmTypeCode.Decimal:
                return "1.5";
            case XdmTypeCode.Float:
            case XdmTypeCode.Double:
                return "1.5";
            case XdmTypeCode.Duration:
                return "P1Y";
            case XdmTypeCode.DayTimeDuration:
                return "P1DT1H";
            case XdmTypeCode.YearMonthDuration:
                return "P1Y2M";
            case XdmTypeCode.DateTime:
                return "2024-01-01T00:00:00";
            case XdmTypeCode.Date:
                return "2024-01-01";
            case XdmTypeCode.Time:
                return "10:00:00";
            case XdmTypeCode.GYearMonth:
                return "2024-01";
            case XdmTypeCode.GYear:
                return "2024";
            case XdmTypeCode.GMonthDay:
                return "--01-01";
            case XdmTypeCode.GDay:
                return "---01";
            case XdmTypeCode.GMonth:
                return "--01";
            case XdmTypeCode.HexBinary:
                return "CAFE";
            case XdmTypeCode.Base64Binary:
                return "AAAA";
            case XdmTypeCode.AnyUri:
                return "http://example.com";
            case XdmTypeCode.QName:
            case XdmTypeCode.Notation:
                return "foo";
            default:
                return "x";
        }
    }

    private static XdmAtomicValue? TryParseAs(string lexical, XdmTypeCode code)
    {
        if (code == XdmTypeCode.Float)
        {
            // Parse as float directly for special values
            if (XdmAtomicValue.TryParse(lexical, XdmTypeCode.Float, out XdmAtomicValue? fv))
            {
                return fv;
            }
            return null;
        }

        if (code == XdmTypeCode.Double)
        {
            if (XdmAtomicValue.TryParse(lexical, XdmTypeCode.Double, out XdmAtomicValue? dv))
            {
                return dv;
            }
            return null;
        }

        if (XdmAtomicValue.TryParse(lexical, code, out XdmAtomicValue? result))
        {
            return result;
        }

        return null;
    }
}
