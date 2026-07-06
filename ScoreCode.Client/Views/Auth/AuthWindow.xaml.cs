using System.Windows;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Client.ViewModels.Auth;

namespace ScoreCode.Client.Views.Auth;

public partial class AuthWindow : Window
{
    private readonly AuthClientService _auth;

    public AuthWindow()
    {
        InitializeComponent();

        _auth = new AuthClientService(new ApiService());

        TitleBarHelper.ApplyDarkMode(this);
        TitleBarHelper.SetCaptionColor(this, 0x0D, 0x11, 0x17);
        TitleBarHelper.SetIcon(this);

        ShowLogin();
    }

    // ── View switching (no window open/close — just content swap) ──────────
    public void ShowLogin()
    {
        Title = "ScoreCode · Sign In";
        var login = new LoginView();
        login.DataContext = new LoginViewModel(_auth);

        // Wire navigation
        login.RegisterRequested += () => ShowRegister();

        ContentHost.Content = login;
    }

    public void ShowRegister()
    {
        Title = "ScoreCode · Create Account";
        var register = new RegisterView();
        var vm = new RegisterViewModel(_auth);
        register.DataContext = vm;

        // Wire navigation
        register.LoginRequested += () => ShowLogin();
        vm.NavigateBackToLogin += () => ShowLogin();

        ContentHost.Content = register;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        Helpers.WindowStateHelper.Save(WindowState);
    }
}
