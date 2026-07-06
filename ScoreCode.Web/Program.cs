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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
