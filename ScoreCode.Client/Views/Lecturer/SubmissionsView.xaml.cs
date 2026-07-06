using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Lecturer;

namespace ScoreCode.Client.Views.Lecturer;

public partial class SubmissionsView : UserControl
{
    /// <summary>Raised when the lecturer clicks Grade on a student row.</summary>
    public event Action<Guid, string, string, List<SubmissionListItemDto>>? OpenGradingRequested;

    private readonly LecturerClientService _svc;
    private List<LecturerExamDto> _exams = [];
    private int _selectedExamIndex = -1;

    public SubmissionsView(ApiService api)
    {
        InitializeComponent();
        _svc = new LecturerClientService(api);
        Loaded += async (_, _) => await LoadExamListAsync();
    }

    // ── Load exam strip ────────────────────────────────────────────────────
    private async Task LoadExamListAsync()
    {
        _exams = await _svc.GetExamsAsync();
        BuildExamStrip();
    }

    private void BuildExamStrip()
    {
        ExamStrip.Children.Clear();

        if (_exams.Count == 0)
        {
            TxtNoExams.Visibility = Visibility.Visible;
            return;
        }
        TxtNoExams.Visibility = Visibility.Collapsed;

        for (int i = 0; i < _exams.Count; i++)
        {
            var exam = _exams[i];
            var idx = i;

            var statusColor = exam.Status switch
            {
                "Active" => ThemeManager.GetColor("ThAccent"),
                "Draft"  => ThemeManager.GetColor("ThWarning"),
                _        => ThemeManager.GetColor("ThTextSecondary")
            };

            var graded = exam.SubmissionCount > 0 && exam.UngradedCount == 0;
            var hasSubmissions = exam.SubmissionCount > 0;

            // Card
            var card = new Border
            {
                MinWidth = 220,
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = ThemeManager.GetBrush("ThBorder"),
                Background = ThemeManager.GetBrush("ThBgPanel"),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = idx
            };

            var stack = new StackPanel();

            // Row 1: title + status dot
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleTb = new TextBlock
            {
                Text = exam.Title,
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.GetBrush("ThTextPrimary"),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 160,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleTb, 0);
            titleRow.Children.Add(titleTb);

            // Status dot
            var dot = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(statusColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = exam.Status
            };
            Grid.SetColumn(dot, 1);
            titleRow.Children.Add(dot);

            stack.Children.Add(titleRow);

            // Row 2: course code
            stack.Children.Add(new TextBlock
            {
                Text = exam.CourseCode,
                FontSize = 11,
                Foreground = ThemeManager.GetBrush("ThTextSecondary"),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                Margin = new Thickness(0, 3, 0, 6)
            });

            // Row 3: stats chips
            var statsRow = new StackPanel { Orientation = Orientation.Horizontal };

            statsRow.Children.Add(MakeChip($"{exam.SubmissionCount} subs"));

            if (hasSubmissions)
            {
                if (graded)
                    statsRow.Children.Add(MakeChip("✓ All graded", "ThSuccess"));
                else if (exam.UngradedCount > 0)
                    statsRow.Children.Add(MakeChip($"{exam.UngradedCount} ungraded", "ThWarning"));
            }

            stack.Children.Add(statsRow);
            card.Child = stack;

            // Click
            card.MouseLeftButtonDown += async (_, _) =>
            {
                _selectedExamIndex = idx;
                HighlightSelectedExam();
                var e = _exams[idx];
                _examReleased = e.ResultsReleased;
                UpdateReleaseBar(e);
                await LoadSubmissionsAsync(e.Id);
            };

            // Hover
            card.MouseEnter += (_, _) =>
            {
                if ((int)card.Tag != _selectedExamIndex)
                {
                    var aC = ThemeManager.GetColor("ThAccent");
                    card.Background = new SolidColorBrush(Color.FromArgb(0x12, aC.R, aC.G, aC.B));
                }
            };
            card.MouseLeave += (_, _) =>
            {
                if ((int)card.Tag != _selectedExamIndex)
                    card.Background = ThemeManager.GetBrush("ThBgPanel");
            };

            ExamStrip.Children.Add(card);
        }
    }

    private void HighlightSelectedExam()
    {
        var accentC = ThemeManager.GetColor("ThAccent");
        foreach (var child in ExamStrip.Children.OfType<Border>())
        {
            if (child.Tag is not int idx) continue;
            if (idx == _selectedExamIndex)
            {
                child.Background = new SolidColorBrush(Color.FromArgb(0x20, accentC.R, accentC.G, accentC.B));
                child.BorderBrush = new SolidColorBrush(accentC);
            }
            else
            {
                child.Background = ThemeManager.GetBrush("ThBgPanel");
                child.BorderBrush = ThemeManager.GetBrush("ThBorder");
            }
        }
    }

    private static Border MakeChip(string text, string? colorKey = null)
    {
        var fg = colorKey is not null
            ? ThemeManager.GetBrush(colorKey)
            : ThemeManager.GetBrush("ThTextMuted");

        return new Border
        {
            Background = ThemeManager.GetBrush("ThBgSurface"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 5, 0),
            Child = new TextBlock
            {
                Text = text, FontSize = 10,
                Foreground = fg,
                FontFamily = new FontFamily("/Assets/Fonts/#Geist")
            }
        };
    }

    // ── Release bar ────────────────────────────────────────────────────────
    private bool _examReleased;
    private bool _allGraded;
    private Guid _currentExamId;

    private void UpdateReleaseBar(LecturerExamDto exam)
    {
        ReleaseBar.Visibility = Visibility.Visible;
        ReleaseBar.Tag = exam.Id;

        if (exam.ResultsReleased)
        {
            TxtReleaseInfo.Text = "Students can view their grades for this exam.";
            BtnRelease.Visibility = Visibility.Collapsed;
            ReleasedBadge.Visibility = Visibility.Visible;
        }
        else
        {
            BtnRelease.Visibility = Visibility.Visible;
            ReleasedBadge.Visibility = Visibility.Collapsed;
            RefreshReleaseButtonState();
        }
    }

    private void RefreshReleaseButtonState()
    {
        if (_allGraded)
        {
            TxtReleaseInfo.Text = "All submissions graded. Ready to release.";
            BtnRelease.IsEnabled = true;
        }
        else
        {
            TxtReleaseInfo.Text = "⚠ Cannot release — not all submissions are graded yet.";
            BtnRelease.IsEnabled = false;
        }
    }

    private async void BtnRelease_Click(object sender, RoutedEventArgs e)
    {
        var examId = (Guid)ReleaseBar.Tag;
        if (_selectedExamIndex < 0 || _selectedExamIndex >= _exams.Count) return;

        var confirm = MessageBox.Show(
            $"Release results for \"{_exams[_selectedExamIndex].Title}\" to all students?\n\n" +
            "This action cannot be undone.",
            "Confirm Release",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnRelease.IsEnabled = false;
        BtnRelease.Content = "Releasing…";

        try
        {
            var (ok, message) = await _svc.ReleaseResultsAsync(examId);
            if (ok)
            {
                _exams[_selectedExamIndex].ResultsReleased = true;
                UpdateReleaseBar(_exams[_selectedExamIndex]);
                TxtStatus.Text = "✓ Results released to students";
                TxtStatus.Foreground = ThemeManager.GetBrush("ThSuccess");
            }
            else
            {
                MessageBox.Show(message ?? "Failed to release results. Please try again.",
                    "Cannot Release Results", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnRelease.IsEnabled = true;
                BtnRelease.Content = "📢  Release Results to Students";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Release Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            BtnRelease.IsEnabled = true;
            BtnRelease.Content = "📢  Release Results to Students";
        }
    }

    // ── Load submissions ───────────────────────────────────────────────────
    private async Task LoadSubmissionsAsync(Guid examId)
    {
        TxtLoading.Text = "Loading submissions…";
        TxtLoading.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;

        try
        {
            var subs = await _svc.GetSubmissionsAsync(examId);
            TxtLoading.Visibility = Visibility.Collapsed;

            if (subs.Count == 0)
            {
                TxtLoading.Text = "No submissions yet for this exam";
                TxtLoading.Visibility = Visibility.Visible;
                TxtStatus.Text = "0 submissions";
                _allGraded = false;
                RefreshReleaseButtonState();
                return;
            }

            TablePanel.Visibility = Visibility.Visible;
            _currentExamId = examId;
            BuildRows(subs, examId);
            var graded = subs.Count(s => s.Score.HasValue);
            _allGraded = graded == subs.Count && subs.Count > 0;
            TxtStatus.Text = $"{subs.Count} submission{(subs.Count == 1 ? "" : "s")} · {graded} graded";
            RefreshReleaseButtonState();
        }
        catch (Exception ex)
        {
            TxtLoading.Text = $"Error: {ex.Message}";
        }
    }

    // ── Table rows (grouped by student) ─────────────────────────────────────
    private void BuildRows(List<SubmissionListItemDto> subs, Guid examId)
    {
        SubRows.Children.Clear();

        var groups = subs.GroupBy(s => s.StudentMatric)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var group in groups)
        {
            var studentSubs = group.OrderBy(s => s.QuestionNumber).ToList();
            var totalScore = studentSubs.Where(s => s.Score.HasValue).Sum(s => s.Score!.Value);
            var totalMax = studentSubs.Sum(s => s.MaxScore);
            var gradedCount = studentSubs.Count(s => s.Score.HasValue);
            var allGraded = gradedCount == studentSubs.Count;
            var noneGraded = gradedCount == 0;

            var statusText = allGraded ? "Graded" : noneGraded ? "Pending" : "Partial";
            var statusColor = allGraded
                ? ThemeManager.GetColor("ThSuccess")
                : noneGraded ? ThemeManager.GetColor("ThWarning")
                : ThemeManager.GetColor("ThInfo");

            var studentRow = new Border
            {
                BorderBrush = ThemeManager.GetBrush("ThBorder"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 10, 16, 10),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var sGrid = new Grid();
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.3, GridUnitType.Star) });
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var arrow = new TextBlock
            {
                Text = "▶", FontSize = 10,
                Foreground = ThemeManager.GetBrush("ThTextSecondary"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(arrow, 0);
            sGrid.Children.Add(arrow);

            var matricTb = MakeCell(group.Key, 1);
            matricTb.FontWeight = FontWeights.SemiBold;
            sGrid.Children.Add(matricTb);

            sGrid.Children.Add(MakeCell($"{studentSubs.Count} question{(studentSubs.Count == 1 ? "" : "s")}", 2));
            sGrid.Children.Add(MakeCell(gradedCount > 0 ? $"{totalScore}/{totalMax}" : "—", 3));

            var statusTb = new TextBlock
            {
                Text = statusText, FontSize = 12,
                Foreground = new SolidColorBrush(statusColor),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusTb, 4);
            sGrid.Children.Add(statusTb);

            studentRow.Child = sGrid;

            // Action button
            var gradeAction = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var examTitle = _exams[_selectedExamIndex].Title;
            var examCourse = _exams[_selectedExamIndex].CourseCode;
            var gradeBtn = new Button
            {
                Content = allGraded ? "Review" : "Grade",
                Background = allGraded
                    ? ThemeManager.GetBrush("ThBgSurface")
                    : ThemeManager.GetBrush("ThAccent"),
                Foreground = allGraded
                    ? ThemeManager.GetBrush("ThTextSecondary")
                    : ThemeManager.GetBrush("ThTextOnAccent"),
                Style = FindResource("SmallBtn") as Style,
                Tag = group.Key
            };
            gradeBtn.Click += (_, _) =>
                OpenGradingRequested?.Invoke(examId, group.Key, $"{examCourse} — {examTitle}", studentSubs);
            gradeAction.Children.Add(gradeBtn);

            // Hover
            var accentC = ThemeManager.GetColor("ThAccent");
            studentRow.MouseEnter += (_, _) => studentRow.Background =
                new SolidColorBrush(Color.FromArgb(0x15, accentC.R, accentC.G, accentC.B));
            studentRow.MouseLeave += (_, _) => studentRow.Background = Brushes.Transparent;

            Grid.SetColumn(gradeAction, 5);
            sGrid.Children.Add(gradeAction);

            SubRows.Children.Add(studentRow);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static TextBlock MakeCell(string text, int column)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = 13,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(tb, column);
        return tb;
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text, FontSize = 12,
        Foreground = ThemeManager.GetBrush("ThTextSecondary"),
        FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static TextBox MakeInput(string value = "") => new()
    {
        Text = value, Height = 40, FontSize = 13,
        FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
        Background = ThemeManager.GetBrush("ThBgInput"),
        Foreground = ThemeManager.GetBrush("ThTextPrimary"),
        BorderBrush = ThemeManager.GetBrush("ThBorder"),
        Margin = new Thickness(0, 0, 0, 12),
        Padding = new Thickness(10, 0, 10, 0),
        VerticalContentAlignment = VerticalAlignment.Center
    };
}
