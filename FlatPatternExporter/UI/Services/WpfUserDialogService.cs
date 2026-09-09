using System.Windows;
using FlatPatternExporter.Services;
using FlatPatternExporter.UI.Windows;

namespace FlatPatternExporter.UI.Services;

public sealed class WpfUserDialogService : IUserDialogService
{
    public UserDialogResult Show(
        string message,
        string title,
        UserDialogButtons buttons = UserDialogButtons.Ok,
        UserDialogIcon icon = UserDialogIcon.None)
    {
        MessageBoxResult result = CustomMessageBox.Show(
            message,
            title,
            MapButtons(buttons),
            MapIcon(icon));

        return result switch
        {
            MessageBoxResult.OK => UserDialogResult.Ok,
            MessageBoxResult.Cancel => UserDialogResult.Cancel,
            MessageBoxResult.Yes => UserDialogResult.Yes,
            MessageBoxResult.No => UserDialogResult.No,
            _ => UserDialogResult.None
        };
    }

    private static MessageBoxButton MapButtons(UserDialogButtons buttons) => buttons switch
    {
        UserDialogButtons.Ok => MessageBoxButton.OK,
        UserDialogButtons.OkCancel => MessageBoxButton.OKCancel,
        UserDialogButtons.YesNo => MessageBoxButton.YesNo,
        UserDialogButtons.YesNoCancel => MessageBoxButton.YesNoCancel,
        _ => MessageBoxButton.OK
    };

    private static MessageBoxImage MapIcon(UserDialogIcon icon) => icon switch
    {
        UserDialogIcon.Information => MessageBoxImage.Information,
        UserDialogIcon.Warning => MessageBoxImage.Warning,
        UserDialogIcon.Error => MessageBoxImage.Error,
        UserDialogIcon.Question => MessageBoxImage.Question,
        _ => MessageBoxImage.None
    };
}
