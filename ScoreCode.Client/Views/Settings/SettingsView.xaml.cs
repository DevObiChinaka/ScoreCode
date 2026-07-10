using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Client.Views.Settings;

public partial class SettingsView : UserControl
{
    private readonly string _role;
    private readonly string _userId;
    private readonly ApiService _api;

    public SettingsView(string role, string userId, ApiService api)
    {
        InitializeComponent();
        _role = role;
        _userId = userId;
        _api = api;

        BuildProfileSection();
        BuildAppearanceSection();
        BuildPasswordSection();

        if (_role == "Lecturer")
            BuildExamDefaultsSection();

        // Listen for theme changes to refresh colours in dynamically-built UI
        ThemeManager.ThemeChanged += () => Dispatcher.Invoke(RefreshAllSections);
    }

    private void RefreshAllSections()
    {
        ProfileSection.Children.Clear();
        AppearanceSection.Children.Clear();
        PasswordSection.Children.Clear();
        ExamDefaultsSection.Children.Clear();

        BuildProfileSection();
        BuildAppearanceSection();
        BuildPasswordSection();
        if (_role == "Lecturer") BuildExamDefaultsSection();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static readonly FontFamily _font = new("/Assets/Fonts/#Geist");

    /// <summary>Uppercase section label rendered above a card (mock style).</summary>
    private TextBlock SectionLabel(string text) => new()
    {
        Text = text.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeights.Medium,
        Foreground = ThemeManager.GetBrush("ThTextMuted"),
        FontFamily = _font, Margin = new Thickness(2, 0, 0, 12)
    };

    private Border MakeCard(UIElement content, Thickness pad)
    {
        return new Border
        {
            Background = ThemeManager.GetBrush("ThBgPanel"),
            CornerRadius = new CornerRadius(14),
            Padding = pad,
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            Child = content
        };
    }

    /// <summary>A label/value settings row with a bottom hairline separator.</summary>
    private Border MakeInfoRow(string label, string value, bool last)
    {
        var row = new Border
        {
            Padding = new Thickness(18, 15, 18, 15),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = label, FontSize = 13.5, Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            FontFamily = _font, VerticalAlignment = VerticalAlignment.Center
        });
        var v = new TextBlock
        {
            Text = value, FontSize = 13.5, FontWeight = FontWeights.Medium,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"), FontFamily = _font,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(v, 1);
        grid.Children.Add(v);
        row.Child = grid;
        return row;
    }

    private TextBlock MakeLabel(string text) => new()
    {
        Text = text, FontSize = 12, FontWeight = FontWeights.Medium,
        Foreground = ThemeManager.GetBrush("ThTextSecondary"),
        FontFamily = _font, Margin = new Thickness(0, 0, 0, 6)
    };

    private TextBox MakeInput(string placeholder = "") => new()
    {
        FontSize = 13, FontFamily = _font, Height = 40,
        Background = ThemeManager.GetBrush("ThBgInput"),
        Foreground = ThemeManager.GetBrush("ThTextPrimary"),
        BorderBrush = ThemeManager.GetBrush("ThBorder"),
        Padding = new Thickness(12, 8, 12, 8),
        Margin = new Thickness(0, 0, 0, 14),
        BorderThickness = new Thickness(1)
    };

    private PasswordBox MakePasswordInput() => new()
    {
        FontSize = 13, FontFamily = _font, Height = 40,
        Background = ThemeManager.GetBrush("ThBgInput"),
        Foreground = ThemeManager.GetBrush("ThTextPrimary"),
        BorderBrush = ThemeManager.GetBrush("ThBorder"),
        Padding = new Thickness(12, 8, 12, 8),
        Margin = new Thickness(0, 0, 0, 14),
        BorderThickness = new Thickness(1)
    };

    private Button MakeButton(string text, bool primary = true) => new()
    {
        Content = text,
        FontSize = 13, FontWeight = FontWeights.Medium, FontFamily = _font,
        Background = primary ? ThemeManager.GetBrush("ThAccent") : ThemeManager.GetBrush("ThBgSurface"),
        Foreground = primary ? ThemeManager.GetBrush("ThTextOnAccent") : ThemeManager.GetBrush("ThTextPrimary"),
        Padding = new Thickness(20, 10, 20, 10),
        Cursor = Cursors.Hand,
        BorderThickness = new Thickness(0),
        HorizontalAlignment = HorizontalAlignment.Left
    };

    // ── Profile Section ──────────────────────────────────────────────────
    private StackPanel? _profileRows;

    private void BuildProfileSection()
    {
        _profileRows = new StackPanel();
        _profileRows.Children.Add(MakeInfoRow("Loading…", "", true));

        ProfileSection.Children.Add(SectionLabel("Account"));
        ProfileSection.Children.Add(MakeCard(_profileRows, new Thickness(0)));

        _ = LoadProfileAsync(_profileRows);
    }

    /// <summary>Fetch the signed-in user's details and render a role-specific profile.</summary>
    private async Task LoadProfileAsync(StackPanel rows)
    {
        UserDto? p = null;
        try { p = (await _api.GetAsync<UserDto>("auth/me"))?.Data; }
        catch { /* fall back to session basics below */ }

        // Build (label, value) pairs, then render as rows with separators.
        var pairs = new List<(string, string)>();

        if (p is null)
        {
            pairs.Add(("ID Number", _userId));
            pairs.Add(("Role", _role));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(p.InstitutionName))
                pairs.Add(("Institution", p.InstitutionName!));

            if (_role == "Student")
            {
                // Anonymous student — no name; identified by matric.
                pairs.Add(("Matric Number", p.IdNumber));
                pairs.Add(("Programme of Study", Dash(p.ProgrammeOfStudy)));
                pairs.Add(("Level", Dash(p.Level)));
            }
            else if (_role == "Lecturer")
            {
                pairs.Add(("Name", Dash(p.Name)));
                pairs.Add(("Staff ID", p.IdNumber));
                pairs.Add(("Department", Dash(p.Department)));
            }
            else
            {
                pairs.Add(("Name", Dash(p.Name)));
                pairs.Add(("ID Number", p.IdNumber));
            }

            pairs.Add(("Role", _role));
        }

        rows.Children.Clear();
        for (int i = 0; i < pairs.Count; i++)
            rows.Children.Add(MakeInfoRow(pairs[i].Item1, pairs[i].Item2, i == pairs.Count - 1));
    }

    private static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    // ── Appearance Section ───────────────────────────────────────────────
    private void BuildAppearanceSection()
    {
        var content = new StackPanel();

        content.Children.Add(MakeLabel("Theme"));

        // Theme toggle row
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

        var darkBtn = MakeThemeOption("Dark", AppTheme.Dark);
        var lightBtn = MakeThemeOption("Light", AppTheme.Light);

        row.Children.Add(darkBtn);
        row.Children.Add(lightBtn);
        content.Children.Add(row);

        content.Children.Add(new TextBlock
        {
            Text = "Your preference is saved automatically",
            FontSize = 11.5, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _font, Margin = new Thickness(0, 12, 0, 0)
        });

        AppearanceSection.Children.Add(SectionLabel("Appearance"));
        AppearanceSection.Children.Add(MakeCard(content, new Thickness(22, 18, 22, 18)));
    }

    private Border MakeThemeOption(string label, AppTheme theme)
    {
        var isActive = ThemeManager.CurrentTheme == theme;
        var border = new Border
        {
            Background = isActive ? ThemeManager.GetBrush("ThAccentSoft") : ThemeManager.GetBrush("ThBgElev"),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(18, 9, 18, 9),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand,
            BorderBrush = isActive ? ThemeManager.GetBrush("ThAccentLine") : ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1)
        };

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 9, Height = 9, Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Fill = isActive ? ThemeManager.GetBrush("ThAccent") : ThemeManager.GetBrush("ThBorderStrong")
        });
        content.Children.Add(new TextBlock
        {
            Text = label, FontSize = 13.5, FontWeight = FontWeights.Medium,
            Foreground = isActive ? ThemeManager.GetBrush("ThAccentStrong") : ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = _font, VerticalAlignment = VerticalAlignment.Center
        });
        border.Child = content;

        border.MouseLeftButtonDown += (_, _) => ThemeManager.ApplyTheme(theme);
        return border;
    }

    // ── Password Section ─────────────────────────────────────────────────
    private PasswordBox? _pwdCurrent, _pwdNew, _pwdConfirm;
    private TextBlock? _pwdStatus;

    private void BuildPasswordSection()
    {
        var content = new StackPanel();

        content.Children.Add(MakeLabel("Current Password"));
        _pwdCurrent = MakePasswordInput();
        content.Children.Add(MakePasswordFieldWithToggle(_pwdCurrent));

        content.Children.Add(MakeLabel("New Password"));
        _pwdNew = MakePasswordInput();
        _pwdNew.PasswordChanged += (_, _) =>
        {
            UpdatePasswordCriteria();
            UpdateConfirmMatch();
        };
        content.Children.Add(MakePasswordFieldWithToggle(_pwdNew));

        // Password criteria indicators
        _critPanel = new StackPanel { Margin = new Thickness(0, -8, 0, 10) };
        _critLength = MakeCriteriaRow("At least 8 characters");
        _critUpper = MakeCriteriaRow("At least one uppercase letter");
        _critDigit = MakeCriteriaRow("At least one number");
        _critSpecial = MakeCriteriaRow("At least one special character");
        _critPanel.Children.Add(_critLength);
        _critPanel.Children.Add(_critUpper);
        _critPanel.Children.Add(_critDigit);
        _critPanel.Children.Add(_critSpecial);
        content.Children.Add(_critPanel);

        content.Children.Add(MakeLabel("Confirm New Password"));
        _pwdConfirm = MakePasswordInput();
        _pwdConfirm.PasswordChanged += (_, _) => UpdateConfirmMatch();
        content.Children.Add(MakePasswordFieldWithToggle(_pwdConfirm));

        // Confirm match indicator
        _confirmRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -8, 0, 12) };
        _confirmIcon = new TextBlock
        {
            Text = "○", FontSize = 11, FontFamily = _font,
            Foreground = ThemeManager.GetBrush("ThTextMuted"),
            Margin = new Thickness(0, 0, 6, 0)
        };
        _confirmLabel = new TextBlock
        {
            Text = "Confirm your password", FontSize = 11, FontFamily = _font,
            Foreground = ThemeManager.GetBrush("ThTextMuted")
        };
        _confirmRow.Children.Add(_confirmIcon);
        _confirmRow.Children.Add(_confirmLabel);
        content.Children.Add(_confirmRow);

        var btn = MakeButton("Change Password");
        btn.Click += BtnChangePassword_Click;
        content.Children.Add(btn);

        _pwdStatus = new TextBlock
        {
            FontSize = 12, FontFamily = _font,
            Margin = new Thickness(0, 10, 0, 0)
        };
        content.Children.Add(_pwdStatus);

        // Enter key submits
        content.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) BtnChangePassword_Click(btn, e); };

        PasswordSection.Children.Add(SectionLabel("Security"));
        PasswordSection.Children.Add(MakeCard(content, new Thickness(22, 18, 22, 18)));
    }

    private TextBlock? _confirmIcon, _confirmLabel;
    private StackPanel? _confirmRow;
    private StackPanel? _critPanel;
    private StackPanel? _critLength, _critUpper, _critDigit, _critSpecial;

    private StackPanel MakeCriteriaRow(string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock
        {
            Text = "○", FontSize = 11, FontFamily = _font,
            Foreground = ThemeManager.GetBrush("ThTextMuted"),
            Margin = new Thickness(0, 0, 6, 0), Tag = "icon"
        });
        row.Children.Add(new TextBlock
        {
            Text = text, FontSize = 11, FontFamily = _font,
            Foreground = ThemeManager.GetBrush("ThTextMuted"), Tag = "label"
        });
        return row;
    }

    private void SetCriteria(StackPanel? row, bool met)
    {
        if (row is null) return;
        foreach (var child in row.Children)
        {
            if (child is TextBlock tb)
            {
                var brush = met ? ThemeManager.GetBrush("ThAccent") : ThemeManager.GetBrush("ThTextMuted");
                if (tb.Tag as string == "icon") tb.Text = met ? "✓" : "○";
                tb.Foreground = brush;
            }
        }
    }

    private void UpdatePasswordCriteria()
    {
        if (_pwdNew is null) return;
        var pwd = _pwdNew.Password;
        SetCriteria(_critLength, pwd.Length >= 8);
        SetCriteria(_critUpper, pwd.Any(char.IsUpper));
        SetCriteria(_critDigit, pwd.Any(char.IsDigit));
        SetCriteria(_critSpecial, pwd.Any(c => !char.IsLetterOrDigit(c)));
    }

    private void UpdateConfirmMatch()
    {
        if (_pwdNew is null || _pwdConfirm is null || _confirmIcon is null || _confirmLabel is null) return;
        var confirm = _pwdConfirm.Password;
        var pwd = _pwdNew.Password;
        bool matches = confirm.Length > 0 && confirm == pwd;
        bool mismatch = confirm.Length > 0 && confirm != pwd;

        _confirmIcon.Text = matches ? "✓" : mismatch ? "✕" : "○";
        _confirmIcon.Foreground = matches ? ThemeManager.GetBrush("ThAccent") : mismatch ? ThemeManager.GetBrush("ThError") : ThemeManager.GetBrush("ThTextMuted");
        _confirmLabel.Text = matches ? "Passwords match" : mismatch ? "Passwords do not match" : "Confirm your password";
        _confirmLabel.Foreground = _confirmIcon.Foreground;
    }

    private Grid MakePasswordFieldWithToggle(PasswordBox pwdBox)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        pwdBox.Margin = new Thickness(0);  // remove default margin, grid handles it
        grid.Children.Add(pwdBox);

        var txtVisible = new TextBox
        {
            FontSize = 13, FontFamily = _font, Height = 40,
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Padding = new Thickness(12, 8, 32, 8),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        grid.Children.Add(txtVisible);

        var eyeBtn = new TextBlock
        {
            Text = "👁", FontSize = 14,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        Grid.SetColumn(eyeBtn, 0);
        System.Windows.Controls.Panel.SetZIndex(eyeBtn, 1);
        grid.Children.Add(eyeBtn);

        bool visible = false;
        eyeBtn.MouseLeftButtonDown += (_, _) =>
        {
            visible = !visible;
            if (visible)
            {
                txtVisible.Text = pwdBox.Password;
                txtVisible.Visibility = Visibility.Visible;
                pwdBox.Visibility = Visibility.Collapsed;
                eyeBtn.Text = "🙈";
            }
            else
            {
                pwdBox.Password = txtVisible.Text;
                pwdBox.Visibility = Visibility.Visible;
                txtVisible.Visibility = Visibility.Collapsed;
                eyeBtn.Text = "👁";
            }
        };

        // Sync text back when toggling
        txtVisible.TextChanged += (_, _) =>
        {
            if (visible) pwdBox.Password = txtVisible.Text;
        };

        return grid;
    }

    private async void BtnChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (_pwdCurrent is null || _pwdNew is null || _pwdConfirm is null || _pwdStatus is null) return;

        if (string.IsNullOrWhiteSpace(_pwdCurrent.Password) ||
            string.IsNullOrWhiteSpace(_pwdNew.Password))
        {
            _pwdStatus.Foreground = ThemeManager.GetBrush("ThError");
            _pwdStatus.Text = "Please fill in all fields.";
            return;
        }

        if (_pwdNew.Password != _pwdConfirm.Password)
        {
            _pwdStatus.Foreground = ThemeManager.GetBrush("ThError");
            _pwdStatus.Text = "New passwords do not match.";
            return;
        }

        var pwd = _pwdNew.Password;
        if (pwd.Length < 8 || !pwd.Any(char.IsUpper) || !pwd.Any(char.IsDigit) || !pwd.Any(c => !char.IsLetterOrDigit(c)))
        {
            _pwdStatus.Foreground = ThemeManager.GetBrush("ThError");
            _pwdStatus.Text = "Password does not meet all criteria.";
            return;
        }

        var btn = sender as Button;
        if (btn != null) { btn.IsEnabled = false; btn.Content = "Changing…"; }

        var resp = await _api.PostAsync<object>("auth/change-password", new
        {
            CurrentPassword = _pwdCurrent.Password,
            NewPassword = _pwdNew.Password
        });

        if (resp?.Success == true)
        {
            _pwdStatus.Foreground = ThemeManager.GetBrush("ThSuccess");
            _pwdStatus.Text = "✓ Password changed successfully.";
            _pwdCurrent.Password = "";
            _pwdNew.Password = "";
            _pwdConfirm.Password = "";
        }
        else
        {
            _pwdStatus.Foreground = ThemeManager.GetBrush("ThError");
            _pwdStatus.Text = resp?.Message ?? "Failed to change password.";
        }

        if (btn != null) { btn.IsEnabled = true; btn.Content = "Change Password"; }
    }

    // ── Exam Defaults Section (Lecturer only) ────────────────────────────
    private void BuildExamDefaultsSection()
    {
        var content = new StackPanel();

        content.Children.Add(MakeLabel("Default Duration (minutes)"));
        var txtDuration = MakeInput();
        txtDuration.Text = Services.UserPreferences.GetInt("DefaultDurationMinutes", 60).ToString();
        content.Children.Add(txtDuration);
        txtDuration.LostFocus += (_, _) =>
        {
            if (int.TryParse(txtDuration.Text, out var d) && d > 0)
                Services.UserPreferences.Set("DefaultDurationMinutes", d);
        };

        content.Children.Add(MakeLabel("Default Languages (comma-separated)"));
        var txtLanguages = MakeInput();
        txtLanguages.Text = Services.UserPreferences.GetString("DefaultLanguages", "Python, C#, Java");
        content.Children.Add(txtLanguages);
        txtLanguages.LostFocus += (_, _) =>
            Services.UserPreferences.Set("DefaultLanguages", txtLanguages.Text.Trim());

        // IntelliSense toggle
        content.Children.Add(MakeLabel("IntelliSense"));
        var chk = new CheckBox
        {
            Content = "Enable by default for new exams",
            IsChecked = Services.UserPreferences.GetBool("DefaultIntelliSense", true),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontSize = 13, FontFamily = _font,
            Margin = new Thickness(0, 0, 0, 14)
        };
        chk.Checked   += (_, _) => Services.UserPreferences.Set("DefaultIntelliSense", true);
        chk.Unchecked += (_, _) => Services.UserPreferences.Set("DefaultIntelliSense", false);
        content.Children.Add(chk);

        content.Children.Add(new TextBlock
        {
            Text = "These defaults are applied when creating new exams. They can be overridden individually.",
            FontSize = 11, Foreground = ThemeManager.GetBrush("ThTextMuted"),
            FontFamily = _font, TextWrapping = TextWrapping.Wrap
        });

        ExamDefaultsSection.Children.Add(SectionLabel("Default Exam Settings"));
        ExamDefaultsSection.Children.Add(MakeCard(content, new Thickness(22, 18, 22, 18)));
    }
}
