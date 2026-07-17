using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;

namespace ScoreCode.Client.Views.Auth;

/// <summary>
/// Self-service password reset. Step one asks for the ID and triggers a code
/// (or an admin request when no email is on file); step two takes the code and
/// a new password. The messaging never reveals which path applied, mirroring
/// the server, so an ID can't be probed.
/// </summary>
public partial class ForgotPasswordDialog : Window
{
    private readonly AuthClientService _auth;

    // Criteria rows, reused from the register screen's rules.
    private readonly (string Label, System.Func<string, bool> Met)[] _rules =
    {
        ("At least 8 characters", p => p.Length >= 8),
        ("One uppercase letter",  p => p.Any(char.IsUpper)),
        ("One number",            p => p.Any(char.IsDigit)),
        ("One special character", p => p.Any(c => !char.IsLetterOrDigit(c))),
    };
    private readonly List<TextBlock> _critRows = new();

    public ForgotPasswordDialog(AuthClientService auth, string? prefillId = null)
    {
        InitializeComponent();
        _auth = auth;

        TitleBarHelper.ApplyDarkMode(this);
        TitleBarHelper.SetIcon(this);

        if (!string.IsNullOrWhiteSpace(prefillId)) TxtId.Text = prefillId;

        BuildCriteriaRows();
        UpdateCriteria(string.Empty);
        Loaded += (_, _) => TxtId.Focus();
    }

    private void BuildCriteriaRows()
    {
        foreach (var rule in _rules)
        {
            var row = new TextBlock
            {
                Text = "○  " + rule.Label,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 3),
                Foreground = ThemeManager.GetBrush("ThTextMuted")
            };
            _critRows.Add(row);
            CritPanel.Children.Add(row);
        }
    }

    private void PbNew_PasswordChanged(object sender, RoutedEventArgs e)
        => UpdateCriteria(PbNew.Password);

    private void UpdateCriteria(string pwd)
    {
        var ok = ThemeManager.GetBrush("ThAccent");
        var muted = ThemeManager.GetBrush("ThTextMuted");
        for (var i = 0; i < _rules.Length; i++)
        {
            var met = _rules[i].Met(pwd);
            _critRows[i].Text = (met ? "✓  " : "○  ") + _rules[i].Label;
            _critRows[i].Foreground = met ? ok : muted;
        }
    }

    private bool PasswordValid(string p) => _rules.All(r => r.Met(p));

    private async void BtnRequest_Click(object sender, RoutedEventArgs e)
    {
        var id = TxtId.Text.Trim();
        if (id.Length == 0)
        {
            ShowStatus("Enter your ID number.", isError: true);
            return;
        }

        SetBusy(BtnRequest, true, "Sending…");
        SetBusy(BtnResend, true, "Sending…");
        try
        {
            // Always succeeds server-side; the response reveals nothing.
            await _auth.RequestPasswordResetAsync(id);
        }
        catch
        {
            // Even a transport error shouldn't tell the user anything specific.
        }
        finally
        {
            SetBusy(BtnRequest, false, "Send code");
            SetBusy(BtnResend, false, "Resend code");
        }

        RequestStep.Visibility = Visibility.Collapsed;
        VerifyStep.Visibility = Visibility.Visible;
        HideStatus();
        TxtCode.Focus();
    }

    private async void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        var id = TxtId.Text.Trim();
        var code = TxtCode.Text.Trim();
        var pwd = PbNew.Password;

        if (code.Length == 0) { ShowStatus("Enter the code from your email.", isError: true); return; }
        if (!PasswordValid(pwd)) { ShowStatus("Password doesn't meet all the requirements.", isError: true); return; }

        SetBusy(BtnReset, true, "Updating…");
        var (success, error) = await _auth.ResetWithCodeAsync(id, code, pwd);
        SetBusy(BtnReset, false, "Reset password");

        if (success)
        {
            VerifyStep.Visibility = Visibility.Collapsed;
            DoneStep.Visibility = Visibility.Visible;
            HideStatus();
        }
        else
        {
            ShowStatus(error ?? "That code is invalid or has expired.", isError: true);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    // ── helpers ────────────────────────────────────────────────────────────
    private static void SetBusy(Button btn, bool busy, string text)
    {
        btn.IsEnabled = !busy;
        btn.Content = text;
    }

    private void ShowStatus(string message, bool isError)
    {
        TxtStatus.Text = message;
        TxtStatus.Foreground = ThemeManager.GetBrush(isError ? "ThError" : "ThSuccess");
        TxtStatus.Visibility = Visibility.Visible;
    }

    private void HideStatus() => TxtStatus.Visibility = Visibility.Collapsed;
}
