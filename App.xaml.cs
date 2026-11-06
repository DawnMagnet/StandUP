using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using StandUP.Services;
using StandUP.Views;

namespace StandUP;

public partial class App : Application
{
    readonly Mutex? _singleInstance;
    readonly EventWaitHandle _showSignal = new(false, EventResetMode.AutoReset, @"Local\StandUP.Show");

    SettingsStore _settings = null!;
    ReminderEngine _engine = null!;
    OverlayManager _overlays = null!;
    TrayHost _tray = null!;
    MainWindow? _settingsWindow;
    DispatcherQueue _queue = null!;
    string _iconPath = null!;
    string _lastTip = string.Empty;

    public App()
    {
        _singleInstance = new Mutex(true, @"Local\StandUP.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            _showSignal.Set();
            Environment.Exit(0);
        }

        InitializeComponent();
        UnhandledException += (_, e) => LogCrash(e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _queue = DispatcherQueue.GetForCurrentThread();
        _iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "StandUP.ico");

        _settings = SettingsStore.Load();
        Loc.Apply(_settings.Current.Language);
        Loc.LanguageChanged += () => { _lastTip = string.Empty; UpdateTooltip(); };
        _engine = new ReminderEngine(_settings, _queue);
        _overlays = new OverlayManager(_engine, _settings);

        _tray = new TrayHost(_iconPath, () => _engine.IsPaused);
        _tray.OpenRequested += ShowSettings;
        _tray.BreakNowRequested += _engine.BreakNow;
        _tray.RestartRequested += _engine.Restart;
        _tray.PauseToggleRequested += _engine.TogglePause;
        _tray.ExitRequested += ExitApp;
        _tray.SessionLocked += _engine.OnSessionLocked;
        _tray.SessionUnlocked += _engine.OnSessionUnlocked;
        _engine.Changed += UpdateTooltip;
        UpdateTooltip();

        if (_settings.Current.StartWithWindows)
        {
            AutoStart.Apply(true);
        }

        var thread = new Thread(() =>
        {
            while (_showSignal.WaitOne())
            {
                _queue.TryEnqueue(ShowSettings);
            }
        })
        { IsBackground = true };
        thread.Start();

        // WinUI exits when the last window closes, so the settings window is kept alive (hidden) for the whole session.
        AppHost.Engine = _engine;
        AppHost.Settings = _settings;
        AppHost.Preview = _overlays.Preview;
        AppHost.Exit = ExitApp;
        AppHost.IconPath = _iconPath;
        _settingsWindow = new MainWindow();
        AppHost.MainWindow = _settingsWindow;
        if (!Environment.GetCommandLineArgs().Contains("--background"))
        {
            ShowSettings();
        }
    }

    void ShowSettings() => _settingsWindow?.ShowAndActivate();

    void UpdateTooltip()
    {
        var tip = _engine switch
        {
            { IsLocked: true } => Loc.T("tip.locked"),
            { IsPaused: true } => Loc.T("tip.paused"),
            { Phase: Phase.Sitting } => Loc.F("tip.sitting", (int)Math.Ceiling(_engine.Remaining.TotalMinutes)),
            { Phase: Phase.Standing } => Loc.F("tip.standing", (int)Math.Ceiling(_engine.Remaining.TotalMinutes)),
            _ => Loc.T("tip.default"),
        };
        if (tip != _lastTip)
        {
            _lastTip = tip;
            _tray.SetTooltip(tip);
        }
    }

    void ExitApp()
    {
        _overlays.CloseAll();
        if (_settingsWindow is { } window)
        {
            window.AllowClose = true;
            window.Close();
        }
        _engine.Dispose();
        _tray.Dispose();
        Exit();
    }

    internal static void LogCrash(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DataDirectory);
            File.AppendAllText(Path.Combine(SettingsStore.DataDirectory, "crash.log"), $"{DateTime.Now:O}\n{ex}\n\n");
        }
        catch (IOException)
        {
        }
    }
}
