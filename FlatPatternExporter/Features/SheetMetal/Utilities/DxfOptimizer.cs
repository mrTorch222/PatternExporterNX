using netDxf;
using netDxf.Entities;
using FlatPatternExporter.Enums;

namespace FlatPatternExporter.Utilities;

public static class DxfOptimizer
{
    [Obsolete("Use DxfPostProcessor to load and save a DXF once and propagate failures.")]
    public static void OptimizeDxfFile(string dxfFilePath, AcadVersionType version)
    {
        DxfPostProcessor.Process(dxfFilePath, new DxfPostProcessOptions
        {
            OptimizeVersion = true,
            TargetVersion = version
        });
    }
}
