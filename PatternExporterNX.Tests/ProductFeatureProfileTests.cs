using FlatPatternExporter.Features;

namespace PatternExporterNX.Tests;

public sealed class ProductFeatureProfileTests
{
    [Theory]
    [InlineData(ProductEdition.Combined, true, true)]
    [InlineData(ProductEdition.SheetMetal, true, false)]
    [InlineData(ProductEdition.Frame, false, true)]
    public void For_EnablesExpectedModules(ProductEdition edition, bool sheetMetal, bool frame)
    {
        var profile = ProductFeatureProfile.For(edition);

        Assert.Equal(edition, profile.Edition);
        Assert.Equal(sheetMetal, profile.SheetMetalEnabled);
        Assert.Equal(frame, profile.FrameEnabled);
    }
}
