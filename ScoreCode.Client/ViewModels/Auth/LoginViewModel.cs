using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScoreCode.Client.Services;
using ScoreCode.Shared;

namespace ScoreCode.Client.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthClientService _auth;

    public LoginViewModel(AuthClientService auth)
    {
        _auth = auth;
    }

    [ObservableProperty]
    private string _idNumber = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError = false;

    [ObservableProperty]
    private bool _isLoading = false;

    // Password is passed in from code-behind (PasswordBox can't bind directly)
    public string Password { get; set; } = string.Empty;

    [RelayCommand]
    private async Task LoginAsync()
    {
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(IdNumber) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your ID number and password.";
            HasError = true;
            return;
        }

        IsLoading = true;
        try
        {
            var (success, error, data) = await _auth.LoginAsync(IdNumber, Password);

            if (!success)
            {
                ErrorMessage = error ?? "Login failed.";
                HasError = true;
                return;
            }

            // Navigate to the appropriate dashboard based on role
            var role = data!.User.Role;
            NavigateByRole(role);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Could not connect to the server. Check your connection.";
            HasError = true;
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void NavigateByRole(string role)
    {
        var session = Helpers.SecureStorage.LoadSession();
        if (session is null) return;

        var dashboard = new Views.DashboardShell(session, session.MustChangePassword);
        Helpers.NavigationService.NavigateFrom(dashboard);
    }

    // Called by RegisterView to navigate back to login
    public event Action? NavigateToRegister;

    [RelayCommand]
    private void GoToRegister() => NavigateToRegister?.Invoke();
}
