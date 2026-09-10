using System.Windows;
using FlatPatternExporter.Features;
using FlatPatternExporter.Features.Frame.UI;
using FlatPatternExporter.Services;
using FlatPatternExporter.UI.Services;
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

        UserDialogService.Current = new WpfUserDialogService();

        // Load settings ONCE at application startup
        var settings = SettingsService.Instance.Settings;

        // Apply global settings (theme and language)
        ThemeService.Apply(this, settings.Interface.SelectedTheme);
        ApplyLanguage(settings.Interface.SelectedLanguage);

        // Create and show main window with settings
        Window mainWindow;
#if PATTERN_EXPORTER_FRAME
        mainWindow = new FrameMainWindow();
#else
        var combinedWindow = new FlatPatternExporterMainWindow(ProductFeatureProfile.Current);
        combinedWindow.ApplySettings(settings);
        mainWindow = combinedWindow;
#endif
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
