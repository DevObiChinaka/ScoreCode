using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScoreCode.Client.Helpers;
using ScoreCode.Client.Services;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Client.Views.Admin;

public partial class UserManagementView : UserControl
{
    private readonly UserClientService _users;
    private List<UserDto> _allUsers = [];
    private string _activeRole = "All";

    private static readonly string[] RoleTabs = ["All", "Student", "Lecturer", "Admin"];

    public UserManagementView(ApiService api)
    {
        InitializeComponent();
        _users = new UserClientService(api);

        BuildRoleTabs();
        Loaded += async (_, _) => await LoadUsersAsync();
    }

    // ── Role Tabs ──────────────────────────────────────────────────────────
    private void BuildRoleTabs()
    {
        TabBar.Children.Clear();
        foreach (var role in RoleTabs)
        {
            var btn = new Button
            {
                Content = role,
                Tag = role,
                Style = FindResource("RoleTab") as Style
            };
            btn.Click += RoleTab_Click;
            TabBar.Children.Add(btn);
        }
        HighlightActiveTab();
    }

    private void RoleTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string role)
        {
            _activeRole = role;
            HighlightActiveTab();
            FilterAndDisplay();
        }
    }

    private void HighlightActiveTab()
    {
        var teal = ThemeManager.GetBrush("ThAccent");
        var textPrimary = ThemeManager.GetBrush("ThTextPrimary");
        var textSecondary = ThemeManager.GetBrush("ThTextSecondary");

        foreach (var child in TabBar.Children)
        {
            if (child is Button btn)
            {
                bool active = btn.Tag as string == _activeRole;
                btn.BorderBrush = active ? teal : Brushes.Transparent;
                btn.Foreground = active ? textPrimary : textSecondary;
            }
        }
    }

    // ── Load Users ─────────────────────────────────────────────────────────
    private async Task LoadUsersAsync()
    {
        TxtLoading.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;

        try
        {
            _allUsers = await _users.GetUsersAsync();
            FilterAndDisplay();
        }
        catch (Exception ex)
        {
            TxtLoading.Text = $"Error loading users: {ex.Message}";
        }
    }

    private void FilterAndDisplay()
    {
        var search = TxtSearch.Text?.Trim().ToLowerInvariant() ?? "";
        var filtered = _allUsers.AsEnumerable();

        // Role filter
        if (_activeRole != "All")
            filtered = filtered.Where(u => u.Role.Equals(_activeRole, StringComparison.OrdinalIgnoreCase));

        // Search filter
        if (!string.IsNullOrEmpty(search))
            filtered = filtered.Where(u =>
                u.IdNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();

        TxtLoading.Visibility = Visibility.Collapsed;

        if (list.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            TablePanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyState.Visibility = Visibility.Collapsed;
            TablePanel.Visibility = Visibility.Visible;
            BuildRows(list);
        }

        TxtStatus.Text = $"{list.Count} user{(list.Count == 1 ? "" : "s")} · {_allUsers.Count} total";
    }

    // ── Table Rows ─────────────────────────────────────────────────────────
    private void BuildRows(List<UserDto> users)
    {
        UserRows.Children.Clear();

        foreach (var user in users)
        {
            var row = new Border
            {
                BorderBrush = ThemeManager.GetBrush("ThBorder"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 10, 16, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

            // ID Number
            var txtId = MakeCell(user.IdNumber, 0);
            grid.Children.Add(txtId);

            // Name
            var txtName = MakeCell(user.Name, 1);
            grid.Children.Add(txtName);

            // Role badge
            var roleBadge = MakeRoleBadge(user.Role);
            Grid.SetColumn(roleBadge, 2);
            grid.Children.Add(roleBadge);

            // Level
            var txtLevel = MakeCell(
                user.Role == "Student" ? (user.Level ?? "—") : "—", 3);
            grid.Children.Add(txtLevel);

            // Status
            var txtStatus = new TextBlock
            {
                Text = user.IsActive ? "Active" : "Inactive",
                Foreground = user.IsActive
                    ? ThemeManager.GetBrush("ThSuccess")
                    : ThemeManager.GetBrush("ThError"),
                FontSize = 13,
                FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtStatus, 4);
            grid.Children.Add(txtStatus);

            // Actions
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Toggle active button
            var toggleBtn = new Button
            {
                Content = user.IsActive ? "Deactivate" : "Activate",
                Background = user.IsActive
                    ? new SolidColorBrush(ThemeManager.GetColor("ThDeleteBg"))
                    : new SolidColorBrush(ThemeManager.GetColor("ThSuccess")),
                Foreground = user.IsActive
                    ? ThemeManager.GetBrush("ThError")
                    : ThemeManager.GetBrush("ThSuccess"),
                Style = FindResource("SmallBtn") as Style,
                Tag = user,
                Margin = new Thickness(0, 0, 6, 0)
            };
            toggleBtn.Click += ToggleActive_Click;
            actions.Children.Add(toggleBtn);

            // Edit button (students only)
            if (user.Role == "Student")
            {
                var editBtn = new Button
                {
                    Content = "Edit",
                    Background = ThemeManager.GetBrush("ThBgSurface"),
                    Foreground = ThemeManager.GetBrush("ThTextPrimary"),
                    Style = FindResource("SmallBtn") as Style,
                    Tag = user
                };
                editBtn.Click += EditStudent_Click;
                actions.Children.Add(editBtn);
            }

            Grid.SetColumn(actions, 5);
            grid.Children.Add(actions);

            row.Child = grid;

            // Hover effect
            row.MouseEnter += (_, _) => row.Background =
                new SolidColorBrush(Color.FromArgb(0x15, 0x00, 0xCD, 0xB7));
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

            UserRows.Children.Add(row);
        }
    }

    private static TextBlock MakeCell(string text, int column)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(tb, column);
        return tb;
    }

    private static Border MakeRoleBadge(string role)
    {
        var (bg, fg) = role switch
        {
            "Admin" => (ThemeManager.GetColor("ThInfo"), ThemeManager.GetColor("ThInfo")),
            "Lecturer" => (ThemeManager.GetColor("ThInfo"), ThemeManager.GetColor("ThInfo")),
            _ => (ThemeManager.GetColor("ThSuccess"), ThemeManager.GetColor("ThSuccess"))
        };

        var badge = new Border
        {
            Background = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = role,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fg),
                FontFamily = new FontFamily("/Assets/Fonts/#Geist")
            }
        };
        return badge;
    }

    // ── Search ─────────────────────────────────────────────────────────────
    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        FilterAndDisplay();
    }

    // ── Actions ────────────────────────────────────────────────────────────
    private async void ToggleActive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not UserDto user) return;
        btn.IsEnabled = false;

        var success = user.IsActive
            ? await _users.DeactivateUserAsync(user.Id)
            : false; // Activate endpoint not implemented yet — handled by deactivate toggle

        if (success || !user.IsActive)
            await LoadUsersAsync();

        btn.IsEnabled = true;
    }

    private void EditStudent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not UserDto user) return;
        ShowEditDialog(user);
    }

    // ── Create Lecturer Dialog ─────────────────────────────────────────────
    private void BtnCreateLecturer_Click(object sender, RoutedEventArgs e)
    {
        ShowCreateLecturerDialog();
    }

    private async void ShowCreateLecturerDialog()
    {
        var dialog = new Window
        {
            Title = "ScoreCode · New Lecturer",
            Width = 420, Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = ThemeManager.GetBrush("ThBgBase")
        };
        Helpers.TitleBarHelper.ApplyDarkMode(dialog);
        Helpers.TitleBarHelper.SetCaptionColor(dialog, 0x0D, 0x11, 0x17);
        Helpers.TitleBarHelper.SetIcon(dialog);

        var form = new StackPanel { Margin = new Thickness(24) };

        form.Children.Add(MakeLabel("Full Name"));
        var txtName = MakeInput(); form.Children.Add(txtName);

        form.Children.Add(MakeLabel("Staff ID"));
        var txtStaffId = MakeInput(); form.Children.Add(txtStaffId);

        form.Children.Add(MakeLabel("Department"));
        var txtDept = MakeInput(ScoreCode.Shared.Constants.Academic.Department);
        txtDept.IsReadOnly = true;
        form.Children.Add(txtDept);

        form.Children.Add(MakeLabel("Password"));
        var txtPass = new PasswordBox
        {
            Height = 40, FontSize = 13,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(10, 0, 10, 0)
        };
        form.Children.Add(txtPass);

        var btnCreate = new Button
        {
            Content = "Create Lecturer",
            Style = FindResource("ActionBtn") as Style,
            Height = 42, Margin = new Thickness(0, 8, 0, 0)
        };

        var errorTxt = new TextBlock
        {
            Foreground = ThemeManager.GetBrush("ThError"),
            FontSize = 12, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        btnCreate.Click += async (_, _) =>
        {
            errorTxt.Text = "";
            if (string.IsNullOrWhiteSpace(txtName.Text) ||
                string.IsNullOrWhiteSpace(txtStaffId.Text) ||
                string.IsNullOrWhiteSpace(txtPass.Password))
            {
                errorTxt.Text = "All fields are required.";
                return;
            }

            btnCreate.IsEnabled = false;
            var req = new CreateLecturerRequest
            {
                Name = txtName.Text.Trim(),
                StaffId = txtStaffId.Text.Trim(),
                Department = txtDept.Text.Trim(),
                Password = txtPass.Password
            };

            var result = await _users.CreateLecturerAsync(req);
            if (result is not null)
            {
                dialog.Close();
                await LoadUsersAsync();
            }
            else
            {
                errorTxt.Text = "Failed to create lecturer. Staff ID may already exist.";
                btnCreate.IsEnabled = true;
            }
        };

        form.Children.Add(btnCreate);
        form.Children.Add(errorTxt);

        // Enter key submits
        dialog.KeyDown += (_, ke) => { if (ke.Key == System.Windows.Input.Key.Enter) btnCreate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };

        dialog.Content = form;
        dialog.ShowDialog();
    }

    // ── Edit Student Dialog ────────────────────────────────────────────────
    private async void ShowEditDialog(UserDto user)
    {
        var dialog = new Window
        {
            Title = $"ScoreCode · Edit {user.IdNumber}",
            Width = 420, Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = ThemeManager.GetBrush("ThBgBase")
        };
        Helpers.TitleBarHelper.ApplyDarkMode(dialog);
        Helpers.TitleBarHelper.SetCaptionColor(dialog, 0x0D, 0x11, 0x17);
        Helpers.TitleBarHelper.SetIcon(dialog);

        var form = new StackPanel { Margin = new Thickness(24) };

        form.Children.Add(MakeLabel("Matric Number"));
        var txtMatric = MakeInput(user.IdNumber); form.Children.Add(txtMatric);

        form.Children.Add(MakeLabel("Programme of Study"));
        var cmbProg = new ComboBox
        {
            Height = 40, FontSize = 13,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        foreach (var p in ScoreCode.Shared.Constants.Academic.Programmes)
            cmbProg.Items.Add(p);
        cmbProg.SelectedItem = user.ProgrammeOfStudy;
        if (cmbProg.SelectedIndex < 0) cmbProg.SelectedIndex = 0;
        form.Children.Add(cmbProg);

        form.Children.Add(MakeLabel("Level"));
        var cmbLevel = new ComboBox
        {
            Height = 40, FontSize = 13,
            FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        foreach (var l in ScoreCode.Shared.Constants.Academic.Levels)
            cmbLevel.Items.Add(l);
        cmbLevel.SelectedItem = user.Level;
        if (cmbLevel.SelectedIndex < 0) cmbLevel.SelectedIndex = 0;
        form.Children.Add(cmbLevel);

        var btnSave = new Button
        {
            Content = "Save Changes",
            Style = FindResource("ActionBtn") as Style,
            Height = 42, Margin = new Thickness(0, 8, 0, 0)
        };

        var errorTxt = new TextBlock
        {
            Foreground = ThemeManager.GetBrush("ThError"),
            FontSize = 12, FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
            Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        };

        btnSave.Click += async (_, _) =>
        {
            errorTxt.Text = "";
            btnSave.IsEnabled = false;

            var req = new UpdateStudentProfileRequest
            {
                MatricNumber = txtMatric.Text.Trim(),
                ProgrammeOfStudy = cmbProg.SelectedItem?.ToString() ?? "",
                Level = cmbLevel.SelectedItem?.ToString() ?? ""
            };

            var success = await _users.UpdateStudentProfileAsync(user.Id, req);
            if (success)
            {
                dialog.Close();
                await LoadUsersAsync();
            }
            else
            {
                errorTxt.Text = "Failed to update profile. Matric number may be in use.";
                btnSave.IsEnabled = true;
            }
        };

        form.Children.Add(btnSave);
        form.Children.Add(errorTxt);

        // Enter key submits
        dialog.KeyDown += (_, ke) => { if (ke.Key == System.Windows.Input.Key.Enter) btnSave.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };

        dialog.Content = form;
        dialog.ShowDialog();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = ThemeManager.GetBrush("ThTextSecondary"),
        FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static TextBox MakeInput(string value = "") => new()
    {
        Text = value,
        Height = 40,
        FontSize = 13,
        FontFamily = new FontFamily("/Assets/Fonts/#Geist"),
        Background = ThemeManager.GetBrush("ThBgInput"),
        Foreground = ThemeManager.GetBrush("ThTextPrimary"),
        BorderBrush = ThemeManager.GetBrush("ThBorder"),
        Margin = new Thickness(0, 0, 0, 12),
        Padding = new Thickness(10, 0, 10, 0),
        VerticalContentAlignment = VerticalAlignment.Center
    };
}
