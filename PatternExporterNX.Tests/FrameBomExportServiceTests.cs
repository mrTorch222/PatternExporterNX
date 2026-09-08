using System.Text;
using ClosedXML.Excel;
using FlatPatternExporter.Models;
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
}
