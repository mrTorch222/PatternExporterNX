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

    [Fact]
    public void Export_AppliesThreeColumnA3LandscapeLayoutAndFieldVisibility()
    {
        var fileName = Path.Combine(Path.GetTempPath(), $"acceptance-layout-{Guid.NewGuid():N}.xlsx");
        try
        {
            var items = Enumerable.Range(1, 4)
                .Select(index => CreateItem($"P-{index:000}", $"Part {index}", index))
                .ToList();
            var settings = new AcceptanceSheetSettings
            {
                PaperSize = AcceptancePaperSize.A3,
                Landscape = true,
                CardsPerRow = 3,
                ShowMaterial = false,
                ShowDetails = false
            };

            AcceptanceSheetExcelExporter.Export(fileName, items, settings);

            using var workbook = new XLWorkbook(fileName);
            var sheet = workbook.Worksheet("Acceptance sheet");
            Assert.Equal("P-001", sheet.Cell("D1").GetString());
            Assert.Equal("P-002", sheet.Cell("J1").GetString());
            Assert.Equal("P-003", sheet.Cell("P1").GetString());
            Assert.Equal("P-004", sheet.Cell("D11").GetString());
            Assert.Equal("", sheet.Cell("D4").GetString());
            Assert.Equal("", sheet.Cell("D5").GetString());
            Assert.Equal(XLPageOrientation.Landscape, sheet.PageSetup.PageOrientation);
            Assert.Equal(XLPaperSize.A3Paper, sheet.PageSetup.PaperSize);
        }
        finally
        {
            if (File.Exists(fileName)) File.Delete(fileName);
        }
    }

    [Fact]
    public void Export_AppliesCardDesignerOrderTopImageAndTypography()
    {
        var fileName = Path.Combine(Path.GetTempPath(), $"acceptance-designer-{Guid.NewGuid():N}.xlsx");
        try
        {
            var settings = new AcceptanceSheetSettings
            {
                ImagePlacement = AcceptanceImagePlacement.Top,
                BorderWeight = AcceptanceBorderWeight.Thick,
                QuantityFontSize = 44,
                ShowFieldLabels = true
            };
            var details = settings.Fields.Single(field => field.Field == AcceptanceCardField.Details);
            settings.Fields.Move(settings.Fields.IndexOf(details), 0);

            AcceptanceSheetExcelExporter.Export(fileName, [CreateItem("P-100", "Bracket", 7)], settings);

            using var workbook = new XLWorkbook(fileName);
            var sheet = workbook.Worksheet("Acceptance sheet");
            Assert.Contains(sheet.MergedRanges, range => range.RangeAddress.ToString() == "A1:F7");
            Assert.Equal("Details: L=250 mm", sheet.Cell("A8").GetString());
            Assert.Equal("Part number: P-100", sheet.Cell("A9").GetString());
            Assert.Equal(7, sheet.Cell("A13").GetValue<int>());
            Assert.Equal(44, sheet.Cell("A13").Style.Font.FontSize);
            Assert.Equal(XLBorderStyleValues.Thick, sheet.Cell("A1").Style.Border.TopBorder);
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
        Details = "L=250 mm",
        Quantity = quantity
    };
}
