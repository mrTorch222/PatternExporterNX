namespace FlatPatternExporter.Features.Frame.Services;

public static class FrameLengthConverter
{
    private const double MillimetersPerInventorDatabaseCentimeter = 10.0;

    public static double FromInventorDatabaseCentimeters(double value) =>
        value * MillimetersPerInventorDatabaseCentimeter;
}
