using FlatPatternExporter.Core;

namespace PatternExporterNX.Tests;

public sealed class PhysicalPropertiesCalculatorTests
{
    [Fact]
    public void CalculateDensity_ConvertsInventorDatabaseUnits()
    {
        var density = PhysicalPropertiesCalculator.CalculateDensityGramsPerCubicCentimeter(
            massKilograms: 7.85,
            volumeCubicCentimeters: 1000);

        Assert.Equal(7.85, density, 6);
    }
}
