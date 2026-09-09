using FlatPatternExporter.Features.Frame.Models;
using FlatPatternExporter.Features.Frame.Services;
using FlatPatternExporter.Services;

namespace PatternExporterNX.Tests;

public sealed class FrameFileNameServiceTests
{
    private static readonly FrameMemberData Member = new()
    {
        FileName = "Tube:source.ipt",
        PartNumber = "PN 10",
        StockNumber = "RHS/40x20",
        Material = "Steel",
        Description = "Main rail",
        LengthMm = 1250.5,
        Quantity = 3
    };

    [Fact]
    public void ResolveExpandsAllIlogicTokensAndSanitizesFileName()
    {
        var result = FrameFileNameService.Resolve(
            "{PartNumber}_{StockNumber}_{Material}_{Description}_{Length}_{Qty}_{FileName}", Member);

        Assert.Equal("PN_10_RHS_40x20_Steel_Main_rail_1250.5_3_Tube_source", result);
    }

    [Fact]
    public void ResolveUsesNaWhenLengthIsUnavailable()
    {
        var member = new FrameMemberData { StockNumber = "Pipe", LengthMm = null, Quantity = 1 };

        Assert.Equal("Pipe_LNA_Q1", FrameFileNameService.Resolve(null, member));
    }

    [Fact]
    public void ResolveSupportsConstructorCustomTextTokens()
    {
        var result = FrameFileNameService.Resolve(
            "{PartNumber}{CUSTOM:_CUT_}{Length}", Member);

        Assert.Equal("PN_10_CUT_1250.5", result);
        Assert.True(FrameFileNameService.ValidateTemplate("{PartNumber}{CUSTOM:_CUT_}{Length}"));
        Assert.False(FrameFileNameService.ValidateTemplate("{UnknownToken}"));
        Assert.False(FrameFileNameService.ValidateTemplate("{PartNumber"));
    }

    [Fact]
    public void ResolveSupportsRegisteredUserDefinedProperties()
    {
        const string propertyName = "Frame Test Property";
        PropertyMetadataRegistry.AddUserDefinedProperty(propertyName);
        try
        {
            var member = new FrameMemberData
            {
                PartNumber = "PN-1",
                UserDefinedProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [propertyName] = "Laser A"
                }
            };

            Assert.Equal("PN-1_Laser_A", FrameFileNameService.Resolve(
                $"{{PartNumber}}_{{UDP_{propertyName}}}", member));
        }
        finally
        {
            PropertyMetadataRegistry.RemoveUserDefinedProperty($"UDP_{propertyName}");
        }
    }

    [Fact]
    public void MakeUniqueUsesNumberedSuffixStartingAtTwo()
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal("Tube", FrameFileNameService.MakeUnique("Tube", reserved));
        Assert.Equal("tube_2", FrameFileNameService.MakeUnique("tube", reserved));
        Assert.Equal("Tube_3", FrameFileNameService.MakeUnique("Tube", reserved));
    }
}
