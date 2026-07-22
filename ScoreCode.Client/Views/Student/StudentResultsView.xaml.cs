using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Exams;

namespace ScoreCode.Client.Views.Student;

public partial class StudentResultsView : UserControl
{
    private readonly ApiService _api;

    private static readonly FontFamily _font = new("/Assets/Fonts/#Geist");
    private static readonly FontFamily _mono = new("/Assets/Fonts/#Geist Mono, Consolas");

    private readonly List<ExamResultDto> _results = new();
    private SortMode _sort = SortMode.Recent;

    private enum SortMode { Recent, Highest, Lowest }

    public StudentResultsView(ApiService api)
    {
        InitializeComponent();
        _api = api;
        Loaded += async (_, _) => await LoadResultsAsync();
    }

    private async Task LoadResultsAsync()
    {
        try
        {
            var examService = new ExamClientService(_api);
            var results = await examService.GetStudentResultsAsync();

            TxtLoading.Visibility = Visibility.Collapsed;

            if (results.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                return;
            }

            _results.Clear();
            _results.AddRange(results);

            BuildSummary();
            RebuildSortBar();
            ListHeader.Visibility = Visibility.Visible;
            Render();
        }
        catch (Exception ex)
        {
            TxtLoading.Text = $"Error loading results: {ex.Message}";
            TxtLoading.Foreground = ThemeManager.GetBrush("ThError");
        }
    }

    // ── Grade helpers ─────────────────────────────────────────────────────
    private static double Pct(ExamResultDto e) => e.TotalMaxScore > 0 ? (double)e.TotalScore / e.TotalMaxScore * 100 : 0;
    private static double Pct(QuestionResultDto q) => q.MaxScore > 0 ? (double)q.Score / q.MaxScore * 100 : 0;

    private static Color GradeColor(double pct) => pct >= 70 ? ThemeManager.GetColor("ThSuccess")
                                                 : pct >= 50 ? ThemeManager.GetColor("ThWarning")
                                                 : ThemeManager.GetColor("ThError");

    private static string GradeLabel(double pct) => pct >= 70 ? "Excellent" : pct >= 60 ? "Good" : pct >= 50 ? "Pass" : "Needs work";

    // ── Performance summary ───────────────────────────────────────────────
    private void BuildSummary()
    {
        SummaryHost.Children.Clear();

        int count = _results.Count;
        double avg = _results.Average(Pct);
        double best = _results.Max(Pct);
        double latest = _results.OrderByDescending(r => r.SubmittedAt).First() is { } l ? Pct(l) : 0;

        var card = new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(26, 22, 26, 22)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var ring = MakeRing(avg, GradeColor(avg), 96, 16, big: true);
        ring.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(ring);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(28, 0, 0, 0) };
        right.Children.Add(new TextBlock
        {
            Text = "OVERALL AVERAGE", FontSize = 11, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _mono
        });
        right.Children.Add(new TextBlock
        {
            Text = GradeLabel(avg) + " · across " + count + " graded exam" + (count == 1 ? "" : "s"),
            FontSize = 17, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _font,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var stats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        stats.Children.Add(MiniStat(count.ToString(), "Graded"));
        stats.Children.Add(MiniStat($"{best:F0}%", "Personal best"));
        stats.Children.Add(MiniStat($"{latest:F0}%", "Latest"));
        right.Children.Add(stats);

        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        card.Child = grid;
        SummaryHost.Children.Add(card);
    }

    private StackPanel MiniStat(string value, string label)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 34, 0) };
        sp.Children.Add(new TextBlock
        {
            Text = value, FontSize = 21, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _mono
        });
        sp.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _font, Margin = new Thickness(0, 3, 0, 0)
        });
        return sp;
    }

    // ── Sort control ──────────────────────────────────────────────────────
    private void RebuildSortBar()
    {
        SortBar.Children.Clear();
        SortBar.Children.Add(SortPill("Recent", SortMode.Recent));
        SortBar.Children.Add(SortPill("Highest", SortMode.Highest));
        SortBar.Children.Add(SortPill("Lowest", SortMode.Lowest));
    }

    private Border SortPill(string label, SortMode mode)
    {
        bool active = _sort == mode;
        var pill = new Border
        {
            Background = active ? ThemeManager.GetBrush("ThAccent") : System.Windows.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(13, 6, 13, 6),
            Margin = new Thickness(1, 0, 1, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = label, FontSize = 13, FontWeight = FontWeights.Medium,
                Foreground = active ? ThemeManager.GetBrush("ThTextOnAccent") : ThemeManager.GetBrush("ThTextSecondary"),
                FontFamily = _font
            }
        };
        if (!active)
        {
            pill.MouseEnter += (_, _) => pill.Background = ThemeManager.GetBrush("ThBgHover");
            pill.MouseLeave += (_, _) => pill.Background = System.Windows.Media.Brushes.Transparent;
        }
        pill.MouseLeftButtonUp += (_, _) => { _sort = mode; RebuildSortBar(); Render(); };
        return pill;
    }

    // ── Results list ──────────────────────────────────────────────────────
    private void Render()
    {
        ResultsList.Children.Clear();

        IEnumerable<ExamResultDto> ordered = _sort switch
        {
            SortMode.Highest => _results.OrderByDescending(Pct),
            SortMode.Lowest => _results.OrderBy(Pct),
            _ => _results.OrderByDescending(r => r.SubmittedAt)
        };

        foreach (var exam in ordered)
            ResultsList.Children.Add(BuildResultRow(exam));
    }

    private Border BuildResultRow(ExamResultDto exam)
    {
        double pct = Pct(exam);
        var color = GradeColor(pct);

        var card = new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var outer = new StackPanel();

        // Header (clickable)
        var header = new Border
        {
            Padding = new Thickness(16, 13, 16, 13),
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = System.Windows.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(13)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // ring
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // main
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // score
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // chevron

        var ring = MakeRing(pct, color, 46, 4, big: false);
        ring.Margin = new Thickness(0, 0, 16, 0);
        grid.Children.Add(ring);

        var main = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        main.Children.Add(new TextBlock
        {
            Text = exam.ExamTitle, FontSize = 15, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _font,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        main.Children.Add(new TextBlock
        {
            Text = $"{exam.CourseCode}  ·  submitted {exam.SubmittedAt.ToLocalTime():dd MMM yyyy}",
            FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _mono,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(main, 1);
        grid.Children.Add(main);

        var scoreStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 14, 0) };
        var score = new TextBlock { FontSize = 15, FontWeight = FontWeights.Medium, FontFamily = _font, HorizontalAlignment = HorizontalAlignment.Right };
        score.Inlines.Add(new System.Windows.Documents.Run(exam.TotalScore.ToString()) { Foreground = ThemeManager.GetBrush("ThTextPrimary") });
        score.Inlines.Add(new System.Windows.Documents.Run($"/{exam.TotalMaxScore}") { Foreground = ThemeManager.GetBrush("ThTextMuted") });
        scoreStack.Children.Add(score);
        scoreStack.Children.Add(new TextBlock
        {
            Text = GradeLabel(pct), FontSize = 12, FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 0, 0), FontFamily = _font
        });
        Grid.SetColumn(scoreStack, 2);
        grid.Children.Add(scoreStack);

        var chevron = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M1 1 L6 6 L11 1"),
            Stroke = ThemeManager.GetBrush("ThTextMuted"), StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform, Width = 12, Height = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(chevron, 3);
        grid.Children.Add(chevron);

        header.Child = grid;

        // Detail (collapsed)
        var detail = new Border
        {
            Padding = new Thickness(16, 4, 16, 14),
            Visibility = Visibility.Collapsed
        };
        var detailStack = new StackPanel();
        detailStack.Children.Add(new Border { Height = 1, Background = ThemeManager.GetBrush("ThBorder"), Margin = new Thickness(0, 0, 0, 14) });
        foreach (var q in exam.Questions)
            detailStack.Children.Add(BuildQuestionRow(q));
        detail.Child = detailStack;

        outer.Children.Add(header);
        outer.Children.Add(detail);
        card.Child = outer;

        header.MouseLeftButtonUp += (_, _) =>
        {
            bool show = detail.Visibility == Visibility.Collapsed;
            detail.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            chevron.RenderTransform = show ? new RotateTransform(180, 6, 4) : null;
        };
        header.MouseEnter += (_, _) => { if (detail.Visibility == Visibility.Collapsed) header.Background = ThemeManager.GetBrush("ThBgSurface"); };
        header.MouseLeave += (_, _) => header.Background = System.Windows.Media.Brushes.Transparent;

        return card;
    }

    // ── Per-question breakdown ────────────────────────────────────────────
    private StackPanel BuildQuestionRow(QuestionResultDto q)
    {
        double qPct = Pct(q);
        var qColor = GradeColor(qPct);

        var wrapper = new StackPanel();

        var row = new Border
        {
            Background = ThemeManager.GetBrush("ThBgElev"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(13, 10, 13, 10),
            Margin = new Thickness(0, 0, 0, 6),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var expandIcon = new TextBlock
        {
            Text = "▸", FontSize = 10, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), FontFamily = _font
        };
        grid.Children.Add(expandIcon);

        var qInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        qInfo.Children.Add(LangTag(q.Language));
        qInfo.Children.Add(new TextBlock
        {
            Text = q.QuestionText.Length > 64 ? q.QuestionText[..64] + "…" : q.QuestionText,
            FontSize = 12.5, Foreground = ThemeManager.GetBrush("ThTextSecondary"), FontFamily = _font,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(qInfo, 1);
        grid.Children.Add(qInfo);

        var hasFeedback = !string.IsNullOrEmpty(q.Feedback) && q.Feedback != "\"\"" && q.Feedback != "[]";

        var scoreText = new TextBlock
        {
            Text = $"{q.Score}/{q.MaxScore}", FontSize = 13, FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(qColor), VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0), FontFamily = _mono
        };
        Grid.SetColumn(scoreText, 3);
        grid.Children.Add(scoreText);

        row.Child = grid;

        // Detail
        // The panel holds prose as well as code, so it follows the theme. The code
        // and output boxes use the ThCodeBg/ThCodeText pair, which tracks the theme
        // too and matches the Monaco editor.
        var detailPanel = new Border
        {
            Background = ThemeManager.GetBrush("ThBgSurface"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(15, 13, 15, 13),
            Margin = new Thickness(0, 0, 0, 6),
            Visibility = Visibility.Collapsed
        };
        var detailStack = new StackPanel();

        detailStack.Children.Add(new TextBlock
        {
            Text = q.QuestionText, FontSize = 12.5, Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            TextWrapping = TextWrapping.Wrap, FontFamily = _font, Margin = new Thickness(0, 0, 0, 12)
        });

        if (!string.IsNullOrWhiteSpace(q.Code))
        {
            detailStack.Children.Add(SubLabel("YOUR ANSWER"));
            var codeBox = new TextBox
            {
                Text = q.Code, IsReadOnly = true, FontFamily = _mono, FontSize = 12.5,
                Background = ThemeManager.GetBrush("ThCodeBg"),
                Foreground = ThemeManager.GetBrush("ThCodeText"),
                BorderBrush = ThemeManager.GetBrush("ThBorder"), BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10), TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 240,
                Margin = new Thickness(0, 6, 0, 10)
            };
            detailStack.Children.Add(codeBox);

            bool isPreview = HtmlPreview.IsPreviewLanguage(q.Language);

            var runBtn = new Border
            {
                Background = ThemeManager.GetBrush("ThAccent"), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 8, 15, 8), HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var runLabel = new TextBlock
            {
                Text = isPreview ? "▶  Show preview" : "▶  Run code",
                FontSize = 12.5, FontWeight = FontWeights.Medium,
                Foreground = ThemeManager.GetBrush("ThTextOnAccent"), FontFamily = _font
            };
            runBtn.Child = runLabel;
            detailStack.Children.Add(runBtn);

            if (isPreview)
            {
                // HTML / CSS → render a live preview instead of text output.
                var previewHost = new Border
                {
                    BorderBrush = ThemeManager.GetBrush("ThBorder"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8), Height = 320, Margin = new Thickness(0, 8, 0, 0),
                    Background = System.Windows.Media.Brushes.White, ClipToBounds = true,
                    Visibility = Visibility.Collapsed
                };
                var web = new Microsoft.Web.WebView2.Wpf.WebView2
                {
                    DefaultBackgroundColor = System.Drawing.Color.White
                };
                previewHost.Child = web;
                detailStack.Children.Add(previewHost);

                bool shown = false, rendered = false;
                runBtn.MouseLeftButtonUp += async (_, _) =>
                {
                    shown = !shown;
                    previewHost.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
                    runLabel.Text = shown ? "Hide preview" : "▶  Show preview";
                    if (shown && !rendered)
                    {
                        rendered = true;
                        try { await HtmlPreview.RenderAsync(web, q.Code, q.Language, "ResultsPreview"); }
                        catch { rendered = false; }
                    }
                };
            }
            else
            {
                var outputBox = new TextBox
                {
                    IsReadOnly = true, FontFamily = _mono, FontSize = 12,
                    Background = ThemeManager.GetBrush("ThCodeBg"),
                    Foreground = ThemeManager.GetBrush("ThCodeText"),
                    BorderBrush = ThemeManager.GetBrush("ThBorder"), BorderThickness = new Thickness(1),
                    Padding = new Thickness(11, 9, 11, 9), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 150, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0)
                };
                detailStack.Children.Add(outputBox);

                bool running = false;
                runBtn.MouseLeftButtonUp += async (_, _) =>
                {
                    if (running) return;
                    running = true;
                    runLabel.Text = "Running…";
                    outputBox.Visibility = Visibility.Visible;
                    outputBox.Text = "Executing…";
                    try
                    {
                        var result = await CodeExecutionService.ExecuteAsync(q.Code, q.Language);
                        var errors = result.CompilationError ?? result.Stderr;
                        outputBox.Text = string.IsNullOrWhiteSpace(result.Stdout)
                            ? (string.IsNullOrWhiteSpace(errors) ? "(no output)" : errors)
                            : result.Stdout + (string.IsNullOrWhiteSpace(errors) ? "" : "\n\n⚠ " + errors);
                    }
                    catch (Exception ex) { outputBox.Text = $"Error: {ex.Message}"; }
                    finally { running = false; runLabel.Text = "▶  Run code"; }
                };
            }
        }

        if (hasFeedback)
        {
            var fb = new Border
            {
                Background = ThemeManager.GetBrush("ThAccentSoft"),
                BorderBrush = ThemeManager.GetBrush("ThAccentLine"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Padding = new Thickness(13, 11, 13, 11),
                Margin = new Thickness(0, 12, 0, 0)
            };
            var fbStack = new StackPanel();
            fbStack.Children.Add(new TextBlock
            {
                Text = "LECTURER FEEDBACK", FontSize = 11, FontWeight = FontWeights.Medium,
                Foreground = ThemeManager.GetBrush("ThAccentStrong"), FontFamily = _mono, Margin = new Thickness(0, 0, 0, 5)
            });
            fbStack.Children.Add(new TextBlock
            {
                Text = q.Feedback ?? "", FontSize = 12.5, Foreground = ThemeManager.GetBrush("ThTextPrimary"),
                TextWrapping = TextWrapping.Wrap, FontFamily = _font
            });
            fb.Child = fbStack;
            detailStack.Children.Add(fb);
        }

        detailPanel.Child = detailStack;

        row.MouseLeftButtonUp += (_, _) =>
        {
            bool show = detailPanel.Visibility == Visibility.Collapsed;
            detailPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            expandIcon.Text = show ? "▾" : "▸";
        };
        row.MouseEnter += (_, _) => { if (detailPanel.Visibility == Visibility.Collapsed) row.BorderBrush = ThemeManager.GetBrush("ThBorderStrong"); };
        row.MouseLeave += (_, _) => row.BorderBrush = ThemeManager.GetBrush("ThBorder");

        wrapper.Children.Add(row);
        wrapper.Children.Add(detailPanel);
        return wrapper;
    }

    // ── Small pieces ──────────────────────────────────────────────────────
    private TextBlock SubLabel(string text) => new()
    {
        Text = text, FontSize = 11, FontWeight = FontWeights.Medium,
        Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _mono, Margin = new Thickness(0, 0, 0, 4)
    };

    private Border LangTag(string lang) => new()
    {
        Background = ThemeManager.GetBrush("ThBgSurface2"),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(7, 2, 7, 2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = lang, FontSize = 11, Foreground = ThemeManager.GetBrush("ThTextSecondary"), FontFamily = _mono
        }
    };

    // Circular progress ring with centred percentage.
    private Grid MakeRing(double pct, Color color, double size, double stroke, bool big)
    {
        double frac = Math.Max(0, Math.Min(1, pct / 100));
        double r = size / 2 - stroke / 2 - 1;
        double cx = size / 2, cy = size / 2;

        var g = new Grid { Width = size, Height = size };
        g.Children.Add(new Ellipse
        {
            Width = r * 2, Height = r * 2, StrokeThickness = stroke,
            Stroke = ThemeManager.GetBrush("ThBorderStrong"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        });

        if (frac >= 0.999)
        {
            g.Children.Add(new Ellipse
            {
                Width = r * 2, Height = r * 2, StrokeThickness = stroke, Stroke = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });
        }
        else if (frac > 0)
        {
            double angle = 360 * frac;
            double endRad = (-90 + angle) * Math.PI / 180;
            var start = new Point(cx, cy - r);
            var end = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));
            var fig = new PathFigure { StartPoint = start, IsClosed = false };
            fig.Segments.Add(new ArcSegment { Point = end, Size = new Size(r, r), IsLargeArc = angle > 180, SweepDirection = SweepDirection.Clockwise });
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            g.Children.Add(new System.Windows.Shapes.Path
            {
                Data = geo, Stroke = new SolidColorBrush(color), StrokeThickness = stroke,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            });
        }

        var label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock
        {
            Text = $"{pct:F0}", FontSize = big ? 26 : 12.5, FontWeight = FontWeights.Medium,
            FontFamily = _mono, Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        if (big)
            label.Children.Add(new TextBlock
            {
                Text = "%", FontSize = 11, Foreground = ThemeManager.GetBrush("ThTextMuted"),
                FontFamily = _font, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -2, 0, 0)
            });
        g.Children.Add(label);
        return g;
    }
}
