using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StandUP.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace StandUP.Views;

public sealed partial class SettingsWindow : Window
{
    sealed record TextField(
        TextBox Box,
        string LabelKey,
        string DefaultKey,
        Func<AppSettings, string> Get,
        Action<AppSettings, string> Set);

    readonly ReminderEngine _engine;
    readonly SettingsStore _store;
    readonly Action<Phase> _preview;
    readonly Action _exit;
    readonly List<TextField> _fields;
    bool _loading;

    public bool AllowClose { get; set; }

    public SettingsWindow(ReminderEngine engine, SettingsStore store, string iconPath, Action<Phase> preview, Action exit)
    {
        InitializeComponent();
        _engine = engine;
        _store = store;
        _preview = preview;
        _exit = exit;

        Title = "StandUP";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(iconPath);

        var scale = NativeMethods.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(520 * scale), (int)(860 * scale)));

        AppWindow.Closing += (_, args) =>
        {
            if (!AllowClose)
            {
                args.Cancel = true;
                AppWindow.Hide();
            }
        };

        _fields =
        [
            new(StandTitleBox, "field.title", "overlay.stand.title", s => s.StandTitle, (s, v) => s.StandTitle = v),
            new(StandMessageBox, "field.message", "overlay.stand.message", s => s.StandMessage, (s, v) => s.StandMessage = v),
            new(StandConfirmBox, "field.confirm", "overlay.stand.confirm", s => s.StandConfirm, (s, v) => s.StandConfirm = v),
            new(StandSnoozeBox, "field.snooze", "overlay.stand.snooze", s => s.StandSnooze, (s, v) => s.StandSnooze = v),
            new(SitTitleBox, "field.title", "overlay.sit.title", s => s.SitTitle, (s, v) => s.SitTitle = v),
            new(SitMessageBox, "field.message", "overlay.sit.message", s => s.SitMessage, (s, v) => s.SitMessage = v),
            new(SitConfirmBox, "field.confirm", "overlay.sit.confirm", s => s.SitConfirm, (s, v) => s.SitConfirm = v),
            new(SitSnoozeBox, "field.snooze", "overlay.sit.snooze", s => s.SitSnooze, (s, v) => s.SitSnooze = v),
        ];
        foreach (var field in _fields)
        {
            field.Box.LostFocus += (_, _) => SaveField(field);
        }

        Localize();
        engine.Changed += Refresh;
        Loc.LanguageChanged += () => DispatcherQueue.TryEnqueue(Localize);
    }

    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
        NativeMethods.SetForegroundWindow(WindowNative.GetWindowHandle(this));
    }

    void Localize()
    {
        _loading = true;

        var s = _store.Current;
        SitBox.Header = Loc.T("settings.sit");
        StandBox.Header = Loc.T("settings.stand");
        SnoozeBox.Header = Loc.T("settings.snooze");
        SitBox.Value = s.SitMinutes;
        StandBox.Value = s.StandMinutes;
        SnoozeBox.Value = s.SnoozeMinutes;

        TimeHeader.Text = Loc.T("settings.timeHeader");
        BreakNowButton.Content = Loc.T("btn.breakNow");
        RestartButton.Content = Loc.T("btn.restart");
        ExitButton.Content = Loc.T("btn.exit");

        SnoozeSwitch.Header = Loc.T("settings.allowSnooze");
        SnoozeSwitch.OnContent = Loc.T("settings.allow");
        SnoozeSwitch.OffContent = Loc.T("settings.disallow");
        SnoozeSwitch.IsOn = s.AllowSnooze;
        AutoStartSwitch.Header = Loc.T("settings.autostart");
        AutoStartSwitch.OnContent = Loc.T("settings.on");
        AutoStartSwitch.OffContent = Loc.T("settings.off");
        AutoStartSwitch.IsOn = s.StartWithWindows;

        LanguageBox.Header = Loc.T("settings.language");
        LanguageBox.Items.Clear();
        foreach (var tag in new[] { Loc.Auto, Loc.ZhCN, Loc.EnUS })
        {
            LanguageBox.Items.Add(new ComboBoxItem { Content = Loc.T("lang." + tag), Tag = tag });
        }
        LanguageBox.SelectedIndex = s.Language switch { Loc.ZhCN => 1, Loc.EnUS => 2, _ => 0 };

        CustomExpander.Header = Loc.T("custom.header");
        CustomHint.Text = Loc.T("custom.hint");
        StandGroupText.Text = Loc.T("custom.standGroup");
        SitGroupText.Text = Loc.T("custom.sitGroup");
        foreach (var field in _fields)
        {
            field.Box.Header = Loc.T(field.LabelKey);
            field.Box.PlaceholderText = Loc.T(field.DefaultKey);
            field.Box.Text = field.Get(s);
        }
        PreviewStandButton.Content = Loc.T("custom.previewStand");
        PreviewSitButton.Content = Loc.T("custom.previewSit");
        ResetTextButton.Content = Loc.T("custom.reset");

        NoteText.Text = Loc.T("settings.note");

        _loading = false;
        Refresh();
    }

    public static string FormatSpan(TimeSpan span)
    {
        var total = (int)Math.Ceiling(span.TotalSeconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    public static string DescribePhase(ReminderEngine engine) => Loc.T(engine switch
    {
        { IsLocked: true } => "status.locked",
        { IsPaused: true } => "status.paused",
        { Phase: Phase.Sitting } => "status.sitting",
        { Phase: Phase.Standing } => "status.standing",
        _ => "status.waiting",
    });

    void Refresh()
    {
        if (!AppWindow.IsVisible)
        {
            return;
        }

        StatusText.Text = DescribePhase(_engine);
        CountdownText.Text = _engine.IsPrompting ? "--:--" : FormatSpan(_engine.Remaining);
        var duration = _engine.PhaseDuration;
        PhaseProgress.Value = duration > TimeSpan.Zero ? Math.Clamp(_engine.Elapsed / duration, 0, 1) : 0;
        PauseButton.Content = Loc.T(_engine.IsPaused ? "btn.resume" : "btn.pause");
    }

    void OnBreakNowClick(object sender, RoutedEventArgs e) => _engine.BreakNow();

    void OnRestartClick(object sender, RoutedEventArgs e) => _engine.Restart();

    void OnPauseClick(object sender, RoutedEventArgs e) => _engine.TogglePause();

    void OnExitClick(object sender, RoutedEventArgs e) => _exit();

    void OnPreviewStandClick(object sender, RoutedEventArgs e) => _preview(Phase.StandPrompt);

    void OnPreviewSitClick(object sender, RoutedEventArgs e) => _preview(Phase.SitPrompt);

    void OnResetTextClick(object sender, RoutedEventArgs e)
    {
        foreach (var field in _fields)
        {
            field.Set(_store.Current, string.Empty);
            field.Box.Text = string.Empty;
        }
        _store.Save();
    }

    void SaveField(TextField field)
    {
        if (_loading)
        {
            return;
        }
        var value = field.Box.Text.Trim();
        if (value == field.Get(_store.Current))
        {
            return;
        }
        field.Set(_store.Current, value);
        _store.Save();
    }

    void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }
        _store.Current.Language = tag;
        _store.Save();
        Loc.Apply(tag);
        // Apply only raises LanguageChanged when the resolved language differs; always refresh labels.
        DispatcherQueue.TryEnqueue(Localize);
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
        Refresh();
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

    void OnAutoStartToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        _store.Current.StartWithWindows = AutoStartSwitch.IsOn;
        _store.Save();
        AutoStart.Apply(AutoStartSwitch.IsOn);
    }
}
