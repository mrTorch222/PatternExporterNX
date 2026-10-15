using ClosedXML.Excel;
using FlatPatternExporter.Features.Acceptance.Models;
using FlatPatternExporter.Features.Acceptance.Services;

namespace PatternExporterNX.Tests;

public sealed class AcceptanceSheetExcelExporterTests
{
    [Fact]
    public void Export_CreatesTwoColumnCardsAndPreservesQuantities()
    {
        var fileName = Path.Combine(Path.GetTempPath(), $"acceptance-{Guid.NewGuid():N}.xlsx");
        try
        {
            AcceptanceSheetExcelExporter.Export(fileName,
            [
                CreateItem("P-001", "Tube", 6),
                CreateItem("P-002", "Bushing", 12),
                CreateItem("P-003", "Bracket", 4)
            ]);

            using var workbook = new XLWorkbook(fileName);
            var sheet = workbook.Worksheet("Acceptance sheet");
            Assert.Equal("P-001", sheet.Cell("D1").GetString());
            Assert.Equal(6, sheet.Cell("D7").GetValue<int>());
            Assert.Equal("P-002", sheet.Cell("J1").GetString());
            Assert.Equal(12, sheet.Cell("J7").GetValue<int>());
            Assert.Equal("P-003", sheet.Cell("D11").GetString());
            Assert.Equal(4, sheet.Cell("D17").GetValue<int>());
            Assert.Contains(sheet.MergedRanges, range => range.RangeAddress.ToString() == "A1:C9");
            Assert.Contains(sheet.MergedRanges, range => range.RangeAddress.ToString() == "G1:I9");
            Assert.Equal(XLPageOrientation.Portrait, sheet.PageSetup.PageOrientation);
            Assert.Equal(1, sheet.PageSetup.PagesWide);
        }
        finally
        {
            if (File.Exists(fileName)) File.Delete(fileName);
        }
    }

    private static AcceptanceSheetItem CreateItem(string partNumber, string description, int quantity) => new()
    {
        PartNumber = partNumber,
        Description = description,
        Material = "Steel",
        Quantity = quantity
    };
}
