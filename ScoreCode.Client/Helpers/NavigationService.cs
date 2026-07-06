using System.Windows;

namespace ScoreCode.Client.Helpers;

/// <summary>
/// Simple navigation helper — opens the target window, copies size/state, closes the current one.
/// WindowState is set BEFORE Show() to prevent visible resize animation.
/// </summary>
public static class NavigationService
{
    public static void NavigateTo(Window newWindow, Window? currentWindow = null)
    {
        if (currentWindow is not null)
        {
            if (currentWindow.WindowState == WindowState.Maximized)
            {
                newWindow.WindowState = WindowState.Maximized;
            }
            else
            {
                newWindow.Width  = currentWindow.ActualWidth;
                newWindow.Height = currentWindow.ActualHeight;
                newWindow.Left   = currentWindow.Left;
                newWindow.Top    = currentWindow.Top;
            }
        }

        newWindow.Show();
        currentWindow?.Close();
    }

    /// <summary>Navigate from the current application main window to a new window.</summary>
    public static void NavigateFrom(Window newWindow)
    {
        var current = Application.Current.MainWindow;

        // Persist + transfer window state
        if (current is not null)
        {
            var state = current.WindowState;
            WindowStateHelper.Save(state);

            if (state == WindowState.Maximized)
            {
                newWindow.WindowState = WindowState.Maximized;
            }
            else
            {
                newWindow.Width  = current.ActualWidth;
                newWindow.Height = current.ActualHeight;
                newWindow.Left   = current.Left;
                newWindow.Top    = current.Top;
            }
        }

        Application.Current.MainWindow = newWindow;
        newWindow.Show();
        current?.Close();
    }
}
