# ScoreCode — Project Specification & Implementation Status

> **Last updated:** 2026-03-20  
> **Target:** .NET 10.0 on Windows · WPF Desktop Client + ASP.NET Core API  
> **Database:** PostgreSQL 5432 (`scorecode` / user `sc` / password `sc_password`)

---

## 1. Project Overview

ScoreCode is a **secure desktop exam management and code evaluation system** for Computer Science students at Admiralty University of Nigeria. Lecturers create exams, students take them in a locked-down environment, and submissions are graded — with AI-assisted marking as an option.

### Roles & Permissions

| Role       | Capabilities |
|------------|-------------|
| **Admin**  | Edit student profiles, system monitoring |
| **Lecturer** | Create exams, select eligible students, view/grade submissions, run code |
| **Student** | Take exams, write code, submit |

### Key Rules
- Students identified **only** by Matric Number (no full name — prevents favouritism)
- One active session per student enforced
- Matric number, programme, and level are **locked after registration** (admin-editable only)

### Academic Structure

| Category | Values |
|----------|--------|
| **Department** | Computing Sciences |
| **Programmes** | Software Engineering, Computer Science, Cyber Security |
| **Levels** | 100, 200, 300, 400, 500, Postgrad |

All values are defined in `Constants.Academic` and used as dropdown options in Registration, Admin Edit, and Lecturer Student Assignment.

---

## 2. Solution Structure

```
ScoreCode/
├── ScoreCode.slnx                  # Solution file
├── SPEC.md                         # ← This document
├── ScoreCode Specifications.txt    # Original spec (v1)
├── ScoreCode_Spec_v4.txt           # Earlier detailed spec
├── docker-compose.yaml.txt         # PostgreSQL container config
│
├── ScoreCode.Client/               # WPF Desktop Application
├── ScoreCode.Api/                  # ASP.NET Core Web API
├── ScoreCode.Shared/               # DTOs, Enums, Constants
└── ScoreCode.Database/             # EF Core DbContext & Migrations
```

---

## 3. ScoreCode.Client (WPF Desktop)

### 3.1 File Inventory

```
ScoreCode.Client/
├── App.xaml / App.xaml.cs          # Entry point, API host startup, session restore
├── appsettings.json                # Client config (ServerUrl, SignalRHub)
├── Assets/
│   ├── scorecode.ico               # Custom branded app icon (teal grid)
│   ├── Icons.xaml                  # Lucide SVG-path icons as WPF Geometry resources
│   └── Fonts/
│       ├── Geist.ttf               # Geist variable font (UI text, all weights)
│       └── GeistMono.ttf           # Geist Mono variable font (code editor)
├── Helpers/
│   ├── NavigationService.cs        # Window-level navigation helper
│   ├── SecureStorage.cs            # DPAPI-encrypted session persistence
│   ├── TitleBarHelper.cs           # DWM dark/light title bar + caption color + icon
│   └── ValueConverters.cs          # BoolToVisibility, InverseBool converters
├── Services/
│   ├── ApiHostService.cs           # Starts API as child process on port 5039
│   ├── ApiService.cs               # HttpClient wrapper, reads appsettings.json
│   └── AuthClientService.cs        # Login, Register, Refresh API calls
├── ViewModels/
│   ├── DashboardViewModel.cs       # Nav items, user info, sidebar toggle, logout
│   └── Auth/
│       ├── LoginViewModel.cs       # IdNumber, Password, LoginCommand
│       └── RegisterViewModel.cs    # MatricNumber, Programme, Level, Password
└── Views/
    ├── DashboardShell.xaml / .cs    # Post-login shell — collapsible sidebar + content area
    ├── Dashboard/
    │   └── HomeView.xaml / .cs      # Role-specific home (stat cards, activity, quick actions)
    └── Auth/
        ├── AuthWindow.xaml / .cs    # Auth shell — hosts Login/Register as UserControls
        ├── LoginView.xaml / .cs     # Login form (UserControl)
        └── RegisterView.xaml / .cs  # Registration form (UserControl)
```

### 3.2 Configuration

**`appsettings.json`:**
```json
{
  "ServerUrl": "http://localhost:5039",
  "ApiVersion": "v1",
  "SignalRHub": "/hubs/exam"
}
```

### 3.3 Architecture Decisions

| Decision | Detail |
|----------|--------|
| **Single AuthWindow** | Login and Register are `UserControl`s inside one `AuthWindow` with a `ContentControl`. Switching between them swaps content — no window open/close flash. |
| **Native Windows Chrome** | Uses standard WPF window chrome (not `WindowStyle=None`). Title bar customisation done via `DwmSetWindowAttribute` for dark mode and caption colour. |
| **TitleBarHelper** | Centralised helper using `DWMWA_USE_IMMERSIVE_DARK_MODE` (attr 20) and `DWMWA_CAPTION_COLOR` (attr 35). Supports dark/light toggle and custom icon. |
| **Session Persistence** | `SecureStorage` uses Windows DPAPI (`ProtectedData`) to encrypt session tokens to `%LOCALAPPDATA%\ScoreCode\session.dat`. |
| **API as Child Process** | `ApiHostService` starts the API via `dotnet run` as a child process. Port 5039. Killed on app exit. |
| **MVVM** | Uses `CommunityToolkit.Mvvm` for ObservableProperty, RelayCommand. |
| **Password Handling** | PasswordBox values are passed from code-behind to ViewModel (WPF PasswordBox doesn't support binding). Login button uses `Click` handler only (no `Command` on XAML) to ensure password is captured before command executes. |

### 3.4 Auth Flow

```
App.xaml.cs → Application_Startup
  ├── ApiHostService.EnsureStarted()     → starts API child process
  ├── await Task.Delay(5000)             → wait for API to be ready
  ├── SecureStorage.HasValidSession()?   → yes → DashboardShell(session)
  ├── auth.TryRefreshAsync()?            → yes → DashboardShell(session)
  └── else → AuthWindow (showing LoginView)
```

**Login:** `POST /api/auth/login` with `{ idNumber, password }`  
**Register:** `POST /api/auth/register/student` with `{ matricNumber, programmeOfStudy, level, password }`  
**Refresh:** `POST /api/auth/refresh` with `{ refreshToken }`

### 3.5 UI Design

- **Dark theme:** `#0D1117` base, `#13192A` panels, `#1C2338` surfaces
- **Accent:** Teal `#00CDB7`
- **Typography:** Geist (Vercel) — embedded variable TTF, all weights
- **Two-panel layout:** Left info panel (logo, locked field cards) + Right form panel
- **Password UX:** Real-time criteria indicators (8+ chars, uppercase, digit, special) + confirm-match indicator
- **Window titles:** `ScoreCode · Sign In`, `ScoreCode · Create Account`, `ScoreCode · [Role]`

### 3.7 Dashboard (DashboardShell)

| Feature | Detail |
|---------|--------|
| **Layout** | Collapsible sidebar (220px ↔ 60px) + ContentControl content area |
| **Sidebar** | Lucide icons, VS Code teal left-border accent on active item |
| **Collapse toggle** | Chevron-left/right icon, hides labels when collapsed |
| **Nav items** | Role-filtered: Student (Home, Exams, Results, Settings), Lecturer (Home, Manage Exams, Submissions, Settings), Admin (Home, Users, Monitoring, Settings) |
| **User profile card** | Teal initials circle + ID number + role at sidebar bottom |
| **Sign out** | Clears DPAPI session → reopens AuthWindow |
| **HomeView** | Welcome banner, 4 stat cards (role-specific), upcoming/activity sections, 3 quick action cards |

### 3.8 Fonts

| Font | File | Usage |
|------|------|-------|
| **Geist** | `Assets/Fonts/Geist.ttf` | All UI text (variable, all weights) |
| **JetBrains Mono** | `Assets/monaco/fonts/JetBrainsMono-*.woff2` | Code editor (loaded via `@font-face` in HTML) |

Geist referenced in XAML as `FontFamily="/Assets/Fonts/#Geist"`. JetBrains Mono loaded locally via `@font-face` in `monaco-editor.html` from bundled WOFF2 files.

### 3.9 Icons (Lucide)

All icons stored as `Geometry` resources in `Assets/Icons.xaml`. Rendered as WPF `Path` elements with `Stroke` (no fill).

| Icon Key | Lucide Name | Used For |
|----------|-------------|----------|
| Icon.Home | home | Sidebar: Home |
| Icon.FileText | file-text | Sidebar: My Exams |
| Icon.BarChart | bar-chart-3 | Sidebar: Results |
| Icon.ClipboardList | clipboard-list | Sidebar: Manage Exams |
| Icon.FileCheck | file-check | Sidebar: Submissions |
| Icon.Users | users | Sidebar: Users |
| Icon.Activity | activity | Sidebar: Monitoring |
| Icon.Settings | settings | Sidebar: Settings |
| Icon.LogOut | log-out | Sign Out |
| Icon.PanelClose | chevron-left | Collapse sidebar |
| Icon.PanelOpen | chevron-right | Expand sidebar |

### 3.6 NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| CommunityToolkit.Mvvm | 8.4.0 | MVVM framework |
| MahApps.Metro | 2.4.11 | UI framework (available, not yet heavily used) |
| MaterialDesignThemes | 5.3.1 | Material Design (available, not yet heavily used) |
| Microsoft.AspNetCore.SignalR.Client | 10.0.5 | Real-time exam events |
| Microsoft.Data.Sqlite | 10.0.5 | Local offline cache |
| Microsoft.Extensions.Configuration.Json | 10.0.5 | appsettings.json loading |

---

## 4. ScoreCode.Api (ASP.NET Core)

### 4.1 File Inventory

```
ScoreCode.Api/
├── Program.cs                      # Startup, DI, middleware, EF migrations
├── appsettings.json                # DB connection, JWT config, admin seed
├── Controllers/
│   ├── AuthController.cs           # Login, Register (student), Refresh
│   └── UsersController.cs          # User CRUD
├── Hubs/                           # SignalR hub (exam events — placeholder)
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
└── Services/
    ├── AuthService.cs / IAuthService.cs
    ├── TokenService.cs / ITokenService.cs
    └── UserService.cs / IUserService.cs
```

### 4.2 Configuration

**`appsettings.json`:**
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=scorecode;Username=sc;Password=sc_password"
  },
  "Jwt": {
    "Key": "<96-char hex key>",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "Admin": {
    "Name": "System Administrator",
    "IdNumber": "ADMIN001",
    "Password": "Admin@123"
  },
  "Anthropic": {
    "ApiKey": "",
    "Model": "claude-sonnet-4-20250514"
  }
}
```

**Port:** `http://localhost:5039` (configured in `launchSettings.json`)

### 4.3 Key Decisions

| Decision | Detail |
|----------|--------|
| **PostgreSQL** | Replaced original SQL Server Express spec. DB `scorecode`, user `sc`. |
| **BCrypt** | Passwords hashed with `BCrypt.Net-Next`. |
| **JWT Auth** | Access token (15 min) + Refresh token (7 days). |
| **Admin Seed** | Auto-created on startup if `ADMIN001` doesn't exist. |
| **User.Name** | Set to MatricNumber during registration (no full name collected). |
| **EF Core** | Code-first with auto-migration on startup. |

---

## 5. ScoreCode.Shared (DTOs & Enums)

```
ScoreCode.Shared/
├── Constants.cs                    # Shared constants
├── DTOs/
│   ├── ApiResponse.cs              # Generic API response wrapper
│   └── Auth/
│       ├── LoginRequest.cs         # { IdNumber, Password }
│       ├── LoginResponse.cs        # { AccessToken, RefreshToken, User }
│       ├── RefreshRequest.cs       # { RefreshToken }
│       └── RegisterRequest.cs      # { MatricNumber, ProgrammeOfStudy, Level, Password }
│   └── Users/
│       ├── CreateLecturerRequest.cs
│       ├── UpdateStudentProfileRequest.cs
│       └── UserDto.cs
└── Enums/
    ├── ExamStatus.cs
    ├── GradingMode.cs
    ├── GradingStatus.cs
    ├── Language.cs                 # Python, Java, Cpp, JavaScript, Html, Css
    ├── Role.cs                     # Admin, Lecturer, Student
    ├── SubmissionStatus.cs
    └── UploadStatus.cs
```

---

## 6. ScoreCode.Database

Contains Entity Framework Core `DbContext`, entity models, and migrations. Referenced by `ScoreCode.Api`.

---

## 7. Implementation Status

### ✅ Completed

| Feature | Status | Notes |
|---------|--------|-------|
| Solution scaffolding | ✅ Done | 4 projects, .NET 10.0 |
| PostgreSQL database setup | ✅ Done | DB, user, EF migrations working |
| User registration (student) | ✅ Done | MatricNumber, Programme, Level, Password |
| User login | ✅ Done | IdNumber + Password → JWT tokens |
| Token refresh | ✅ Done | Silent refresh on app startup |
| Session persistence | ✅ Done | DPAPI-encrypted file at `%LOCALAPPDATA%` |
| API self-hosting | ✅ Done | Client starts API as child process |
| Admin auto-seed | ✅ Done | ADMIN001 created on first run |
| Login UI | ✅ Done | Dark theme, two-panel, modern design |
| Register UI | ✅ Done | Password criteria, confirm match |
| Native dark title bar | ✅ Done | DWM API, custom caption color |
| Dashboard shell | ✅ Done | Collapsible sidebar, Lucide icons |
| Home view | ✅ Done | Role-specific stat cards, activity |
| Geist font & Lucide icons | ✅ Done | Variable TTF + 12 SVG geometry icons |
| Code editor (Monaco) | ✅ Done | Bundled locally (offline), JetBrains Mono font |
| Exam taking flow | ✅ Done | Restricted exam mode, timer, auto-submit on timeout |
| Anti-cheating | ✅ Done | Fullscreen lock, copy/paste disable, released on submit |
| Submission system | ✅ Done | Network retry with anti-cheat release |
| Autosave | ✅ Done | SQLite every 5s, dirty-flag polling |
| Local code execution | ✅ Done | RuntimePaths, Job Objects, 5s/256MB limits |
| Student Run panel | ✅ Done | Collapsible output, Run/Clear buttons |
| Lecturer Run button | ✅ Done | Same execution in grade dialog |
| Submissions view | ✅ Done | Grouped by student, expandable rows |
| Editor state management | ✅ Done | Cursor/scroll preserved across tab switches |

### 🔲 Not Yet Started

| Feature | Priority | Notes |
|---------|----------|-------|
| HTML/CSS WebView2 preview | Medium | Live render for HTML/CSS questions |
| AI-assisted grading | Medium | Anthropic Claude integration (config exists) |
| SignalR real-time events | Medium | Exam start/end notifications |
| Light/dark mode toggle | Low | TitleBarHelper already supports it |
| SQLite submission queue | Low | Crash-safe local persistence for submissions |

---

## 8. Running the Application

### Prerequisites
- .NET 10.0 SDK
- PostgreSQL running on `localhost:5432`
- Database `scorecode` with user `sc` / password `sc_password`

### Quick Start

```powershell
# 1. Ensure PostgreSQL is running with the scorecode database

# 2. Build everything
cd C:\Users\USER\source\repos\ScoreCode
dotnet build

# 3. Run the client (it auto-starts the API)
dotnet run --project ScoreCode.Client\ScoreCode.Client.csproj

# Or run API separately for debugging:
dotnet run --project ScoreCode.Api\ScoreCode.Api.csproj
```

### Clearing Session (to see login screen again)
```powershell
Remove-Item "$env:LOCALAPPDATA\ScoreCode\session.dat" -Force
```

### Default Admin Credentials
- **ID Number:** `ADMIN001`
- **Password:** `Admin@123`

---

## 9. Design System

### Colour Tokens

| Token | Hex | Usage |
|-------|-----|-------|
| BgBase | `#0D1117` | Window/page background |
| BgPanel | `#13192A` | Side panels |
| BgSurface | `#1C2338` | Cards, elevated surfaces |
| BgInput | `#181F30` | Input fields |
| Teal | `#00CDB7` | Primary accent, buttons, links |
| TealDark | `#009E8C` | Button hover |
| TextPrimary | `#E8EEFF` | Main text |
| TextSecondary | `#5C6B88` | Labels, subtitles |
| TextMuted | `#364059` | Hints, placeholders |
| BorderDefault | `#1F2A40` | Input/card borders |
| ErrorText | `#FC8181` | Error messages |
| ErrorBg | `#1A0A0A` | Error banner background |
| SuccessText | `#68D391` | Success messages |

### UI Patterns

- **Inputs:** 46px height, 6px corner radius, teal focus border
- **Buttons:** 46px height, 6px radius, teal bg with `#009E8C` hover
- **Password:** PasswordBox with custom template + placeholder TextBlock (toggled in code-behind)
- **Forms:** 400px max width, centred vertically in scroll viewer
- **Left panel:** 38% width, dot-pattern overlay, teal accent bar

---

## 10. Known Gotchas & Patterns

1. **PasswordBox binding:** WPF PasswordBox doesn't support data binding for security. Password is captured in code-behind `Click` handler and set on ViewModel before executing the command. **Never** use both `Command=` and `Click=` on the same button.

2. **Title bar customisation:** Done via `TitleBarHelper` using Win32 `DwmSetWindowAttribute`. Do NOT use `WindowStyle=None` + custom chrome — it causes taskbar, maximize, and navigation bugs.

3. **Auth view navigation:** Login ↔ Register uses content swapping in `AuthWindow.ContentHost`. Do NOT create new Windows for this. Events (`RegisterRequested`, `LoginRequested`) bubble up to `AuthWindow` which calls `ShowLogin()` / `ShowRegister()`.

4. **API startup:** The client starts the API as a child process via `ApiHostService`. There's a 5-second delay to let the API build and start. If the API is already running, the port check prevents double-start.

5. **Icon loading:** The `.ico` file is included as `Content` with `CopyToOutputDirectory` (not embedded Resource). `TitleBarHelper.SetIcon()` loads it from `{BaseDirectory}/Assets/scorecode.ico` at runtime.

6. **Session clear for testing:** Delete `%LOCALAPPDATA%\ScoreCode\session.dat` to force the login screen.

7. **Geist font reference:** Use `FontFamily="/Assets/Fonts/#Geist"` in XAML. The `#Geist` refers to the font family name inside the TTF file, not the filename. For the code editor, use `#Geist Mono`.

8. **Lucide icon usage:** Icons are Geometry resources in `Icons.xaml`. Use with `<Path Data="{StaticResource Icon.Home}" Stroke="..." StrokeThickness="1.5" Stretch="Uniform"/>`. Always use `Stroke`, NOT `Fill`.
