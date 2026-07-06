using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ScoreCode.Api.Middleware;
using ScoreCode.Api.Services;
using ScoreCode.Database;
using ScoreCode.Database.Entities;
using ScoreCode.Shared;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ScoreCodeDb>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ── Auth ──────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException(
        "Jwt:Key is not configured. Copy ScoreCode.Api/appsettings.Development.example.json " +
        "to appsettings.Development.json and set a real key.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        // Allow SignalR to pass token via query string
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<ILecturerExamService, LecturerExamService>();
builder.Services.AddScoped<IInstitutionService, InstitutionService>();
builder.Services.AddSingleton<IEmailService, LoggingEmailService>();
// AI grading provider is config-driven (Ai:Provider): "Gemini" or any
// OpenAI-compatible endpoint (Groq/OpenRouter/OpenAI/Ollama) via "OpenAiCompatible".
var aiProvider = builder.Configuration["Ai:Provider"] ?? "Gemini";
if (aiProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<IAiGradingService, GeminiGradingService>();
else
    builder.Services.AddHttpClient<IAiGradingService, OpenAiCompatibleGradingService>();
builder.Services.AddSingleton<ExamAutoPublishService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ExamAutoPublishService>());

// ── SignalR ───────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Controllers ───────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── CORS (allow WPF client by origin or any during dev) ──────────────────
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader()));

// ── Swagger (dev only) ────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "ScoreCode API", Version = "v1" }));

var app = builder.Build();

// ── Seed admin user on first startup ─────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScoreCodeDb>();
    await db.Database.MigrateAsync(); // Apply any pending migrations

    var adminExists = await db.Users.AnyAsync(u => u.Role == Constants.Roles.Admin);
    if (!adminExists)
    {
        var adminCfg = builder.Configuration.GetSection("Admin");
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Name = adminCfg["Name"] ?? "System Administrator",
            IdNumber = adminCfg["IdNumber"] ?? "ADMIN001",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminCfg["Password"] ?? "Admin@123"),
            Role = Constants.Roles.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        Console.WriteLine($"[Seed] Admin created — IdNumber: {admin.IdNumber}");
    }

    // ── Tenancy backfill (Phase 1) ─────────────────────────────────────────
    // Ensure a default institution exists and assign any tenant-less rows to it,
    // so existing single-tenant data keeps working under the new model. The
    // platform admin (role Admin) intentionally stays tenant-less.
    var defaultInst = await db.Institutions
        .FirstOrDefaultAsync(i => i.Name == "Default Institution");
    if (defaultInst is null)
    {
        defaultInst = new Institution
        {
            Id = Guid.NewGuid(),
            Name = "Default Institution",
            Type = "University",
            Status = Constants.InstitutionStatuses.Approved,
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };
        db.Institutions.Add(defaultInst);
        await db.SaveChangesAsync();
        Console.WriteLine($"[Seed] Default institution created — {defaultInst.Id}");
    }

    await db.Users
        .Where(u => u.InstitutionId == null
                 && u.Role != Constants.Roles.Admin
                 && u.Role != Constants.Roles.PlatformAdmin)
        .ExecuteUpdateAsync(s => s.SetProperty(u => u.InstitutionId, defaultInst.Id));
    await db.Exams
        .Where(e => e.InstitutionId == null)
        .ExecuteUpdateAsync(s => s.SetProperty(e => e.InstitutionId, defaultInst.Id));
    await db.Sessions
        .Where(x => x.InstitutionId == null)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.InstitutionId, defaultInst.Id));
    await db.Submissions
        .Where(x => x.InstitutionId == null)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.InstitutionId, defaultInst.Id));
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SignalR hub — wire up when ExamHub is implemented
// app.MapHub<ExamHub>("/hubs/exam");

app.Run();