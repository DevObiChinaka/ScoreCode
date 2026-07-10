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

    private bool _pwVisible;

    // ── Password placeholder ───────────────────────────────────────────────
    private void PbPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        TogglePlaceholder((PasswordBox)sender);
        if (!_pwVisible && DataContext is ViewModels.Auth.LoginViewModel vm)
            vm.Password = PbPassword.Password;
    }

    private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_pwVisible && DataContext is ViewModels.Auth.LoginViewModel vm)
            vm.Password = TxtPasswordVisible.Text;
    }

    private void BtnTogglePw_Click(object sender, RoutedEventArgs e)
    {
        _pwVisible = !_pwVisible;
        if (_pwVisible)
        {
            TxtPasswordVisible.Text = PbPassword.Password;
            TxtPasswordVisible.Visibility = Visibility.Visible;
            PbPassword.Visibility = Visibility.Collapsed;
            EyeSlash.Visibility = Visibility.Visible;
            BtnTogglePw.ToolTip = "Hide password";
            TxtPasswordVisible.Focus();
            TxtPasswordVisible.CaretIndex = TxtPasswordVisible.Text.Length;
        }
        else
        {
            PbPassword.Password = TxtPasswordVisible.Text;
            PbPassword.Visibility = Visibility.Visible;
            TxtPasswordVisible.Visibility = Visibility.Collapsed;
            EyeSlash.Visibility = Visibility.Collapsed;
            BtnTogglePw.ToolTip = "Show password";
            PbPassword.Focus();
        }
    }

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
            vm.Password = _pwVisible ? TxtPasswordVisible.Text : PbPassword.Password;

            if (vm.LoginCommand.CanExecute(null))
                vm.LoginCommand.Execute(null);
        }
    }

    // ── Navigate to register ───────────────────────────────────────────────
    private void BtnRegister_Click(object sender, RoutedEventArgs e)
        => RegisterRequested?.Invoke();
}
