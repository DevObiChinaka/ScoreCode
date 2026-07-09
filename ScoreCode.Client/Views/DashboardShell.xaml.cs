using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Client.Views.Auth;
using ScoreCode.Client.Views.Dashboard;
using ScoreCode.Client.Views.Student;
using ScoreCode.Client.Views.Admin;
using ScoreCode.Client.Views.Lecturer;
using ScoreCode.Client.ViewModels;

namespace ScoreCode.Client.Views;

public partial class DashboardShell : Window
{
    private readonly DashboardViewModel _vm;
    private readonly ApiService _api;
    private string _activeNavKey = "Home";

    public DashboardShell(StoredSession session)
    {
        InitializeComponent();

        _vm = new DashboardViewModel(session);
        DataContext = _vm;

        // API client with auth token
        _api = new ApiService();
        _api.SetBearerToken(session.AccessToken);

        // Title bar
        TitleBarHelper.ApplyDarkMode(this);
        TitleBarHelper.SetCaptionColor(this, 0x0D, 0x11, 0x17);
        TitleBarHelper.SetIcon(this);

        // User card
        TxtInitials.Text = _vm.UserInitials;
        TxtUserId.Text = _vm.UserIdNumber;
        TxtUserRole.Text = _vm.UserRole;

        BuildNavButtons();
        NavigateTo("Home");
    }

    // ── Build sidebar nav buttons ──────────────────────────────────────────
    private void BuildNavButtons()
    {
        NavList.Items.Clear();

        foreach (var item in _vm.NavItems)
        {
            var btn = CreateNavButton(item);
            NavList.Items.Add(btn);
        }
    }

    private Button CreateNavButton(NavItem item)
    {
        // Icon
        var iconPath = new Path
        {
            Stretch = Stretch.Uniform,
            Width = 18,
            Height = 18,
            StrokeThickness = 1.5,
            Stroke = FindResource("TextSecondary") as Brush,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (TryFindResource(item.IconResource) is Geometry geo)
            iconPath.Data = geo;

        // Label
        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = 13,
            Foreground = FindResource("TextSecondary") as Brush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        label.SetValue(FrameworkElement.NameProperty, $"Lbl_{item.Key}");

        // Active pill background (hidden by default)
        var accent = new Border
        {
            Background = FindResource("ThBrAccentSoft") as Brush,
            CornerRadius = new CornerRadius(10),
            Visibility = Visibility.Collapsed
        };
        accent.SetValue(FrameworkElement.NameProperty, $"Accent_{item.Key}");

        // Layout
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(11, 0, 0, 0)
        };
        stack.Children.Add(iconPath);
        stack.Children.Add(label);

        var grid = new Grid { Height = 38 };
        grid.Children.Add(accent);
        grid.Children.Add(stack);

        var btn = new Button
        {
            Style = FindResource("NavButton") as Style,
            Tag = item.Key,
            Content = grid
        };
        btn.Click += NavButton_Click;

        return btn;
    }

    // ── Nav click ──────────────────────────────────────────────────────────
    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string key)
            NavigateTo(key);
    }

    private void NavigateTo(string key)
    {
        _activeNavKey = key;
        UpdateNavHighlights();

        // Load the appropriate view
        UserControl view = key switch
        {
            "Home" => CreateHomeView(),
            "Exams" => CreateExamListView(),
            "Users" => new UserManagementView(_api),
            "Monitoring" => new MonitoringView(_api),
            "ManageExams" => CreateExamManagementView(),
            "Submissions" => CreateSubmissionsView(),
            "Results" => new StudentResultsView(_api),
            "Settings" => new Settings.SettingsView(_vm.UserRole, _vm.UserIdNumber, _api),
            _ => CreateHomeView()
        };

        ContentHost.Content = view;
    }

    private HomeView CreateHomeView()
    {
        var home = new HomeView(_vm.UserRole, _vm.UserIdNumber, _vm.UserName, _api);
        home.NavigateRequested += (navKey) => NavigateTo(navKey);
        home.OpenExamEditorRequested += (examId) => OpenExamEditor(examId);
        return home;
    }

    private ExamManagementView CreateExamManagementView()
    {
        var view = new ExamManagementView(_api);
        view.OpenExamEditorRequested += (examId) => OpenExamEditor(examId);
        return view;
    }

    private void OpenExamEditor(Guid examId)
    {
        var editor = new ExamEditorView(_api, examId);
        editor.BackRequested += () => NavigateTo("ManageExams");
        ContentHost.Content = editor;
    }

    private SubmissionsView CreateSubmissionsView()
    {
        var view = new SubmissionsView(_api);
        view.OpenGradingRequested += (examId, matric, title, subs) =>
            OpenGradingView(examId, matric, title, subs);
        return view;
    }

    private void OpenGradingView(Guid examId, string matric, string title,
        List<ScoreCode.Shared.DTOs.Lecturer.SubmissionListItemDto> subs)
    {
        var grading = new GradingView(_api, examId, matric, title, subs);
        grading.BackRequested += () => NavigateTo("Submissions");
        ContentHost.Content = grading;
    }

    private ExamListView CreateExamListView()
    {
        var examList = new ExamListView(_api);
        examList.ExamStartRequested += async (examId) => await StartExamAsync(examId);
        return examList;
    }

    private async Task StartExamAsync(Guid examId)
    {
        try
        {
            var examService = new ExamClientService(_api);
            var response = await examService.StartExamAsync(examId);

            if (response is null || !response.Success || response.Data is null)
            {
                // Surface the server's reason (e.g. exam not open yet / window closed).
                var message = response?.Message ?? "Failed to start exam. Please try again.";
                System.Windows.MessageBox.Show(
                    message,
                    "Cannot Start Exam", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Open fullscreen exam view
            var examView = new ExamView(response.Data, _api);
            examView.Closed += (_, _) =>
            {
                // Return to dashboard when exam window closes
                Show();
                NavigateTo("Exams"); // Refresh exam list
            };

            Hide(); // Hide dashboard while exam is active
            examView.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error starting exam: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateNavHighlights()
    {
        var teal = FindResource("Teal") as Brush;
        var textPrimary = FindResource("TextPrimary") as Brush;
        var textSecondary = FindResource("TextSecondary") as Brush;

        foreach (var obj in NavList.Items)
        {
            if (obj is not Button btn || btn.Tag is not string key) continue;
            if (btn.Content is not Grid grid) continue;

            bool active = key == _activeNavKey;

            foreach (var child in grid.Children)
            {
                // Active pill background
                if (child is Border accent)
                    accent.Visibility = active ? Visibility.Visible : Visibility.Collapsed;

                // Icon + label
                if (child is StackPanel sp)
                {
                    foreach (var spChild in sp.Children)
                    {
                        if (spChild is Path p)
                            p.Stroke = active ? teal : textSecondary;
                        if (spChild is TextBlock tb)
                            tb.Foreground = active ? textPrimary : textSecondary;
                    }
                }
            }
        }
    }

    // ── Collapse / Expand ──────────────────────────────────────────────────
    private bool _isExpanded = true;

    private void BtnCollapse_Click(object sender, RoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;

        if (_isExpanded)
        {
            SidebarColumn.Width = new GridLength(220);
            CollapseIcon.Data = FindResource("Icon.PanelClose") as Geometry;
            LogoFull.Visibility = Visibility.Visible;

            // Show labels
            foreach (var obj in NavList.Items)
            {
                if (obj is Button btn && btn.Content is Grid g)
                {
                    foreach (var child in g.Children)
                    {
                        if (child is StackPanel sp)
                        {
                            sp.Margin = new Thickness(11, 0, 0, 0);
                            foreach (var spChild in sp.Children)
                                if (spChild is TextBlock tb)
                                    tb.Visibility = Visibility.Visible;
                        }
                    }
                }
            }

            // Show user card text
            TxtUserId.Visibility = Visibility.Visible;
            TxtUserRole.Visibility = Visibility.Visible;
            TxtInitials.FontSize = 13;
            TxtSignOut.Visibility = Visibility.Visible;
        }
        else
        {
            SidebarColumn.Width = new GridLength(60);
            CollapseIcon.Data = FindResource("Icon.PanelOpen") as Geometry;
            LogoFull.Visibility = Visibility.Collapsed;

            // Hide labels
            foreach (var obj in NavList.Items)
            {
                if (obj is Button btn && btn.Content is Grid g)
                {
                    foreach (var child in g.Children)
                    {
                        if (child is StackPanel sp)
                        {
                            sp.Margin = new Thickness(11, 0, 0, 0);
                            foreach (var spChild in sp.Children)
                                if (spChild is TextBlock tb)
                                    tb.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }

            // Collapse user card
            TxtUserId.Visibility = Visibility.Collapsed;
            TxtUserRole.Visibility = Visibility.Collapsed;
            TxtInitials.FontSize = 11;
            TxtSignOut.Visibility = Visibility.Collapsed;
        }
    }

    // ── Sign out ───────────────────────────────────────────────────────────
    private bool _isSigningOut;

    private void BtnSignOut_Click(object sender, RoutedEventArgs e)
    {
        _isSigningOut = true;

        // Persist window state so the auth window (and next launch) match
        Helpers.WindowStateHelper.Save(WindowState);

        SecureStorage.ClearSession();

        var authWindow = new AuthWindow();
        authWindow.WindowState = WindowState;   // keep maximized / normal
        Application.Current.MainWindow = authWindow;
        authWindow.Show();
        Close();
    }

    // ── Save state on close (covers Alt+F4, X button, etc.) ────────────
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        Helpers.WindowStateHelper.Save(WindowState);
        base.OnClosing(e);

        // If not signing out, this is a genuine close (X button / Alt+F4) — shut down the app
        if (!_isSigningOut)
        {
            Application.Current.Shutdown();
        }
    }
}
