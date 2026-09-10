using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using FlatPatternExporter.Features.Frame.Models;
using FlatPatternExporter.Services;

namespace FlatPatternExporter.Features.Frame.Services;

public static partial class FrameFileNameService
{
    public const int MaximumBaseNameLength = 180;
    public const string DefaultTemplate = FrameExportSettings.DefaultFileNameTemplate;
    public const string FallbackTemplate = "{PartNumber}";
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string Resolve(string? template, FrameMemberData member)
    {
        var source = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        var resolved = TokenRegex().Replace(source, match => ResolveToken(match.Groups[1].Value, member));
        resolved = CustomTextRegex().Replace(resolved, match => match.Groups[1].Value);
        resolved = SanitizePathSegment(resolved);
        if (string.IsNullOrWhiteSpace(resolved)) resolved = "FrameMember";
        return Truncate(resolved, MaximumBaseNameLength);
    }

    public static bool ValidateTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return false;

        if (!TokenRegex().Matches(template).All(match => IsKnownToken(match.Groups[1].Value))) return false;

        var unmatched = CustomTextRegex().Replace(template, "");
        unmatched = TokenRegex().Replace(unmatched, "");
        return !unmatched.Contains('{') && !unmatched.Contains('}');
    }

    public static string MakeUnique(string baseName, ISet<string> reservedNames)
    {
        baseName = Truncate(baseName, MaximumBaseNameLength);
        var candidate = baseName;
        var suffix = 2;
        while (!reservedNames.Add(candidate))
        {
            var suffixText = $"_{suffix}";
            candidate = Truncate(baseName, MaximumBaseNameLength - suffixText.Length) + suffixText;
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
        _ => ResolveUserDefinedToken(token, member)
    };

    private static string ResolveUserDefinedToken(string token, FrameMemberData member)
    {
        var property = PropertyMetadataRegistry.UserDefinedProperties.FirstOrDefault(
            item => string.Equals(item.TokenName, token, StringComparison.OrdinalIgnoreCase));
        return property?.InventorPropertyName is { Length: > 0 } propertyName &&
               member.UserDefinedProperties.TryGetValue(propertyName, out var value)
            ? value
            : "";
    }

    private static bool IsKnownToken(string token) => token.ToUpperInvariant() is
        "PARTNUMBER" or "STOCKNUMBER" or "MATERIAL" or "DESCRIPTION" or "LENGTH" or "QTY" or "FILENAME" ||
        PropertyMetadataRegistry.UserDefinedProperties.Any(
            item => string.Equals(item.TokenName, token, StringComparison.OrdinalIgnoreCase));

    public static string SanitizePathSegment(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');

        value = WhitespaceRegex().Replace(value.Trim(), "_");
        value = RepeatedUnderscoreRegex().Replace(value, "_");
        value = value.Trim(' ', '.', '_');
        var deviceStem = value.Split('.', 2)[0];
        return ReservedDeviceNames.Contains(deviceStem) ? "_" + value : value;
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength].TrimEnd(' ', '.', '_');

    [GeneratedRegex(@"\{([^{}:]+)\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\{CUSTOM:([^}]*)\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CustomTextRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("_+")]
    private static partial Regex RepeatedUnderscoreRegex();
}
