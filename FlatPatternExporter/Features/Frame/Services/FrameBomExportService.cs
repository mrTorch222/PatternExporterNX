using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using FlatPatternExporter.Features.Frame.Models;
using FlatPatternExporter.Services;

namespace FlatPatternExporter.Features.Frame.Services;

public static class FrameBomExportService
{
    public static void ExportExcel(
        string filePath,
        IEnumerable<FrameMemberData> source,
        IReadOnlyList<FrameBomColumn>? selectedColumns = null)
    {
        var columns = selectedColumns ?? GetDefaultColumns();
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(LocalizationManager.Instance.GetString("Frame_BomSheetName"));
        for (var column = 0; column < columns.Count; column++)
            worksheet.Cell(1, column + 1).Value = columns[column].Header;

        var row = 2;
        foreach (var member in source)
        {
            for (var column = 0; column < columns.Count; column++)
            {
                var cell = worksheet.Cell(row, column + 1);
                switch (columns[column].GetValue(member))
                {
                    case double number: cell.Value = number; break;
                    case int number: cell.Value = number; break;
                    case null: break;
                    case object value: cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; break;
                }
            }
            row++;
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    public static void ExportCsv(
        string filePath,
        IEnumerable<FrameMemberData> source,
        string delimiter = ";",
        IReadOnlyList<FrameBomColumn>? selectedColumns = null)
    {
        var columns = selectedColumns ?? GetDefaultColumns();
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(delimiter, columns.Select(column => Escape(column.Header, delimiter))));
        foreach (var member in source)
        {
            var values = columns.Select(column => Convert.ToString(column.GetValue(member), CultureInfo.InvariantCulture) ?? "");
            writer.WriteLine(string.Join(delimiter, values.Select(value => Escape(value, delimiter))));
        }
    }

    private static string Escape(string value, string delimiter)
    {
        if (!value.Contains(delimiter, StringComparison.Ordinal) &&
            !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n')) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static IReadOnlyList<FrameBomColumn> GetDefaultColumns()
    {
        var localization = LocalizationManager.Instance;
        return
        [
            new(localization.GetString("Property_PartNumber_ColumnHeader"), member => member.PartNumber),
            new(localization.GetString("Property_StockNumber_ColumnHeader"), member => member.StockNumber),
            new(localization.GetString("Property_Material_ColumnHeader"), member => member.Material),
            new(localization.GetString("Property_Description_ColumnHeader"), member => member.Description),
            new(localization.GetString("Frame_ColumnLength"), member => member.LengthMm),
            new(localization.GetString("Property_Quantity_ColumnHeader"), member => member.Quantity),
            new(localization.GetString("Frame_ColumnSourceFile"), member => member.FullFileName),
            new(localization.GetString("Frame_ColumnOutputFile"), member => member.OutputFile)
        ];
    }
}

public sealed record FrameBomColumn(string Header, Func<FrameMemberData, object?> GetValue);
