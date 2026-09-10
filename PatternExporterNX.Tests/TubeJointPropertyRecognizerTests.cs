using FlatPatternExporter.Features.Frame.Services;

namespace PatternExporterNX.Tests;

public sealed class TubeJointPropertyRecognizerTests
{
    [Theory]
    [InlineData("Rectangular", "820,12", 820.12)]
    [InlineData("Round", "820.12", 820.12)]
    public void RecognizesValidTubeJointPropertiesWithLocalizedDecimalSeparator(string profileType, string length, double expectedLength)
    {
        var properties = CreateValidProperties();
        properties[TubeJointPropertyRecognizer.ProfileTypeProperty] = profileType;
        properties[TubeJointPropertyRecognizer.LengthProperty] = length;

        var result = TubeJointPropertyRecognizer.Recognize(name => properties.GetValueOrDefault(name, ""));

        Assert.True(result.IsCandidate);
        Assert.True(result.IsRecognized);
        Assert.NotNull(result.Metadata);
        Assert.Equal(expectedLength, result.Metadata.LengthMm, 6);
        Assert.Equal(profileType, result.Metadata.ProfileType);
        Assert.True(result.Metadata.Normalized);
        Assert.Equal(1, result.Metadata.RecognitionVersion);
    }

    [Fact]
    public void PartWithoutTubeJointMarkerIsNotCandidate()
    {
        var result = TubeJointPropertyRecognizer.Recognize(_ => "");

        Assert.False(result.IsCandidate);
        Assert.False(result.IsRecognized);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("ProfileType", "Hexagonal")]
    [InlineData("LengthMm", "0")]
    [InlineData("WallThicknessMm", "not-a-number")]
    [InlineData("RecognitionVersion", "0")]
    [InlineData("RecognitionVersion", "2")]
    [InlineData("Normalized", "yes")]
    public void RejectsCandidateWithInvalidRequiredProperty(string propertySuffix, string value)
    {
        var properties = CreateValidProperties();
        properties[TubeJointPropertyRecognizer.Prefix + propertySuffix] = value;

        var result = TubeJointPropertyRecognizer.Recognize(name => properties.GetValueOrDefault(name, ""));

        Assert.True(result.IsCandidate);
        Assert.False(result.IsRecognized);
        Assert.NotNull(result.Error);
    }

    private static Dictionary<string, string> CreateValidProperties() => new(StringComparer.OrdinalIgnoreCase)
    {
        [TubeJointPropertyRecognizer.ProfileTypeProperty] = "Rectangular",
        [TubeJointPropertyRecognizer.ProfileProperty] = "Труба профильная 20×20×1",
        [TubeJointPropertyRecognizer.LengthProperty] = "820,12",
        [TubeJointPropertyRecognizer.WidthProperty] = "20",
        [TubeJointPropertyRecognizer.HeightProperty] = "20",
        [TubeJointPropertyRecognizer.WallThicknessProperty] = "1",
        [TubeJointPropertyRecognizer.NormalizedProperty] = "True",
        [TubeJointPropertyRecognizer.RecognitionVersionProperty] = "1",
        [TubeJointPropertyRecognizer.OriginalFileNameProperty] = "DIN EN 10305-5 20 x 20 x 1 - 820"
    };
}
