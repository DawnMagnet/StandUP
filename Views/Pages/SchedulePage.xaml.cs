using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using StandUP.Services;

namespace StandUP.Views.Pages;

public sealed partial class SchedulePage : Page
{
    sealed record Preset(string Key, int Sit, int Stand);

    static readonly Preset[] Presets =
    [
        new("preset.classic", 50, 15),
        new("preset.balanced", 45, 15),
        new("preset.light", 30, 5),
        new("preset.pomodoro", 25, 5),
    ];

    readonly SettingsStore _store = AppHost.Settings;
    readonly List<(Preset Preset, ToggleButton Button)> _presetButtons = [];
    bool _loading;

    public SchedulePage()
    {
        InitializeComponent();
        SitSegment.Background = Palette.Gradient(Palette.Sit);
        StandSegment.Background = Palette.Gradient(Palette.Stand);
        BuildPresets();
        BuildLockModes();
        Load();
    }

    void BuildPresets()
    {
        for (var i = 0; i < Presets.Length; i++)
        {
            var preset = Presets[i];
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(new TextBlock
            {
                Text = string.Create(CultureInfo.InvariantCulture, $"{preset.Sit} / {preset.Stand}"),
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            });
            content.Children.Add(new TextBlock
            {
                Text = Loc.T(preset.Key),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Opacity = 0.8,
            });
            var button = new ToggleButton
            {
                Content = content,
                Style = (Style)Application.Current.Resources["PresetButtonStyle"],
            };
            AutomationProperties.SetName(button, $"{Loc.T(preset.Key)} {preset.Sit} / {preset.Stand}");
            button.Click += (_, _) => ApplyPreset(preset);
            Grid.SetColumn(button, i);
            PresetGrid.Children.Add(button);
            _presetButtons.Add((preset, button));
        }
    }

    void BuildLockModes()
    {
        foreach (var mode in LockBehavior.All)
        {
            var content = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 6) };
            content.Children.Add(new TextBlock { Text = Loc.T($"lock.{mode}") });
            content.Children.Add(new TextBlock
            {
                Text = Loc.T($"lock.{mode}.desc"),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
            var button = new RadioButton { Content = content, Tag = mode };
            AutomationProperties.SetName(button, Loc.T($"lock.{mode}"));
            LockModeButtons.Items.Add(button);
        }
    }

    void Load()
    {
        _loading = true;
        var s = _store.Current;
        SitBox.Value = s.SitMinutes;
        StandBox.Value = s.StandMinutes;
        SnoozeBox.Value = s.SnoozeMinutes;
        SnoozeSwitch.IsOn = s.AllowSnooze;
        LockModeButtons.SelectedIndex = Math.Max(0, Array.IndexOf(LockBehavior.All, s.LockMode));
        _loading = false;
        RefreshSummary();
    }

    void RefreshSummary()
    {
        var s = _store.Current;
        CycleText.Text = Loc.F("schedule.cycle", s.SitMinutes + s.StandMinutes);
        SitColumn.Width = new GridLength(s.SitMinutes, GridUnitType.Star);
        StandColumn.Width = new GridLength(s.StandMinutes, GridUnitType.Star);
        SitSegmentText.Text = Loc.F("fmt.m", s.SitMinutes);
        StandSegmentText.Text = Loc.F("fmt.m", s.StandMinutes);
        foreach (var (preset, button) in _presetButtons)
        {
            button.IsChecked = preset.Sit == s.SitMinutes && preset.Stand == s.StandMinutes;
        }
    }

    void ApplyPreset(Preset preset)
    {
        _store.Current.SitMinutes = preset.Sit;
        _store.Current.StandMinutes = preset.Stand;
        _store.Save();
        Load();
    }

    void OnSitChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        Apply(sender, args, v => _store.Current.SitMinutes = v);

    void OnStandChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        Apply(sender, args, v => _store.Current.StandMinutes = v);

    void OnSnoozeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        Apply(sender, args, v => _store.Current.SnoozeMinutes = v);

    void Apply(NumberBox box, NumberBoxValueChangedEventArgs args, Action<int> set)
    {
        if (_loading)
        {
            return;
        }
        if (double.IsNaN(args.NewValue))
        {
            box.Value = double.IsNaN(args.OldValue) ? box.Minimum : args.OldValue;
            return;
        }
        set((int)Math.Round(args.NewValue));
        _store.Save();
        RefreshSummary();
    }

    void OnLockModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LockModeButtons.SelectedItem is not RadioButton { Tag: string mode })
        {
            return;
        }
        _store.Current.LockMode = mode;
        _store.Save();
    }

    void OnSnoozeToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        _store.Current.AllowSnooze = SnoozeSwitch.IsOn;
        _store.Save();
    }
}
