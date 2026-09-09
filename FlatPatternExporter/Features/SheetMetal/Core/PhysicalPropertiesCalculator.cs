using System.Globalization;
using Inventor;

namespace FlatPatternExporter.Core;

public sealed record CalculatedPhysicalProperties(double MassKg, double DensityGramsPerCubicCentimeter)
{
    public string FormattedMass => MassKg.ToString("0.###", CultureInfo.CurrentCulture);
    public string FormattedDensity => DensityGramsPerCubicCentimeter.ToString("0.###", CultureInfo.CurrentCulture);
}

public static class PhysicalPropertiesCalculator
{
    public static CalculatedPhysicalProperties Calculate(PartDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Update2(false);

        var properties = document.ComponentDefinition.MassProperties;
        var originalAccuracy = properties.Accuracy;
        var originalCacheSetting = properties.CacheResultsOnCompute;
        try
        {
            properties.Accuracy = MassPropertiesAccuracyEnum.k_Low;
            properties.CacheResultsOnCompute = false;
            var massKg = properties.Mass;
            var volumeCubicCentimeters = properties.Volume;
            var density = CalculateDensityGramsPerCubicCentimeter(massKg, volumeCubicCentimeters);
            return new CalculatedPhysicalProperties(massKg, density);
        }
        finally
        {
            properties.Accuracy = originalAccuracy;
            properties.CacheResultsOnCompute = originalCacheSetting;
        }
    }

    public static double CalculateDensityGramsPerCubicCentimeter(
        double massKilograms,
        double volumeCubicCentimeters) =>
        volumeCubicCentimeters > double.Epsilon
            ? massKilograms * 1000.0 / volumeCubicCentimeters
            : 0.0;
}
