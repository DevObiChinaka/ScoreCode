using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Exams;

namespace ScoreCode.Client.Views.Student;

public partial class StudentResultsView : UserControl
{
    private readonly ApiService _api;

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

            foreach (var exam in results)
                ResultsList.Children.Add(BuildExamCard(exam));
        }
        catch (Exception ex)
        {
            TxtLoading.Text = $"Error loading results: {ex.Message}";
            TxtLoading.Foreground = ThemeManager.GetBrush("ThError");
        }
    }

    private Border BuildExamCard(ExamResultDto exam)
    {
        var percentage = exam.TotalMaxScore > 0
            ? (double)exam.TotalScore / exam.TotalMaxScore * 100
            : 0;

        var gradeColor = percentage >= 70 ? "#34D399"   // Green
                       : percentage >= 50 ? "#F6A623"   // Orange
                       : "#FC8181";                      // Red

        var card = new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            CornerRadius = new CornerRadius(10),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 20, 24, 20),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var stack = new StackPanel();

        // ── Header row: Title + Score ──
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = exam.ExamTitle,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeManager.GetBrush("ThTextPrimary")
        });

        var metaPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        metaPanel.Children.Add(CreateMetaTag(exam.CourseCode, "#00CDB7"));
        metaPanel.Children.Add(new TextBlock
        {
            Text = $"  ·  Submitted {exam.SubmittedAt:dd MMM yyyy}",
            FontSize = 11,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            VerticalAlignment = VerticalAlignment.Center
        });
        titleStack.Children.Add(metaPanel);

        Grid.SetColumn(titleStack, 0);
        header.Children.Add(titleStack);

        // Score badge
        var scoreBadge = new Border
        {
            Background = ThemeManager.GetBrush("ThBgBase"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10),
            VerticalAlignment = VerticalAlignment.Center
        };
        var scoreStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        scoreStack.Children.Add(new TextBlock
        {
            Text = $"{exam.TotalScore}/{exam.TotalMaxScore}",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(gradeColor)),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        scoreStack.Children.Add(new TextBlock
        {
            Text = $"{percentage:F0}%",
            FontSize = 11,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        scoreBadge.Child = scoreStack;

        Grid.SetColumn(scoreBadge, 1);
        header.Children.Add(scoreBadge);

        stack.Children.Add(header);

        // ── Progress bar ──
        var progressBg = new Border
        {
            Background = ThemeManager.GetBrush("ThBorder"),
            CornerRadius = new CornerRadius(3),
            Height = 6,
            Margin = new Thickness(0, 12, 0, 16)
        };
        var progressFill = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(gradeColor)),
            CornerRadius = new CornerRadius(3),
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0 // Set after render
        };
        progressBg.Child = progressFill;
        stack.Children.Add(progressBg);

        // Set progress bar width after layout
        progressBg.Loaded += (_, _) =>
        {
            var maxWidth = progressBg.ActualWidth;
            progressFill.Width = maxWidth * (percentage / 100.0);
        };

        // ── Question breakdown ──
        foreach (var q in exam.Questions)
        {
            stack.Children.Add(BuildQuestionRow(q));
        }

        card.Child = stack;
        return card;
    }

    private StackPanel BuildQuestionRow(QuestionResultDto q)
    {
        var qPercentage = q.MaxScore > 0 ? (double)q.Score / q.MaxScore * 100 : 0;
        var qColor = qPercentage >= 70 ? "#34D399" : qPercentage >= 50 ? "#F6A623" : "#FC8181";

        var wrapper = new StackPanel();

        // ── Clickable header row ──
        var row = new Border
        {
            Background = ThemeManager.GetBrush("ThBgBase"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 2),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });       // expand icon
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // question
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });       // feedback
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });       // score

        // Expand indicator
        var expandIcon = new TextBlock
        {
            Text = "▶", FontSize = 9,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(expandIcon, 0);
        grid.Children.Add(expandIcon);

        // Question text + language
        var qInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        qInfo.Children.Add(CreateMetaTag(q.Language, "#A78BFA"));
        qInfo.Children.Add(new TextBlock
        {
            Text = q.QuestionText.Length > 60 ? q.QuestionText[..60] + "…" : q.QuestionText,
            FontSize = 12,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(qInfo, 1);
        grid.Children.Add(qInfo);

        // Feedback indicator (clickable)
        var hasFeedback = !string.IsNullOrEmpty(q.Feedback) && q.Feedback != "\"\"" && q.Feedback != "[]";
        if (hasFeedback)
        {
            var feedbackBtn = new Button
            {
                Content = "💬",
                FontSize = 13,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                ToolTip = "Click to view feedback",
                Padding = new Thickness(4, 2, 4, 2)
            };
            feedbackBtn.Click += (_, _) => ShowFeedbackPopup(q);
            Grid.SetColumn(feedbackBtn, 2);
            grid.Children.Add(feedbackBtn);
        }

        // Score
        var scoreText = new TextBlock
        {
            Text = $"{q.Score}/{q.MaxScore}",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(qColor)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(scoreText, 3);
        grid.Children.Add(scoreText);

        row.Child = grid;

        // ── Expandable detail panel (hidden by default) ──
        var detailPanel = new Border
        {
            Background = ThemeManager.GetBrush("ThCodeBg"),
            CornerRadius = new CornerRadius(0, 0, 6, 6),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 6),
            Visibility = Visibility.Collapsed
        };

        var detailStack = new StackPanel();

        // Question text (full)
        detailStack.Children.Add(new TextBlock
        {
            Text = q.QuestionText,
            FontSize = 12,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Code block (read-only)
        if (!string.IsNullOrWhiteSpace(q.Code))
        {
            detailStack.Children.Add(new TextBlock
            {
                Text = "Your Answer",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.GetBrush("ThTextSecondary"),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var codeBox = new TextBox
            {
                Text = q.Code,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = 12,
                Background = ThemeManager.GetBrush("ThCodeBg"),
                Foreground = ThemeManager.GetBrush("ThTextPrimary"),
                BorderBrush = ThemeManager.GetBrush("ThBorder"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 250,
                Margin = new Thickness(0, 0, 0, 8)
            };
            detailStack.Children.Add(codeBox);

            // Run button + output
            var runBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var runBtn = new Button
            {
                Content = "▶  Run Code",
                FontSize = 12,
                Background = ThemeManager.GetBrush("ThAccent"),
                Foreground = ThemeManager.GetBrush("ThBgBase"),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            var outputBlock = new TextBox
            {
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = ThemeManager.GetBrush("ThCodeBg"),
                Foreground = ThemeManager.GetBrush("ThTextSecondary"),
                BorderBrush = ThemeManager.GetBrush("ThBorder"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 150,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 6, 0, 0)
            };

            runBtn.Click += async (_, _) =>
            {
                runBtn.IsEnabled = false;
                runBtn.Content = "Running…";
                outputBlock.Visibility = Visibility.Visible;
                outputBlock.Text = "Executing…";

                try
                {
                    var result = await Services.CodeExecutionService.ExecuteAsync(q.Code, q.Language);
                    var errors = result.CompilationError ?? result.Stderr;
                    outputBlock.Text = string.IsNullOrWhiteSpace(result.Stdout)
                        ? (string.IsNullOrWhiteSpace(errors) ? "(no output)" : errors)
                        : result.Stdout + (string.IsNullOrWhiteSpace(errors) ? "" : "\n\n⚠ " + errors);
                }
                catch (Exception ex)
                {
                    outputBlock.Text = $"Error: {ex.Message}";
                }
                finally
                {
                    runBtn.IsEnabled = true;
                    runBtn.Content = "▶  Run Code";
                }
            };

            runBar.Children.Add(runBtn);
            detailStack.Children.Add(runBar);
            detailStack.Children.Add(outputBlock);
        }

        // Feedback section
        if (hasFeedback)
        {
            detailStack.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0xA7, 0x8B, 0xFA)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "💬 Lecturer Feedback",
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)),
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        new TextBlock
                        {
                            Text = q.Feedback ?? "",
                            FontSize = 12,
                            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            });
        }

        detailPanel.Child = detailStack;

        // Toggle expand/collapse on header click
        row.MouseLeftButtonDown += (_, _) =>
        {
            if (detailPanel.Visibility == Visibility.Collapsed)
            {
                detailPanel.Visibility = Visibility.Visible;
                expandIcon.Text = "▼";
                row.CornerRadius = new CornerRadius(6, 6, 0, 0);
            }
            else
            {
                detailPanel.Visibility = Visibility.Collapsed;
                expandIcon.Text = "▶";
                row.CornerRadius = new CornerRadius(6);
            }
        };

        // Hover effect
        row.MouseEnter += (_, _) => row.Background =
            ThemeManager.GetBrush("ThBgHover");
        row.MouseLeave += (_, _) => row.Background =
            ThemeManager.GetBrush("ThBgBase");

        wrapper.Children.Add(row);
        wrapper.Children.Add(detailPanel);
        return wrapper;
    }

    private void ShowFeedbackPopup(QuestionResultDto q)
    {
        var dialog = new Window
        {
            Title = "Lecturer Feedback",
            Width = 450, Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = ThemeManager.GetBrush("ThBgBase")
        };
        Helpers.TitleBarHelper.ApplyDarkMode(dialog);
        Helpers.TitleBarHelper.SetCaptionColor(dialog, 0x0D, 0x11, 0x17);
        Helpers.TitleBarHelper.SetIcon(dialog);

        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(new TextBlock
        {
            Text = q.QuestionText.Length > 80 ? q.QuestionText[..80] + "…" : q.QuestionText,
            FontSize = 13,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var qPercentage = q.MaxScore > 0 ? (double)q.Score / q.MaxScore * 100 : 0;
        var qColor = qPercentage >= 70 ? "#34D399" : qPercentage >= 50 ? "#F6A623" : "#FC8181";
        stack.Children.Add(new TextBlock
        {
            Text = $"Score: {q.Score}/{q.MaxScore} ({qPercentage:F0}%)",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(qColor)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Feedback",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)),
            Margin = new Thickness(0, 0, 0, 6)
        });
        stack.Children.Add(new TextBlock
        {
            Text = q.Feedback ?? "No feedback provided.",
            FontSize = 13,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            TextWrapping = TextWrapping.Wrap
        });

        dialog.Content = stack;
        dialog.ShowDialog();
    }

    private static Border CreateMetaTag(string text, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x20, color.R, color.G, color.B)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(color)
            }
        };
    }
}
