using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Lecturer;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Client.Views.Lecturer;

public partial class ExamEditorView : UserControl
{
    private readonly LecturerClientService _svc;
    private readonly Guid _examId;
    private LecturerExamDto? _exam;

    /// <summary>Date + time input; created on load once the stored value is known.</summary>
    private Helpers.DateTimeField? _dateField;
    private DateTime _loadedScheduledAt;

    /// <summary>Raised when user clicks Back.</summary>
    public event Action? BackRequested;

    public ExamEditorView(ApiService api, Guid examId)
    {
        InitializeComponent();
        _svc = new LecturerClientService(api);
        _examId = examId;
        Loaded += async (_, _) => await LoadAsync();
    }

    // ── Load ───────────────────────────────────────────────────────────────
    private async Task LoadAsync()
    {
        _exam = await _svc.GetExamAsync(_examId);
        if (_exam is null) { TxtTitle.Text = "Exam not found"; return; }

        TxtTitle.Text = $"{_exam.CourseCode} — {_exam.Title}";

        // Populate metadata form
        TxtExamTitle.Text = _exam.Title;
        TxtExamCode.Text = _exam.CourseCode;
        _loadedScheduledAt = _exam.ScheduledAt.ToLocalTime();
        _dateField = new Helpers.DateTimeField(_loadedScheduledAt);
        ExamDateHost.Content = _dateField.Root;
        TxtExamDuration.Text = _exam.DurationMinutes.ToString();
        TxtExamLangs.Text = string.Join(", ", _exam.Languages);
        ChkIntelli.IsChecked = _exam.IntelliSenseEnabled;

        // Status badge
        var (badgeColor, badgeText) = _exam.Status switch
        {
            "Draft" => ("#F6AD55", "DRAFT"),
            "Active" => ("#68D391", "ACTIVE"),
            "Closed" => ("#5C6B88", "CLOSED"),
            _ => ("#5C6B88", _exam.Status.ToUpper())
        };
        TxtStatusBadge.Text = badgeText;
        TxtStatusBadge.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(badgeColor));

        // Draft-only editing
        var isDraft = _exam.Status == "Draft";
        if (!isDraft)
        {
            LockBanner.Visibility = Visibility.Visible;
            TxtExamTitle.IsReadOnly = true;
            TxtExamCode.IsReadOnly = true;
            // The date field is a picker, not a text box — disable it outright.
            if (_dateField is not null) _dateField.Root.IsEnabled = false;
            TxtExamDuration.IsReadOnly = true;
            TxtExamLangs.IsReadOnly = true;
            ChkIntelli.IsEnabled = false;
            BtnAddQuestion.Visibility = Visibility.Collapsed;
            BtnAssignStudents.Visibility = Visibility.Collapsed;

            // Make readonly fields visually muted
            var muted = ThemeManager.GetBrush("ThTextMuted");
            TxtExamTitle.Foreground = muted;
            TxtExamCode.Foreground = muted;
            TxtExamDuration.Foreground = muted;
            TxtExamLangs.Foreground = muted;
        }
        else
        {
            BtnSaveExam.Visibility = Visibility.Visible;
        }

        await LoadQuestionsAsync();
        await LoadStudentsAsync();
    }

    private async void BtnSaveExam_Click(object sender, RoutedEventArgs e)
    {
        if (_exam is null || _exam.Status != "Draft") return;

        if (string.IsNullOrWhiteSpace(TxtExamTitle.Text) || string.IsNullOrWhiteSpace(TxtExamCode.Text))
        {
            MessageBox.Show("Title and Course Code are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_dateField is null || !_dateField.TryGetValue(out var scheduledAt))
        {
            MessageBox.Show("Pick a scheduled date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Only block past dates when the lecturer actually moved the schedule —
        // an exam that has already run must stay editable.
        if (scheduledAt != _loadedScheduledAt && scheduledAt <= DateTime.Now)
        {
            MessageBox.Show("The scheduled time must be in the future.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(TxtExamDuration.Text, out var duration) || duration <= 0)
        {
            MessageBox.Show("Duration must be a positive number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnSaveExam.IsEnabled = false;
        BtnSaveExam.Content = "Saving…";

        var langs = TxtExamLangs.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var req = new UpdateExamRequest
        {
            Title = TxtExamTitle.Text.Trim(),
            CourseCode = TxtExamCode.Text.Trim(),
            ScheduledAt = scheduledAt,
            DurationMinutes = duration,
            Languages = langs,
            IntelliSenseEnabled = ChkIntelli.IsChecked == true
        };

        var result = await _svc.UpdateExamAsync(_examId, req);
        if (result is not null)
        {
            _exam = result;
            TxtTitle.Text = $"{_exam.CourseCode} — {_exam.Title}";
            MessageBox.Show("Exam details saved.", "✓ Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Failed to save changes.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        BtnSaveExam.IsEnabled = true;
        BtnSaveExam.Content = "Save Changes";
    }

    // ── Questions ──────────────────────────────────────────────────────────
    private async Task LoadQuestionsAsync()
    {
        QuestionList.Children.Clear();
        var questions = await _svc.GetQuestionsAsync(_examId);

        if (questions.Count == 0)
        {
            QuestionList.Children.Add(new TextBlock
            {
                Text = "No questions added yet",
                FontSize = 13, Foreground = Brushes.Gray,
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var q in questions)
        {
            var card = new Border
            {
                Background = ThemeManager.GetBrush("ThBgSurface"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            if (_exam?.Status == "Draft")
            {
                card.Cursor = System.Windows.Input.Cursors.Hand;
                card.MouseLeftButtonDown += (_, _) => ShowEditQuestionDialog(q);
                card.MouseEnter += (_, _) => card.Background = ThemeManager.GetBrush("ThBgHover");
                card.MouseLeave += (_, _) => card.Background = ThemeManager.GetBrush("ThBgSurface");
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: question info
            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text = $"Q{q.OrderIndex + 1} · {q.Language} · {q.Marks} marks",
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.GetBrush("ThAccent"),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist")
            });
            info.Children.Add(new TextBlock
            {
                Text = q.Text.Length > 120 ? q.Text[..120] + "…" : q.Text,
                FontSize = 13,
                Foreground = ThemeManager.GetBrush("ThTextPrimary"),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

            // Show "click to edit" hint for Draft
            if (_exam?.Status == "Draft")
            {
                info.Children.Add(new TextBlock
                {
                    Text = "Click to edit",
                    FontSize = 10,
                    Foreground = ThemeManager.GetBrush("ThTextMuted"),
                    FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            // Right: delete button (draft only)
            if (_exam?.Status == "Draft")
            {
                var deleteBtn = new Button
                {
                    Content = "✕",
                    Background = new SolidColorBrush(ThemeManager.GetColor("ThDeleteBg")),
                    Foreground = ThemeManager.GetBrush("ThError"),
                    Style = FindResource("SmallBtn") as Style,
                    Tag = q.Id, VerticalAlignment = VerticalAlignment.Top
                };
                deleteBtn.Click += async (s, ev) =>
                {
                    ev.Handled = true;
                    var ok = await _svc.DeleteQuestionAsync(_examId, q.Id);
                    if (ok) await LoadQuestionsAsync();
                };
                Grid.SetColumn(deleteBtn, 1);
                grid.Children.Add(deleteBtn);
            }

            card.Child = grid;
            QuestionList.Children.Add(card);
        }
    }

    // ── Edit Question Dialog ──────────────────────────────────────────────
    private async void ShowEditQuestionDialog(LecturerQuestionDto q)
    {
        if (_exam?.Status != "Draft") return;

        var dialog = new Window
        {
            Title = "ScoreCode · Edit Question",
            Width = 520, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = ThemeManager.GetBrush("ThBgBase")
        };
        Helpers.TitleBarHelper.ApplyDarkMode(dialog);
        Helpers.TitleBarHelper.SetCaptionColor(dialog, 0x0D, 0x11, 0x17);
        Helpers.TitleBarHelper.SetIcon(dialog);

        var form = new StackPanel { Margin = new Thickness(24) };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = form };

        form.Children.Add(MakeLabel("Question Text"));
        var txtText = new TextBox
        {
            Text = q.Text,
            Height = 100, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            FontSize = 13, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(10, 8, 10, 8)
        };
        form.Children.Add(txtText);

        form.Children.Add(MakeLabel("Marks"));
        var txtMarks = MakeInput(q.Marks.ToString()); form.Children.Add(txtMarks);

        form.Children.Add(MakeLabel("Language"));
        var cmbLang = new ComboBox
        {
            Height = 40, FontSize = 13,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        foreach (var lang in _exam?.Languages ?? [])
            cmbLang.Items.Add(lang);
        cmbLang.SelectedItem = q.Language;
        if (cmbLang.SelectedIndex < 0 && cmbLang.Items.Count > 0)
            cmbLang.SelectedIndex = 0;
        form.Children.Add(cmbLang);

        form.Children.Add(MakeLabel("Marking Guide (optional)"));
        var txtGuide = new TextBox
        {
            Text = q.MarkingGuide,
            Height = 70, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            FontSize = 13, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 16), Padding = new Thickness(10, 8, 10, 8)
        };
        form.Children.Add(txtGuide);

        var btnSave = new Button
        {
            Content = "Save Changes",
            Background = ThemeManager.GetBrush("ThAccent"),
            Foreground = ThemeManager.GetBrush("ThBgBase"),
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Padding = new Thickness(20, 12, 20, 12),
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        btnSave.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(txtText.Text))
            {
                MessageBox.Show("Question text is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtMarks.Text, out var marks) || marks <= 0)
            {
                MessageBox.Show("Marks must be a positive number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnSave.IsEnabled = false;
            btnSave.Content = "Saving…";

            var req = new CreateQuestionRequest
            {
                Text = txtText.Text,
                Marks = marks,
                Language = cmbLang.SelectedItem?.ToString() ?? q.Language,
                MarkingGuide = txtGuide.Text,
                OrderIndex = q.OrderIndex
            };

            var result = await _svc.UpdateQuestionAsync(_examId, q.Id, req);
            if (result is not null)
            {
                dialog.Close();
                await LoadQuestionsAsync();
            }
            else
            {
                btnSave.IsEnabled = true;
                btnSave.Content = "Save Changes";
                MessageBox.Show("Failed to update question.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        form.Children.Add(btnSave);
        dialog.Content = scroll;
        dialog.ShowDialog();
    }

    // ── Add Question Dialog ────────────────────────────────────────────────
    private void BtnAddQuestion_Click(object sender, RoutedEventArgs e)
    {
        if (_exam?.Status != "Draft")
        {
            MessageBox.Show("Questions can only be added to draft exams.",
                "Not Allowed", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ShowAddQuestionDialog();
    }

    private async void ShowAddQuestionDialog()
    {
        var dialog = new Window
        {
            Title = "ScoreCode · Add Question",
            Width = 520, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = ThemeManager.GetBrush("ThBgBase")
        };
        Helpers.TitleBarHelper.ApplyDarkMode(dialog);
        Helpers.TitleBarHelper.SetCaptionColor(dialog, 0x0D, 0x11, 0x17);
        Helpers.TitleBarHelper.SetIcon(dialog);

        var form = new StackPanel { Margin = new Thickness(24) };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = form };

        form.Children.Add(MakeLabel("Question Text"));
        var txtText = new TextBox
        {
            Height = 100, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            FontSize = 13, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(10, 8, 10, 8)
        };
        form.Children.Add(txtText);

        form.Children.Add(MakeLabel("Marks"));
        var txtMarks = MakeInput("10"); form.Children.Add(txtMarks);

        form.Children.Add(MakeLabel("Language"));
        var cmbLang = new ComboBox
        {
            Height = 40, FontSize = 13,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        foreach (var lang in new[] { "Python", "Java", "C++", "JavaScript", "HTML", "CSS" })
            cmbLang.Items.Add(lang);
        cmbLang.SelectedIndex = 0;
        form.Children.Add(cmbLang);

        form.Children.Add(MakeLabel("Marking Guide"));
        var txtGuide = new TextBox
        {
            Height = 80, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            FontSize = 13, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(10, 8, 10, 8)
        };
        form.Children.Add(txtGuide);

        var btnAdd = new Button
        {
            Content = "Add Question",
            Style = FindResource("ActionBtn") as Style,
            Height = 42, Margin = new Thickness(0, 8, 0, 0)
        };

        var errorTxt = new TextBlock
        {
            Foreground = ThemeManager.GetBrush("ThError"),
            FontSize = 12, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        };

        // Get current question count for OrderIndex
        var questions = await _svc.GetQuestionsAsync(_examId);

        btnAdd.Click += async (_, _) =>
        {
            errorTxt.Text = "";
            if (string.IsNullOrWhiteSpace(txtText.Text))
            { errorTxt.Text = "Question text is required."; return; }
            if (!int.TryParse(txtMarks.Text, out var marks) || marks <= 0)
            { errorTxt.Text = "Marks must be a positive number."; return; }

            btnAdd.IsEnabled = false;
            var req = new CreateQuestionRequest
            {
                Text = txtText.Text.Trim(),
                Marks = marks,
                Language = cmbLang.SelectedItem?.ToString() ?? "Python",
                MarkingGuide = txtGuide.Text.Trim(),
                OrderIndex = questions.Count
            };

            var result = await _svc.AddQuestionAsync(_examId, req);
            if (result is not null)
            {
                dialog.Close();
                await LoadQuestionsAsync();
            }
            else
            {
                errorTxt.Text = "Failed to add question.";
                btnAdd.IsEnabled = true;
            }
        };

        form.Children.Add(btnAdd);
        form.Children.Add(errorTxt);
        dialog.Content = scroll;
        dialog.ShowDialog();
    }

    // ── Students ──────────────────────────────────────────────────────────
    private async Task LoadStudentsAsync()
    {
        StudentList.Children.Clear();
        var students = await _svc.GetStudentsAsync(_examId);
        TxtStudentCount.Text = $"{students.Count} assigned";

        if (students.Count == 0)
        {
            StudentList.Children.Add(new TextBlock
            {
                Text = "No students assigned",
                FontSize = 13, Foreground = Brushes.Gray,
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var s in students)
        {
            var row = new Border
            {
                BorderBrush = ThemeManager.GetBrush("ThBorder"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 8, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Orientation = Orientation.Horizontal };
            info.Children.Add(new TextBlock
            {
                Text = s.MatricNumber, FontSize = 13,
                Foreground = ThemeManager.GetBrush("ThTextPrimary"),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                VerticalAlignment = VerticalAlignment.Center
            });
            if (!string.IsNullOrEmpty(s.SessionStatus))
            {
                info.Children.Add(new Border
                {
                    Background = ThemeManager.GetBrush("ThBgSurface"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = s.SessionStatus, FontSize = 10,
                        Foreground = ThemeManager.GetBrush("ThTextSecondary"),
                        FontFamily = new FontFamily("/Assets/Fonts/#Geist")
                    }
                });
            }
            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            // Remove button
            var removeBtn = new Button
            {
                Content = "✕",
                Background = new SolidColorBrush(ThemeManager.GetColor("ThDeleteBg")),
                Foreground = ThemeManager.GetBrush("ThError"),
                Style = FindResource("SmallBtn") as Style,
                VerticalAlignment = VerticalAlignment.Center
            };
            removeBtn.Click += async (_, _) =>
            {
                var ok = await _svc.RemoveStudentAsync(_examId, s.StudentId);
                if (ok) await LoadStudentsAsync();
            };
            Grid.SetColumn(removeBtn, 1);
            grid.Children.Add(removeBtn);

            row.Child = grid;
            StudentList.Children.Add(row);
        }
    }

    // ── Assign Students Dialog ─────────────────────────────────────────────
    private void BtnAssignStudents_Click(object sender, RoutedEventArgs e) =>
        ShowAssignDialog();

    private async void ShowAssignDialog()
    {
        var dialog = new Window
        {
            Title = "ScoreCode · Assign Students",
            Width = 520, Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = ThemeManager.GetBrush("ThBgBase")
        };
        Helpers.TitleBarHelper.ApplyDarkMode(dialog);
        Helpers.TitleBarHelper.SetCaptionColor(dialog, 0x0D, 0x11, 0x17);
        Helpers.TitleBarHelper.SetIcon(dialog);

        var outer = new StackPanel { Margin = new Thickness(24) };

        // ── Filter row: Programme + Level ──────────────────────────────
        outer.Children.Add(MakeLabel("Filter by Programme & Level"));
        var filterRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

        var cmbProgramme = new ComboBox
        {
            Height = 38, FontSize = 13,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder")
        };
        cmbProgramme.Items.Add("All Programmes");
        foreach (var p in ScoreCode.Shared.Constants.Academic.Programmes)
            cmbProgramme.Items.Add(p);
        cmbProgramme.SelectedIndex = 0;
        Grid.SetColumn(cmbProgramme, 0);
        filterRow.Children.Add(cmbProgramme);

        var cmbLevel = new ComboBox
        {
            Height = 38, FontSize = 13,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder")
        };
        cmbLevel.Items.Add("All Levels");
        foreach (var l in ScoreCode.Shared.Constants.Academic.Levels)
            cmbLevel.Items.Add(l);
        cmbLevel.SelectedIndex = 0;
        Grid.SetColumn(cmbLevel, 2);
        filterRow.Children.Add(cmbLevel);
        outer.Children.Add(filterRow);

        // ── Search row ─────────────────────────────────────────────────
        outer.Children.Add(MakeLabel("Search by matric / name"));
        var txtSearch = MakeInput();
        outer.Children.Add(txtSearch);

        // ── Select All toggle ──────────────────────────────────────────
        var btnSelectAll = new Button
        {
            Content = "✓  Select All",
            FontSize = 12,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Foreground = ThemeManager.GetBrush("ThAccent"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(0)
        };
        outer.Children.Add(btnSelectAll);

        // ── Student list ───────────────────────────────────────────────
        var listPanel = new StackPanel();
        var scroll = new ScrollViewer
        {
            Height = 270,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = listPanel
        };
        outer.Children.Add(scroll);

        // ── State ──────────────────────────────────────────────────────
        var allStudents = new List<UserDto>();
        HashSet<Guid> assignedIds;
        var selectedIds = new HashSet<Guid>();

        var errorTxt = new TextBlock
        {
            Foreground = ThemeManager.GetBrush("ThError"),
            FontSize = 12, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
        };

        // Load assigned students (for de-duplication)
        try
        {
            var assigned = await _svc.GetStudentsAsync(_examId);
            assignedIds = assigned.Select(a => a.StudentId).ToHashSet();
        }
        catch
        {
            assignedIds = [];
        }

        // ── Render helper ──────────────────────────────────────────────
        void RenderStudents(string filter)
        {
            listPanel.Children.Clear();
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? allStudents
                : allStudents.Where(s =>
                    s.IdNumber.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filtered.Count == 0)
            {
                listPanel.Children.Add(new TextBlock
                {
                    Text = allStudents.Count == 0
                        ? "No students found for the selected filters."
                        : "No matches for your search.",
                    FontSize = 12, Foreground = Brushes.Gray,
                    FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                    Margin = new Thickness(0, 12, 0, 0)
                });
                return;
            }

            foreach (var stu in filtered)
            {
                var already = assignedIds.Contains(stu.Id);
                var label = $"{stu.IdNumber} — {stu.Name}";
                if (!string.IsNullOrEmpty(stu.ProgrammeOfStudy))
                    label += $" · {stu.ProgrammeOfStudy}";
                if (!string.IsNullOrEmpty(stu.Level))
                    label += $" (L{stu.Level})";

                var cb = new CheckBox
                {
                    Content = label,
                    FontSize = 12,
                    FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                    Foreground = already
                        ? ThemeManager.GetBrush("ThTextMuted")
                        : ThemeManager.GetBrush("ThTextPrimary"),
                    IsChecked = already || selectedIds.Contains(stu.Id),
                    IsEnabled = !already,
                    Margin = new Thickness(0, 4, 0, 4),
                    Tag = stu.Id
                };
                cb.Checked += (_, _) => selectedIds.Add(stu.Id);
                cb.Unchecked += (_, _) => selectedIds.Remove(stu.Id);
                listPanel.Children.Add(cb);
            }
        }

        // ── Fetch students from API ────────────────────────────────────
        async Task FetchAndRender()
        {
            errorTxt.Text = "";
            listPanel.Children.Clear();
            listPanel.Children.Add(new TextBlock
            {
                Text = "Loading students…", FontSize = 12,
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily("/Assets/Fonts/#Geist")
            });

            try
            {
                var prog = cmbProgramme.SelectedIndex == 0 ? null : cmbProgramme.SelectedItem?.ToString();
                var lvl = cmbLevel.SelectedIndex == 0 ? null : cmbLevel.SelectedItem?.ToString();
                allStudents = await _svc.GetStudentListAsync(prog, lvl);
                RenderStudents(txtSearch.Text);
            }
            catch (Exception ex)
            {
                listPanel.Children.Clear();
                errorTxt.Text = $"Failed to load students: {ex.Message}";
            }
        }

        // Initial load
        await FetchAndRender();

        // Re-fetch on filter change
        cmbProgramme.SelectionChanged += async (_, _) => await FetchAndRender();
        cmbLevel.SelectionChanged += async (_, _) => await FetchAndRender();
        txtSearch.TextChanged += (_, _) => RenderStudents(txtSearch.Text);

        // Select All / Deselect All for currently visible students
        btnSelectAll.Click += (_, _) =>
        {
            var filter = txtSearch.Text;
            var visible = string.IsNullOrWhiteSpace(filter)
                ? allStudents
                : allStudents.Where(s =>
                    s.IdNumber.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            // Only consider students not already assigned
            var selectable = visible.Where(s => !assignedIds.Contains(s.Id)).ToList();
            bool allSelected = selectable.Count > 0 && selectable.All(s => selectedIds.Contains(s.Id));

            if (allSelected)
            {
                // Deselect all visible
                foreach (var s in selectable) selectedIds.Remove(s.Id);
                btnSelectAll.Content = "✓  Select All";
            }
            else
            {
                // Select all visible
                foreach (var s in selectable) selectedIds.Add(s.Id);
                btnSelectAll.Content = "✕  Deselect All";
            }
            RenderStudents(txtSearch.Text);
        };

        // ── Assign button ──────────────────────────────────────────────
        var btnAssign = new Button
        {
            Content = "Assign Selected",
            Style = FindResource("ActionBtn") as Style,
            Height = 42, Margin = new Thickness(0, 12, 0, 0)
        };

        btnAssign.Click += async (_, _) =>
        {
            if (selectedIds.Count == 0) { dialog.Close(); return; }
            btnAssign.IsEnabled = false;
            try
            {
                await _svc.AssignStudentsAsync(_examId, selectedIds.ToList());
                dialog.Close();
                await LoadStudentsAsync();
            }
            catch (Exception ex)
            {
                errorTxt.Text = $"Assignment failed: {ex.Message}";
                btnAssign.IsEnabled = true;
            }
        };

        outer.Children.Add(btnAssign);
        outer.Children.Add(errorTxt);
        dialog.Content = outer;
        dialog.ShowDialog();
    }

    // ── Navigation ─────────────────────────────────────────────────────────
    private void BtnBack_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke();

    // ── Helpers ─────────────────────────────────────────────────────────────
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
