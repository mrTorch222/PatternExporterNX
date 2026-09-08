using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using FlatPatternExporter.Models;

namespace FlatPatternExporter.Services;

public static class FrameBomExportService
{
    public static void ExportExcel(string filePath, IEnumerable<FrameMemberData> source)
    {
        var headers = GetHeaders();
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(LocalizationManager.Instance.GetString("Frame_BomSheetName"));
        for (var column = 0; column < headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = headers[column];

        var row = 2;
        foreach (var member in source)
        {
            worksheet.Cell(row, 1).Value = member.PartNumber;
            worksheet.Cell(row, 2).Value = member.StockNumber;
            worksheet.Cell(row, 3).Value = member.Material;
            worksheet.Cell(row, 4).Value = member.Description;
            if (member.LengthMm is double length) worksheet.Cell(row, 5).Value = length;
            worksheet.Cell(row, 6).Value = member.Quantity;
            worksheet.Cell(row, 7).Value = member.FullFileName;
            worksheet.Cell(row, 8).Value = member.OutputFile;
            row++;
        }

        worksheet.Column(5).Style.NumberFormat.Format = "0.##";
        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    public static void ExportCsv(string filePath, IEnumerable<FrameMemberData> source, string delimiter = ";")
    {
        var headers = GetHeaders();
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(delimiter, headers.Select(value => Escape(value, delimiter))));
        foreach (var member in source)
        {
            var values = new[]
            {
                member.PartNumber,
                member.StockNumber,
                member.Material,
                member.Description,
                member.LengthMm?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                member.Quantity.ToString(CultureInfo.InvariantCulture),
                member.FullFileName,
                member.OutputFile
            };
            writer.WriteLine(string.Join(delimiter, values.Select(value => Escape(value, delimiter))));
        }
    }

    private static string Escape(string value, string delimiter)
    {
        if (!value.Contains(delimiter, StringComparison.Ordinal) &&
            !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n')) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string[] GetHeaders()
    {
        var localization = LocalizationManager.Instance;
        return
        [
            localization.GetString("Property_PartNumber_ColumnHeader"),
            localization.GetString("Property_StockNumber_ColumnHeader"),
            localization.GetString("Property_Material_ColumnHeader"),
            localization.GetString("Property_Description_ColumnHeader"),
            localization.GetString("Frame_ColumnLength"),
            localization.GetString("Property_Quantity_ColumnHeader"),
            localization.GetString("Frame_ColumnSourceFile"),
            localization.GetString("Frame_ColumnOutputFile")
        ];
    }
}
