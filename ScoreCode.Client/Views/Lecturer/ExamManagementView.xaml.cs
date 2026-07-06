using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Lecturer;

namespace ScoreCode.Client.Views.Lecturer;

public partial class ExamManagementView : UserControl
{
    private readonly LecturerClientService _svc;
    private readonly ApiService _api;

    /// <summary>Raised when the user wants to open the exam editor for a specific exam.</summary>
    public event Action<Guid>? OpenExamEditorRequested;

    // ── State ─────────────────────────────────────────────────────────────
    private List<LecturerExamDto> _allExams = [];
    private List<LecturerExamDto> _filteredExams = [];
    private string _activeFilter = "All";
    private string _searchText = "";

    // ── Pagination ────────────────────────────────────────────────────────
    private const int PageSize = 8;
    private int _currentPage = 1;
    private int _totalPages = 1;

    public ExamManagementView(ApiService api)
    {
        InitializeComponent();
        _api = api;
        _svc = new LecturerClientService(api);
        Loaded += async (_, _) => await LoadExamsAsync();
    }

    // ── Load ───────────────────────────────────────────────────────────────
    private async Task LoadExamsAsync()
    {
        TxtLoading.Visibility = Visibility.Visible;
        ExamTable.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        PaginationBar.Visibility = Visibility.Collapsed;

        try
        {
            _allExams = await _svc.GetExamsAsync();
            TxtLoading.Visibility = Visibility.Collapsed;

            if (_allExams.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                TxtStatus.Text = "No exams";
                return;
            }

            ApplyFilterAndSearch();
        }
        catch (Exception ex)
        {
            TxtLoading.Text = $"Error: {ex.Message}";
        }
    }

    // ── Filter + Search ───────────────────────────────────────────────────
    private void TxtSearch_Changed(object sender, TextChangedEventArgs e)
    {
        _searchText = TxtSearch.Text.Trim();
        _currentPage = 1;
        ApplyFilterAndSearch();
    }

    private void FilterBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        _activeFilter = tag;
        _currentPage = 1;

        // Update button visuals
        foreach (var filter in new[] { BtnAll, BtnDraft, BtnActive, BtnClosed })
        {
            var isActive = (string)filter.Tag == _activeFilter;
            filter.Background = isActive
                ? ThemeManager.GetBrush("ThAccent")
                : ThemeManager.GetBrush("ThBgSurface");
            filter.Foreground = isActive
                ? ThemeManager.GetBrush("ThTextOnAccent")
                : ThemeManager.GetBrush("ThTextSecondary");
        }

        ApplyFilterAndSearch();
    }

    private void ApplyFilterAndSearch()
    {
        var filtered = _allExams.AsEnumerable();

        // Status filter
        if (_activeFilter != "All")
            filtered = filtered.Where(e => e.Status.Equals(_activeFilter, StringComparison.OrdinalIgnoreCase));

        // Text search
        if (!string.IsNullOrEmpty(_searchText))
        {
            var q = _searchText.ToLowerInvariant();
            filtered = filtered.Where(e =>
                e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.CourseCode.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        _filteredExams = filtered.OrderByDescending(e => e.ScheduledAt).ToList();
        _totalPages = Math.Max(1, (int)Math.Ceiling(_filteredExams.Count / (double)PageSize));
        if (_currentPage > _totalPages) _currentPage = _totalPages;

        RenderPage();
    }

    // ── Pagination Logic ──────────────────────────────────────────────────
    private void RenderPage()
    {
        if (_filteredExams.Count == 0)
        {
            ExamTable.Visibility = Visibility.Collapsed;
            PaginationBar.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            TxtStatus.Text = _activeFilter == "All" && string.IsNullOrEmpty(_searchText)
                ? "No exams" : "No exams match the filter";
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        ExamTable.Visibility = Visibility.Visible;

        var pageItems = _filteredExams
            .Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        BuildExamRows(pageItems);

        // Pagination bar
        if (_totalPages > 1)
        {
            PaginationBar.Visibility = Visibility.Visible;
            BuildPaginationButtons();
            var start = (_currentPage - 1) * PageSize + 1;
            var end = Math.Min(_currentPage * PageSize, _filteredExams.Count);
            TxtPageInfo.Text = $"Showing {start}–{end}";
            TxtTotalInfo.Text = $"{_filteredExams.Count} exam{(_filteredExams.Count == 1 ? "" : "s")} total";
        }
        else
        {
            PaginationBar.Visibility = Visibility.Collapsed;
        }

        TxtStatus.Text = $"{_filteredExams.Count} exam{(_filteredExams.Count == 1 ? "" : "s")}" +
            (_activeFilter != "All" ? $" · {_activeFilter}" : "");
    }

    private void BuildPaginationButtons()
    {
        PageButtons.Children.Clear();

        // Previous
        var prev = MakePageButton("‹", _currentPage > 1);
        prev.Click += (_, _) => { _currentPage--; RenderPage(); };
        PageButtons.Children.Add(prev);

        // Page numbers (show max 5 around current)
        var startPage = Math.Max(1, _currentPage - 2);
        var endPage = Math.Min(_totalPages, startPage + 4);
        if (endPage - startPage < 4) startPage = Math.Max(1, endPage - 4);

        for (int p = startPage; p <= endPage; p++)
        {
            var page = p;
            var btn = MakePageButton(p.ToString(), true, p == _currentPage);
            btn.Click += (_, _) => { _currentPage = page; RenderPage(); };
            PageButtons.Children.Add(btn);
        }

        // Next
        var next = MakePageButton("›", _currentPage < _totalPages);
        next.Click += (_, _) => { _currentPage++; RenderPage(); };
        PageButtons.Children.Add(next);
    }

    private Button MakePageButton(string text, bool enabled, bool isActive = false)
    {
        return new Button
        {
            Content = text,
            Style = FindResource("PageBtn") as Style,
            IsEnabled = enabled,
            Margin = new Thickness(2, 0, 2, 0),
            Background = isActive
                ? ThemeManager.GetBrush("ThAccent")
                : ThemeManager.GetBrush("ThBgPanel"),
            Foreground = isActive
                ? ThemeManager.GetBrush("ThTextOnAccent")
                : ThemeManager.GetBrush("ThTextPrimary"),
            FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal
        };
    }

    // ── Table Rows ────────────────────────────────────────────────────────
    private void BuildExamRows(List<LecturerExamDto> exams)
    {
        ExamRows.Children.Clear();

        foreach (var exam in exams)
        {
            var accentColor = exam.Status switch
            {
                "Active" => ThemeManager.GetColor("ThAccent"),
                "Draft"  => ThemeManager.GetColor("ThWarning"),
                _        => ThemeManager.GetColor("ThTextSecondary")
            };

            var row = new Border
            {
                BorderBrush = ThemeManager.GetBrush("ThBorder"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 12, 18, 12),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

            // Title + marks
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = exam.Title,
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.GetBrush("ThTextPrimary"),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = $"{exam.TotalMarks} marks · {exam.DurationMinutes} min",
                FontSize = 11,
                Foreground = ThemeManager.GetBrush("ThTextMuted"),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(titleStack, 0);
            grid.Children.Add(titleStack);

            // Course
            grid.Children.Add(MakeCell(exam.CourseCode, 1));

            // Date
            grid.Children.Add(MakeCell(exam.ScheduledAt.ToLocalTime().ToString("MMM dd, yyyy"), 2));

            // Status badge
            var badge = MakeStatusBadge(exam.Status);
            Grid.SetColumn(badge, 3);
            grid.Children.Add(badge);

            // Questions count
            var qTb = MakeCell(exam.QuestionCount.ToString(), 4);
            qTb.HorizontalAlignment = HorizontalAlignment.Center;
            grid.Children.Add(qTb);

            // Students count
            var sTb = MakeCell(exam.StudentCount.ToString(), 5);
            sTb.HorizontalAlignment = HorizontalAlignment.Center;
            grid.Children.Add(sTb);

            // Actions
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (exam.Status == "Draft")
            {
                actions.Children.Add(MakeActionPill("▶ Publish", ThemeManager.GetColor("ThAccent"),
                    ThemeManager.GetColor("ThTextOnAccent"), () => PublishExamAsync(exam.Id)));
                actions.Children.Add(MakeActionPill("✕ Delete", ThemeManager.GetColor("ThDeleteBg"),
                    ThemeManager.GetColor("ThError"), () => DeleteExamAsync(exam.Id)));
            }
            else if (exam.Status == "Active")
            {
                actions.Children.Add(MakeActionPill("⏹ Close", ThemeManager.GetColor("ThWarningBg"),
                    ThemeManager.GetColor("ThWarning"), () => CloseExamAsync(exam.Id)));
            }

            // Edit button (pencil icon with tooltip)
            var editBtn = new Button
            {
                ToolTip = "Edit",
                Width = 30, Height = 28,
                Background = ThemeManager.GetBrush("ThBgSurface"),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var pencilPath = new System.Windows.Shapes.Path
            {
                Data = (System.Windows.Media.Geometry)FindResource("Icon.Pencil"),
                Stroke = ThemeManager.GetBrush("ThTextSecondary"),
                StrokeThickness = 2,
                Width = 13, Height = 13,
                Stretch = Stretch.Uniform
            };
            editBtn.Content = pencilPath;
            editBtn.Click += (_, ev) => { ev.Handled = true; OpenExamEditorRequested?.Invoke(exam.Id); };
            actions.Children.Add(editBtn);

            Grid.SetColumn(actions, 6);
            grid.Children.Add(actions);

            row.Child = grid;

            // Click entire row → open editor
            row.MouseLeftButtonDown += (_, e) =>
            {
                if (e.OriginalSource is Button) return; // don't double-fire
                OpenExamEditorRequested?.Invoke(exam.Id);
            };

            // Hover
            row.MouseEnter += (_, _) =>
            {
                row.Background = new SolidColorBrush(Color.FromArgb(0x12, accentColor.R, accentColor.G, accentColor.B));
            };
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

            ExamRows.Children.Add(row);
        }
    }

    // ── Actions ────────────────────────────────────────────────────────────
    private async void PublishExamAsync(Guid examId)
    {
        var result = MessageBox.Show(
            "Publish this exam? Students will be able to see it.",
            "Publish Exam", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        var ok = await _svc.PublishExamAsync(examId);
        if (ok) await LoadExamsAsync();
        else MessageBox.Show("Failed to publish. Exam may need at least one question.",
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private async void CloseExamAsync(Guid examId)
    {
        var result = MessageBox.Show("Close this exam? No more submissions will be accepted.",
            "Close Exam", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        var ok = await _svc.CloseExamAsync(examId);
        if (ok) await LoadExamsAsync();
    }

    private async void DeleteExamAsync(Guid examId)
    {
        var result = MessageBox.Show("Delete this exam? This cannot be undone.",
            "Delete Exam", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        var ok = await _svc.DeleteExamAsync(examId);
        if (ok) await LoadExamsAsync();
    }

    // ── Create Exam Dialog ─────────────────────────────────────────────────
    private void BtnCreateExam_Click(object sender, RoutedEventArgs e) =>
        ShowCreateExamDialog();

    private async void ShowCreateExamDialog()
    {
        var dialog = new Window
        {
            Title = "ScoreCode · New Exam",
            Width = 480, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = ThemeManager.GetBrush("ThBgBase")
        };
        Helpers.TitleBarHelper.ApplyDarkMode(dialog);
        Helpers.TitleBarHelper.SetCaptionColor(dialog, 0x0D, 0x11, 0x17);
        Helpers.TitleBarHelper.SetIcon(dialog);

        var form = new StackPanel { Margin = new Thickness(24) };
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = form
        };

        form.Children.Add(MakeLabel("Exam Title"));
        var txtTitle = MakeInput(); form.Children.Add(txtTitle);

        form.Children.Add(MakeLabel("Course Code"));
        var txtCode = MakeInput(); form.Children.Add(txtCode);

        form.Children.Add(MakeLabel("Scheduled Date & Time (yyyy-MM-dd HH:mm)"));
        var txtDate = MakeInput(DateTime.Now.AddDays(7).ToString("yyyy-MM-dd HH:mm"));
        form.Children.Add(txtDate);

        form.Children.Add(MakeLabel("Duration (minutes)"));
        var defaultDuration = Services.UserPreferences.GetInt("DefaultDurationMinutes", 60).ToString();
        var txtDuration = MakeInput(defaultDuration); form.Children.Add(txtDuration);

        form.Children.Add(MakeLabel("Languages (comma-separated)"));
        var defaultLangs = Services.UserPreferences.GetString("DefaultLanguages", "Python,JavaScript");
        var txtLangs = MakeInput(defaultLangs); form.Children.Add(txtLangs);

        var chkIntelli = new CheckBox
        {
            Content = "Enable IntelliSense",
            IsChecked = Services.UserPreferences.GetBool("DefaultIntelliSense", true),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            FontSize = 13, Margin = new Thickness(0, 0, 0, 12)
        };
        form.Children.Add(chkIntelli);

        var btnCreate = new Button
        {
            Content = "Create Exam",
            Style = FindResource("ActionBtn") as Style,
            Height = 42, Margin = new Thickness(0, 8, 0, 0)
        };

        var errorTxt = new TextBlock
        {
            Foreground = ThemeManager.GetBrush("ThError"),
            FontSize = 12, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        };

        btnCreate.Click += async (_, _) =>
        {
            errorTxt.Text = "";
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || string.IsNullOrWhiteSpace(txtCode.Text))
            {
                errorTxt.Text = "Title and Course Code are required.";
                return;
            }
            if (!DateTime.TryParse(txtDate.Text, out var scheduledAt))
            {
                errorTxt.Text = "Invalid date format.";
                return;
            }
            if (!int.TryParse(txtDuration.Text, out var duration) || duration <= 0)
            {
                errorTxt.Text = "Duration must be a positive number.";
                return;
            }

            btnCreate.IsEnabled = false;
            var langs = txtLangs.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var req = new CreateExamRequest
            {
                Title = txtTitle.Text.Trim(),
                CourseCode = txtCode.Text.Trim(),
                ScheduledAt = scheduledAt,
                DurationMinutes = duration,
                Languages = langs,
                IntelliSenseEnabled = chkIntelli.IsChecked == true,
                GradingMode = "Manual"
            };

            var result = await _svc.CreateExamAsync(req);
            if (result is not null)
            {
                dialog.Close();
                await LoadExamsAsync();
                OpenExamEditorRequested?.Invoke(result.Id);
            }
            else
            {
                errorTxt.Text = "Failed to create exam.";
                btnCreate.IsEnabled = true;
            }
        };

        form.Children.Add(btnCreate);
        form.Children.Add(errorTxt);

        dialog.KeyDown += (_, ke) => { if (ke.Key == System.Windows.Input.Key.Enter) btnCreate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };

        dialog.Content = scrollViewer;
        dialog.ShowDialog();
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

    private static Border MakeStatusBadge(string status)
    {
        var (bgKey, fgKey) = status switch
        {
            "Draft"  => ("ThWarningBg", "ThWarning"),
            "Active" => ("ThAccent", "ThTextOnAccent"),
            _        => ("ThBgSurface", "ThTextSecondary")
        };

        var badge = new Border
        {
            Background = ThemeManager.GetBrush(bgKey),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = status.ToUpper(), FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.GetBrush(fgKey),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist")
            }
        };
        return badge;
    }

    private static Button MakeActionPill(string text, Color bg, Color fg, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            FontSize = 11,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(bg),
            Foreground = new SolidColorBrush(fg),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 4, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btn.Click += (_, e) => { e.Handled = true; onClick(); };
        return btn;
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
