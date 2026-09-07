using FlatPatternExporter.Services;

namespace FlatPatternExporter.Utilities;

public enum DocumentLengthUnit
{
    Unknown,
    Millimeter,
    Meter,
    Centimeter,
    Inch,
    Foot
}

public static class LengthUnitConverter
{
    public static string GetDisplayName(DocumentLengthUnit unit) =>
        LocalizationManager.Instance.GetString(unit switch
        {
            DocumentLengthUnit.Millimeter => "Unit_Millimeter",
            DocumentLengthUnit.Meter => "Unit_Meter",
            DocumentLengthUnit.Centimeter => "Unit_Centimeter",
            DocumentLengthUnit.Inch => "Unit_Inch",
            DocumentLengthUnit.Foot => "Unit_Foot",
            _ => "Unit_Unknown"
        });

    public static double? ToMillimeters(double value, DocumentLengthUnit unit) => unit switch
    {
        DocumentLengthUnit.Millimeter => value,
        DocumentLengthUnit.Meter => value * 1000.0,
        DocumentLengthUnit.Centimeter => value * 10.0,
        DocumentLengthUnit.Inch => value * 25.4,
        DocumentLengthUnit.Foot => value * 304.8,
        _ => null
    };
}
