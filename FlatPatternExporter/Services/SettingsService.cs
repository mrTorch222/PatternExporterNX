using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatPatternExporter.Services;

/// <summary>
/// Thread-safe singleton implementation of <see cref="ISettingsService"/>.
/// Manages loading, saving, exporting, and importing application settings as JSON.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static SettingsService? _instance;
    private static readonly object _instanceLock = new();

    private readonly object _ioLock = new();

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FlatPatternExporter",
        "settings.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static SettingsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new SettingsService();
                }
            }
            return _instance;
        }
    }

    public ApplicationSettings Settings { get; private set; }

    public event EventHandler? SettingsChanged;

    private SettingsService()
    {
        Settings = Load();
    }

    public void Save(ApplicationSettings settings)
    {
        lock (_ioLock)
        {
            WriteToFile(SettingsFilePath, settings);
            Settings = settings;
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        var defaults = new ApplicationSettings();

        lock (_ioLock)
        {
            WriteToFile(SettingsFilePath, defaults);
            Settings = defaults;
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Export(string filePath)
    {
        lock (_ioLock)
        {
            WriteToFile(filePath, Settings);
        }
    }

    public void Import(string filePath)
    {
        lock (_ioLock)
        {
            var imported = ReadFromFile(filePath);
            WriteToFile(SettingsFilePath, imported);
            Settings = imported;
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ApplicationSettings Load()
    {
        try
        {
            return ReadFromFile(SettingsFilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading settings: {ex.Message}");
            return new ApplicationSettings();
        }
    }

    private static ApplicationSettings ReadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<ApplicationSettings>(json, JsonOptions)
               ?? new ApplicationSettings();
    }

    private static void WriteToFile(string filePath, ApplicationSettings settings)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(filePath, json);
    }
}
