using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Exams;

namespace ScoreCode.Client.Views.Student;

public partial class ExamView : Window
{
    private readonly ExamStartResponse _exam;
    private readonly ApiService _api;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _autosaveTimer;
    private readonly AutosaveService _autosave;
    private int _activeQuestionIndex = 0;
    private bool _editorReady = false;
    private bool _submitting = false;

    // Anti-cheat lockdown
    private KeyboardLockHook? _keyboardLock;
    private bool _lockdownActive;
    private int _violationCount;
    private DateTime _lastBlockedKeyLogUtc = DateTime.MinValue;

    // Stores the student's code per question (questionId → code)
    private readonly Dictionary<Guid, string> _answers = new();

    // Stores full editor state per question (code + cursor + scroll)
    private readonly Dictionary<Guid, string> _questionStates = new();

    // Rail item references for fast highlight updates
    private readonly List<(Button btn, Ellipse ring, TextBlock num, TextBlock title, TextBlock sub)> _tabRefs = new();
    private static readonly FontFamily _railFont = new("/Assets/Fonts/#Geist");
    private static readonly FontFamily _railMono = new("/Assets/Fonts/#Geist Mono, Consolas");

    public ExamView(ExamStartResponse exam, ApiService api)
    {
        InitializeComponent();
        _exam = exam;
        _api = api;

        // Title bar — apply dark mode and branded colors
        TitleBarHelper.ApplyDarkMode(this);
        TitleBarHelper.SetCaptionColor(this, 0x0D, 0x11, 0x17);
        TitleBarHelper.SetIcon(this);

        // Exam info
        TxtExamTitle.Text = $"{exam.CourseCode} — {exam.Title}";
        Title = $"ScoreCode · {exam.CourseCode}";

        // Build the question rail
        BuildQuestionTabs();

        // Timer
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        _timer.Start();
        UpdateTimerDisplay();

        // Autosave timer (every 5 seconds)
        _autosave = new AutosaveService();
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _autosaveTimer.Tick += AutosaveTick;
        _autosaveTimer.Start();

        // Restore from autosave if exists
        RestoreFromAutosave();

        // Initialize Monaco via WebView2
        InitializeEditorAsync();

        // Show first question (editor content loaded after Monaco is ready)
        ShowQuestion(0);

        // Engage anti-cheat lockdown (keyboard hook + focus-loss detection)
        EnterLockdown();
    }

    // ── Anti-cheat lockdown ────────────────────────────────────────────────
    private void EnterLockdown()
    {
        if (_lockdownActive) return;
        _lockdownActive = true;

        // Swallow OS task-switch shortcuts (Alt+Tab, Win, Ctrl+Esc, Alt+F4).
        _keyboardLock = new KeyboardLockHook(OnBlockedKey);

        // Detect any focus loss the hook can't prevent (mouse to a second
        // monitor, Ctrl+Alt+Del, etc.) and pull the window back to the front.
        Deactivated += ExamView_Deactivated;
    }

    /// <summary>Release the lockdown. Idempotent; safe to call more than once.</summary>
    private void ExitLockdown()
    {
        if (!_lockdownActive) return;
        _lockdownActive = false;

        Deactivated -= ExamView_Deactivated;
        _keyboardLock?.Dispose();
        _keyboardLock = null;
    }

    private void OnBlockedKey(string description) =>
        RegisterViolation(
            ScoreCode.Shared.Constants.ActivityEvents.BlockedKey,
            $"Blocked shortcut: {description}",
            throttleNetwork: true);

    private void ExamView_Deactivated(object? sender, EventArgs e)
    {
        if (!_lockdownActive || _submitting) return;

        // Ignore focus changes to our own dialogs (MessageBox, etc.).
        if (IsForegroundOwnProcess()) return;

        // Force the exam window back to the foreground.
        Dispatcher.BeginInvoke(() =>
        {
            if (!_lockdownActive || _submitting) return;
            Activate();
            Topmost = false;
            Topmost = true;
        });

        RegisterViolation(
            ScoreCode.Shared.Constants.ActivityEvents.FocusLost,
            "Exam window lost focus (task switch / second monitor).",
            throttleNetwork: false);
    }

    /// <summary>
    /// Records a lockdown violation: bumps the visible counter and reports it to
    /// the server. Blocked-key spam (key auto-repeat) is throttled so we don't
    /// flood the API; genuine focus-loss events are always reported.
    /// </summary>
    private void RegisterViolation(string eventType, string detail, bool throttleNetwork)
    {
        _violationCount++;

        TxtAutosave.Text = $"⚠ Locked — leaving the exam is recorded ({_violationCount})";
        TxtAutosave.Foreground = ThemeManager.GetBrush("ThError");

        if (throttleNetwork)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastBlockedKeyLogUtc).TotalSeconds < 2) return;
            _lastBlockedKeyLogUtc = now;
        }

        _ = new ExamClientService(_api).LogViolationAsync(_exam.ExamId,
            new LogViolationRequest
            {
                SessionId = _exam.SessionId,
                EventType = eventType,
                Detail = detail
            });
    }

    private static bool IsForegroundOwnProcess()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Environment.ProcessId;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // ── Monaco / WebView2 ─────────────────────────────────────────────────
    private async void InitializeEditorAsync()
    {
        try
        {
            // Paint the WebView2 in the theme's code background so there's no
            // dark flash before Monaco finishes loading in light mode.
            var codeBg = ThemeManager.GetColor("ThCodeBg");
            Editor.DefaultBackgroundColor = System.Drawing.Color.FromArgb(codeBg.R, codeBg.G, codeBg.B);

            // Set up user data folder for WebView2
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScoreCode", "WebView2");

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder);

            await Editor.EnsureCoreWebView2Async(env);

            // Listen for messages from Monaco
            Editor.CoreWebView2.WebMessageReceived += Monaco_WebMessageReceived;

            // Settings
            Editor.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Editor.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Editor.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            // Map Assets folder to a virtual host so fonts & Monaco load correctly
            var assetsFolder = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets");
            Editor.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.scorecode.local", assetsFolder,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

            // Navigate via virtual host (not file://) — fixes font loading and widget DPI
            Editor.CoreWebView2.Navigate("https://app.scorecode.local/monaco-editor.html");
        }
        catch (Exception ex)
        {
            TxtAutosave.Text = $"Editor error: {ex.Message}";
        }
    }

    private void Monaco_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var rawMessage = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(rawMessage)) return;

            using var doc = JsonDocument.Parse(rawMessage);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "ready":
                    _editorReady = true;
                    Dispatcher.InvokeAsync(async () =>
                    {
                        // Sync Monaco theme with app theme
                        var monacoTheme = ThemeManager.CurrentTheme == AppTheme.Dark ? "scorecode-dark" : "scorecode-light";
                        await Editor.ExecuteScriptAsync($"setTheme('{monacoTheme}')");

                        // Apply IntelliSense setting from exam config
                        var intelliSense = _exam.IntelliSenseEnabled ? "true" : "false";
                        await Editor.ExecuteScriptAsync($"setIntelliSense({intelliSense})");

                        await LoadQuestionIntoEditor(_activeQuestionIndex);
                        await Editor.ExecuteScriptAsync("disableCopyPaste()");
                    });
                    break;

                case "error":
                    var errorMsg = root.GetProperty("message").GetString() ?? "Unknown editor error";
                    Dispatcher.InvokeAsync(() =>
                    {
                        TxtAutosave.Text = errorMsg;
                        TxtAutosave.Foreground = ThemeManager.GetBrush("ThError");
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            Dispatcher.InvokeAsync(() =>
                TxtAutosave.Text = $"Msg error: {ex.Message}");
        }
    }

    private async Task LoadQuestionIntoEditor(int index)
    {
        if (!_editorReady) return;

        var q = _exam.Questions[index];
        var monacoLang = MapLanguage(q.Language);

        // Restore full state if we have it, otherwise create fresh state
        if (_questionStates.TryGetValue(q.Id, out var savedState))
        {
            var escapedState = JsonSerializer.Serialize(savedState);
            await Editor.ExecuteScriptAsync($"setEditorState({escapedState})");
        }
        else
        {
            var existingCode = _answers.TryGetValue(q.Id, out var c) ? c : "";
            var freshState = JsonSerializer.Serialize(new
            {
                code = existingCode,
                language = monacoLang,
                cursorLine = 1,
                cursorColumn = 1,
                scrollTop = 0
            });
            var escapedFresh = JsonSerializer.Serialize(freshState);
            await Editor.ExecuteScriptAsync($"setEditorState({escapedFresh})");
        }
    }

    private static string MapLanguage(string language) => language.ToLowerInvariant() switch
    {
        "python" => "python",
        "java" => "java",
        "cpp" or "c++" => "cpp",
        "javascript" or "js" => "javascript",
        "html" => "html",
        "css" => "css",
        _ => "plaintext"
    };

    /// <summary>Save full editor state (code + cursor + scroll) for the active question.</summary>
    private async Task SaveCurrentEditorContent()
    {
        if (!_editorReady) return;

        try
        {
            // Get full state (code + cursor + scroll)
            var rawState = await Editor.ExecuteScriptAsync("getEditorState()");
            var stateJson = JsonSerializer.Deserialize<string>(rawState) ?? "{}";
            var qId = _exam.Questions[_activeQuestionIndex].Id;
            _questionStates[qId] = stateJson;

            // Also extract code into _answers for submission/autosave
            using var stateDoc = JsonDocument.Parse(stateJson);
            if (stateDoc.RootElement.TryGetProperty("code", out var codeProp))
                _answers[qId] = codeProp.GetString() ?? "";
        }
        catch { /* ignore if editor not ready */ }
    }

    // ── Question Rail ──────────────────────────────────────────────────────
    private void BuildQuestionTabs()
    {
        QuestionTabs.Children.Clear();
        _tabRefs.Clear();

        for (int i = 0; i < _exam.Questions.Count; i++)
        {
            var q = _exam.Questions[i];
            var index = i;

            // Numbered state badge (ring + number)
            var ring = new Ellipse
            {
                Width = 30, Height = 30, StrokeThickness = 2.5,
                Stroke = ThemeManager.GetBrush("ThBorderStrong"),
                Fill = Brushes.Transparent
            };
            var num = new TextBlock
            {
                Text = (i + 1).ToString(), FontSize = 12, FontFamily = _railMono,
                Foreground = ThemeManager.GetBrush("ThTextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            var badge = new Grid
            {
                Width = 30, Height = 30, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Children = { ring, num }
            };

            var title = new TextBlock
            {
                Text = $"Question {q.OrderIndex + 1}", FontSize = 13, FontWeight = FontWeights.Medium,
                Foreground = ThemeManager.GetBrush("ThTextSecondary"), FontFamily = _railFont,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var sub = new TextBlock
            {
                Text = $"{q.Marks} marks · not started", FontSize = 11,
                Foreground = ThemeManager.GetBrush("ThTextMuted"), FontFamily = _railFont,
                Margin = new Thickness(0, 1, 0, 0)
            };
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(title);
            textStack.Children.Add(sub);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(badge);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var tab = new Button
            {
                Style = FindResource("RailItem") as Style,
                Content = grid,
                Tag = index
            };
            tab.Click += QuestionTab_Click;
            tab.MouseEnter += (_, _) => { if (index != _activeQuestionIndex) tab.Background = ThemeManager.GetBrush("ThBgHover"); };
            tab.MouseLeave += (_, _) => { if (index != _activeQuestionIndex) tab.Background = Brushes.Transparent; };

            QuestionTabs.Children.Add(tab);
            _tabRefs.Add((tab, ring, num, title, sub));
        }
    }

    private async void QuestionTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index)
        {
            // Save current answer, switch, load — fully sequential
            await SaveCurrentEditorContent();
            await ShowQuestionAsync(index);
        }
    }

    /// <summary>Non-async overload for initial load (editor not ready yet).</summary>
    private void ShowQuestion(int index)
    {
        if (index < 0 || index >= _exam.Questions.Count) return;

        _activeQuestionIndex = index;
        var q = _exam.Questions[index];

        TxtQuestionLabel.Text = $"Question {q.OrderIndex + 1}";
        TxtQuestionMarks.Text = $" · {q.Marks} marks";
        TxtQuestionText.Text = q.Text;
        TxtStatusLang.Text = q.Language;
        UpdateTabHighlights();

        // Editor content loaded later when "ready" message arrives
    }

    /// <summary>Async overload — updates UI AND awaits editor content swap.</summary>
    private async Task ShowQuestionAsync(int index)
    {
        if (index < 0 || index >= _exam.Questions.Count) return;

        _activeQuestionIndex = index;
        var q = _exam.Questions[index];

        TxtQuestionLabel.Text = $"Question {q.OrderIndex + 1}";
        TxtQuestionMarks.Text = $" · {q.Marks} marks";
        TxtQuestionText.Text = q.Text;
        TxtStatusLang.Text = q.Language;
        UpdateTabHighlights();

        if (_editorReady)
            await LoadQuestionIntoEditor(index);
    }

    private void UpdateTabHighlights()
    {
        var accent = ThemeManager.GetBrush("ThAccent");
        var accentStrong = ThemeManager.GetBrush("ThAccentStrong");
        var accentSoft = ThemeManager.GetBrush("ThAccentSoft");
        var success = ThemeManager.GetBrush("ThSuccess");
        var successSoft = ThemeManager.GetBrush("ThSuccessSoft");
        var borderStrong = ThemeManager.GetBrush("ThBorderStrong");
        var primary = ThemeManager.GetBrush("ThTextPrimary");
        var secondary = ThemeManager.GetBrush("ThTextSecondary");
        var muted = ThemeManager.GetBrush("ThTextMuted");

        for (int i = 0; i < _tabRefs.Count; i++)
        {
            var (btn, ring, num, title, sub) = _tabRefs[i];
            bool active = i == _activeQuestionIndex;
            var q = _exam.Questions[i];
            bool answered = _answers.TryGetValue(q.Id, out var a) && !string.IsNullOrWhiteSpace(a);

            btn.Background = active ? accentSoft : Brushes.Transparent;

            ring.Stroke = answered ? success : active ? accent : borderStrong;
            ring.Fill = answered ? successSoft : Brushes.Transparent;
            num.Foreground = answered ? success : active ? accentStrong : secondary;

            title.Foreground = active ? primary : secondary;
            sub.Text = $"{q.Marks} marks · {(answered ? "answered" : active ? "in progress" : "not started")}";
            sub.Foreground = muted;
        }

        UpdateProgress();
    }

    // ── Progress card ──────────────────────────────────────────────────────
    private void UpdateProgress()
    {
        int total = _exam.Questions.Count;
        int answered = _exam.Questions.Count(q => _answers.TryGetValue(q.Id, out var a) && !string.IsNullOrWhiteSpace(a));

        TxtProgressCount.Text = $"{answered}/{total}";
        ProgFill.Width = new GridLength(answered, GridUnitType.Star);
        ProgRest.Width = new GridLength(Math.Max(total - answered, 0), GridUnitType.Star);
    }

    // ── Timer ──────────────────────────────────────────────────────────────
    private void Timer_Tick(object? sender, EventArgs e) => UpdateTimerDisplay();

    private void UpdateTimerDisplay()
    {
        var remaining = _exam.EndsAt.ToUniversalTime() - DateTime.UtcNow;

        // Auto-submit when time runs out
        if (remaining.TotalSeconds <= 0 && !_submitting)
        {
            _submitting = true;
            _timer.Stop();
            _autosaveTimer.Stop();
            TxtTimer.Text = "00:00:00";
            TxtTimer.Foreground = ThemeManager.GetBrush("ThError");

            Dispatcher.InvokeAsync(async () =>
            {
                await SaveCurrentEditorContent();
                EnterSubmissionMode();
                MessageBox.Show(
                    "Time's up! Your exam is being submitted automatically.",
                    "Exam Time Expired", MessageBoxButton.OK, MessageBoxImage.Warning);
                await AttemptSubmitAsync();
            });
            return;
        }

        if (remaining.TotalSeconds <= 0) return; // already submitting

        TxtTimer.Text = $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";

        var totalSeconds = _exam.DurationMinutes * 60.0;
        var percentRemaining = remaining.TotalSeconds / totalSeconds;

        if (percentRemaining > 0.25)
            TxtTimer.Foreground = ThemeManager.GetBrush("ThAccent");
        else if (percentRemaining > 0.10)
            TxtTimer.Foreground = ThemeManager.GetBrush("ThWarning");
        else
            TxtTimer.Foreground = ThemeManager.GetBrush("ThError");
    }

    // ── Run Code ──────────────────────────────────────────────────────────
    private async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (!_editorReady) return;

        // Get current code
        string code;
        try
        {
            var raw = await Editor.ExecuteScriptAsync("getValue()");
            code = System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? "";
        }
        catch { return; }

        if (string.IsNullOrWhiteSpace(code))
        {
            ShowOutput("No code to run.", "", 0);
            return;
        }

        var lang = _exam.Questions[_activeQuestionIndex].Language;

        // HTML/CSS → live preview instead of text execution
        if (HtmlPreview.IsPreviewLanguage(lang))
        {
            await ShowHtmlPreviewAsync(code, lang);
            return;
        }

        // Show output panel + "Running…" (text mode)
        OutputPanel.Visibility = Visibility.Visible;
        OutputTextScroll.Visibility = Visibility.Visible;
        PreviewWeb.Visibility = Visibility.Collapsed;
        TxtOutput.Text = "Running…";
        TxtOutput.Foreground = ThemeManager.GetBrush("ThTextSecondary");
        TxtExecTime.Text = "";
        BtnRun.IsEnabled = false;

        try
        {
            var result = await Services.CodeExecutionService.ExecuteAsync(code, lang);

            if (result.CompilationError is not null)
            {
                ShowOutput("", $"Compilation Error:\n{result.CompilationError}", result.ExecutionTimeMs);
            }
            else if (result.TimedOut)
            {
                ShowOutput(result.Stdout, "⏱ Execution timed out (5s CPU limit exceeded)", result.ExecutionTimeMs);
            }
            else if (result.MemoryExceeded)
            {
                ShowOutput(result.Stdout, "⚠ Memory limit exceeded (256 MB)", result.ExecutionTimeMs);
            }
            else
            {
                ShowOutput(result.Stdout, result.Stderr, result.ExecutionTimeMs);
            }
        }
        catch (Exception ex)
        {
            ShowOutput("", $"Execution failed: {ex.Message}", 0);
        }
        finally
        {
            BtnRun.IsEnabled = true;
        }
    }

    /// <summary>Render an HTML/CSS answer live in the WebView2 preview pane.</summary>
    private async Task ShowHtmlPreviewAsync(string code, string lang)
    {
        OutputPanel.Visibility = Visibility.Visible;
        OutputTextScroll.Visibility = Visibility.Collapsed;
        PreviewWeb.Visibility = Visibility.Visible;
        TxtExecTime.Text = "Live preview";

        try
        {
            // Reuse the editor's WebView2 user-data folder ("WebView2") so the
            // process keeps a single user-data folder.
            await HtmlPreview.RenderAsync(PreviewWeb, code, lang, "WebView2");
        }
        catch (Exception ex)
        {
            PreviewWeb.Visibility = Visibility.Collapsed;
            ShowOutput("", $"Preview failed: {ex.Message}", 0);
        }
    }

    private void ShowOutput(string stdout, string stderr, double timeMs)
    {
        OutputPanel.Visibility = Visibility.Visible;
        OutputTextScroll.Visibility = Visibility.Visible;
        PreviewWeb.Visibility = Visibility.Collapsed;

        var output = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(stdout))
            output.AppendLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            if (output.Length > 0) output.AppendLine();
            output.AppendLine(stderr);
        }

        TxtOutput.Text = output.Length > 0 ? output.ToString().TrimEnd() : "(no output)";
        TxtOutput.Foreground = !string.IsNullOrWhiteSpace(stderr)
            ? ThemeManager.GetBrush("ThError")   // Red for errors
            : ThemeManager.GetBrush("ThTextPrimary");  // Normal text

        TxtExecTime.Text = timeMs > 0 ? $"{timeMs:F0}ms" : "";
    }

    private void BtnClearOutput_Click(object sender, RoutedEventArgs e)
    {
        TxtOutput.Text = "";
        TxtExecTime.Text = "";
        PreviewWeb.Visibility = Visibility.Collapsed;
        OutputTextScroll.Visibility = Visibility.Visible;
        OutputPanel.Visibility = Visibility.Collapsed;
    }

    // ── Submit ─────────────────────────────────────────────────────────────
    private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        if (_submitting) return;

        // Save the current editor state
        await SaveCurrentEditorContent();

        // Build list of unanswered question numbers
        var unanswered = new List<string>();
        for (int i = 0; i < _exam.Questions.Count; i++)
        {
            var q = _exam.Questions[i];
            if (!_answers.ContainsKey(q.Id) || string.IsNullOrWhiteSpace(_answers[q.Id]))
                unanswered.Add($"Q{q.OrderIndex + 1}");
        }

        var message = unanswered.Count > 0
            ? $"{string.Join(" and ", unanswered)} {(unanswered.Count == 1 ? "has" : "have")} no code written.\n\nAre you sure you want to submit?"
            : "All questions have been answered.\n\nSubmit your exam?";

        var result = MessageBox.Show(message, "Submit Exam",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _submitting = true;
            _timer.Stop();
            _autosaveTimer.Stop();
            EnterSubmissionMode();
            await AttemptSubmitAsync();
        }
    }

    /// <summary>Lock editor, release anti-cheat so student can fix network if needed.</summary>
    private void EnterSubmissionMode()
    {
        // Release the anti-cheat lockdown — the student is done and may need to
        // interact with the OS (e.g. fix their network to retry submission).
        ExitLockdown();

        // Lock editor to read-only
        if (_editorReady)
            _ = Editor.ExecuteScriptAsync("setReadOnly(true)");

        // Release the restricted-mode lock but keep window maximized for UI consistency
        Topmost = false;
        ResizeMode = ResizeMode.CanResize;
        WindowStyle = WindowStyle.SingleBorderWindow;

        // Re-apply dark title bar (WindowStyle change recreates chrome)
        TitleBarHelper.ApplyDarkMode(this);
        TitleBarHelper.SetCaptionColor(this, 0x0D, 0x11, 0x17);
        TitleBarHelper.SetIcon(this);

        // Show submission overlay
        SubmissionOverlay.Visibility = Visibility.Visible;
        TxtSubmitIcon.Text = "📤";
        TxtSubmitStatus.Text = "Submitting your exam...";
        TxtSubmitDetail.Text = "Please wait while we upload your answers.";
        BtnRetry.Visibility = Visibility.Collapsed;

        // Update status bar
        TxtAutosave.Text = "Submitting...";
        TxtAutosave.Foreground = ThemeManager.GetBrush("ThWarning");
        BtnSubmit.IsEnabled = false;
        BtnRun.IsEnabled = false;
    }

    /// <summary>
    /// SQLite-first submission: persist locally, then attempt API call.
    /// On failure, shows retry UI. On success, clears queue and closes.
    /// </summary>
    private async Task AttemptSubmitAsync()
    {
        // Step 1: Build the submission request
        var request = new SubmitExamRequest
        {
            SessionId = _exam.SessionId,
            Answers = _exam.Questions.Select(q =>
            {
                var code = _answers.TryGetValue(q.Id, out var c) ? c : "";
                var finalizedAt = DateTime.UtcNow;
                var hash = ComputeHash(_exam.Nonce, q.Id, finalizedAt, code);
                return new AnswerDto
                {
                    QuestionId = q.Id,
                    Code = code,
                    FinalizedAt = finalizedAt,
                    Hash = hash
                };
            }).ToList()
        };

        // Step 2: Persist to SQLite FIRST (crash safety)
        try
        {
            var payloadJson = JsonSerializer.Serialize(request);
            using var queue = new SubmissionQueueService();
            queue.QueueSubmission(_exam.SessionId, _exam.ExamId, payloadJson);
        }
        catch (Exception ex)
        {
            // SQLite write failed — extremely rare, but still show the error
            ShowSubmissionFailure($"Could not save locally: {ex.Message}");
            return;
        }

        // Step 3: Attempt API call
        await SendToApiAsync(request);
    }

    /// <summary>Attempt to send the submission to the API server.</summary>
    private async Task SendToApiAsync(SubmitExamRequest? request = null)
    {
        // Update overlay to "submitting" state
        TxtSubmitIcon.Text = "📤";
        TxtSubmitStatus.Text = "Submitting your exam...";
        TxtSubmitDetail.Text = "Please wait while we upload your answers.";
        BtnRetry.Visibility = Visibility.Collapsed;
        BtnRetry.IsEnabled = false;

        try
        {
            // If no request passed (retry), load from SQLite
            if (request is null)
            {
                using var queue = new SubmissionQueueService();
                var pending = queue.GetPending();
                if (pending is null)
                {
                    ShowSubmissionSuccess(); // Nothing to submit
                    return;
                }
                request = JsonSerializer.Deserialize<SubmitExamRequest>(pending.PayloadJson);
            }

            // Attempt API call
            var examService = new ExamClientService(_api);
            var success = await examService.SubmitExamAsync(_exam.ExamId, request!);

            if (success)
            {
                // Mark submitted in SQLite and clear autosave
                using var queue = new SubmissionQueueService();
                queue.MarkSubmitted(_exam.SessionId);
                queue.Remove(_exam.SessionId);
                _autosave.ClearSession(_exam.SessionId);

                ShowSubmissionSuccess();
            }
            else
            {
                ShowSubmissionFailure("Server rejected the submission.");
            }
        }
        catch (Exception ex)
        {
            ShowSubmissionFailure(ex.Message);
        }
    }

    private void ShowSubmissionSuccess()
    {
        TxtSubmitIcon.Text = "✅";
        TxtSubmitStatus.Text = "Exam Submitted Successfully!";
        TxtSubmitStatus.Foreground = ThemeManager.GetBrush("ThAccent");
        TxtSubmitDetail.Text = "Your answers have been received. This window will close shortly.";
        BtnRetry.Visibility = Visibility.Collapsed;
        TxtAutosave.Text = "✓ Submitted";
        TxtAutosave.Foreground = ThemeManager.GetBrush("ThAccent");

        // Close after a brief delay so student sees the success message
        var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        closeTimer.Tick += (_, _) => { closeTimer.Stop(); Close(); };
        closeTimer.Start();
    }

    private void ShowSubmissionFailure(string errorMessage)
    {
        TxtSubmitIcon.Text = "⚠️";
        TxtSubmitStatus.Text = "Could not reach the server";
        TxtSubmitStatus.Foreground = ThemeManager.GetBrush("ThError");
        TxtSubmitDetail.Text = $"{errorMessage}\n\nYour code is safely saved on this machine.\nCheck your network connection and try again.";
        BtnRetry.Visibility = Visibility.Visible;
        BtnRetry.IsEnabled = true;

        TxtAutosave.Text = "⚠ Submission failed";
        TxtAutosave.Foreground = ThemeManager.GetBrush("ThError");
    }

    private async void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
        await SendToApiAsync();
    }

    // ── Hash ───────────────────────────────────────────────────────────────
    private static string ComputeHash(string nonce, Guid questionId,
        DateTime finalizedAt, string code)
    {
        var session = SecureStorage.LoadSession();
        var studentId = session?.UserId ?? Guid.Empty;

        var input = $"{nonce}|{studentId}|{questionId}|{finalizedAt:O}|{code}";
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    // ── Autosave ──────────────────────────────────────────────────────────
    private void RestoreFromAutosave()
    {
        if (!_autosave.HasSavedSession(_exam.SessionId)) return;

        var saved = _autosave.LoadSession(_exam.SessionId);
        if (saved.Count == 0) return;

        foreach (var (qId, code) in saved)
            _answers[qId] = code;

        TxtAutosave.Text = $"Restored {saved.Count} answer(s)";
    }

    private async void AutosaveTick(object? sender, EventArgs e)
    {
        if (!_editorReady || _submitting) return;

        try
        {
            // Check if editor has been modified since last check
            var dirtyRaw = await Editor.ExecuteScriptAsync("isDirtyFlag()");
            var isDirty = dirtyRaw?.Trim('"') == "true";

            if (isDirty)
            {
                // Pull current code and update _answers for active question
                var raw = await Editor.ExecuteScriptAsync("getValue()");
                var code = JsonSerializer.Deserialize<string>(raw) ?? "";
                var qId = _exam.Questions[_activeQuestionIndex].Id;
                _answers[qId] = code;
                await Editor.ExecuteScriptAsync("clearDirtyFlag()");

                UpdateTabHighlights();
            }
        }
        catch { /* editor not ready */ }

        // Persist all answers to autosave
        if (_answers.Count == 0) return;

        foreach (var (qId, code) in _answers)
            _autosave.Save(_exam.SessionId, qId, code);

        TxtAutosave.Text = $"Saved · {DateTime.Now:HH:mm:ss}";
    }

    // ── Cleanup ────────────────────────────────────────────────────────────
    protected override void OnClosed(EventArgs e)
    {
        ExitLockdown(); // safety net — release the keyboard hook if still engaged
        _timer.Stop();
        _autosaveTimer.Stop();
        _autosave.Dispose();
        Editor.Dispose();
        PreviewWeb.Dispose();
        base.OnClosed(e);
    }
}
