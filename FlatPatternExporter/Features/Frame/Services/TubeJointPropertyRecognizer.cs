using System.Globalization;

namespace FlatPatternExporter.Features.Frame.Services;

public static class TubeJointPropertyRecognizer
{
    public const int SupportedRecognitionVersion = 1;
    public const string Prefix = "TubeJoint.";
    public const string ProfileTypeProperty = Prefix + "ProfileType";
    public const string ProfileProperty = Prefix + "Profile";
    public const string LengthProperty = Prefix + "LengthMm";
    public const string WidthProperty = Prefix + "WidthMm";
    public const string HeightProperty = Prefix + "HeightMm";
    public const string WallThicknessProperty = Prefix + "WallThicknessMm";
    public const string NormalizedProperty = Prefix + "Normalized";
    public const string RecognitionVersionProperty = Prefix + "RecognitionVersion";
    public const string OriginalFileNameProperty = Prefix + "OriginalFileName";

    public static TubeJointRecognitionResult Recognize(Func<string, string> readProperty)
    {
        ArgumentNullException.ThrowIfNull(readProperty);

        var profileType = readProperty(ProfileTypeProperty).Trim();
        if (string.IsNullOrEmpty(profileType)) return TubeJointRecognitionResult.NotCandidate;

        if (!profileType.Equals("Rectangular", StringComparison.OrdinalIgnoreCase) &&
            !profileType.Equals("Round", StringComparison.OrdinalIgnoreCase))
            return Invalid($"{ProfileTypeProperty}: unsupported value '{profileType}'.");

        var profile = readProperty(ProfileProperty).Trim();
        if (string.IsNullOrEmpty(profile)) return Invalid($"{ProfileProperty}: value is missing.");
        if (!TryParsePositiveDouble(readProperty(LengthProperty), out var lengthMm))
            return Invalid($"{LengthProperty}: expected a positive number.");
        if (!TryParsePositiveDouble(readProperty(WidthProperty), out var widthMm))
            return Invalid($"{WidthProperty}: expected a positive number.");
        if (!TryParsePositiveDouble(readProperty(HeightProperty), out var heightMm))
            return Invalid($"{HeightProperty}: expected a positive number.");
        if (!TryParsePositiveDouble(readProperty(WallThicknessProperty), out var wallThicknessMm))
            return Invalid($"{WallThicknessProperty}: expected a positive number.");
        if (!TryParseInteger(readProperty(RecognitionVersionProperty), out var recognitionVersion))
            return Invalid($"{RecognitionVersionProperty}: expected an integer.");
        if (recognitionVersion != SupportedRecognitionVersion)
            return Invalid($"{RecognitionVersionProperty}: unsupported version '{recognitionVersion}'.");

        var normalizedText = readProperty(NormalizedProperty).Trim();
        if (!bool.TryParse(normalizedText, out var normalized))
            return Invalid($"{NormalizedProperty}: expected True or False.");
        return new TubeJointRecognitionResult(
            IsCandidate: true,
            IsRecognized: true,
            Error: null,
            Metadata: new TubeJointMetadata(
                profileType,
                profile,
                lengthMm,
                widthMm,
                heightMm,
                wallThicknessMm,
                normalized,
                recognitionVersion,
                readProperty(OriginalFileNameProperty).Trim()));
    }

    private static TubeJointRecognitionResult Invalid(string error) =>
        new(IsCandidate: true, IsRecognized: false, error, Metadata: null);

    private static bool TryParsePositiveDouble(string value, out double result)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
        if (double.TryParse(value, styles, CultureInfo.CurrentCulture, out result) && result > 0) return true;
        if (double.TryParse(value, styles, CultureInfo.InvariantCulture, out result) && result > 0) return true;
        return double.TryParse(value.Replace(',', '.'), styles, CultureInfo.InvariantCulture, out result) && result > 0;
    }

    private static bool TryParseInteger(string value, out int result) =>
        (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out result) ||
         int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result));
}

public sealed record TubeJointRecognitionResult(
    bool IsCandidate,
    bool IsRecognized,
    string? Error,
    TubeJointMetadata? Metadata)
{
    public static TubeJointRecognitionResult NotCandidate { get; } = new(false, false, null, null);
}

public sealed record TubeJointMetadata(
    string ProfileType,
    string Profile,
    double LengthMm,
    double WidthMm,
    double HeightMm,
    double WallThicknessMm,
    bool Normalized,
    int RecognitionVersion,
    string OriginalFileName);
