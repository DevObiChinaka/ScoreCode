using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScoreCode.Client.Views.Auth;

public partial class LoginView : UserControl
{
    /// <summary>Raised when the user clicks "Create account".</summary>
    public event Action? RegisterRequested;

    public LoginView()
    {
        InitializeComponent();

        // Enter key submits login
        KeyDown += (_, e) => { if (e.Key == Key.Enter) BtnLogin_Click(this, e); };
    }

    // ── Password placeholder ───────────────────────────────────────────────
    private void PbPassword_PasswordChanged(object sender, RoutedEventArgs e)
        => TogglePlaceholder((PasswordBox)sender);

    private static void TogglePlaceholder(PasswordBox pb)
    {
        var ph = pb.Template.FindName("PasswordPlaceholder", pb) as TextBlock;
        if (ph is not null)
            ph.Visibility = pb.Password.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Login click ────────────────────────────────────────────────────────
    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Auth.LoginViewModel vm)
        {
            var pb = FindName("PbPassword") as PasswordBox;
            if (pb is not null) vm.Password = pb.Password;

            if (vm.LoginCommand.CanExecute(null))
                vm.LoginCommand.Execute(null);
        }
    }

    // ── Navigate to register ───────────────────────────────────────────────
    private void BtnRegister_Click(object sender, RoutedEventArgs e)
        => RegisterRequested?.Invoke();
}
