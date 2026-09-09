using System.Text;
using ClosedXML.Excel;
using FlatPatternExporter.Features.Frame.Models;
using FlatPatternExporter.Features.Frame.Services;
using FlatPatternExporter.Services;

namespace PatternExporterNX.Tests;

public sealed class FrameBomExportServiceTests
{
    private static readonly FrameMemberData Member = new()
    {
        PartNumber = "PN;42",
        StockNumber = "RHS 40x20",
        Material = "Steel",
        Description = "Rail \"A\"",
        LengthMm = 1234.5,
        Quantity = 4,
        FullFileName = @"C:\Models\Rail.ipt",
        OutputFile = @"C:\IGES\Rail.igs"
    };

    [Fact]
    public void CsvPreservesNumericValuesAndEscapesFields()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"frame-bom-{Guid.NewGuid():N}.csv");
        try
        {
            FrameBomExportService.ExportCsv(filePath, [Member]);
            var bytes = File.ReadAllBytes(filePath);
            var text = Encoding.UTF8.GetString(bytes);

            Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
            Assert.Contains("\"PN;42\"", text);
            Assert.Contains("1234.5;4", text);
            Assert.Contains("\"Rail \"\"A\"\"\"", text);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void CsvUsesSelectedDelimiter()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"frame-bom-{Guid.NewGuid():N}.csv");
        try
        {
            FrameBomExportService.ExportCsv(filePath, [Member], "\t");
            var text = File.ReadAllText(filePath, Encoding.UTF8);

            Assert.Contains("PN;42\tRHS 40x20", text);
            Assert.DoesNotContain("\"PN;42\"", text);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void ExcelWritesLengthAndQuantityAsNumbers()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"frame-bom-{Guid.NewGuid():N}.xlsx");
        try
        {
            FrameBomExportService.ExportExcel(filePath, [Member]);
            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheet(LocalizationManager.Instance.GetString("Frame_BomSheetName"));

            Assert.Equal(1234.5, sheet.Cell(2, 5).GetDouble(), 6);
            Assert.Equal(4, sheet.Cell(2, 6).GetDouble());
            Assert.Equal(Member.OutputFile, sheet.Cell(2, 8).GetString());
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void CsvExportsOnlySelectedColumnsInTheirDisplayOrder()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"frame-bom-{Guid.NewGuid():N}.csv");
        try
        {
            FrameBomExportService.ExportCsv(
                filePath,
                [Member],
                ";",
                [
                    new FrameBomColumn("Material", member => member.Material),
                    new FrameBomColumn("Part", member => member.PartNumber)
                ]);
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);

            Assert.Equal("Material;Part", lines[0]);
            Assert.Equal("Steel;\"PN;42\"", lines[1]);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }
}
