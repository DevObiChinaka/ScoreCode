using ScoreCode.Web.Components;
using ScoreCode.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Per-circuit auth state + typed client for the central ScoreCode API.
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5039";
builder.Services.AddScoped<WebAuthState>();
builder.Services.AddHttpClient<ApiClient>(c => c.BaseAddress = new Uri(apiBase));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

// Public marketing landing at the root. Served as a static page (no Blazor
// circuit) for an instant first paint; the app's own routes (/login, /apply,
// /platform, …) stay with Blazor.
app.MapGet("/", (IWebHostEnvironment env) =>
    Results.File(Path.Combine(env.WebRootPath, "landing.html"), "text/html"));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Desktop build download. The publish script (publish-desktop.ps1) drops a
// zipped Windows build here; until it does, the landing page shows the button
// as unavailable and this returns 404.
app.MapGet("/download/windows", (IWebHostEnvironment env) =>
{
    var path = Path.Combine(env.WebRootPath, "downloads", DesktopDownload.FileName);
    return File.Exists(path)
        ? Results.File(path, "application/zip", DesktopDownload.FileName)
        : Results.NotFound("The desktop app hasn't been published yet.");
});

app.Run();

/// <summary>Shared constants for the desktop build artifact.</summary>
static class DesktopDownload
{
    public const string FileName = "ScoreCode-Windows.zip";
}
