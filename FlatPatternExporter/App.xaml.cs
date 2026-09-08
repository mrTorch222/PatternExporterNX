using System.Windows;
using FlatPatternExporter.Features;
using FlatPatternExporter.Services;
using FlatPatternExporter.UI.Windows;

namespace FlatPatternExporter;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load settings ONCE at application startup
        var settings = SettingsService.Instance.Settings;

        // Apply global settings (theme and language)
        ThemeService.Apply(this, settings.Interface.SelectedTheme);
        ApplyLanguage(settings.Interface.SelectedLanguage);

        // Create and show main window with settings
        var mainWindow = new FlatPatternExporterMainWindow(ProductFeatureProfile.Current);
        mainWindow.ApplySettings(settings);
        mainWindow.Show();
    }

    private void ApplyLanguage(string languageCode)
    {
        try
        {
            var savedLanguage = SupportedLanguages.All
                .FirstOrDefault(lang => lang.Code == languageCode);

            if (savedLanguage != null)
            {
                LocalizationManager.Instance.CurrentCulture = savedLanguage.Culture;
            }
        }
        catch
        {
            // If language application fails, use default language
        }
    }
}
