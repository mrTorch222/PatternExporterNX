using FlatPatternExporter.Enums;

namespace FlatPatternExporter.Services;

public static class ThemeService
{
    private static readonly IReadOnlyDictionary<AppTheme, string> ThemeFiles = new Dictionary<AppTheme, string>
    {
        [AppTheme.Light] = "ColorResources.xaml",
        [AppTheme.Dark] = "DarkTheme.xaml",
        [AppTheme.Ocean] = "OceanTheme.xaml",
        [AppTheme.Forest] = "ForestTheme.xaml",
        [AppTheme.Graphite] = "GraphiteTheme.xaml"
    };

    public static void Apply(System.Windows.Application application, AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);
        var fileName = ThemeFiles.GetValueOrDefault(theme, ThemeFiles[AppTheme.Dark]);
        var dictionaries = application.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            ThemeFiles.Values.Any(candidate => dictionary.Source?.OriginalString.Contains(candidate) == true));
        if (existing is null) return;

        var index = dictionaries.IndexOf(existing);
        dictionaries.RemoveAt(index);
        dictionaries.Insert(index, new System.Windows.ResourceDictionary
        {
            Source = new Uri($"Styles/{fileName}", UriKind.Relative)
        });
    }
}
