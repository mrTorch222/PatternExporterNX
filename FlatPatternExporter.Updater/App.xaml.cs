using System.Windows;

namespace FlatPatternExporter.Updater;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!UpdaterArguments.TryParse(e.Args, out var arguments, out var error))
        {
            MessageBox.Show(
                error,
                "Invalid Arguments",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var mainWindow = new MainWindow(
            arguments!.ProcessId,
            arguments.UpdateFilesPath,
            arguments.TargetExecutablePath);
        mainWindow.Show();
    }
}
