namespace FlatPatternExporter.Services;

/// <summary>
/// Provides application settings persistence and change notification.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Current application settings loaded from persistent storage.
    /// </summary>
    ApplicationSettings Settings { get; }

    /// <summary>
    /// Persists application settings to storage and raises <see cref="SettingsChanged"/>.
    /// </summary>
    void Save(ApplicationSettings settings);

    /// <summary>
    /// Resets settings to defaults, persists, and raises <see cref="SettingsChanged"/>.
    /// </summary>
    void Reset();

    /// <summary>
    /// Exports current settings to the specified file path.
    /// </summary>
    void Export(string filePath);

    /// <summary>
    /// Imports settings from the specified file path and raises <see cref="SettingsChanged"/>.
    /// </summary>
    void Import(string filePath);

    /// <summary>
    /// Raised after settings have been saved, reset, or imported.
    /// </summary>
    event EventHandler? SettingsChanged;
}
