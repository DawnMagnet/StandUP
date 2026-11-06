using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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

        IconGlyph.Glyph = stand ? "\uE805" : "\uE7F4";
        ChipText.Text = Loc.Expand(Loc.T(stand ? "overlay.stand.chip" : "overlay.sit.chip"), settings);
        TitleText.Text = Loc.Reminder(stand ? settings.StandTitle : settings.SitTitle, stand ? "overlay.stand.title" : "overlay.sit.title", settings);
        SubtitleText.Text = Loc.Reminder(stand ? settings.StandMessage : settings.SitMessage, stand ? "overlay.stand.message" : "overlay.sit.message", settings);
        PrimaryText.Text = Loc.Reminder(stand ? settings.StandConfirm : settings.SitConfirm, stand ? "overlay.stand.confirm" : "overlay.sit.confirm", settings);
        SecondaryText.Text = Loc.Reminder(stand ? settings.StandSnooze : settings.SitSnooze, stand ? "overlay.stand.snooze" : "overlay.sit.snooze", settings);
        SecondaryButton.Visibility = settings.AllowSnooze ? Visibility.Visible : Visibility.Collapsed;
        HintText.Text = Loc.T("overlay.hint");
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
            EntranceStoryboard.Begin();
            PulseStoryboard.Begin();
            DriftStoryboard.Begin();
            PrimaryButton.Focus(FocusState.Programmatic);
        };
        Closed += (_, _) => _clock.Stop();
    }

    void UpdateClock() =>
        ClockText.Text = DateTime.Now.ToString("t", CultureInfo.GetCultureInfo(Loc.Current));

    void ApplyPalette((Color From, Color To) palette)
    {
        GlowWash.Background = Palette.Gradient(palette, 0.22, 0.04);
        OrbA.Fill = Orb(palette.From, 0.50);
        OrbB.Fill = Orb(palette.To, 0.42);
        IconDisc.Fill = Palette.Gradient(palette);
        PulseA.Stroke = Palette.Solid(palette.From);
        PulseB.Stroke = Palette.Solid(palette.To);

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

    void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        if (_closePreview is not null)
        {
            _closePreview();
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

    void OnSecondaryClick(object sender, RoutedEventArgs e)
    {
        if (_closePreview is not null)
        {
            _closePreview();
        }
        else
        {
            _engine.Snooze();
        }
    }
}
