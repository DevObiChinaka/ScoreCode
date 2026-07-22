using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScoreCode.Client.Helpers;

/// <summary>
/// A themed date + time input that always yields a real <see cref="DateTime"/>.
///
/// Replaces free-text date entry, which relied on <c>DateTime.TryParse</c> and so
/// depended on the machine's culture: "03/04/2026" means 3 April on one machine
/// and 4 March on another, and anything unparseable only surfaced as a generic
/// error. Here the calendar supplies the date and the hour/minute come from
/// fixed lists, so an invalid or ambiguous value cannot be entered at all.
/// </summary>
public sealed class DateTimeField
{
    private const int MinuteStep = 5;

    private readonly DatePicker _date;
    private readonly ComboBox _hour;
    private readonly ComboBox _minute;

    /// <summary>The control to place in a form.</summary>
    public Panel Root { get; }

    public DateTimeField(DateTime initial)
    {
        var font = new FontFamily("/Assets/Fonts/#Geist");

        _date = new DatePicker
        {
            Height = 38,
            FontSize = 13,
            FontFamily = font,
            Background = ThemeManager.GetBrush("ThBgInput"),
            Foreground = ThemeManager.GetBrush("ThTextPrimary"),
            BorderBrush = ThemeManager.GetBrush("ThBorder"),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            SelectedDateFormat = DatePickerFormat.Short,
            CalendarStyle = BuildCalendarStyle()
        };

        _hour = MakeCombo(font, 58);
        for (var h = 0; h < 24; h++) _hour.Items.Add(h.ToString("00"));

        _minute = MakeCombo(font, 58);
        for (var m = 0; m < 60; m += MinuteStep) _minute.Items.Add(m.ToString("00"));

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var colon = new TextBlock
        {
            Text = ":",
            FontFamily = font,
            FontSize = 14,
            Foreground = ThemeManager.GetBrush("ThTextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0)
        };

        Grid.SetColumn(_date, 0);
        Grid.SetColumn(_hour, 1);
        Grid.SetColumn(colon, 2);
        Grid.SetColumn(_minute, 3);
        grid.Children.Add(_date);
        grid.Children.Add(_hour);
        grid.Children.Add(colon);
        grid.Children.Add(_minute);

        Root = grid;
        SetValue(initial);
    }

    private static ComboBox MakeCombo(FontFamily font, double width) => new()
    {
        Width = width,
        Height = 38,
        FontSize = 13,
        FontFamily = font,
        Margin = new Thickness(8, 0, 0, 0),
        Background = ThemeManager.GetBrush("ThBgInput"),
        Foreground = ThemeManager.GetBrush("ThTextPrimary"),
        BorderBrush = ThemeManager.GetBrush("ThBorder"),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    /// <summary>Tint the drop-down calendar so it doesn't flash the system light theme.</summary>
    private static Style BuildCalendarStyle()
    {
        var style = new Style(typeof(Calendar));
        style.Setters.Add(new Setter(Control.BackgroundProperty, ThemeManager.GetBrush("ThBgPanel")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, ThemeManager.GetBrush("ThTextPrimary")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, ThemeManager.GetBrush("ThBorder")));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, new FontFamily("/Assets/Fonts/#Geist")));
        return style;
    }

    /// <summary>Show an existing value, keeping the exact minute even if it's off-step.</summary>
    public void SetValue(DateTime value)
    {
        _date.SelectedDate = value.Date;
        _hour.SelectedItem = value.Hour.ToString("00");

        var minute = value.Minute.ToString("00");
        if (!_minute.Items.Contains(minute))
        {
            // An exam saved at, say, 14:07 must round-trip unchanged.
            var index = 0;
            while (index < _minute.Items.Count &&
                   string.CompareOrdinal((string)_minute.Items[index]!, minute) < 0) index++;
            _minute.Items.Insert(index, minute);
        }
        _minute.SelectedItem = minute;
    }

    /// <summary>
    /// The chosen moment as local time. False only if the calendar is empty,
    /// since the hour and minute lists cannot hold an invalid value.
    /// </summary>
    public bool TryGetValue(out DateTime value)
    {
        value = default;
        if (_date.SelectedDate is null) return false;

        var hour = int.Parse((string)(_hour.SelectedItem ?? "00"));
        var minute = int.Parse((string)(_minute.SelectedItem ?? "00"));
        value = _date.SelectedDate.Value.Date.AddHours(hour).AddMinutes(minute);
        return true;
    }
}
