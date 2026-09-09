namespace FlatPatternExporter.Services;

public enum UserDialogButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel
}

public enum UserDialogIcon
{
    None,
    Information,
    Warning,
    Error,
    Question
}

public enum UserDialogResult
{
    None,
    Ok,
    Cancel,
    Yes,
    No
}

public interface IUserDialogService
{
    UserDialogResult Show(
        string message,
        string title,
        UserDialogButtons buttons = UserDialogButtons.Ok,
        UserDialogIcon icon = UserDialogIcon.None);
}

public static class UserDialogService
{
    private sealed class NonInteractiveUserDialogService : IUserDialogService
    {
        public UserDialogResult Show(
            string message,
            string title,
            UserDialogButtons buttons = UserDialogButtons.Ok,
            UserDialogIcon icon = UserDialogIcon.None) => buttons switch
            {
                UserDialogButtons.Ok => UserDialogResult.Ok,
                UserDialogButtons.YesNo => UserDialogResult.No,
                _ => UserDialogResult.Cancel
            };
    }

    public static IUserDialogService Current { get; set; } = new NonInteractiveUserDialogService();

    public static UserDialogResult Show(
        string message,
        string title,
        UserDialogButtons buttons = UserDialogButtons.Ok,
        UserDialogIcon icon = UserDialogIcon.None) => Current.Show(message, title, buttons, icon);
}
