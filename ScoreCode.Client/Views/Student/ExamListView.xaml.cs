using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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

    private const int PageSize = 8;
    private int _page = 1;
    private int _totalPages = 1;
    private int _selected = -1;
    private string _query = "";
    private int _pendingSelect = -2; // -2 none, -1 last, 0 first
    private readonly List<(Border row, TextBlock meta, ExamListItemDto exam, ExamState state)> _pageRows = new();

    public ExamListView(ApiService api)
    {
        InitializeComponent();
        _examService = new ExamClientService(api);

        SearchBox.TextChanged += (_, _) =>
        {
            bool empty = string.IsNullOrEmpty(SearchBox.Text);
            SearchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            ClearBtn.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            _page = 1;
            Render();
        };
        SearchBox.GotKeyboardFocus += (_, _) => SearchBorder.BorderBrush = ThemeManager.GetBrush("ThAccentLine");
        SearchBox.LostKeyboardFocus += (_, _) => SearchBorder.BorderBrush = ThemeManager.GetBrush("ThBorder");
        ClearBtn.MouseLeftButtonUp += (_, _) => { SearchBox.Clear(); SearchBox.Focus(); };

        // Keyboard navigation: ↑/↓ to move, Enter to open, Esc to clear search.
        PreviewKeyDown += OnKeyNav;

        Loaded += async (_, _) => await LoadExamsAsync();
    }

    private void OnKeyNav(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: MoveSelection(+1); e.Handled = true; break;
            case Key.Up: MoveSelection(-1); e.Handled = true; break;
            case Key.Enter: ActivateSelected(); e.Handled = true; break;
            case Key.Escape:
                if (!string.IsNullOrEmpty(SearchBox.Text)) { SearchBox.Clear(); e.Handled = true; }
                break;
        }
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
        pill.MouseLeftButtonUp += (_, _) => { _filter = filter; _page = 1; RebuildFilterBar(); Render(); };
        return pill;
    }

    private void Render()
    {
        ListPanel.Children.Clear();
        _pageRows.Clear();
        _selected = -1;

        if (_allExams.Count == 0)
        {
            ListScroller.Visibility = Visibility.Collapsed;
            PagerHost.Visibility = Visibility.Collapsed;
            EmptyTitle.Text = "No exams assigned yet";
            EmptySubtitle.Text = "Check back when your lecturer publishes an exam";
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        _query = SearchBox.Text?.Trim() ?? "";
        var matches = _allExams
            .Where(e => _filter == ExamFilter.All || CategoryOf(e) == _filter)
            .Where(e => _query.Length == 0
                        || e.Title.Contains(_query, StringComparison.OrdinalIgnoreCase)
                        || e.CourseCode.Contains(_query, StringComparison.OrdinalIgnoreCase))
            // Available first, then Scheduled, then Completed; each by nearest date.
            .OrderBy(e => (int)CategoryOf(e))
            .ThenBy(e => e.ScheduledAt)
            .ToList();

        if (matches.Count == 0)
        {
            ListScroller.Visibility = Visibility.Collapsed;
            PagerHost.Visibility = Visibility.Collapsed;
            EmptyTitle.Text = "No matching exams";
            EmptySubtitle.Text = "Try a different search or filter";
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        ListScroller.Visibility = Visibility.Visible;
        PagerHost.Visibility = Visibility.Visible;

        int total = matches.Count;
        _totalPages = (total + PageSize - 1) / PageSize;
        _page = Math.Clamp(_page, 1, _totalPages);
        int start = (_page - 1) * PageSize;
        var pageItems = matches.Skip(start).Take(PageSize).ToList();

        for (int i = 0; i < pageItems.Count; i++)
            ListPanel.Children.Add(BuildExamRow(pageItems[i], i, i == pageItems.Count - 1));

        BuildPager(total, start, pageItems.Count);
        ListScroller.ScrollToTop();

        // Apply any pending selection carried across a page change.
        if (_pendingSelect == 0) SelectRow(0, scroll: true);
        else if (_pendingSelect == -1) SelectRow(_pageRows.Count - 1, scroll: true);
        _pendingSelect = -2;
    }

    // ── Compact list row ──────────────────────────────────────────────────
    private Border BuildExamRow(ExamListItemDto exam, int index, bool last)
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
        var titleTb = new TextBlock
        {
            FontSize = 14.5, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _font,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
        };
        Highlight(titleTb, exam.Title, _query);
        main.Children.Add(titleTb);
        var course = new TextBlock
        {
            FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _mono, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
        };
        Highlight(course, exam.CourseCode, _query);
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
        _pageRows.Add((row, meta, exam, state));

        // Whole-row click target
        bool clickable = state == ExamState.Active || state == ExamState.Completed;
        if (clickable) row.Cursor = System.Windows.Input.Cursors.Hand;
        row.MouseLeftButtonUp += (_, _) =>
        {
            if (state == ExamState.Active) ExamStartRequested?.Invoke(exam);
            else if (state == ExamState.Completed) NavigateRequested?.Invoke("Results");
        };

        // Hover: highlight row + reveal meta (unless it's the keyboard-selected row)
        int idx = index;
        row.MouseEnter += (_, _) =>
        {
            if (idx != _selected) row.Background = ThemeManager.GetBrush("ThBgSurface");
            meta.Opacity = 1;
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = idx == _selected ? ThemeManager.GetBrush("ThAccentSoft") : System.Windows.Media.Brushes.Transparent;
            meta.Opacity = idx == _selected ? 1 : 0;
        };

        return row;
    }

    // ── Match highlighting ────────────────────────────────────────────────
    private void Highlight(TextBlock tb, string text, string query)
    {
        tb.Inlines.Clear();
        if (string.IsNullOrEmpty(query))
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        var accent = ThemeManager.GetBrush("ThAccentStrong");
        int pos = 0;
        int idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            if (idx > pos) tb.Inlines.Add(new Run(text[pos..idx]));
            tb.Inlines.Add(new Run(text.Substring(idx, query.Length))
            {
                Foreground = accent, FontWeight = FontWeights.SemiBold
            });
            pos = idx + query.Length;
            idx = text.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
        }
        if (pos < text.Length) tb.Inlines.Add(new Run(text[pos..]));
    }

    // ── Keyboard selection ────────────────────────────────────────────────
    private void MoveSelection(int delta)
    {
        if (_pageRows.Count == 0) return;

        if (_selected == -1)
        {
            SelectRow(delta > 0 ? 0 : _pageRows.Count - 1, scroll: true);
            return;
        }

        int next = _selected + delta;
        if (next < 0)
        {
            if (_page > 1) { _page--; _pendingSelect = -1; Render(); }
        }
        else if (next >= _pageRows.Count)
        {
            if (_page < _totalPages) { _page++; _pendingSelect = 0; Render(); }
        }
        else SelectRow(next, scroll: true);
    }

    private void SelectRow(int i, bool scroll)
    {
        if (i < 0 || i >= _pageRows.Count) return;

        if (_selected >= 0 && _selected < _pageRows.Count)
        {
            var old = _pageRows[_selected];
            old.row.Background = System.Windows.Media.Brushes.Transparent;
            old.meta.Opacity = 0;
        }

        _selected = i;
        var cur = _pageRows[i];
        cur.row.Background = ThemeManager.GetBrush("ThAccentSoft");
        cur.meta.Opacity = 1;
        if (scroll) cur.row.BringIntoView();
    }

    private void ActivateSelected()
    {
        if (_selected < 0 || _selected >= _pageRows.Count) return;
        var sel = _pageRows[_selected];
        if (sel.state == ExamState.Active) ExamStartRequested?.Invoke(sel.exam);
        else if (sel.state == ExamState.Completed) NavigateRequested?.Invoke("Results");
    }

    // ── Pager ─────────────────────────────────────────────────────────────
    private void BuildPager(int total, int startIndex, int pageCount)
    {
        PagerBar.Children.Clear();
        int from = startIndex + 1, to = startIndex + pageCount;

        if (_totalPages <= 1)
        {
            RangeLabel.Text = $"{total} exam" + (total == 1 ? "" : "s");
            return;
        }

        RangeLabel.Text = $"Showing {from}–{to} of {total}";
        PagerBar.Children.Add(PagerArrow("‹", _page > 1, () => GoPage(_page - 1)));
        foreach (var p in PageWindow(_page, _totalPages))
            PagerBar.Children.Add(p == -1 ? PagerEllipsis() : PageNumber(p));
        PagerBar.Children.Add(PagerArrow("›", _page < _totalPages, () => GoPage(_page + 1)));
    }

    private void GoPage(int p)
    {
        _page = Math.Clamp(p, 1, _totalPages);
        Render();
    }

    private static IEnumerable<int> PageWindow(int cur, int total)
    {
        if (total <= 7)
        {
            for (int i = 1; i <= total; i++) yield return i;
            yield break;
        }
        yield return 1;
        int lo = Math.Max(2, cur - 1), hi = Math.Min(total - 1, cur + 1);
        if (lo > 2) yield return -1;
        for (int i = lo; i <= hi; i++) yield return i;
        if (hi < total - 1) yield return -1;
        yield return total;
    }

    private Border PageNumber(int p)
    {
        bool active = p == _page;
        var b = new Border
        {
            Background = active ? ThemeManager.GetBrush("ThAccent") : System.Windows.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            MinWidth = 32, Height = 32,
            Margin = new Thickness(2, 0, 2, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = p.ToString(), FontSize = 13, FontFamily = _font,
                FontWeight = active ? FontWeights.Medium : FontWeights.Normal,
                Foreground = active ? ThemeManager.GetBrush("ThTextOnAccent") : ThemeManager.GetBrush("ThTextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        if (!active)
        {
            b.MouseEnter += (_, _) => b.Background = ThemeManager.GetBrush("ThBgHover");
            b.MouseLeave += (_, _) => b.Background = System.Windows.Media.Brushes.Transparent;
            b.MouseLeftButtonUp += (_, _) => GoPage(p);
        }
        return b;
    }

    private Border PagerArrow(string glyph, bool enabled, Action onClick)
    {
        var b = new Border
        {
            Background = System.Windows.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            MinWidth = 32, Height = 32,
            Margin = new Thickness(2, 0, 2, 0),
            Cursor = enabled ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
            Child = new TextBlock
            {
                Text = glyph, FontSize = 15, FontFamily = _font,
                Foreground = ThemeManager.GetBrush(enabled ? "ThTextSecondary" : "ThTextMuted"),
                Opacity = enabled ? 1 : 0.45,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        if (enabled)
        {
            b.MouseEnter += (_, _) => b.Background = ThemeManager.GetBrush("ThBgHover");
            b.MouseLeave += (_, _) => b.Background = System.Windows.Media.Brushes.Transparent;
            b.MouseLeftButtonUp += (_, _) => onClick();
        }
        return b;
    }

    private TextBlock PagerEllipsis() => new()
    {
        Text = "…", FontSize = 14, Foreground = ThemeManager.GetBrush("ThTextMuted"),
        FontFamily = _font, Width = 24, Height = 32,
        TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };

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
