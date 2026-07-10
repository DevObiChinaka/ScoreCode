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

    /// <summary>Raised when the student chooses to begin/resume an exam.</summary>
    public event Action<ExamListItemDto>? ExamStartRequested;

    private static readonly FontFamily _font = new("/Assets/Fonts/#Geist");
    private static readonly FontFamily _mono = new("/Assets/Fonts/#Geist Mono, Consolas");

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
        ExamCards.Visibility = Visibility.Collapsed;

        try
        {
            var exams = await _examService.GetStudentExamsAsync();

            TxtLoading.Visibility = Visibility.Collapsed;

            if (exams.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                return;
            }

            ExamCards.Visibility = Visibility.Visible;
            ExamCards.Items.Clear();

            foreach (var exam in exams)
                ExamCards.Items.Add(BuildExamCard(exam));
        }
        catch
        {
            TxtLoading.Text = "Failed to load exams. Check your connection.";
        }
    }

    // ── Card builder (mock layout) ────────────────────────────────────────
    private UIElement BuildExamCard(ExamListItemDto exam)
    {
        var state = GetExamState(exam);
        bool resuming = exam.SessionStatus == "Active";

        var card = new Border
        {
            Width = 468,
            MinHeight = 172,
            Margin = new Thickness(0, 0, 16, 16),
            CornerRadius = new CornerRadius(15),
            Background = ThemeManager.GetBrush("ThBgPanel"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(22)
        };

        var content = new StackPanel();

        // Top row: status badge (left) + course code (right)
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        (string text, Color color) badge = state switch
        {
            ExamState.Active when resuming => ("In progress", ThemeManager.GetColor("ThAccentStrong")),
            ExamState.Active => ("Available now", ThemeManager.GetColor("ThSuccess")),
            ExamState.Upcoming => ("Scheduled", ThemeManager.GetColor("ThWarning")),
            _ => exam.SessionStatus == "Completed"
                ? ("Completed", ThemeManager.GetColor("ThSuccess"))
                : ("Closed", ThemeManager.GetColor("ThTextMuted"))
        };
        top.Children.Add(Pill(badge.text, badge.color));

        var course = new TextBlock
        {
            Text = exam.CourseCode, FontSize = 11.5,
            Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _mono, VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(course, 1);
        top.Children.Add(course);
        content.Children.Add(top);

        // Title
        content.Children.Add(new TextBlock
        {
            Text = exam.Title, FontSize = 17, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            TextWrapping = TextWrapping.Wrap, FontFamily = _font,
            Margin = new Thickness(0, 15, 0, 0)
        });

        // Meta line
        var meta = $"{exam.DurationMinutes} min · {exam.QuestionCount} question" + (exam.QuestionCount == 1 ? "" : "s");
        if (exam.Languages.Count > 0) meta += "  ·  " + string.Join(", ", exam.Languages);
        content.Children.Add(new TextBlock
        {
            Text = meta, FontSize = 12.5,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            FontFamily = _font, Margin = new Thickness(0, 6, 0, 0)
        });

        // When
        var whenText = state switch
        {
            ExamState.Active when resuming => "Resume your in-progress attempt",
            ExamState.Active => "Open now — closes " + exam.ScheduledAt.ToLocalTime().ToString("MMM dd, HH:mm"),
            ExamState.Upcoming => FormatCountdown(exam.ScheduledAt),
            _ => "Submitted " + exam.ScheduledAt.ToLocalTime().ToString("MMM dd, yyyy")
        };
        content.Children.Add(new TextBlock
        {
            Text = whenText, FontSize = 12,
            Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _font, Margin = new Thickness(0, 14, 0, 0)
        });

        // Action
        content.Children.Add(BuildCardAction(exam, state, resuming));

        card.Child = content;

        // Hover: accent the border for actionable cards
        if (state == ExamState.Active)
        {
            card.MouseEnter += (_, _) => card.BorderBrush = ThemeManager.GetBrush("ThAccentLine");
            card.MouseLeave += (_, _) => card.BorderBrush = ThemeManager.GetBrush("ThBorder");
        }

        return card;
    }

    private UIElement BuildCardAction(ExamListItemDto exam, ExamState state, bool resuming)
    {
        if (state == ExamState.Active)
        {
            var btn = new Border
            {
                Background = ThemeManager.GetBrush("ThAccent"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(17, 10, 17, 10),
                Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock
                {
                    Text = resuming ? "Resume exam" : "Begin exam",
                    FontSize = 13.5, FontWeight = FontWeights.Medium,
                    Foreground = ThemeManager.GetBrush("ThTextOnAccent"), FontFamily = _font
                }
            };
            btn.MouseLeftButtonUp += (_, _) => ExamStartRequested?.Invoke(exam);
            btn.MouseEnter += (_, _) => btn.Background = ThemeManager.GetBrush("ThAccentStrong");
            btn.MouseLeave += (_, _) => btn.Background = ThemeManager.GetBrush("ThAccent");
            return btn;
        }

        // Non-actionable: a quiet outlined label
        string label = state == ExamState.Upcoming ? "Not yet open" : "View in Results";
        return new Border
        {
            BorderBrush = ThemeManager.GetBrush("ThBorderStrong"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 9, 16, 9),
            Margin = new Thickness(0, 18, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = label, FontSize = 13,
                Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _font
            }
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static Border Pill(string text, Color color) => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(0x24, color.R, color.G, color.B)),
        CornerRadius = new CornerRadius(999),
        Padding = new Thickness(11, 4, 11, 4),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text, FontSize = 11.5, FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(color), FontFamily = _font
        }
    };

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
