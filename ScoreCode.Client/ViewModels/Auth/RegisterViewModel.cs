using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Institutions;

namespace ScoreCode.Client.ViewModels.Auth;

public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthClientService _auth;

    public RegisterViewModel(AuthClientService auth) => _auth = auth;

    /// <summary>Approved institutions for the dropdown.</summary>
    public ObservableCollection<InstitutionOption> Institutions { get; } = [];

    /// <summary>Set from code-behind (selected dropdown item) before Register runs.</summary>
    public Guid InstitutionId { get; set; }

    /// <summary>Load approved institutions into the dropdown.</summary>
    public async Task LoadInstitutionsAsync()
    {
        try
        {
            var options = await _auth.GetInstitutionOptionsAsync();
            Institutions.Clear();
            foreach (var o in options) Institutions.Add(o);
        }
        catch
        {
            // Leave empty; the register button will report the missing selection.
        }
    }

    [ObservableProperty] private string _matricNumber      = string.Empty;
    [ObservableProperty] private string _programmeOfStudy = string.Empty;
    [ObservableProperty] private string _level             = string.Empty;
    [ObservableProperty] private string _errorMessage      = string.Empty;
    [ObservableProperty] private bool   _hasError          = false;
    [ObservableProperty] private string _successMessage    = string.Empty;
    [ObservableProperty] private bool   _hasSuccess        = false;
    [ObservableProperty] private bool   _isLoading         = false;

    // Passwords are injected from code-behind on button click (PasswordBox can't bind)
    public string Password        { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        HasError   = false;
        HasSuccess = false;
        ErrorMessage  = string.Empty;
        SuccessMessage = string.Empty;

        if (InstitutionId == Guid.Empty)
        {
            ErrorMessage = "Please select your institution.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(MatricNumber) ||
            string.IsNullOrWhiteSpace(ProgrammeOfStudy) ||
            string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(Level))
        {
            ErrorMessage = "All fields are required.";
            HasError = true;
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
            HasError = true;
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            HasError = true;
            return;
        }

        IsLoading = true;
        try
        {
            var (success, error) = await _auth.RegisterStudentAsync(
                InstitutionId, MatricNumber, ProgrammeOfStudy, Level, Password, ConfirmPassword);

            if (!success)
            {
                ErrorMessage = error ?? "Registration failed.";
                HasError = true;
                return;
            }

            SuccessMessage = "Account created! Redirecting to login…";
            HasSuccess = true;

            await Task.Delay(1500);
            NavigateBackToLogin?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Could not connect to the server. Check your connection.";
            HasError = true;
            System.Diagnostics.Debug.WriteLine($"Register error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event Action? NavigateBackToLogin;

    [RelayCommand]
    private void GoToLogin() => NavigateBackToLogin?.Invoke();
}
