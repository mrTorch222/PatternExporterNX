namespace FlatPatternExporter.Features;

public enum ProductEdition
{
    Combined,
    SheetMetal,
    Frame
}

public sealed record ProductFeatureProfile(ProductEdition Edition, bool SheetMetalEnabled, bool FrameEnabled)
{
    public static ProductFeatureProfile Current { get; } = CreateCurrent();

    public static ProductFeatureProfile For(ProductEdition edition) => edition switch
    {
        ProductEdition.Combined => new ProductFeatureProfile(edition, SheetMetalEnabled: true, FrameEnabled: true),
        ProductEdition.SheetMetal => new ProductFeatureProfile(edition, SheetMetalEnabled: true, FrameEnabled: false),
        ProductEdition.Frame => new ProductFeatureProfile(edition, SheetMetalEnabled: false, FrameEnabled: true),
        _ => throw new ArgumentOutOfRangeException(nameof(edition), edition, null)
    };

    private static ProductFeatureProfile CreateCurrent()
    {
#if PATTERN_EXPORTER_SHEET_METAL
        return For(ProductEdition.SheetMetal);
#elif PATTERN_EXPORTER_FRAME
        return For(ProductEdition.Frame);
#else
        return For(ProductEdition.Combined);
#endif
    }
}
