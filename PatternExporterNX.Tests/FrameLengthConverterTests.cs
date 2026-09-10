using FlatPatternExporter.Features.Frame.Services;

namespace PatternExporterNX.Tests;

public sealed class FrameLengthConverterTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(125.05, 1250.5)]
    public void ConvertsInventorDatabaseCentimetersToMillimeters(double centimeters, double expectedMillimeters)
    {
        Assert.Equal(expectedMillimeters, FrameLengthConverter.FromInventorDatabaseCentimeters(centimeters), 9);
    }
}
