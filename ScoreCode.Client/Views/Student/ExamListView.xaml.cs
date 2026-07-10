using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Exams;

namespace ScoreCode.Client.Views.Student;

public partial class ExamListView : UserControl
{
    private readonly ExamClientService _examService;

    /// <summary>Raised when the student chooses to begin/resume an exam.</summary>
    public event Action<ExamListItemDto>? ExamStartRequested;

    /// <summary>Raised to navigate elsewhere in the shell (e.g. Results).</summary>
    public event Action<string>? NavigateRequested;

    private static readonly FontFamily _font = new("/Assets/Fonts/#Geist");
    private static readonly FontFamily _mono = new("/Assets/Fonts/#Geist Mono, Consolas");

    private readonly List<ExamListItemDto> _allExams = new();
    private ExamFilter _filter = ExamFilter.All;

    public ExamListView(ApiService api)
    {
        InitializeComponent();
        _examService = new ExamClientService(api);

        SearchBox.TextChanged += (_, _) =>
        {
            bool empty = string.IsNullOrEmpty(SearchBox.Text);
            SearchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            ClearBtn.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            Render();
        };
        SearchBox.GotKeyboardFocus += (_, _) => SearchBorder.BorderBrush = ThemeManager.GetBrush("ThAccentLine");
        SearchBox.LostKeyboardFocus += (_, _) => SearchBorder.BorderBrush = ThemeManager.GetBrush("ThBorder");
        SearchBox.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) SearchBox.Clear(); };
        ClearBtn.MouseLeftButtonUp += (_, _) => { SearchBox.Clear(); SearchBox.Focus(); };

        Loaded += async (_, _) => await LoadExamsAsync();
    }

    private async Task LoadExamsAsync()
    {
        TxtLoading.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        ListScroller.Visibility = Visibility.Collapsed;

        try
        {
            var exams = await _examService.GetStudentExamsAsync();
            _allExams.Clear();
            _allExams.AddRange(exams);

            TxtLoading.Visibility = Visibility.Collapsed;
            RebuildFilterBar();
            Render();
        }
        catch
        {
            TxtLoading.Text = "Failed to load exams. Check your connection.";
        }
    }

    // ── Filtering + rendering ─────────────────────────────────────────────
    private enum ExamFilter { All, Available, Scheduled, Completed }

    private static ExamFilter CategoryOf(ExamListItemDto e) => GetExamState(e) switch
    {
        ExamState.Active => ExamFilter.Available,
        ExamState.Upcoming => ExamFilter.Scheduled,
        _ => ExamFilter.Completed
    };

    private void RebuildFilterBar()
    {
        FilterBar.Children.Clear();
        FilterBar.Children.Add(MakeFilterPill("All", ExamFilter.All, _allExams.Count));
        FilterBar.Children.Add(MakeFilterPill("Available", ExamFilter.Available, _allExams.Count(e => CategoryOf(e) == ExamFilter.Available)));
        FilterBar.Children.Add(MakeFilterPill("Scheduled", ExamFilter.Scheduled, _allExams.Count(e => CategoryOf(e) == ExamFilter.Scheduled)));
        FilterBar.Children.Add(MakeFilterPill("Completed", ExamFilter.Completed, _allExams.Count(e => CategoryOf(e) == ExamFilter.Completed)));
    }

    private Border MakeFilterPill(string label, ExamFilter filter, int count)
    {
        bool active = _filter == filter;
        var pill = new Border
        {
            Background = active ? ThemeManager.GetBrush("ThAccent") : System.Windows.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(13, 6, 13, 6),
            Margin = new Thickness(1, 0, 1, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock
        {
            Text = label, FontSize = 13, FontWeight = FontWeights.Medium,
            Foreground = active ? ThemeManager.GetBrush("ThTextOnAccent") : ThemeManager.GetBrush("ThTextSecondary"),
            FontFamily = _font, VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(new TextBlock
        {
            Text = count.ToString(), FontSize = 12,
            Foreground = active ? ThemeManager.GetBrush("ThTextOnAccent") : ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _mono, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Opacity = active ? 0.9 : 0.7
        });
        pill.Child = sp;
        if (!active)
        {
            pill.MouseEnter += (_, _) => pill.Background = ThemeManager.GetBrush("ThBgHover");
            pill.MouseLeave += (_, _) => pill.Background = System.Windows.Media.Brushes.Transparent;
        }
        pill.MouseLeftButtonUp += (_, _) => { _filter = filter; RebuildFilterBar(); Render(); };
        return pill;
    }

    private void Render()
    {
        ListPanel.Children.Clear();

        if (_allExams.Count == 0)
        {
            ListScroller.Visibility = Visibility.Collapsed;
            EmptyTitle.Text = "No exams assigned yet";
            EmptySubtitle.Text = "Check back when your lecturer publishes an exam";
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        var q = SearchBox.Text?.Trim() ?? "";
        var matches = _allExams
            .Where(e => _filter == ExamFilter.All || CategoryOf(e) == _filter)
            .Where(e => q.Length == 0
                        || e.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || e.CourseCode.Contains(q, StringComparison.OrdinalIgnoreCase))
            // Available first, then Scheduled, then Completed; each by nearest date.
            .OrderBy(e => (int)CategoryOf(e))
            .ThenBy(e => e.ScheduledAt)
            .ToList();

        if (matches.Count == 0)
        {
            ListScroller.Visibility = Visibility.Collapsed;
            EmptyTitle.Text = "No matching exams";
            EmptySubtitle.Text = "Try a different search or filter";
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        ListScroller.Visibility = Visibility.Visible;

        for (int i = 0; i < matches.Count; i++)
            ListPanel.Children.Add(BuildExamRow(matches[i], i == matches.Count - 1));
    }

    // ── Compact list row ──────────────────────────────────────────────────
    private Border BuildExamRow(ExamListItemDto exam, bool last)
    {
        var state = GetExamState(exam);
        bool resuming = exam.SessionStatus == "Active";

        var statusColor = state switch
        {
            ExamState.Active when resuming => ThemeManager.GetColor("ThAccentStrong"),
            ExamState.Active => ThemeManager.GetColor("ThSuccess"),
            ExamState.Upcoming => ThemeManager.GetColor("ThWarning"),
            _ => ThemeManager.GetColor("ThTextMuted") // completed / closed
        };

        var row = new Border
        {
            Padding = new Thickness(14, 0, 12, 0),
            Height = 58,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Background = System.Windows.Media.Brushes.Transparent
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // dot
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // title+course
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // meta (hover)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // action

        // Status dot
        grid.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 9, Height = 9, Fill = new SolidColorBrush(statusColor),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0)
        });

        // Title + course (single line)
        var main = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        main.Children.Add(new TextBlock
        {
            Text = exam.Title, FontSize = 14.5, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _font,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
        });
        var course = new TextBlock
        {
            Text = exam.CourseCode, FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _mono, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(course, 1);
        main.Children.Add(course);
        Grid.SetColumn(main, 1);
        grid.Children.Add(main);

        // Meta — revealed on hover (opacity, so layout doesn't shift)
        var meta = new TextBlock
        {
            Text = $"{exam.DurationMinutes} min · {exam.QuestionCount} Q",
            FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _font, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0), Opacity = 0
        };
        Grid.SetColumn(meta, 2);
        grid.Children.Add(meta);

        // Action / status (right)
        var action = BuildRowAction(exam, state, resuming);
        Grid.SetColumn(action, 3);
        grid.Children.Add(action);

        row.Child = grid;

        // Whole-row click target
        bool clickable = state == ExamState.Active || state == ExamState.Completed;
        if (clickable) row.Cursor = System.Windows.Input.Cursors.Hand;
        row.MouseLeftButtonUp += (_, _) =>
        {
            if (state == ExamState.Active) ExamStartRequested?.Invoke(exam);
            else if (state == ExamState.Completed) NavigateRequested?.Invoke("Results");
        };

        // Hover: highlight row + reveal meta
        row.MouseEnter += (_, _) =>
        {
            row.Background = ThemeManager.GetBrush("ThBgSurface");
            meta.Opacity = 1;
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = System.Windows.Media.Brushes.Transparent;
            meta.Opacity = 0;
        };

        return row;
    }

    private UIElement BuildRowAction(ExamListItemDto exam, ExamState state, bool resuming)
    {
        if (state == ExamState.Active)
        {
            var btn = new Border
            {
                Background = ThemeManager.GetBrush("ThAccent"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 7, 15, 7),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock
                {
                    Text = resuming ? "Resume" : "Begin",
                    FontSize = 13, FontWeight = FontWeights.Medium,
                    Foreground = ThemeManager.GetBrush("ThTextOnAccent"), FontFamily = _font
                }
            };
            btn.MouseLeftButtonUp += (_, e) => { e.Handled = true; ExamStartRequested?.Invoke(exam); };
            btn.MouseEnter += (_, _) => btn.Background = ThemeManager.GetBrush("ThAccentStrong");
            btn.MouseLeave += (_, _) => btn.Background = ThemeManager.GetBrush("ThAccent");
            return btn;
        }

        if (state == ExamState.Upcoming)
        {
            return new TextBlock
            {
                Text = FormatCountdown(exam.ScheduledAt), FontSize = 12.5,
                Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _font,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // Completed → view result affordance
        return new TextBlock
        {
            Text = "View result →", FontSize = 12.5, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThAccentStrong"), FontFamily = _font,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    // ── State helpers ─────────────────────────────────────────────────────
    private static string FormatCountdown(DateTime scheduledAt)
    {
        var diff = scheduledAt.ToUniversalTime() - DateTime.UtcNow;
        if (diff.TotalSeconds <= 0) return "Opening soon…";
        if (diff.TotalDays >= 1) return $"Opens in {(int)diff.TotalDays}d {diff.Hours}h";
        if (diff.TotalHours >= 1) return $"Opens in {(int)diff.TotalHours}h {diff.Minutes}m";
        return $"Opens in {(int)diff.TotalMinutes}m";
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
