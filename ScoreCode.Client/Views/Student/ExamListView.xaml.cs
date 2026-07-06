using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Exams;

namespace ScoreCode.Client.Views.Student;

public partial class ExamListView : UserControl
{
    private readonly ExamClientService _examService;

    /// <summary>Raised when the student clicks "Start Exam" on an active card.</summary>
    public event Action<Guid>? ExamStartRequested;

    public ExamListView(ApiService api)
    {
        InitializeComponent();
        _examService = new ExamClientService(api);
        Loaded += async (_, _) => await LoadExamsAsync();
    }

    private async Task LoadExamsAsync()
    {
        TxtLoading.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        CardScroller.Visibility = Visibility.Collapsed;

        try
        {
            var exams = await _examService.GetStudentExamsAsync();

            TxtLoading.Visibility = Visibility.Collapsed;

            if (exams.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                return;
            }

            CardScroller.Visibility = Visibility.Visible;
            ExamCards.Items.Clear();

            foreach (var exam in exams)
                ExamCards.Items.Add(BuildExamCard(exam));
        }
        catch
        {
            TxtLoading.Text = "Failed to load exams. Check your connection.";
        }
    }

    // ── Card builder ──────────────────────────────────────────────────────
    private UIElement BuildExamCard(ExamListItemDto exam)
    {
        var state = GetExamState(exam);

        // Left accent border colour
        var accentBrush = state switch
        {
            ExamState.Active => ThemeManager.GetBrush("ThSuccess"),
            ExamState.Upcoming => ThemeManager.GetBrush("ThWarning"),
            _ => ThemeManager.GetBrush("ThTextMuted")
        };

        // Card container
        var cardBorder = new Border
        {
            Width = 320,
            MinHeight = 180,
            Margin = new Thickness(0, 0, 16, 16),
            CornerRadius = new CornerRadius(10),
            Background = ThemeManager.GetBrush("ThBgSurface"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(0)
        };

        // Inner grid with left accent bar
        var innerGrid = new Grid();
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left accent bar
        var accentBar = new Border
        {
            Background = accentBrush,
            CornerRadius = new CornerRadius(10, 0, 0, 10),
            Width = 4
        };
        Grid.SetColumn(accentBar, 0);
        innerGrid.Children.Add(accentBar);

        // Pulse animation for active exams
        if (state == ExamState.Active)
        {
            var pulse = new DoubleAnimation(1.0, 0.3, TimeSpan.FromSeconds(1.2))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase()
            };
            accentBar.BeginAnimation(OpacityProperty, pulse);
        }

        // Content stack
        var content = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
        Grid.SetColumn(content, 1);

        // Status badge
        var badgeText = state switch
        {
            ExamState.Active => "LIVE",
            ExamState.Upcoming => "UPCOMING",
            ExamState.Completed => exam.SessionStatus == "Completed" ? "SUBMITTED" : "CLOSED",
            _ => exam.Status.ToUpper()
        };

        var badgeFg = state switch
        {
            ExamState.Active => ThemeManager.GetColor("ThSuccess"),
            ExamState.Upcoming => ThemeManager.GetColor("ThWarning"),
            _ => ThemeManager.GetColor("ThTextSecondary")
        };

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x25, badgeFg.R, badgeFg.G, badgeFg.B)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10),
            Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(badgeFg)
            }
        };
        content.Children.Add(badge);

        // Course code
        content.Children.Add(new TextBlock
        {
            Text = exam.CourseCode,
            FontSize = 11,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            Margin = new Thickness(0, 0, 0, 2)
        });

        // Title
        content.Children.Add(new TextBlock
        {
            Text = exam.Title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Info row: duration + questions
        var infoPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        infoPanel.Children.Add(CreateInfoChip($"{exam.DurationMinutes} min"));
        infoPanel.Children.Add(CreateInfoChip($"{exam.QuestionCount} Q's"));

        if (exam.Languages.Count > 0)
            infoPanel.Children.Add(CreateInfoChip(string.Join(", ", exam.Languages)));

        content.Children.Add(infoPanel);

        // Date / countdown
        var dateText = state switch
        {
            ExamState.Active => "Started — click to enter",
            ExamState.Upcoming => FormatCountdown(exam.ScheduledAt),
            _ => exam.ScheduledAt.ToLocalTime().ToString("MMM dd, yyyy · h:mm tt")
        };

        content.Children.Add(new TextBlock
        {
            Text = dateText,
            FontSize = 12,
            Foreground = state == ExamState.Active
                ? ThemeManager.GetBrush("ThSuccess")
                : ThemeManager.GetBrush("ThTextSecondary"),
            Margin = new Thickness(0, 4, 0, 0)
        });

        // Start button for active exams
        if (state == ExamState.Active)
        {
            var startBtn = new Border
            {
                Background = ThemeManager.GetBrush("ThAccent"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(0, 8, 0, 8),
                Margin = new Thickness(0, 12, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "Start Exam",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.GetBrush("ThTextOnAccent"),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            startBtn.MouseLeftButtonDown += (_, _) => ExamStartRequested?.Invoke(exam.Id);
            content.Children.Add(startBtn);
        }

        innerGrid.Children.Add(content);
        cardBorder.Child = innerGrid;

        // Hover effect
        cardBorder.MouseEnter += (_, _) =>
            cardBorder.Background = ThemeManager.GetBrush("ThBgHover");
        cardBorder.MouseLeave += (_, _) =>
            cardBorder.Background = ThemeManager.GetBrush("ThBgSurface");

        return cardBorder;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static Border CreateInfoChip(string text)
    {
        return new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = ThemeManager.GetBrush("ThTextSecondary")
            }
        };
    }

    private static string FormatCountdown(DateTime scheduledAt)
    {
        var diff = scheduledAt.ToUniversalTime() - DateTime.UtcNow;
        if (diff.TotalSeconds <= 0) return "Starting soon...";
        if (diff.TotalDays >= 1) return $"Starts in {(int)diff.TotalDays}d {diff.Hours}h";
        if (diff.TotalHours >= 1) return $"Starts in {(int)diff.TotalHours}h {diff.Minutes}m";
        return $"Starts in {(int)diff.TotalMinutes}m";
    }

    private enum ExamState { Active, Upcoming, Completed }

    private static ExamState GetExamState(ExamListItemDto exam)
    {
        // Already submitted or session completed
        if (exam.SessionStatus == "Completed")
            return ExamState.Completed;

        // Exam closed
        if (exam.Status == "Closed")
            return ExamState.Completed;

        // An in-progress session can always be resumed.
        if (exam.SessionStatus == "Active")
            return ExamState.Active;

        // An Active exam only becomes startable from its scheduled time onward
        // (the server enforces a start grace window from ScheduledAt). Before
        // then it is still "Upcoming".
        if (exam.Status == "Active" && exam.ScheduledAt <= DateTime.UtcNow)
            return ExamState.Active;

        return ExamState.Upcoming;
    }
}
