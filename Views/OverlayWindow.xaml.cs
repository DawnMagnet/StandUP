using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using StandUP.Controls;
using StandUP.Services;
using Windows.UI;
using WinRT.Interop;

namespace StandUP.Views;

public sealed partial class OverlayWindow : Window
{
    readonly ReminderEngine _engine;
    readonly Phase _phase;
    readonly Action? _closePreview;
    readonly DispatcherQueueTimer _clock;
    readonly CancellationTokenSource _lifetime = new();
    bool _resolving;
    bool _closed;
    bool _motionSubscribed;

    public bool AllowClose { get; set; }

    // closePreview is non-null when the overlay is only a preview and must not drive the engine.
    public OverlayWindow(DisplayArea area, ReminderEngine engine, AppSettings settings, Phase phase, Action? closePreview = null)
    {
        InitializeComponent();
        _engine = engine;
        _phase = phase;
        _closePreview = closePreview;

        ExtendsContentIntoTitleBar = true;
        Title = "StandUP";

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.MoveAndResize(area.OuterBounds);
        AppWindow.Closing += (_, args) => args.Cancel = !AllowClose;

        var stand = phase == Phase.StandPrompt;
        var palette = stand ? Palette.Prompt : Palette.Stand;
        ApplyPalette(palette);

        ReminderMascot.State = stand ? MascotState.Prompt : MascotState.SitPrompt;
        ChipText.Text = Loc.Expand(Loc.T(stand ? "overlay.stand.chip" : "overlay.sit.chip"), settings);
        TitleText.Text = Loc.Reminder(stand ? settings.StandTitle : settings.SitTitle, stand ? "overlay.stand.title" : "overlay.sit.title", settings);
        SubtitleText.Text = Loc.Reminder(stand ? settings.StandMessage : settings.SitMessage, stand ? "overlay.stand.message" : "overlay.sit.message", settings);
        PrimaryText.Text = Loc.Reminder(stand ? settings.StandConfirm : settings.SitConfirm, stand ? "overlay.stand.confirm" : "overlay.sit.confirm", settings);
        SecondaryText.Text = Loc.Reminder(stand ? settings.StandSnooze : settings.SitSnooze, stand ? "overlay.stand.snooze" : "overlay.sit.snooze", settings);
        SecondaryButton.Visibility = settings.AllowSnooze ? Visibility.Visible : Visibility.Collapsed;
        HintText.Text = Loc.T($"lock.{settings.LockMode}.short");
        BrandIcon.Source = new BitmapImage(new Uri(AppHost.IconPath));
        if (closePreview is not null)
        {
            PreviewChip.Visibility = Visibility.Visible;
            PreviewChipText.Text = Loc.T("overlay.preview");
        }

        _clock = DispatcherQueue.CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clock.Start();

        Root.Loaded += (_, _) =>
        {
            if (!_motionSubscribed)
            {
                UiMotion.AnimationsChanged += OnAnimationsChanged;
                _motionSubscribed = true;
            }
            UpdateLayoutForSize();
            if (UiMotion.AnimationsEnabled)
            {
                EntranceStoryboard.Begin();
                Pulse.Start();
                DriftStoryboard.Begin();
            }
            else
            {
                ContentPanel.Opacity = 1;
                Glow.Opacity = 1;
            }
            PrimaryButton.Focus(FocusState.Programmatic);
        };
        Root.Unloaded += (_, _) =>
        {
            UnsubscribeMotion();
            StopMotion();
        };
        Root.SizeChanged += (_, _) => UpdateLayoutForSize();
        Closed += (_, _) =>
        {
            _closed = true;
            UnsubscribeMotion();
            _lifetime.Cancel();
            _clock.Stop();
            StopMotion();
            _lifetime.Dispose();
        };
    }

    void UnsubscribeMotion()
    {
        if (!_motionSubscribed) return;
        UiMotion.AnimationsChanged -= OnAnimationsChanged;
        _motionSubscribed = false;
    }

    void OnAnimationsChanged()
    {
        // UISettings raises this event off the UI thread. A queued callback can outlive the window.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed || !Root.IsLoaded) return;
            if (!UiMotion.AnimationsEnabled)
            {
                StopMotion();
            }
            else if (!_resolving)
            {
                // Resume ambient motion without replaying the entrance or an in-flight confirmation.
                Pulse.Start();
                DriftStoryboard.Begin();
            }
        });
    }

    void StopMotion()
    {
        EntranceStoryboard.Stop();
        ExitStoryboard.Stop();
        Pulse.Stop();
        DriftStoryboard.Stop();
        ContentPanel.Opacity = Glow.Opacity = 1;
        ContentTransform.TranslateY = 0;
    }

    void UpdateClock() =>
        ClockText.Text = DateTime.Now.ToString("t", CultureInfo.GetCultureInfo(Loc.Current));

    void ApplyPalette((Color From, Color To) palette)
    {
        GlowWash.Background = Palette.Gradient(palette, 0.22, 0.04);
        OrbA.Fill = Orb(palette.From, 0.50);
        OrbB.Fill = Orb(palette.To, 0.42);
        Pulse.SetColors(palette.From, palette.To);

        var accent = Palette.Gradient(palette);
        var accentHover = Palette.Gradient((Lighten(palette.From), Lighten(palette.To)));
        var white = Palette.Solid(Palette.White);
        PrimaryButton.Resources["AccentButtonBackground"] = accent;
        PrimaryButton.Resources["AccentButtonBackgroundPointerOver"] = accentHover;
        PrimaryButton.Resources["AccentButtonBackgroundPressed"] = accent;
        PrimaryButton.Resources["AccentButtonForeground"] = white;
        PrimaryButton.Resources["AccentButtonForegroundPointerOver"] = white;
        PrimaryButton.Resources["AccentButtonForegroundPressed"] = Palette.Solid(Palette.WithAlpha(Palette.White, 0.85));
        PrimaryButton.Resources["AccentButtonBorderBrush"] = Palette.Solid(Palette.WithAlpha(Palette.White, 0.25));
        PrimaryButton.Resources["AccentButtonBorderBrushPointerOver"] = Palette.Solid(Palette.WithAlpha(Palette.White, 0.4));
        PrimaryButton.Resources["AccentButtonBorderBrushPressed"] = Palette.Solid(Palette.WithAlpha(Palette.White, 0.2));
    }

    static Color Lighten(Color c) => Color.FromArgb(c.A, (byte)(c.R + (255 - c.R) / 5), (byte)(c.G + (255 - c.G) / 5), (byte)(c.B + (255 - c.B) / 5));

    static RadialGradientBrush Orb(Color color, double alpha)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop { Color = Palette.WithAlpha(color, alpha), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = Palette.WithAlpha(color, 0), Offset = 1 });
        return brush;
    }

    public void BringToFront()
    {
        Activate();
        NativeMethods.SetForegroundWindow(WindowNative.GetWindowHandle(this));
    }

    void UpdateLayoutForSize()
    {
        var compact = Root.ActualHeight < 760;
        var narrow = Root.ActualWidth < 700;
        MascotStage.Width = MascotStage.Height = compact ? 140 : 220;
        MascotStage.Margin = new Thickness(0, 0, 0, compact ? 0 : 12);
        TitleText.FontSize = compact || narrow ? 34 : 56;
        SubtitleText.FontSize = compact ? 17 : 22;
        SubtitleText.LineHeight = compact ? 25 : 34;
        ContentPanel.Spacing = compact ? 12 : 18;
        ActionPanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        ActionPanel.Margin = new Thickness(0, compact ? 4 : 14, 0, 0);
        PrimaryButton.FontSize = SecondaryButton.FontSize = compact ? 17 : 20;
        PrimaryButton.Padding = SecondaryButton.Padding = compact ? new Thickness(28, 12, 28, 12) : new Thickness(40, 16, 40, 16);
        TopBar.Padding = compact || narrow ? new Thickness(24, 18, 24, 18) : new Thickness(48, 28, 48, 28);
        ContentScroll.Padding = new Thickness(narrow ? 20 : 40, 12, narrow ? 20 : 40, 12);
        ClockText.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        HintText.MaxWidth = Math.Max(160, Root.ActualWidth - 100);
    }

    async Task CompleteAsync(bool snooze)
    {
        if (_resolving || _closed)
        {
            return;
        }
        _resolving = true;
        PrimaryButton.IsEnabled = SecondaryButton.IsEnabled = false;
        ReminderMascot.State = snooze ? MascotState.Paused : MascotState.Celebrating;
        if (snooze)
        {
            SecondaryText.Text = Loc.T("overlay.snoozed");
        }
        else
        {
            PrimaryText.Text = Loc.T(_phase == Phase.StandPrompt ? "overlay.confirmed.stand" : "overlay.confirmed.sit");
        }

        if (UiMotion.AnimationsEnabled)
        {
            EntranceStoryboard.Stop();
            ContentPanel.Opacity = 1;
            Glow.Opacity = 1;
            ExitStoryboard.Begin();
            try
            {
                await Task.Delay(180, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        // Another display, a lock event or a newer reminder may have closed this window.
        if (_closed)
        {
            return;
        }
        if (_closePreview is not null)
        {
            _closePreview();
        }
        else if (_engine.Phase == _phase)
        {
            if (snooze)
            {
                _engine.Snooze();
            }
            else if (_phase == Phase.StandPrompt)
            {
                _engine.ConfirmStanding();
            }
            else
            {
                _engine.ConfirmSitting();
            }
        }
    }

    async void OnPrimaryClick(object sender, RoutedEventArgs e) => await CompleteAsync(false);

    async void OnSecondaryClick(object sender, RoutedEventArgs e) => await CompleteAsync(true);
}
