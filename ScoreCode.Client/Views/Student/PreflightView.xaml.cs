using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScoreCode.Client.Helpers;
using ScoreCode.Shared.DTOs.Exams;

namespace ScoreCode.Client.Views.Student;

/// <summary>
/// Pre-flight "Focus Room" screen shown before an exam launches. It confirms the
/// student's identity and the exam they're about to enter, and walks through the
/// lockdown steps that engage once they commit.
/// </summary>
public partial class PreflightView : UserControl
{
    private readonly ExamListItemDto _exam;
    private readonly string _matric;

    public event Action? EnterRequested;
    public event Action? CancelRequested;

    private static readonly FontFamily _font = new("/Assets/Fonts/#Geist");
    private static readonly FontFamily _mono = new("/Assets/Fonts/#Geist Mono, Consolas");

    private readonly List<(Border dot, TextBlock title, TextBlock detail)> _checkRows = new();
    private Border? _enterBtn;
    private TextBlock? _enterLabel;
    private bool _ready;

    private static readonly (string title, string detail)[] Checks =
    {
        ("Full-screen lockdown", "The exam takes over your screen"),
        ("Single active session", "One device per matric number"),
        ("Clipboard disabled", "Copy and paste are turned off"),
        ("Autosave engaged", "Your work is saved continuously"),
    };

    public PreflightView(ExamListItemDto exam, string matric)
    {
        InitializeComponent();
        _exam = exam;
        _matric = matric;
        Build();
        Loaded += async (_, _) => await RunChecksAsync();
    }

    private void Build()
    {
        // Centred max-width block
        var host = new Grid { Margin = new Thickness(40) };
        var inner = new Grid
        {
            MaxWidth = 800,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        inner.Children.Add(BuildChecklist());
        var identity = BuildIdentityCard();
        Grid.SetColumn(identity, 2);
        inner.Children.Add(identity);

        host.Children.Add(inner);
        Root.Children.Add(host);
    }

    // ── Left: lockdown checklist ──────────────────────────────────────────
    private StackPanel BuildChecklist()
    {
        var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        col.Children.Add(new TextBlock
        {
            Text = "PRE-FLIGHT CHECK", FontSize = 12, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _mono
        });
        col.Children.Add(new TextBlock
        {
            Text = "Entering the Focus Room", FontSize = 25, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _font,
            Margin = new Thickness(0, 12, 0, 4)
        });
        col.Children.Add(new TextBlock
        {
            Text = "Once you begin, ScoreCode takes over the screen and your work autosaves continuously.",
            FontSize = 13.5, Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            TextWrapping = TextWrapping.Wrap, FontFamily = _font, Margin = new Thickness(0, 0, 0, 22)
        });

        foreach (var (title, detail) in Checks)
            col.Children.Add(BuildCheckRow(title, detail));

        return col;
    }

    private Border BuildCheckRow(string title, string detail)
    {
        var row = new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(15, 13, 15, 13),
            Margin = new Thickness(0, 0, 0, 11)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Status dot (pending → done)
        var dot = new Border
        {
            Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
            Background = ThemeManager.GetBrush("ThBgSurface2"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        grid.Children.Add(dot);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleTb = new TextBlock
        {
            Text = title, FontSize = 13.5, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _font
        };
        var detailTb = new TextBlock
        {
            Text = detail, FontSize = 11.5, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _font, Margin = new Thickness(0, 1, 0, 0)
        };
        text.Children.Add(titleTb);
        text.Children.Add(detailTb);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        row.Child = grid;
        _checkRows.Add((dot, titleTb, detailTb));
        return row;
    }

    private async Task RunChecksAsync()
    {
        for (int i = 0; i < _checkRows.Count; i++)
        {
            await Task.Delay(i == 0 ? 400 : 480);
            MarkDone(_checkRows[i].dot);
        }
        SetReady(true);
    }

    private void MarkDone(Border dot)
    {
        dot.Background = ThemeManager.GetBrush("ThSuccessSoft");
        var check = new Path
        {
            Data = Geometry.Parse("M20 6 L9 17 L4 12"),
            Stroke = ThemeManager.GetBrush("ThSuccess"),
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            Width = 13, Height = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        dot.Child = check;
    }

    // ── Right: identity + enter ───────────────────────────────────────────
    private Border BuildIdentityCard()
    {
        var card = new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Center
        };

        var stack = new StackPanel();

        // Identity header
        var idRow = new StackPanel { Orientation = Orientation.Horizontal };
        var avatar = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(12),
            Background = ThemeManager.GetBrush("ThBrAccentGradient"),
            Margin = new Thickness(0, 0, 13, 0),
            Child = new TextBlock
            {
                Text = Initials(_matric), FontSize = 16, FontWeight = FontWeights.Medium,
                Foreground = Brushes.White, FontFamily = _font,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        idRow.Children.Add(avatar);
        var idText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        idText.Children.Add(new TextBlock
        {
            Text = "Signed in", FontSize = 11, Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _font
        });
        idText.Children.Add(new TextBlock
        {
            Text = _matric, FontSize = 13.5, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _mono, Margin = new Thickness(0, 2, 0, 0)
        });
        idRow.Children.Add(idText);
        stack.Children.Add(idRow);

        stack.Children.Add(Divider(18));

        // About to start
        stack.Children.Add(new TextBlock
        {
            Text = "YOU ARE ABOUT TO START", FontSize = 11, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _mono, Margin = new Thickness(0, 0, 0, 10)
        });
        stack.Children.Add(new TextBlock
        {
            Text = _exam.Title, FontSize = 16, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _font, TextWrapping = TextWrapping.Wrap
        });
        var meta = $"{_exam.CourseCode} · {_exam.DurationMinutes} min · {_exam.QuestionCount} question" + (_exam.QuestionCount == 1 ? "" : "s");
        stack.Children.Add(new TextBlock
        {
            Text = meta, FontSize = 12.5, Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            FontFamily = _mono, Margin = new Thickness(0, 4, 0, 0)
        });

        stack.Children.Add(Divider(18));

        stack.Children.Add(new TextBlock
        {
            Text = "A single session is allowed per matric number. Leaving full-screen is logged for your lecturer.",
            FontSize = 12, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            TextWrapping = TextWrapping.Wrap, FontFamily = _font, Margin = new Thickness(0, 0, 0, 20)
        });

        // Enter button (disabled until checks complete)
        _enterBtn = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Background = ThemeManager.GetBrush("ThBgSurface2")
        };
        _enterLabel = new TextBlock
        {
            Text = "Running checks…", FontSize = 15, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _font,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _enterBtn.Child = _enterLabel;
        _enterBtn.MouseLeftButtonUp += (_, _) => { if (_ready) EnterRequested?.Invoke(); };
        stack.Children.Add(_enterBtn);

        // Not now
        var cancel = new Border
        {
            CornerRadius = new CornerRadius(11),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(11),
            Margin = new Thickness(0, 10, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = "Not now", FontSize = 13, FontWeight = FontWeights.Medium,
                Foreground = ThemeManager.GetBrush("ThTextSecondary"), FontFamily = _font,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        cancel.MouseLeftButtonUp += (_, _) => CancelRequested?.Invoke();
        stack.Children.Add(cancel);

        card.Child = stack;
        return card;
    }

    private void SetReady(bool ready)
    {
        _ready = ready;
        if (_enterBtn is null || _enterLabel is null) return;
        _enterBtn.Background = ready ? ThemeManager.GetBrush("ThAccent") : ThemeManager.GetBrush("ThBgSurface2");
        _enterBtn.Cursor = ready ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
        _enterLabel.Text = ready ? "Enter Focus Room" : "Running checks…";
        _enterLabel.Foreground = ready ? ThemeManager.GetBrush("ThTextOnAccent") : ThemeManager.GetBrush("ThTextMuted");
    }

    private Border Divider(double vertical) => new()
    {
        Height = 1, Background = ThemeManager.GetBrush("ThBorder"),
        Margin = new Thickness(0, vertical, 0, vertical)
    };

    private static string Initials(string matric)
    {
        var letters = new string(matric.Where(char.IsLetterOrDigit).ToArray());
        return letters.Length >= 2 ? letters[..2].ToUpperInvariant()
             : letters.Length == 1 ? letters.ToUpperInvariant() : "ST";
    }
}
