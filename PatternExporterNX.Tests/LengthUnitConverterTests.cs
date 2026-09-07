using FlatPatternExporter.Utilities;

namespace PatternExporterNX.Tests;

public sealed class LengthUnitConverterTests
{
    [Theory]
    [InlineData(DocumentLengthUnit.Millimeter, "mm")]
    [InlineData(DocumentLengthUnit.Meter, "m")]
    [InlineData(DocumentLengthUnit.Centimeter, "cm")]
    [InlineData(DocumentLengthUnit.Inch, "in")]
    [InlineData(DocumentLengthUnit.Foot, "ft")]
    [InlineData(DocumentLengthUnit.Unknown, "Unknown")]
    public void GetDisplayName_UsesUnambiguousEnglishAbbreviation(DocumentLengthUnit unit, string expected)
    {
        FlatPatternExporter.Services.LocalizationManager.Instance.SetLanguage("en-US");
        Assert.Equal(expected, LengthUnitConverter.GetDisplayName(unit));
    }

    [Theory]
    [InlineData(DocumentLengthUnit.Millimeter, 2.5, 2.5)]
    [InlineData(DocumentLengthUnit.Centimeter, 2.5, 25)]
    [InlineData(DocumentLengthUnit.Meter, 2.5, 2500)]
    [InlineData(DocumentLengthUnit.Inch, 2.5, 63.5)]
    [InlineData(DocumentLengthUnit.Foot, 2.5, 762)]
    public void ToMillimeters_UsesExplicitKnownUnit(DocumentLengthUnit unit, double value, double expected)
    {
        Assert.Equal(expected, LengthUnitConverter.ToMillimeters(value, unit));
    }

    [Fact]
    public void ToMillimeters_DoesNotAssumeUnknownIsMillimeters()
    {
        Assert.Null(LengthUnitConverter.ToMillimeters(42, DocumentLengthUnit.Unknown));
    }
}
