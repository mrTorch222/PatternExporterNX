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
        var left = 1 + index % settings.CardsPerRow * 6;
        var top = 1 + index / settings.CardsPerRow * 10;
        var range = sheet.Range(top, left, top + 8, left + 5);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        sheet.Range(top, left, top + 8, left + 2).Merge();
        sheet.Range(top, left + 3, top, left + 5).Merge().Value = item.PartNumber;
        sheet.Range(top + 1, left + 3, top + 2, left + 5).Merge().Value = item.Description;
        sheet.Range(top + 3, left + 3, top + 3, left + 5).Merge().Value = settings.ShowMaterial ? item.Material : "";
        sheet.Range(top + 4, left + 3, top + 4, left + 5).Merge().Value = settings.ShowDetails ? item.Details : "";
        sheet.Range(top + 5, left + 3, top + 5, left + 4).Merge().Value = LocalizationManager.Instance.GetString("Acceptance_Set");
        sheet.Cell(top + 5, left + 5).Value = LocalizationManager.Instance.GetString("Acceptance_Available");
        sheet.Range(top + 6, left + 3, top + 8, left + 4).Merge().Value = item.Quantity;
        sheet.Range(top + 6, left + 3, top + 8, left + 4).Style.Font.FontSize = 30;
        sheet.Range(top + 6, left + 3, top + 8, left + 4).Style.Font.Bold = true;
        sheet.Range(top, left + 3, top, left + 5).Style.Font.Bold = true;
        for (var row = top; row <= top + 8; row++) sheet.Row(row).Height = row == top ? 18 : 16;

        if (item.Preview is null) return;
        using var imageStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(item.Preview));
        encoder.Save(imageStream);
        imageStream.Position = 0;
        var picture = sheet.AddPicture(imageStream, $"preview_{index}");
        picture.MoveTo(sheet.Cell(top, left), 4, 4).WithSize(185, 135);
    }
}
