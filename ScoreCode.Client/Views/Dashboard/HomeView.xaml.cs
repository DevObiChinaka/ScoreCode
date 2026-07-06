using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs;

namespace ScoreCode.Client.Views.Dashboard;

public partial class HomeView : UserControl
{
    private readonly string _role;
    private readonly string _idNumber;
    private readonly ApiService? _api;

    /// <summary>Raised when a quick-action wants to navigate somewhere.</summary>
    public event Action<string>? NavigateRequested;

    /// <summary>Raised when user clicks an exam to open in editor.</summary>
    public event Action<Guid>? OpenExamEditorRequested;

    public HomeView(string role, string idNumber, string? name = null, ApiService? api = null)
    {
        InitializeComponent();
        _role = role;
        _idNumber = idNumber;
        _api = api;

        var now = DateTime.Now;
        var greeting = now.Hour < 12 ? "Good morning" : now.Hour < 17 ? "Good afternoon" : "Good evening";
        var displayName = string.IsNullOrWhiteSpace(name) ? idNumber : name;
        TxtWelcome.Text = $"{greeting}, {displayName}.";
        TxtSubtitle.Text = $"Signed in as {idNumber}  ·  {now:dddd, dd MMMM yyyy}";

        Loaded += async (_, _) => await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        // Build quick actions (no API needed)
        BuildQuickActions();

        if (_api is null) { BuildStatCards(); return; }

        try
        {
            if (_role == "Student")
                await LoadStudentDashboardAsync();
            else if (_role == "Lecturer")
                await LoadLecturerDashboardAsync();
            else
                BuildStatCards(); // Admin — just show static for now
        }
        catch
        {
            BuildStatCards(); // Fallback to empty stats on error
        }
    }

    // ── Student Dashboard ─────────────────────────────────────────────────
    private async Task LoadStudentDashboardAsync()
    {
        var response = await _api!.GetAsync<StudentDashboardDto>("dashboard/student");
        var result = response?.Data;
        if (result is null) { BuildStatCards(); return; }

        StatCardsGrid.Children.Clear();
        AddStatCard("Exams Taken", result.ExamsTaken.ToString(), ThemeManager.GetBrush("ThAccent"));
        AddStatCard("Average", result.AverageScore < 0 ? "—" : $"{result.AverageScore:F0}%", Accent("#A78BFA"));
        AddStatCard("Next Exam", result.NextExamTitle ?? "None", ThemeManager.GetBrush("ThWarning"),
            result.NextExamDate.HasValue ? result.NextExamDate.Value.ToLocalTime().ToString("MMM dd, HH:mm") : null);
        AddStatCard("Exams Taken", result.TotalSubmissions.ToString(), ThemeManager.GetBrush("ThInfo"));

        // Upcoming exams
        TxtSection1Title.Text = "Upcoming Exams";
        Section1Content.Children.Clear();
        if (result.UpcomingExams.Count == 0)
        {
            Section1Content.Children.Add(MakeMutedText("No upcoming exams."));
        }
        else
        {
            foreach (var exam in result.UpcomingExams)
            {
                Section1Content.Children.Add(BuildUpcomingExamRow(exam));
            }
        }

        // Recent results
        Section2Content.Children.Clear();
        if (result.RecentResults.Count == 0)
        {
            Section2Content.Children.Add(MakeMutedText("No results released yet."));
        }
        else
        {
            foreach (var r in result.RecentResults)
            {
                Section2Content.Children.Add(BuildRecentResultRow(r));
            }
        }
    }

    // ── Lecturer Dashboard ────────────────────────────────────────────────
    private async Task LoadLecturerDashboardAsync()
    {
        var response = await _api!.GetAsync<LecturerDashboardDto>("dashboard/lecturer");
        var result = response?.Data;
        if (result is null) { BuildStatCards(); return; }

        StatCardsGrid.Children.Clear();
        AddStatCard("Active Exams", result.ActiveExams.ToString(), ThemeManager.GetBrush("ThAccent"));
        AddStatCard("Ungraded", result.UngradedSubmissions.ToString(), ThemeManager.GetBrush("ThWarning"),
            result.UngradedSubmissions > 0 ? "needs attention" : null);
        AddStatCard("Students", result.TotalStudents.ToString(), Accent("#A78BFA"));
        AddStatCard("Total Exams", result.TotalExams.ToString(), ThemeManager.GetBrush("ThInfo"));

        // Recent exams (top 3) + View More link
        TxtSection1Title.Text = "Recent Exams";
        Section1Content.Children.Clear();
        if (result.RecentExams.Count == 0)
        {
            Section1Content.Children.Add(MakeMutedText("No exams created yet."));
        }
        else
        {
            foreach (var exam in result.RecentExams)
            {
                var examRow = BuildLecturerExamRow(exam);
                examRow.Cursor = System.Windows.Input.Cursors.Hand;
                examRow.MouseLeftButtonDown += (_, _) => OpenExamEditorRequested?.Invoke(exam.Id);
                Section1Content.Children.Add(examRow);
            }

            // View More link
            var viewMore = new TextBlock
            {
                Text = "View all exams →",
                FontSize = 12,
                Foreground = ThemeManager.GetBrush("ThAccent"),
                FontFamily = Font(),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            viewMore.MouseLeftButtonDown += (_, _) => NavigateRequested?.Invoke("ManageExams");
            Section1Content.Children.Add(viewMore);
        }

        // Section 2: Grading summary
        Section2Content.Children.Clear();
        Section2Content.Children.Add(MakeMutedText(
            result.UngradedSubmissions > 0
                ? $"You have {result.UngradedSubmissions} ungraded submission{(result.UngradedSubmissions == 1 ? "" : "s")} waiting."
                : "All submissions are graded. ✓"));
    }

    // ── Row Builders ──────────────────────────────────────────────────────
    private Border BuildUpcomingExamRow(UpcomingExamDto exam)
    {
        var row = MakeRow();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = $"{exam.CourseCode} — {exam.Title}",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = Font()
        });
        left.Children.Add(new TextBlock
        {
            Text = $"{exam.ScheduledAt.ToLocalTime():MMM dd, HH:mm}  ·  {exam.DurationMinutes} min",
            FontSize = 11,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            FontFamily = Font(),
            Margin = new Thickness(0, 2, 0, 0)
        });
        grid.Children.Add(left);

        var badge = MakeTag("UPCOMING", ThemeManager.GetColor("ThAccent"));
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);

        row.Child = grid;
        return row;
    }

    private Border BuildRecentResultRow(RecentResultDto result)
    {
        var row = MakeRow();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pct = result.MaxScore > 0 ? (double)result.Score / result.MaxScore * 100 : 0;
        var color = pct >= 70 ? ThemeManager.GetColor("ThSuccess")
                  : pct >= 50 ? ThemeManager.GetColor("ThWarning")
                  : ThemeManager.GetColor("ThError");

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = $"{result.CourseCode} — {result.ExamTitle}",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = Font()
        });
        left.Children.Add(new TextBlock
        {
            Text = result.SubmittedAt.ToLocalTime().ToString("MMM dd, yyyy"),
            FontSize = 11,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            FontFamily = Font(),
            Margin = new Thickness(0, 2, 0, 0)
        });
        grid.Children.Add(left);

        var score = new TextBlock
        {
            Text = $"{result.Score}/{result.MaxScore} ({pct:F0}%)",
            FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = Font()
        };
        Grid.SetColumn(score, 1);
        grid.Children.Add(score);

        row.Child = grid;
        row.Cursor = System.Windows.Input.Cursors.Hand;
        row.MouseLeftButtonDown += (_, _) => NavigateRequested?.Invoke("Results");
        return row;
    }

    private Border BuildLecturerExamRow(ExamSummaryDto exam)
    {
        var row = MakeRow();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = $"{exam.CourseCode} — {exam.Title}",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = Font()
        });
        left.Children.Add(new TextBlock
        {
            Text = $"{exam.StudentCount} students  ·  {exam.CreatedAt.ToLocalTime():MMM dd}",
            FontSize = 11,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            FontFamily = Font(),
            Margin = new Thickness(0, 2, 0, 0)
        });
        grid.Children.Add(left);

        // Ungraded count
        if (exam.UngradedCount > 0)
        {
            var ungradedTag = MakeTag($"{exam.UngradedCount} ungraded", ThemeManager.GetColor("ThWarning"));
            Grid.SetColumn(ungradedTag, 1);
            grid.Children.Add(ungradedTag);
        }

        // Status
        var statusColor = exam.Status switch
        {
            "Active" => ThemeManager.GetColor("ThSuccess"),
            "Draft" => ThemeManager.GetColor("ThWarning"),
            _ => ThemeManager.GetColor("ThTextSecondary")
        };
        var statusTag = MakeTag(exam.Status.ToUpper(), statusColor);
        statusTag.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(statusTag, 2);
        grid.Children.Add(statusTag);

        row.Child = grid;
        return row;
    }

    // ── Stat Cards ─────────────────────────────────────────────────────────
    private void BuildStatCards()
    {
        StatCardsGrid.Children.Clear();
        switch (_role)
        {
            case "Student":
                AddStatCard("Exams Taken", "0", ThemeManager.GetBrush("ThAccent"));
                AddStatCard("Average Score", "—", Accent("#A78BFA"));
                AddStatCard("Next Exam", "None", ThemeManager.GetBrush("ThWarning"));
                AddStatCard("Submissions", "0", ThemeManager.GetBrush("ThInfo"));
                break;
            case "Lecturer":
                AddStatCard("Active Exams", "0", ThemeManager.GetBrush("ThAccent"));
                AddStatCard("Ungraded", "0", ThemeManager.GetBrush("ThWarning"));
                AddStatCard("Students", "0", Accent("#A78BFA"));
                AddStatCard("Total Exams", "0", ThemeManager.GetBrush("ThInfo"));
                break;
            case "Admin":
                AddStatCard("Total Students", "0", ThemeManager.GetBrush("ThAccent"));
                AddStatCard("Total Lecturers", "0", Accent("#A78BFA"));
                AddStatCard("Active Exams", "0", ThemeManager.GetBrush("ThWarning"));
                AddStatCard("System Health", "OK", ThemeManager.GetBrush("ThInfo"));
                break;
        }
    }

    private void AddStatCard(string title, string value, SolidColorBrush accent, string? subtitle = null)
    {
        var card = new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            CornerRadius = new CornerRadius(10),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 18, 20, 18),
            Margin = new Thickness(0, 0, 12, 0),
        };

        var stack = new StackPanel();

        // Accent dot + title
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new Border
        {
            Width = 8, Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = accent,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = Font()
        });
        stack.Children.Add(header);

        // Value
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = Font()
        });

        // Optional subtitle
        if (subtitle != null)
        {
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 10,
                Foreground = accent,
                FontFamily = Font(),
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        card.Child = stack;
        StatCardsGrid.Children.Add(card);
    }

    // ── Quick Actions ──────────────────────────────────────────────────────
    private void BuildQuickActions()
    {
        QuickActionsGrid.Children.Clear();
        switch (_role)
        {
            case "Student":
                AddActionCard("Start Exam", "Join an active exam", ThemeManager.GetBrush("ThAccent"), "Exams");
                AddActionCard("View Results", "Check your scores", Accent("#A78BFA"), "Results");
                break;
            case "Lecturer":
                AddActionCard("Create Exam", "Set up a new exam", ThemeManager.GetBrush("ThAccent"), "ManageExams");
                AddActionCard("Grade Submissions", "Review pending work", ThemeManager.GetBrush("ThWarning"), "Submissions");
                AddActionCard("Manage Exams", "View all your exams", Accent("#A78BFA"), "ManageExams");
                break;
            case "Admin":
                AddActionCard("Manage Users", "Add or edit accounts", ThemeManager.GetBrush("ThAccent"), "Users");
                break;
        }
    }

    private void AddActionCard(string title, string subtitle, SolidColorBrush accent, string navKey)
    {
        var card = new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            CornerRadius = new CornerRadius(10),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 16, 18, 16),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = accent,
            FontFamily = Font()
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            Margin = new Thickness(0, 4, 0, 0),
            FontFamily = Font()
        });
        stack.Children.Add(new TextBlock
        {
            Text = "→",
            FontSize = 16,
            Foreground = accent,
            Margin = new Thickness(0, 8, 0, 0)
        });

        card.Child = stack;
        card.MouseLeftButtonDown += (_, _) => NavigateRequested?.Invoke(navKey);

        card.MouseEnter += (_, _) => card.Background = ThemeManager.GetBrush("ThBgSurface");
        card.MouseLeave += (_, _) => card.Background = ThemeManager.GetBrush("ThBgPanel");

        QuickActionsGrid.Children.Add(card);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static Border MakeRow() => new()
    {
        Background = ThemeManager.GetBrush("ThBgPanel"),
        BorderBrush = ThemeManager.GetBrush("ThBorder"),
        BorderThickness = new Thickness(0, 0, 0, 1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(14, 10, 14, 10),
        Margin = new Thickness(0, 0, 0, 6)
    };

    private static Border MakeTag(string text, Color color) => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(0x20, color.R, color.G, color.B)),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 3, 8, 3),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text, FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(color),
            FontFamily = new FontFamily("/Assets/Fonts/#Geist")
        }
    };

    private static TextBlock MakeMutedText(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = ThemeManager.GetBrush("ThTextMuted"),
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 20, 0, 20),
        FontFamily = new FontFamily("/Assets/Fonts/#Geist")
    };

    /// <summary>For accent colours not in the theme dictionary (purple etc.).</summary>
    private static SolidColorBrush Accent(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    private static FontFamily Font() =>
        new("/Assets/Fonts/#Geist");
}
