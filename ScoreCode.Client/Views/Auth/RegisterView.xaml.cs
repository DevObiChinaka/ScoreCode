using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScoreCode.Client.Helpers;

namespace ScoreCode.Client.Views.Auth;

public partial class RegisterView : UserControl
{
    /// <summary>Raised when the user clicks "Sign in".</summary>
    public event Action? LoginRequested;

    public RegisterView()
    {
        InitializeComponent();

        // Populate dropdowns from canonical values
        foreach (var p in ScoreCode.Shared.Constants.Academic.Programmes)
            CmbProgramme.Items.Add(p);
        CmbProgramme.SelectedIndex = 0;

        foreach (var l in ScoreCode.Shared.Constants.Academic.Levels)
            CmbLevel.Items.Add(l);
        CmbLevel.SelectedIndex = 0;

        // Enter key submits
        KeyDown += (_, e) => { if (e.Key == Key.Enter) BtnRegister_Click(this, e); };
    }

    // ── Password placeholders ──────────────────────────────────────────────
    private void PbPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var pb = (PasswordBox)sender;
        TogglePlaceholder(pb);
        UpdateCriteria(pb.Password);
    }

    private void PbConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var pb = (PasswordBox)sender;
        TogglePlaceholder(pb);
        UpdateConfirmIndicator(pb.Password);
    }

    private static void TogglePlaceholder(PasswordBox pb)
    {
        var ph = pb.Template.FindName("PasswordPlaceholder", pb) as TextBlock;
        if (ph is not null)
            ph.Visibility = pb.Password.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Real-time password criteria ────────────────────────────────────────
    private void UpdateCriteria(string pwd)
    {
        SetCriteria("CritLength",  pwd.Length >= 8);
        SetCriteria("CritUpper",   pwd.Any(char.IsUpper));
        SetCriteria("CritNumber",  pwd.Any(char.IsDigit));
        SetCriteria("CritSpecial", pwd.Any(c => !char.IsLetterOrDigit(c)));
    }

    private void SetCriteria(string name, bool met)
    {
        var teal  = ThemeManager.GetBrush("ThAccent");
        var muted = ThemeManager.GetBrush("ThTextMuted");

        if (FindName(name + "Icon") is TextBlock icon)
        {
            icon.Text       = met ? "✓" : "○";
            icon.Foreground = met ? teal : muted;
        }
        if (FindName(name + "Label") is TextBlock label)
            label.Foreground = met ? teal : muted;
    }

    private void UpdateConfirmIndicator(string confirmPwd)
    {
        var pw1 = FindName("PbPassword") as PasswordBox;
        if (pw1 is null) return;

        bool matches  = confirmPwd.Length > 0 && confirmPwd == pw1.Password;
        bool mismatch = confirmPwd.Length > 0 && confirmPwd != pw1.Password;

        var teal = ThemeManager.GetBrush("ThAccent");
        var red  = ThemeManager.GetBrush("ThError");
        var gray = ThemeManager.GetBrush("ThTextMuted");

        if (FindName("ConfirmMatchIcon") is TextBlock icon)
        {
            icon.Text       = matches ? "✓" : mismatch ? "✕" : "○";
            icon.Foreground = matches ? teal : mismatch ? red : gray;
        }
        if (FindName("ConfirmMatchLabel") is TextBlock label)
        {
            label.Text       = matches ? "Passwords match" : mismatch ? "Passwords do not match" : "Confirm your password";
            label.Foreground = matches ? teal : mismatch ? red : gray;
        }
    }

    // ── Register click ─────────────────────────────────────────────────────
    private void BtnRegister_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Auth.RegisterViewModel vm)
        {
            vm.Password        = (FindName("PbPassword")        as PasswordBox)?.Password ?? string.Empty;
            vm.ConfirmPassword = (FindName("PbConfirmPassword") as PasswordBox)?.Password ?? string.Empty;
            vm.ProgrammeOfStudy = CmbProgramme.SelectedItem?.ToString() ?? "";
            vm.Level            = CmbLevel.SelectedItem?.ToString() ?? "";

            if (vm.RegisterCommand.CanExecute(null))
                vm.RegisterCommand.Execute(null);
        }
    }

    // ── Back to login ──────────────────────────────────────────────────────
    private void BtnLogin_Click(object sender, RoutedEventArgs e)
        => LoginRequested?.Invoke();
}
