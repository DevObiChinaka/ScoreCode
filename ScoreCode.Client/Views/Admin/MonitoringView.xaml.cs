using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Monitoring;

namespace ScoreCode.Client.Views.Admin;

public partial class MonitoringView : UserControl
{
    private readonly MonitoringClientService _svc;
    private readonly DispatcherTimer _refreshTimer;
    private static readonly FontFamily Font = new("/Assets/Fonts/#Geist");

    public MonitoringView(ApiService api)
    {
        InitializeComponent();
        _svc = new MonitoringClientService(api);

        BtnRefresh.Click += async (_, _) => await LoadAsync();

        // Auto-refresh while the tab is open (this is a live monitor).
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _refreshTimer.Tick += async (_, _) => await LoadAsync();

        Loaded += async (_, _) => { await LoadAsync(); _refreshTimer.Start(); };
        Unloaded += (_, _) => _refreshTimer.Stop();
    }

    private async Task LoadAsync()
    {
        try
        {
            var data = await _svc.GetOverviewAsync();
            if (data is null)
            {
                TxtStatus.Text = "Could not load monitoring data";
                return;
            }

            BuildStats(data);
            BuildSessions(data.ActiveSessions);
            BuildViolations(data.RecentViolations);

            TxtStatus.Text = $"Updated {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error: {ex.Message}";
        }
    }

    // ── Stat cards ─────────────────────────────────────────────────────────
    private void BuildStats(MonitoringOverviewDto d)
    {
        StatsRow.Children.Clear();
        StatsRow.Children.Add(MakeStatCard("Active Sessions", d.ActiveSessionCount.ToString(), "ThAccent"));
        StatsRow.Children.Add(MakeStatCard("Anti-Cheat Events", d.TotalViolations.ToString(),
            d.TotalViolations > 0 ? "ThWarning" : "ThTextPrimary"));
        StatsRow.Children.Add(MakeStatCard("Completed Today", d.CompletedTodayCount.ToString(), "ThSuccess"));
    }

    private Border MakeStatCard(string label, string value, string valueBrushKey)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = value, FontSize = 30, FontWeight = FontWeights.Bold,
            Foreground = ThemeManager.GetBrush(valueBrushKey), FontFamily = Font
        });
        stack.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            FontFamily = Font, Margin = new Thickness(0, 4, 0, 0)
        });

        return new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 16, 32, 16),
            Margin = new Thickness(0, 0, 14, 0),
            Child = stack
        };
    }

    // ── Active sessions ────────────────────────────────────────────────────
    private void BuildSessions(List<ActiveSessionDto> sessions)
    {
        SessionRows.Children.Clear();

        if (sessions.Count == 0)
        {
            SessionRows.Children.Add(MakeEmpty("No active sessions"));
            return;
        }

        foreach (var s in sessions)
        {
            var stack = new StackPanel { Margin = new Thickness(16, 10, 16, 10) };

            var top = new DockPanel();
            top.Children.Add(new TextBlock
            {
                Text = s.StudentMatric, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = Font
            });
            if (s.ViolationCount > 0)
            {
                var badge = MakeBadge($"⚠ {s.ViolationCount}", "ThWarning");
                DockPanel.SetDock(badge, Dock.Right);
                badge.HorizontalAlignment = HorizontalAlignment.Right;
                top.Children.Add(badge);
            }
            stack.Children.Add(top);

            stack.Children.Add(new TextBlock
            {
                Text = $"{s.CourseCode} · {s.ExamTitle}", FontSize = 12,
                Foreground = ThemeManager.GetBrush("ThTextSecondary"),
                FontFamily = Font, Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"Started {Elapsed(s.StartedAt)} ago · {s.StartedAt.ToLocalTime():HH:mm}",
                FontSize = 11, Foreground = ThemeManager.GetBrush("ThTextMuted"),
                FontFamily = Font, Margin = new Thickness(0, 2, 0, 0)
            });

            SessionRows.Children.Add(MakeRowBorder(stack));
        }
    }

    // ── Recent violations ──────────────────────────────────────────────────
    private void BuildViolations(List<ViolationEventDto> violations)
    {
        ViolationRows.Children.Clear();

        if (violations.Count == 0)
        {
            ViolationRows.Children.Add(MakeEmpty("No anti-cheat events recorded"));
            return;
        }

        foreach (var v in violations)
        {
            var grid = new Grid { Margin = new Thickness(16, 9, 16, 9) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = MakeBadge(FriendlyEvent(v.EventType), EventBrushKey(v.EventType));
            badge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(badge, 0);
            grid.Children.Add(badge);

            var mid = new StackPanel { Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            mid.Children.Add(new TextBlock
            {
                Text = $"{v.StudentMatric} · {v.ExamTitle}", FontSize = 12,
                Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = Font,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (!string.IsNullOrWhiteSpace(v.Detail))
                mid.Children.Add(new TextBlock
                {
                    Text = v.Detail, FontSize = 11,
                    Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = Font,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            Grid.SetColumn(mid, 1);
            grid.Children.Add(mid);

            var time = new TextBlock
            {
                Text = v.OccurredAt.ToLocalTime().ToString("HH:mm:ss"),
                FontSize = 11, Foreground = ThemeManager.GetBrush("ThTextMuted"),
                FontFamily = Font, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(time, 2);
            grid.Children.Add(time);

            ViolationRows.Children.Add(MakeRowBorder(grid));
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private Border MakeRowBorder(UIElement child) => new()
    {
        BorderBrush = ThemeManager.GetBrush("ThBorder"),
        BorderThickness = new Thickness(0, 0, 0, 1),
        Child = child
    };

    private Border MakeBadge(string text, string brushKey)
    {
        var c = ThemeManager.GetColor(brushKey);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x22, c.R, c.G, c.B)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2, 7, 2),
            Child = new TextBlock
            {
                Text = text, FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(c), FontFamily = Font
            }
        };
    }

    private TextBlock MakeEmpty(string text) => new()
    {
        Text = text, FontSize = 13,
        Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = Font,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 30, 0, 30)
    };

    private static string FriendlyEvent(string eventType) => eventType switch
    {
        Shared.Constants.ActivityEvents.FocusLost => "FOCUS LOST",
        Shared.Constants.ActivityEvents.BlockedKey => "BLOCKED KEY",
        _ => eventType.ToUpperInvariant()
    };

    private static string EventBrushKey(string eventType) => eventType switch
    {
        Shared.Constants.ActivityEvents.FocusLost => "ThError",
        Shared.Constants.ActivityEvents.BlockedKey => "ThWarning",
        _ => "ThTextSecondary"
    };

    private static string Elapsed(DateTime startedUtc)
    {
        var span = DateTime.UtcNow - startedUtc;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m";
        return $"{(int)span.TotalHours}h {span.Minutes}m";
    }
}
