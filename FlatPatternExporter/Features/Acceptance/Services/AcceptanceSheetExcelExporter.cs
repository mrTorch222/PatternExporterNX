using System.IO;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using FlatPatternExporter.Features.Acceptance.Models;
using FlatPatternExporter.Services;

namespace FlatPatternExporter.Features.Acceptance.Services;

public static class AcceptanceSheetExcelExporter
{
    public static void Export(string fileName, IReadOnlyList<AcceptanceSheetItem> items, AcceptanceSheetSettings? settings = null)
    {
        settings ??= new AcceptanceSheetSettings();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Acceptance sheet");
        sheet.PageSetup.PageOrientation = settings.Landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.PaperSize = settings.PaperSize == AcceptancePaperSize.A3 ? XLPaperSize.A3Paper : XLPaperSize.A4Paper;
        sheet.PageSetup.PagesWide = 1;
        sheet.PageSetup.PagesTall = 0;
        sheet.PageSetup.Margins.SetLeft(0.2).SetRight(0.2).SetTop(0.25).SetBottom(0.25);
        sheet.ShowGridLines = false;

        for (var column = 1; column <= settings.CardsPerRow * 6; column++)
            sheet.Column(column).Width = (column - 1) % 6 == 0 ? 2 : 10;
        for (var index = 0; index < items.Count; index++) AddCard(sheet, items[index], index, settings);
        workbook.SaveAs(fileName);
    }

    private static void AddCard(IXLWorksheet sheet, AcceptanceSheetItem item, int index, AcceptanceSheetSettings settings)
    {
        var cardHeight = settings.ImagePlacement == AcceptanceImagePlacement.Top ? 14 : 9;
        var left = 1 + index % settings.CardsPerRow * 6;
        var top = 1 + index / settings.CardsPerRow * (cardHeight + 1);
        var range = sheet.Range(top, left, top + cardHeight - 1, left + 5);
        range.Style.Border.OutsideBorder = settings.BorderWeight switch
        {
            AcceptanceBorderWeight.Thin => XLBorderStyleValues.Thin,
            AcceptanceBorderWeight.Thick => XLBorderStyleValues.Thick,
            _ => XLBorderStyleValues.Medium
        };
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var imageBottom = settings.ImagePlacement == AcceptanceImagePlacement.Top ? top + 6 : top + 8;
        var imageRight = settings.ImagePlacement == AcceptanceImagePlacement.Top ? left + 5 : left + 2;
        sheet.Range(top, left, imageBottom, imageRight).Merge();

        var textTop = settings.ImagePlacement == AcceptanceImagePlacement.Top ? top + 7 : top;
        var textLeft = settings.ImagePlacement == AcceptanceImagePlacement.Top ? left : left + 3;
        var textRight = left + 5;
        var row = textTop;
        foreach (var field in settings.Fields.Where(field => IsVisible(field, settings)).Take(4))
        {
            var value = GetFieldValue(field.Field, item);
            sheet.Range(row, textLeft, row, textRight).Merge().Value = settings.ShowFieldLabels
                ? $"{field.Label}: {value}"
                : value;
            if (field.Field == AcceptanceCardField.PartNumber)
                sheet.Range(row, textLeft, row, textRight).Style.Font.Bold = true;
            row++;
        }

        var labelRow = settings.ImagePlacement == AcceptanceImagePlacement.Top ? top + 11 : top + 5;
        var quantityTop = labelRow + 1;
        var quantityBottom = top + cardHeight - 1;
        sheet.Range(labelRow, textLeft, labelRow, textLeft + 1).Merge().Value = LocalizationManager.Instance.GetString("Acceptance_Set");
        sheet.Range(labelRow, textLeft + 2, labelRow, textRight).Merge().Value = LocalizationManager.Instance.GetString("Acceptance_Available");
        sheet.Range(quantityTop, textLeft, quantityBottom, textLeft + 1).Merge().Value = item.Quantity;
        sheet.Range(quantityTop, textLeft, quantityBottom, textLeft + 1).Style.Font.FontSize = settings.QuantityFontSize;
        sheet.Range(quantityTop, textLeft, quantityBottom, textLeft + 1).Style.Font.Bold = true;
        for (var cardRow = top; cardRow < top + cardHeight; cardRow++) sheet.Row(cardRow).Height = cardRow == top ? 18 : 16;

        if (item.Preview is null) return;
        using var imageStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(item.Preview));
        encoder.Save(imageStream);
        imageStream.Position = 0;
        var picture = sheet.AddPicture(imageStream, $"preview_{index}");
        picture.MoveTo(sheet.Cell(top, left), 4, 4).WithSize(
            settings.ImagePlacement == AcceptanceImagePlacement.Top ? 360 : 185,
            settings.ImagePlacement == AcceptanceImagePlacement.Top ? 100 : 135);
    }

    private static bool IsVisible(AcceptanceCardFieldOption field, AcceptanceSheetSettings settings) =>
        field.IsVisible &&
        (field.Field != AcceptanceCardField.Material || settings.ShowMaterial) &&
        (field.Field != AcceptanceCardField.Details || settings.ShowDetails);

    private static string GetFieldValue(AcceptanceCardField field, AcceptanceSheetItem item) => field switch
    {
        AcceptanceCardField.PartNumber => item.PartNumber,
        AcceptanceCardField.Description => item.Description,
        AcceptanceCardField.Material => item.Material,
        AcceptanceCardField.Details => item.Details,
        _ => ""
    };
}
