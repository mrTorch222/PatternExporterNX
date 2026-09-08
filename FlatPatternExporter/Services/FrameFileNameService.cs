using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using FlatPatternExporter.Models;

namespace FlatPatternExporter.Services;

public static partial class FrameFileNameService
{
    public const string DefaultTemplate = "{StockNumber}_{Material}_L{Length}_Q{Qty}";

    public static string Resolve(string? template, FrameMemberData member)
    {
        var source = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        var resolved = TokenRegex().Replace(source, match => ResolveToken(match.Groups[1].Value, member));
        resolved = Sanitize(resolved);
        return string.IsNullOrWhiteSpace(resolved) ? "FrameMember" : resolved;
    }

    public static string MakeUnique(string baseName, ISet<string> reservedNames)
    {
        var candidate = baseName;
        var suffix = 2;
        while (!reservedNames.Add(candidate))
        {
            candidate = $"{baseName}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string ResolveToken(string token, FrameMemberData member) => token.ToUpperInvariant() switch
    {
        "PARTNUMBER" => member.PartNumber,
        "STOCKNUMBER" => member.StockNumber,
        "MATERIAL" => member.Material,
        "DESCRIPTION" => member.Description,
        "LENGTH" => member.LengthMm?.ToString("0.##", CultureInfo.InvariantCulture) ?? "NA",
        "QTY" => member.Quantity.ToString(CultureInfo.InvariantCulture),
        "FILENAME" => Path.GetFileNameWithoutExtension(member.FileName),
        _ => ""
    };

    private static string Sanitize(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');

        value = WhitespaceRegex().Replace(value.Trim(), "_");
        value = RepeatedUnderscoreRegex().Replace(value, "_");
        return value.Trim(' ', '.', '_');
    }

    [GeneratedRegex(@"\{(PartNumber|StockNumber|Material|Description|Length|Qty|FileName)\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("_+")]
    private static partial Regex RepeatedUnderscoreRegex();
}
