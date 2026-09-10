using System.ComponentModel;
using System.Windows;
using FlatPatternExporter.Core;
using FlatPatternExporter.Services;

namespace FlatPatternExporter.Features.Frame.UI;

public partial class FrameMainWindow : Window
{
    private readonly InventorManager _inventorManager = new();

    public FrameMainWindow()
    {
        _inventorManager.InitializeInventor();
        InitializeComponent();
        RestoreSettings();
        FrameExporter.Initialize(_inventorManager);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        FrameExporter.CancelActiveOperation();
        SaveSettings();
        base.OnClosing(e);
    }

    private void RestoreSettings()
    {
        var settings = SettingsService.Instance.Settings;
        PropertyMetadataRegistry.UserDefinedProperties.Clear();
        foreach (var propertyName in settings.Interface.UserDefinedProperties)
        {
            if (!string.IsNullOrWhiteSpace(propertyName))
                PropertyMetadataRegistry.AddUserDefinedProperty(propertyName);
        }

        PropertyMetadataRegistry.PropertySubstitutions.Clear();
        foreach (var substitution in settings.Interface.PropertySubstitutions)
            PropertyMetadataRegistry.PropertySubstitutions[substitution.Key] = substitution.Value;

        FrameExporter.ApplySettings(settings.FrameExport);
    }

    private void SaveSettings()
    {
        var current = SettingsService.Instance.Settings;
        var userProperties = PropertyMetadataRegistry.UserDefinedProperties
            .Select(property => property.InventorPropertyName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var updatedInterface = current.Interface with
        {
            UserDefinedProperties = userProperties,
            PropertySubstitutions = new Dictionary<string, string>(PropertyMetadataRegistry.PropertySubstitutions)
        };
        SettingsService.Instance.Save(current with
        {
            Interface = updatedInterface,
            FrameExport = FrameExporter.CollectSettings()
        });
    }
}
