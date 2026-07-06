using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScoreCode.Client.Helpers;

namespace ScoreCode.Client.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    // ── User info ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _userIdNumber = string.Empty;
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userLevel = string.Empty;
    [ObservableProperty] private string _userInitials = string.Empty;
    [ObservableProperty] private string _windowTitle = "ScoreCode · Dashboard";

    // ── Sidebar ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSidebarExpanded = true;
    [ObservableProperty] private string _selectedNavKey = "Home";

    // ── Navigation items (set by role) ─────────────────────────────────────
    public List<NavItem> NavItems { get; private set; } = new();

    public DashboardViewModel(StoredSession session)
    {
        UserIdNumber = session.IdNumber;
        UserName = session.Name;
        UserRole = session.Role;
        UserInitials = session.IdNumber.Length >= 2
            ? session.IdNumber[..2].ToUpperInvariant()
            : session.IdNumber.ToUpperInvariant();
        WindowTitle = $"ScoreCode · {session.Role}";

        BuildNavItems(session.Role);
    }

    private void BuildNavItems(string role)
    {
        var items = new List<NavItem>
        {
            new("Home", "Icon.Home", "Home")
        };

        switch (role)
        {
            case "Student":
                items.Add(new("Exams", "Icon.FileText", "My Exams"));
                items.Add(new("Results", "Icon.BarChart", "Results"));
                break;
            case "Lecturer":
                items.Add(new("ManageExams", "Icon.ClipboardList", "Manage Exams"));
                items.Add(new("Submissions", "Icon.FileCheck", "Submissions"));
                break;
            case "Admin":
                items.Add(new("Users", "Icon.Users", "Users"));
                items.Add(new("Monitoring", "Icon.Activity", "Monitoring"));
                break;
        }

        items.Add(new("Settings", "Icon.Settings", "Settings"));
        NavItems = items;
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    [RelayCommand]
    private void Logout()
    {
        SecureStorage.ClearSession();
        // DashboardShell.xaml.cs handles window transition
    }
}

/// <summary>Represents one sidebar navigation entry.</summary>
public record NavItem(string Key, string IconResource, string Label);
