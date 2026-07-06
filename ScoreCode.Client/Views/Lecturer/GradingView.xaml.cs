using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Lecturer;

namespace ScoreCode.Client.Views.Lecturer;

public partial class GradingView : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────────
    public event Action? BackRequested;

    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly LecturerClientService _svc;
    private readonly ApiService _api;

    // ── Context ───────────────────────────────────────────────────────────────
    private readonly Guid _examId;
    private readonly string _studentMatric;
    private readonly string _examTitle;
    private readonly List<SubmissionListItemDto> _subs;   // ordered by QuestionNumber

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly Dictionary<Guid, SubmissionDetailDto> _detailCache = [];
    private readonly Dictionary<Guid, (string Score, string Feedback)> _edits = [];
    private readonly Dictionary<Guid, bool> _isGraded = [];
    private Guid _activeSubId = Guid.Empty;

    // ── Monaco ────────────────────────────────────────────────────────────────
    private bool _editorReady;

    // ── Constructor ───────────────────────────────────────────────────────────
    public GradingView(ApiService api, Guid examId,
        string studentMatric, string examTitle,
        List<SubmissionListItemDto> subs)
    {
        InitializeComponent();
        _api = api;
        _svc = new LecturerClientService(api);
        _examId = examId;
        _studentMatric = studentMatric;
        _examTitle = examTitle;
        _subs = [.. subs.OrderBy(s => s.QuestionNumber)];

        // Seed grading state from list data (score already on server)
        foreach (var s in _subs)
            _isGraded[s.Id] = s.Score.HasValue;

        TxtStudent.Text = $"Grading: {studentMatric}";
        TxtExamTitle.Text = examTitle;

        BtnBack.Click          += (_, _) => BackRequested?.Invoke();
        BtnSaveAll.Click       += async (_, _) => await SaveAllGradedAsync();
        BtnSaveQuestion.Click  += async (_, _) => await SaveCurrentQuestionAsync();
        BtnClearGrade.Click    += (_, _) => ClearCurrentGrade();
        BtnRun.Click           += async (_, _) => await RunCurrentCodeAsync();
        BtnAiSuggest.Click     += async (_, _) => await AiSuggestAsync();

        Loaded += async (_, _) =>
        {
            BuildNav();
            UpdateProgress();
            await InitMonacoAsync();

            // Auto-select first ungraded, or first if all done
            var first = _subs.FirstOrDefault(s => !_isGraded[s.Id]) ?? _subs.FirstOrDefault();
            if (first is not null)
                await SelectQuestionAsync(first.Id);
        };
    }

    // ── Left Navigator ────────────────────────────────────────────────────────
    private void BuildNav()
    {
        NavPanel.Children.Clear();

        NavPanel.Children.Add(new TextBlock
        {
            Text = "QUESTIONS",
            FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Margin = new Thickness(16, 16, 16, 8)
        });

        foreach (var sub in _subs)
            NavPanel.Children.Add(BuildNavItem(sub));
    }

    private Border BuildNavItem(SubmissionListItemDto sub)
    {
        var graded = _isGraded[sub.Id];
        var statusColor = graded
            ? ThemeManager.GetColor("ThSuccess")
            : ThemeManager.GetColor("ThTextMuted");

        var item = new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = sub.Id,
            Background = Brushes.Transparent,
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var stack = new StackPanel();

        // Title row: Q# + status badge
        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var qLabel = new TextBlock
        {
            Text = $"Question {sub.QuestionNumber}",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(qLabel, 0);

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x25, statusColor.R, statusColor.G, statusColor.B)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = graded ? "✓" : "—",
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(statusColor),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist")
            }
        };
        Grid.SetColumn(badge, 1);

        titleRow.Children.Add(qLabel);
        titleRow.Children.Add(badge);

        stack.Children.Add(titleRow);

        // Score preview
        stack.Children.Add(new TextBlock
        {
            Text = sub.Score.HasValue ? $"{sub.Score} / {sub.MaxScore} pts" : $"max {sub.MaxScore} pts",
            FontSize = 11,
            Foreground = ThemeManager.GetBrush(sub.Score.HasValue ? "ThTextSecondary" : "ThTextMuted"),
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Margin = new Thickness(0, 3, 0, 0)
        });

        item.Child = stack;

        // Hover & click
        var accentC = ThemeManager.GetColor("ThAccent");
        item.MouseEnter += (_, _) =>
        {
            if ((Guid)item.Tag != _activeSubId)
                item.Background = new SolidColorBrush(Color.FromArgb(0x0C, 0, 0, 0));
        };
        item.MouseLeave += (_, _) =>
        {
            if ((Guid)item.Tag != _activeSubId)
                item.Background = Brushes.Transparent;
        };
        item.MouseLeftButtonDown += async (_, _) => await SelectQuestionAsync(sub.Id);

        return item;
    }

    private void RefreshNavItem(Guid subId)
    {
        // Find and update the list item data
        var sub = _subs.First(s => s.Id == subId);
        // Reflect latest cached score
        if (_detailCache.TryGetValue(subId, out var detail) && detail.Grade is not null)
            sub.Score = detail.Grade.Score;

        foreach (var child in NavPanel.Children.OfType<Border>())
        {
            if (child.Tag is not Guid id || id != subId) continue;
            var idx = NavPanel.Children.IndexOf(child);
            var newItem = BuildNavItem(sub);
            NavPanel.Children.RemoveAt(idx);
            NavPanel.Children.Insert(idx, newItem);
            break;
        }
        HighlightActiveNavItem();
    }

    private void HighlightActiveNavItem()
    {
        var accentC = ThemeManager.GetColor("ThAccent");
        foreach (var child in NavPanel.Children.OfType<Border>())
        {
            if (child.Tag is not Guid id) continue;
            child.Background = id == _activeSubId
                ? new SolidColorBrush(Color.FromArgb(0x18, accentC.R, accentC.G, accentC.B))
                : Brushes.Transparent;
        }
    }

    private void UpdateProgress()
    {
        var graded = _isGraded.Count(kv => kv.Value);
        var total = _subs.Count;
        TxtProgress.Text = $"{graded} / {total} graded";
        TxtProgress.Foreground = graded == total && total > 0
            ? ThemeManager.GetBrush("ThSuccess")
            : ThemeManager.GetBrush("ThTextSecondary");
    }

    // ── Question Selection ────────────────────────────────────────────────────
    private async Task SelectQuestionAsync(Guid subId)
    {
        // Persist unsaved edits from current question
        if (_activeSubId != Guid.Empty)
            _edits[_activeSubId] = (ScoreBox.Text, FeedbackBox.Text);

        _activeSubId = subId;
        HighlightActiveNavItem();
        TxtSaveStatus.Text = "";
        OutputBorder.Visibility = Visibility.Collapsed;

        // Show all panels
        TxtSelectHint.Visibility  = Visibility.Collapsed;
        InfoScroll.Visibility     = Visibility.Visible;
        CodeHeaderRow.Visibility  = Visibility.Visible;
        MonacoBorder.Visibility   = Visibility.Visible;
        GradingScroll.Visibility  = Visibility.Visible;

        // Fetch detail with cache
        if (!_detailCache.TryGetValue(subId, out var detail))
        {
            var fetched = await _svc.GetSubmissionDetailAsync(subId);
            if (fetched is null) { ShowStatus("Could not load question.", error: true); return; }
            _detailCache[subId] = fetched;
            detail = fetched;
        }

        // Populate question + guide
        TxtQuestionText.Text = detail.QuestionText;

        var hasGuide = !string.IsNullOrWhiteSpace(detail.MarkingGuide);
        TxtGuidLabel.Visibility = hasGuide ? Visibility.Visible : Visibility.Collapsed;
        GuideCard.Visibility    = hasGuide ? Visibility.Visible : Visibility.Collapsed;
        if (hasGuide)
        {
            TxtGuide.Text = detail.MarkingGuide;
            var wC = ThemeManager.GetColor("ThWarning");
            GuideCard.Background      = new SolidColorBrush(Color.FromArgb(0x14, wC.R, wC.G, wC.B));
            GuideCard.BorderBrush     = new SolidColorBrush(Color.FromArgb(0x50, wC.R, wC.G, wC.B));
            GuideCard.BorderThickness = new Thickness(3, 0, 0, 0);
        }

        // Code header
        TxtLang.Text = detail.Language.ToUpper();
        var aC = ThemeManager.GetColor("ThAccent");
        LangBadge.Background = new SolidColorBrush(Color.FromArgb(0x22, aC.R, aC.G, aC.B));
        TxtLang.Foreground   = new SolidColorBrush(aC);

        var executable = detail.Language.ToLowerInvariant() is "python" or "java" or "cpp" or "c++" or "javascript" or "js";
        var previewable = HtmlPreview.IsPreviewLanguage(detail.Language);
        BtnRun.Visibility = executable || previewable ? Visibility.Visible : Visibility.Collapsed;
        BtnRun.Content = previewable ? "▣  Preview" : "▶  Run Code";

        // Grading inputs
        TxtMaxMarks.Text = $"/ {detail.MaxMarks}";

        // Priority: in-memory edits → stored grade → empty
        if (_edits.TryGetValue(subId, out var pending))
        {
            ScoreBox.Text    = pending.Score;
            FeedbackBox.Text = pending.Feedback;
        }
        else if (detail.Grade is not null)
        {
            ScoreBox.Text    = detail.Grade.Score.ToString();
            FeedbackBox.Text = detail.Grade.Feedback;
        }
        else
        {
            ScoreBox.Text    = "";
            FeedbackBox.Text = "";
        }

        // Load code into Monaco (or queue if not ready)
        if (_editorReady)
            await LoadCodeAsync(detail.Code, detail.Language);
    }

    // ── Monaco ────────────────────────────────────────────────────────────────
    private async Task InitMonacoAsync()
    {
        try
        {
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScoreCode", "WebView2Grading");

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await MonacoView.EnsureCoreWebView2Async(env);

            MonacoView.CoreWebView2.Settings.AreDevToolsEnabled          = false;
            MonacoView.CoreWebView2.Settings.IsStatusBarEnabled          = false;
            MonacoView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            var assetsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            MonacoView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "grading.scorecode.local", assetsFolder,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

            MonacoView.CoreWebView2.WebMessageReceived += Monaco_MessageReceived;
            MonacoView.CoreWebView2.Navigate("https://grading.scorecode.local/monaco-editor.html");
        }
        catch { /* editor init failed — fallback gracefully */ }
    }

    private void Monaco_MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.TryGetWebMessageAsString());
            if (doc.RootElement.GetProperty("type").GetString() != "ready") return;

            _editorReady = true;
            Dispatcher.InvokeAsync(async () =>
            {
                // Apply current app theme
                var monacoTheme = ThemeManager.CurrentTheme == AppTheme.Dark
                    ? "scorecode-dark" : "scorecode-light";
                await MonacoView.ExecuteScriptAsync($"setTheme('{monacoTheme}')");
                await MonacoView.ExecuteScriptAsync("setReadOnly(true)");

                // If a question was already selected before Monaco was ready, load it now
                if (_activeSubId != Guid.Empty && _detailCache.TryGetValue(_activeSubId, out var d))
                    await LoadCodeAsync(d.Code, d.Language);
            });
        }
        catch { }
    }

    private async Task LoadCodeAsync(string code, string language)
    {
        var lang    = MapLang(language);
        var escaped = JsonSerializer.Serialize(code);
        await MonacoView.ExecuteScriptAsync($"setLanguage('{lang}')");
        await MonacoView.ExecuteScriptAsync($"setValue({escaped})");
    }

    private static string MapLang(string lang) => lang.ToLowerInvariant() switch
    {
        "python"                    => "python",
        "java"                      => "java",
        "cpp" or "c++"              => "cpp",
        "javascript" or "js"        => "javascript",
        "html"                      => "html",
        "css"                       => "css",
        _                           => "plaintext"
    };

    // ── Save current question ─────────────────────────────────────────────────
    private async Task SaveCurrentQuestionAsync()
    {
        if (_activeSubId == Guid.Empty) return;

        var sub = _subs.First(s => s.Id == _activeSubId);

        if (!_detailCache.TryGetValue(_activeSubId, out var detail)) return;

        var scoreText = ScoreBox.Text.Trim();

        // Blank = skip gracefully
        if (string.IsNullOrEmpty(scoreText))
        {
            ShowStatus("Enter a score to save, or use Skip / Clear.", error: true);
            return;
        }

        if (!int.TryParse(scoreText, out var score) || score < 0 || score > detail.MaxMarks)
        {
            ShowStatus($"Score must be 0 – {detail.MaxMarks}.", error: true);
            return;
        }

        BtnSaveQuestion.IsEnabled = false;
        ShowStatus("Saving…");

        var req = new GradeSubmissionRequest
        {
            Score    = score,
            Feedback = FeedbackBox.Text.Trim()
        };

        var ok = await _svc.GradeAsync(_activeSubId, req);
        BtnSaveQuestion.IsEnabled = true;

        if (ok)
        {
            // Update cache so we don't overwrite with old values
            detail.Grade = new GradeDto
            {
                Score    = score,
                Feedback = req.Feedback,
                GradedBy = "",
                GradedAt = DateTime.UtcNow,
                MaxScore = detail.MaxMarks
            };
            // Update list item score for nav preview
            sub.Score     = score;
            _isGraded[_activeSubId] = true;
            _edits.Remove(_activeSubId);   // persisted — clear pending edits

            RefreshNavItem(_activeSubId);
            UpdateProgress();
            ShowStatus("✓ Saved", error: false);
        }
        else
        {
            ShowStatus("Failed to save. Please try again.", error: true);
        }
    }

    // ── Save all graded (partial allowed) ────────────────────────────────────
    private async Task SaveAllGradedAsync()
    {
        // Persist current field values into edits dict first
        if (_activeSubId != Guid.Empty)
            _edits[_activeSubId] = (ScoreBox.Text, FeedbackBox.Text);

        BtnSaveAll.IsEnabled = false;
        int saved = 0, skipped = 0, failed = 0;

        foreach (var sub in _subs)
        {
            // Determine score to save: current scoreBox (if active) or stored edit
            string scoreText;
            string feedbackText;

            if (sub.Id == _activeSubId)
            {
                scoreText    = ScoreBox.Text.Trim();
                feedbackText = FeedbackBox.Text.Trim();
            }
            else if (_edits.TryGetValue(sub.Id, out var edit))
            {
                scoreText    = edit.Score.Trim();
                feedbackText = edit.Feedback.Trim();
            }
            else if (_isGraded[sub.Id])
            {
                // Already graded on server, nothing to do
                saved++;
                continue;
            }
            else
            {
                skipped++;
                continue;   // blank = intentionally ungraded
            }

            if (string.IsNullOrEmpty(scoreText)) { skipped++; continue; }

            if (!_detailCache.TryGetValue(sub.Id, out var detail))
            {
                // Need to fetch to know maxMarks for validation
                detail = await _svc.GetSubmissionDetailAsync(sub.Id);
                if (detail is null) { failed++; continue; }
                _detailCache[sub.Id] = detail;
            }

            if (!int.TryParse(scoreText, out var score) || score < 0 || score > detail.MaxMarks)
            {
                failed++;
                continue;
            }

            var req = new GradeSubmissionRequest { Score = score, Feedback = feedbackText };
            var ok  = await _svc.GradeAsync(sub.Id, req);

            if (ok)
            {
                detail.Grade = new GradeDto
                {
                    Score = score, Feedback = req.Feedback,
                    GradedBy = "", GradedAt = DateTime.UtcNow, MaxScore = detail.MaxMarks
                };
                sub.Score           = score;
                _isGraded[sub.Id]   = true;
                _edits.Remove(sub.Id);
                saved++;
                RefreshNavItem(sub.Id);
            }
            else { failed++; }
        }

        UpdateProgress();
        BtnSaveAll.IsEnabled = true;

        var msg = $"✓ {saved} saved";
        if (skipped > 0) msg += $"  ·  {skipped} skipped";
        if (failed  > 0) msg += $"  ·  {failed} failed";
        ShowStatus(msg, error: failed > 0);
    }

    // ── AI-suggested grade ─────────────────────────────────────────────────────
    private async Task AiSuggestAsync()
    {
        if (_activeSubId == Guid.Empty) return;

        var original = BtnAiSuggest.Content;
        BtnAiSuggest.IsEnabled = false;
        BtnAiSuggest.Content = "✨ Thinking…";
        ShowStatus("Asking AI for a suggested grade…");

        try
        {
            var resp = await _svc.AiSuggestAsync(_activeSubId);
            if (resp?.Success == true && resp.Data is not null)
            {
                ScoreBox.Text = resp.Data.Score.ToString();

                // Prefix so it's obvious the feedback is an editable AI draft.
                var suggestion = resp.Data.Feedback?.Trim() ?? "";
                if (!string.IsNullOrEmpty(suggestion))
                    FeedbackBox.Text = $"[AI suggestion — review before saving]\n{suggestion}";

                ShowStatus($"✨ AI suggested {resp.Data.Score}/{resp.Data.MaxScore}. Review and Save.");
            }
            else
            {
                ShowStatus(resp?.Message ?? "AI grading failed.", error: true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"AI grading failed: {ex.Message}", error: true);
        }
        finally
        {
            BtnAiSuggest.Content = original;
            BtnAiSuggest.IsEnabled = true;
        }
    }

    // ── Clear / Skip ─────────────────────────────────────────────────────────
    private void ClearCurrentGrade()
    {
        ScoreBox.Text    = "";
        FeedbackBox.Text = "";
        _edits.Remove(_activeSubId);
        ShowStatus("Cleared — question will remain ungraded.", error: false);
    }

    // ── Run Code ─────────────────────────────────────────────────────────────
    private async Task RunCurrentCodeAsync()
    {
        if (_activeSubId == Guid.Empty) return;
        if (!_detailCache.TryGetValue(_activeSubId, out var detail)) return;

        // HTML/CSS → live preview instead of execution
        if (HtmlPreview.IsPreviewLanguage(detail.Language))
        {
            await ShowPreviewAsync(detail.Code, detail.Language);
            return;
        }

        OutputTextScroll.Visibility = Visibility.Visible;
        PreviewWeb.Visibility      = Visibility.Collapsed;
        BtnRun.IsEnabled           = false;
        OutputBorder.Visibility    = Visibility.Visible;
        TxtOutput.Text             = "Running…";
        TxtOutput.Foreground       = ThemeManager.GetBrush("ThTextSecondary");

        try
        {
            var result = await CodeExecutionService.ExecuteAsync(detail.Code, detail.Language);
            var sb     = new StringBuilder();

            if (result.CompilationError is not null)
            {
                sb.AppendLine($"Compilation Error:\n{result.CompilationError}");
                TxtOutput.Foreground = ThemeManager.GetBrush("ThError");
            }
            else if (result.TimedOut)
            {
                if (!string.IsNullOrWhiteSpace(result.Stdout)) sb.AppendLine(result.Stdout);
                sb.AppendLine("⏱ Timed out (5s CPU limit)");
                TxtOutput.Foreground = ThemeManager.GetBrush("ThWarning");
            }
            else if (result.MemoryExceeded)
            {
                if (!string.IsNullOrWhiteSpace(result.Stdout)) sb.AppendLine(result.Stdout);
                sb.AppendLine("⚠ Memory limit exceeded (256 MB)");
                TxtOutput.Foreground = ThemeManager.GetBrush("ThWarning");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(result.Stdout)) sb.AppendLine(result.Stdout);
                if (!string.IsNullOrWhiteSpace(result.Stderr)) sb.AppendLine(result.Stderr);
                TxtOutput.Foreground = !string.IsNullOrWhiteSpace(result.Stderr)
                    ? ThemeManager.GetBrush("ThError")
                    : ThemeManager.GetBrush("ThTextPrimary");
            }

            TxtOutput.Text = sb.Length > 0 ? sb.ToString().TrimEnd() : "(no output)";
        }
        catch (Exception ex)
        {
            TxtOutput.Text       = $"Execution failed: {ex.Message}";
            TxtOutput.Foreground = ThemeManager.GetBrush("ThError");
        }
        finally { BtnRun.IsEnabled = true; }
    }

    /// <summary>Render an HTML/CSS submission live in the WebView2 preview pane.</summary>
    private async Task ShowPreviewAsync(string code, string lang)
    {
        OutputBorder.Visibility     = Visibility.Visible;
        OutputTextScroll.Visibility = Visibility.Collapsed;
        PreviewWeb.Visibility       = Visibility.Visible;

        try
        {
            await HtmlPreview.RenderAsync(PreviewWeb, code, lang, "WebView2Grading");
        }
        catch (Exception ex)
        {
            PreviewWeb.Visibility       = Visibility.Collapsed;
            OutputTextScroll.Visibility = Visibility.Visible;
            TxtOutput.Text = $"Preview failed: {ex.Message}";
            TxtOutput.Foreground = ThemeManager.GetBrush("ThError");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void ShowStatus(string msg, bool error = false)
    {
        TxtSaveStatus.Text       = msg;
        TxtSaveStatus.Foreground = error
            ? ThemeManager.GetBrush("ThError")
            : ThemeManager.GetBrush("ThSuccess");
    }
}
