using System.Windows;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Client.Views;
using ScoreCode.Client.Views.Auth;

namespace ScoreCode.Client;

public partial class App : Application
{
    // Held for the lifetime of the application; Dispose() kills the API process.
    private readonly ApiHostService _apiHost = new();

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        // ── 0. Initialize theme system ──────────────────────────────────
        ThemeManager.Initialize();

        // ── 1. Start the API in the background ──────────────────────────
        _apiHost.EnsureStarted();

        // Give the API a moment to build and start listening.
        // First run takes ~4s (build+migrate+seed), subsequent runs ~2s.
        await Task.Delay(5000);

        // ── 2. Attempt session restore ──────────────────────────────────
        var api  = new ApiService();
        var auth = new AuthClientService(api);

        if (SecureStorage.HasValidSession())
        {
            var session = SecureStorage.LoadSession()!;
            api.SetBearerToken(session.AccessToken);

            // Check for pending exam submissions from a previous crash
            await TryRecoverPendingSubmissionAsync(api, session.Role);

            var dashboard = new DashboardShell(session);
            dashboard.WindowState = WindowStateHelper.Load();
            dashboard.Show();
            MainWindow = dashboard;
            return;
        }

        // ── 3. Try silent token refresh ─────────────────────────────────
        bool refreshed = await auth.TryRefreshAsync();
        if (refreshed)
        {
            var session = SecureStorage.LoadSession()!;
            var dashboard = new DashboardShell(session);
            dashboard.WindowState = WindowStateHelper.Load();
            dashboard.Show();
            MainWindow = dashboard;
            return;
        }

        // ── 4. No valid session — show login ────────────────────────────
        var authWindow = new AuthWindow();
        authWindow.WindowState = WindowStateHelper.Load();
        authWindow.Show();
        MainWindow = authWindow;
    }

    /// <summary>
    /// Check for exams that were queued but never submitted (crash recovery).
    /// Only relevant for students — lecturers/admins don't take exams.
    /// </summary>
    private async Task TryRecoverPendingSubmissionAsync(ApiService api, string role)
    {
        if (role != "Student") return;

        try
        {
            using var queue = new SubmissionQueueService();
            var pending = queue.GetPending();
            if (pending is null) return;

            var result = MessageBox.Show(
                $"You have an unsubmitted exam from {pending.QueuedAt:g}.\n\n" +
                "Your code was saved locally. Would you like to submit it now?",
                "Pending Submission Found",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var request = System.Text.Json.JsonSerializer
                    .Deserialize<Shared.DTOs.Exams.SubmitExamRequest>(pending.PayloadJson);
                var examService = new ExamClientService(api);
                var success = await examService.SubmitExamAsync(pending.ExamId, request!);

                if (success)
                {
                    queue.MarkSubmitted(pending.SessionId);
                    queue.Remove(pending.SessionId);
                    MessageBox.Show("Your exam has been submitted successfully!",
                        "Submission Recovered ✓", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Could not submit. The exam is still saved locally and will be retried next time.",
                        "Submission Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            // Don't block app startup on recovery failures
            MessageBox.Show($"Could not recover pending submission: {ex.Message}",
                "Recovery Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _apiHost.Dispose();   // kills the API child process
        base.OnExit(e);
    }
}
