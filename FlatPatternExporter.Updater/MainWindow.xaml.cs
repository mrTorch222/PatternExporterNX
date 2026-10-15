using System.Diagnostics;
using System.IO;
using System.Windows;

namespace FlatPatternExporter.Updater;

public partial class MainWindow : Window
{
    private readonly int _processId;
    private readonly string _updateFilePath;
    private readonly string _targetExecutablePath;
    private readonly string _logPath;
    private string _backupDirectory = string.Empty;

    public MainWindow(int processId, string updateFilesPath, string targetExecutablePath)
    {
        InitializeComponent();

        _processId = processId;
        _updateFilePath = updateFilesPath;
        _targetExecutablePath = targetExecutablePath;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logsDirectory = Path.Combine(appDataPath, "FlatPatternExporter", "Logs");
        Directory.CreateDirectory(logsDirectory);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        _logPath = Path.Combine(logsDirectory, $"Updater_{timestamp}.log");

        RotateLogs(logsDirectory, maxLogFiles: 10);

        Log($"=== Updater Started ===");
        Log($"Process ID: {_processId}");
        Log($"Update files path: {_updateFilePath}");
        Log($"Target executable: {_targetExecutablePath}");

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;

        Title = "PatternExporterNX - Updater";
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Log("MainWindow_Closing event fired");
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        Log("MainWindow_Closed event fired");
    }

    private void Log(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] {message}\n";
            File.AppendAllText(_logPath, logMessage);

            // Force flush to disk immediately
            using (var fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.Flush(true);
            }
        }
        catch
        {
        }
    }

    private void RotateLogs(string logsDirectory, int maxLogFiles)
    {
        try
        {
            var logFiles = Directory.GetFiles(logsDirectory, "Updater_*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (logFiles.Count > maxLogFiles)
            {
                var filesToDelete = logFiles.Skip(maxLogFiles);
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log("MainWindow_Loaded started");
        SafeUpdateDetailsText($"Log file: {_logPath}\n\nPreparing for update...");

        try
        {
            Log("Calling PerformUpdateAsync");
            await PerformUpdateAsync();
            Log("PerformUpdateAsync completed successfully");
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
            Log($"STACK TRACE: {ex.StackTrace}");

            UpdateStatus($"Update error: {ex.Message}", 0);
            SafeUpdateProgressText($"Error");
            SafeUpdateDetailsText($"Update error:\n{ex.Message}\n\nLog file:\n{_logPath}");

            MessageBox.Show(
                this,
                $"Update error:\n\n{ex.Message}\n\n{ex.StackTrace}\n\nLog file: {_logPath}",
                "Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            await Task.Delay(5000);
            Application.Current.Shutdown(1);
        }
    }

    private async Task PerformUpdateAsync()
    {
        var updateSuccessful = false;

        try
        {
            Log("Step 1: Waiting for process to exit");
            UpdateStatus("Waiting for application to exit...", 10);

            Log("About to await WaitForProcessToExitAsync");
            await WaitForProcessToExitAsync(_processId);
            Log("WaitForProcessToExitAsync returned");
            Log("Step 1 completed");

            using var transaction = new UpdateTransaction(
                _updateFilePath,
                _targetExecutablePath,
                message => Log(message));

            Log("Step 2: Creating backup");
            UpdateStatus("Creating backup...", 30);
            Log("About to create backup");
            await Task.Run(transaction.CreateBackup);
            _backupDirectory = transaction.BackupDirectory;
            Log("Backup created");
            Log("Step 2 completed");

            Log("Step 3: Replacing application files");
            UpdateStatus("Replacing application files...", 60);
            Log("About to replace files");

            try
            {
                SafeUpdateDetailsText($"Updating {transaction.FileCount} files...");
                await Task.Run(transaction.Apply);
                Log("Files replaced successfully");
                Log("Step 3 completed");
                updateSuccessful = true;
            }
            catch (Exception ex)
            {
                Log($"ERROR during file replacement: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");

                Log("Step 4: Rolling back from backup");
                UpdateStatus("Update failed. Rolling back...", 50);

                var rollbackSucceeded = false;
                try
                {
                    await Task.Run(transaction.Rollback);
                    rollbackSucceeded = true;
                    Log("Rollback completed successfully");

                    UpdateStatus("Update failed. System restored to previous state.", 0);
                    SafeUpdateProgressText("Update failed - Rolled back");
                    SafeUpdateDetailsText($"Update failed and rolled back:\n{ex.Message}\n\nYour application has been restored to the previous state.\n\nLog file:\n{_logPath}");

                    MessageBox.Show(
                        this,
                        $"Update failed and rolled back:\n\n{ex.Message}\n\nYour application has been restored to the previous state.\n\nLog file: {_logPath}",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch (Exception rollbackEx)
                {
                    transaction.PreserveBackup();
                    Log($"CRITICAL: Rollback failed: {rollbackEx.Message}");
                    Log($"Stack trace: {rollbackEx.StackTrace}");

                    UpdateStatus("CRITICAL: Update and rollback failed!", 0);
                    SafeUpdateProgressText("Critical Error");
                    SafeUpdateDetailsText($"Update failed and rollback also failed!\n\nOriginal error:\n{ex.Message}\n\nRollback error:\n{rollbackEx.Message}\n\nBackup location:\n{_backupDirectory}\n\nLog file:\n{_logPath}");

                    MessageBox.Show(
                        this,
                        $"CRITICAL ERROR:\n\nUpdate failed:\n{ex.Message}\n\nRollback also failed:\n{rollbackEx.Message}\n\nPlease manually restore from backup:\n{_backupDirectory}\n\nLog file: {_logPath}",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                if (rollbackSucceeded)
                {
                    transaction.Cleanup();
                    _backupDirectory = string.Empty;
                }

                await Task.Delay(5000);
                Application.Current.Shutdown(1);
                return;
            }

            if (updateSuccessful)
            {
                Log("Step 4: Cleaning up backup");
                UpdateStatus("Cleaning up...", 90);
                transaction.Cleanup();
                _backupDirectory = string.Empty;
                Log("Backup cleanup completed");

                Log("All steps completed successfully");
                UpdateStatus("Update completed successfully!", 100);
                SafeUpdateProgressText("Ready to restart");
                SafeUpdateDetailsText("All steps completed successfully!\nUpdate finished.\n\nClick OK to restart the application.");

                Log("Enabling close button");
                Dispatcher.Invoke(() =>
                {
                    CloseButton.IsEnabled = true;
                });
            }
        }
        catch (Exception ex)
        {
            Log($"EXCEPTION in PerformUpdateAsync: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private async Task WaitForProcessToExitAsync(int processId)
    {
        try
        {
            Log($"Looking for process with ID {processId}");
            var process = Process.GetProcessById(processId);
            Log($"Process found: {process.ProcessName}");
            SafeUpdateProgressText($"Waiting for process to exit...");
            SafeUpdateDetailsText($"Process: {process.ProcessName}\nPID: {processId}");
            await process.WaitForExitAsync();
            Log("Process has exited");
            SafeUpdateProgressText("Process exited");
            SafeUpdateDetailsText($"Process {process.ProcessName} successfully exited");
        }
        catch (ArgumentException ex)
        {
            Log($"Process not found (already exited): {ex.Message}");
            SafeUpdateProgressText("Process already exited");
            SafeUpdateDetailsText($"Process with PID {processId} already exited");
        }
        Log("WaitForProcessToExitAsync completed");
    }

    private void SafeUpdateProgressText(string message)
    {
        try
        {
            Log($"SafeUpdateProgressText: {message}");
            Dispatcher.Invoke(() =>
            {
                ProgressTextBlock.Text = message;
            });
            Log($"SafeUpdateProgressText completed");
        }
        catch (Exception ex)
        {
            Log($"ERROR in SafeUpdateProgressText: {ex.Message}");
        }
    }

    private void SafeUpdateDetailsText(string message)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                DetailsTextBlock.Text = message;
            });
        }
        catch (Exception ex)
        {
            Log($"ERROR in SafeUpdateDetailsText: {ex.Message}");
        }
    }

    private void RestartApplication(string targetExecutablePath)
    {
        Log($"Restarting application: {targetExecutablePath}");
        SafeUpdateProgressText("Launching updated application...");
        SafeUpdateDetailsText($"Launching: {Path.GetFileName(targetExecutablePath)}");

        var workingDir = Path.GetDirectoryName(targetExecutablePath);
        Log($"Working directory: {workingDir}");

        var startInfo = new ProcessStartInfo
        {
            FileName = targetExecutablePath,
            UseShellExecute = true,
            WorkingDirectory = workingDir
        };

        var process = Process.Start(startInfo);
        Log($"Application started with PID: {process?.Id ?? -1}");
        SafeUpdateProgressText("Application successfully launched");
        SafeUpdateDetailsText($"Application successfully launched\nPID: {process?.Id ?? -1}");
    }

    private void UpdateStatus(string message, double progress)
    {
        try
        {
            Log($"UpdateStatus: {message} ({progress}%)");
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = message;
                UpdateProgressBar.Value = progress;
            });
        }
        catch (Exception ex)
        {
            Log($"ERROR in UpdateStatus: {ex.Message}");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Log("Close button clicked");

        try
        {
            Log("Restarting application");
            RestartApplication(_targetExecutablePath);
            Log("Application restarted");

            Log("Shutting down updater");
            Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            Log($"ERROR restarting application: {ex.Message}");
            MessageBox.Show(
                this,
                $"Failed to restart application:\n\n{ex.Message}",
                "Restart Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Log("Shutting down updater despite error");
            Application.Current.Shutdown(1);
        }
    }
}
