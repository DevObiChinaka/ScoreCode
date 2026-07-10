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
        TxtEyebrow.Text = now.ToString("dddd, dd MMMM").ToUpperInvariant();
        TxtWelcome.Text = $"{greeting}, {displayName}";
        TxtSubtitle.Text = $"Signed in as {idNumber}";

        Loaded += async (_, _) => await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        if (_role == "Student")
        {
            StudentRoot.Visibility = Visibility.Visible;
            GenericRoot.Visibility = Visibility.Collapsed;

            StudentDashboardDto? data = null;
            if (_api is not null)
            {
                try { data = (await _api.GetAsync<StudentDashboardDto>("dashboard/student"))?.Data; }
                catch { data = null; }
            }
            BuildStudentHome(data);
            return;
        }

        // Lecturer / Admin — generic layout
        BuildQuickActions();
        if (_api is null) { BuildStatCards(); return; }
        try
        {
            if (_role == "Lecturer") await LoadLecturerDashboardAsync();
            else BuildStatCards(); // Admin — static for now
        }
        catch
        {
            BuildStatCards();
        }
    }

    // ── Student Dashboard (mock layout) ───────────────────────────────────
    private void BuildStudentHome(StudentDashboardDto? d)
    {
        StudentRoot.Children.Clear();

        // Hero — next / available exam
        UpcomingExamDto? next = d?.UpcomingExams.FirstOrDefault();
        StudentRoot.Children.Add(BuildHeroCard(next));

        // Stat strip: exams completed · average · personal best
        int taken = d?.ExamsTaken ?? 0;
        double avg = d?.AverageScore ?? -1;
        double best = -1;
        if (d is not null)
            foreach (var r in d.RecentResults)
                if (r.MaxScore > 0) best = Math.Max(best, (double)r.Score / r.MaxScore * 100);

        var strip = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Margin = new Thickness(0, 22, 0, 0) };
        strip.Children.Add(MakeStatCard(taken.ToString(), "Exams completed", null));
        strip.Children.Add(MakeStatCard(avg < 0 ? "—" : $"{avg:F0}%", "Average score", ThemeManager.GetBrush("ThSuccess")));
        strip.Children.Add(MakeStatCard(best < 0 ? "—" : $"{best:F0}%", "Personal best", null));
        StudentRoot.Children.Add(strip);

        // Recent results
        StudentRoot.Children.Add(SectionLabel("RECENT RESULTS"));
        if (d is null || d.RecentResults.Count == 0)
        {
            StudentRoot.Children.Add(MakeMutedText("No results released yet."));
        }
        else
        {
            foreach (var r in d.RecentResults)
                StudentRoot.Children.Add(BuildResultCard(r));
        }
    }

    private Border BuildHeroCard(UpcomingExamDto? exam)
    {
        var outer = new Border
        {
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Background = ThemeManager.GetBrush("ThBgPanel"),
            Padding = new Thickness(30, 28, 30, 28)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        if (exam is null)
        {
            left.Children.Add(MakePill("All caught up", "ThSuccess"));
            left.Children.Add(HeroTitle("No exams scheduled"));
            left.Children.Add(new TextBlock
            {
                Text = "You have no exams available right now. This page updates when your lecturer schedules one.",
                FontSize = 14, Foreground = ThemeManager.GetBrush("ThTextSecondary"),
                TextWrapping = TextWrapping.Wrap, MaxWidth = 460, Margin = new Thickness(0, 10, 0, 0),
                FontFamily = Font()
            });
            grid.Children.Add(left);

            var browse = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            browse.Children.Add(HeroButton("Browse exams", () => NavigateRequested?.Invoke("Exams")));
            Grid.SetColumn(browse, 1);
            grid.Children.Add(browse);

            outer.Child = grid;
            return outer;
        }

        bool available = exam.ScheduledAt.ToLocalTime() <= DateTime.Now;
        outer.BorderBrush = ThemeManager.GetBrush("ThAccentLine");

        left.Children.Add(available ? MakePill("Available now", "ThSuccess") : MakePill("Scheduled", "ThWarning"));
        left.Children.Add(HeroTitle(exam.Title));
        left.Children.Add(new TextBlock
        {
            Text = exam.CourseCode, FontSize = 14,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            Margin = new Thickness(0, 6, 0, 0), FontFamily = MonoFont()
        });

        var stats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        stats.Children.Add(HeroStat(exam.DurationMinutes.ToString(), "min", "Duration"));
        stats.Children.Add(HeroStat(
            exam.ScheduledAt.ToLocalTime().ToString("MMM dd"), "",
            (available ? "Opened " : "Opens ") + exam.ScheduledAt.ToLocalTime().ToString("HH:mm")));
        left.Children.Add(stats);
        grid.Children.Add(left);

        var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MinWidth = 190 };
        col.Children.Add(HeroButton(available ? "Begin exam" : "View exam", () => NavigateRequested?.Invoke("Exams")));
        col.Children.Add(new TextBlock
        {
            Text = available ? "Opens in locked-down mode" : "Not yet open",
            FontSize = 11.5, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 9, 0, 0),
            FontFamily = Font()
        });
        Grid.SetColumn(col, 1);
        grid.Children.Add(col);

        outer.Child = grid;
        return outer;
    }

    private Border MakeStatCard(string value, string label, Brush? valueColor)
    {
        var card = new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            CornerRadius = new CornerRadius(13),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 18, 20, 18),
            Margin = new Thickness(0, 0, 14, 0)
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = value, FontSize = 27, FontWeight = FontWeights.Medium,
            Foreground = valueColor ?? ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = MonoFont()
        });
        sp.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12.5,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            Margin = new Thickness(0, 4, 0, 0), FontFamily = Font()
        });
        card.Child = sp;
        return card;
    }

    private Border BuildResultCard(RecentResultDto r)
    {
        double pct = r.MaxScore > 0 ? (double)r.Score / r.MaxScore * 100 : 0;
        var color = pct >= 70 ? ThemeManager.GetColor("ThSuccess")
                  : pct >= 50 ? ThemeManager.GetColor("ThAccentStrong")
                  : ThemeManager.GetColor("ThWarning");
        var grade = pct >= 70 ? "Excellent" : pct >= 60 ? "Good" : pct >= 50 ? "Pass" : "Needs work";

        var card = new Border
        {
            Background = ThemeManager.GetBrush("ThBgSurface"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(16, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 11),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var ring = MakeRing(pct, color, 46);
        Grid.SetColumn(ring, 0);
        grid.Children.Add(ring);

        var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        mid.Children.Add(new TextBlock
        {
            Text = r.ExamTitle, FontSize = 14.5, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = Font(),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        mid.Children.Add(new TextBlock
        {
            Text = $"{r.CourseCode}  ·  {r.SubmittedAt.ToLocalTime():MMM dd, yyyy}",
            FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            Margin = new Thickness(0, 3, 0, 0), FontFamily = MonoFont()
        });
        Grid.SetColumn(mid, 1);
        grid.Children.Add(mid);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        var score = new TextBlock { FontSize = 15, FontWeight = FontWeights.Medium, FontFamily = Font(), HorizontalAlignment = HorizontalAlignment.Right };
        score.Inlines.Add(new System.Windows.Documents.Run(r.Score.ToString()) { Foreground = ThemeManager.GetBrush("ThTextPrimary") });
        score.Inlines.Add(new System.Windows.Documents.Run($"/{r.MaxScore}") { Foreground = ThemeManager.GetBrush("ThTextMuted") });
        right.Children.Add(score);
        right.Children.Add(new TextBlock
        {
            Text = grade, FontSize = 12, FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(color), Margin = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right, FontFamily = Font()
        });
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        card.Child = grid;
        card.MouseLeftButtonUp += (_, _) => NavigateRequested?.Invoke("Results");
        return card;
    }

    // Circular progress ring with centred percentage.
    private Grid MakeRing(double pct, Color color, double size)
    {
        double frac = Math.Max(0, Math.Min(1, pct / 100));
        double stroke = 4;
        double r = size / 2 - stroke / 2 - 1;
        double cx = size / 2, cy = size / 2;

        var g = new Grid { Width = size, Height = size };

        g.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = r * 2, Height = r * 2, StrokeThickness = stroke,
            Stroke = ThemeManager.GetBrush("ThBorderStrong"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (frac >= 0.999)
        {
            g.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = r * 2, Height = r * 2, StrokeThickness = stroke,
                Stroke = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else if (frac > 0)
        {
            double angle = 360 * frac;
            double endRad = (-90 + angle) * Math.PI / 180;
            var start = new Point(cx, cy - r);
            var end = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));
            var fig = new PathFigure { StartPoint = start, IsClosed = false };
            fig.Segments.Add(new ArcSegment
            {
                Point = end, Size = new Size(r, r),
                IsLargeArc = angle > 180, SweepDirection = SweepDirection.Clockwise
            });
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            g.Children.Add(new System.Windows.Shapes.Path
            {
                Data = geo, Stroke = new SolidColorBrush(color), StrokeThickness = stroke,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            });
        }

        g.Children.Add(new TextBlock
        {
            Text = $"{pct:F0}", FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            FontFamily = MonoFont(), Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        });
        return g;
    }

    // ── Hero helpers ──────────────────────────────────────────────────────
    private TextBlock HeroTitle(string text) => new()
    {
        Text = text, FontSize = 26, FontWeight = FontWeights.Medium,
        Foreground = ThemeManager.GetBrush("ThTextPrimary"),
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0),
        FontFamily = Font()
    };

    private Border MakePill(string text, string colorKey)
    {
        var c = ThemeManager.GetColor(colorKey);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x24, c.R, c.G, c.B)),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(11, 4, 11, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = text, FontSize = 12, FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(c), FontFamily = Font()
            }
        };
    }

    private Border HeroButton(string text, Action onClick)
    {
        var b = new Border
        {
            Background = ThemeManager.GetBrush("ThAccent"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(22, 14, 22, 14),
            Cursor = System.Windows.Input.Cursors.Hand,
            MinWidth = 180
        };
        b.Child = new TextBlock
        {
            Text = text, FontSize = 15, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextOnAccent"),
            HorizontalAlignment = HorizontalAlignment.Center, FontFamily = Font()
        };
        b.MouseLeftButtonUp += (_, _) => onClick();
        b.MouseEnter += (_, _) => b.Background = ThemeManager.GetBrush("ThAccentStrong");
        b.MouseLeave += (_, _) => b.Background = ThemeManager.GetBrush("ThAccent");
        return b;
    }

    private StackPanel HeroStat(string value, string unit, string label)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 26, 0) };
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(new TextBlock
        {
            Text = value, FontSize = 20, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = Font()
        });
        if (!string.IsNullOrEmpty(unit))
            line.Children.Add(new TextBlock
            {
                Text = " " + unit, FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"),
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2, 0, 0, 3),
                FontFamily = Font()
            });
        sp.Children.Add(line);
        sp.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            Margin = new Thickness(0, 2, 0, 0), FontFamily = Font()
        });
        return sp;
    }

    private TextBlock SectionLabel(string text) => new()
    {
        Text = text, FontSize = 12.5, FontWeight = FontWeights.Medium,
        Foreground = ThemeManager.GetBrush("ThTextSecondary"),
        Margin = new Thickness(0, 30, 0, 14), FontFamily = Font()
    };

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
            FontSize = 13, FontWeight = FontWeights.Medium,
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
            CornerRadius = new CornerRadius(13),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 18, 20, 18),
            Margin = new Thickness(0, 0, 14, 0),
        };

        var stack = new StackPanel();

        // Value (mono figure)
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 27,
            FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = MonoFont()
        });

        // Title
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12.5,
            Foreground = ThemeManager.GetBrush("ThTextMuted"),
            Margin = new Thickness(0, 4, 0, 0),
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
            CornerRadius = new CornerRadius(13),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 16, 18, 16),
            Margin = new Thickness(0, 0, 14, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
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
        Background = System.Windows.Media.Brushes.Transparent,
        BorderBrush = ThemeManager.GetBrush("ThBorder"),
        BorderThickness = new Thickness(0, 0, 0, 1),
        CornerRadius = new CornerRadius(0),
        Padding = new Thickness(4, 12, 4, 12),
        Margin = new Thickness(0)
    };

    private static Border MakeTag(string text, Color color) => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(0x24, color.R, color.G, color.B)),
        CornerRadius = new CornerRadius(999),
        Padding = new Thickness(10, 3, 10, 3),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text, FontSize = 11, FontWeight = FontWeights.Medium,
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

    private static FontFamily MonoFont() =>
        new("/Assets/Fonts/#Geist Mono, Consolas");
}
