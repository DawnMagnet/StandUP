using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using StandUP.Services;
using WinRT.Interop;

namespace StandUP.Views;

public sealed partial class OverlayWindow : Window
{
    readonly ReminderEngine _engine;
    readonly Phase _phase;
    readonly Action? _closePreview;

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

        if (phase == Phase.StandPrompt)
        {
            IconGlyph.Glyph = "\uE77B";
            TitleText.Text = Loc.Reminder(settings.StandTitle, "overlay.stand.title", settings);
            SubtitleText.Text = Loc.Reminder(settings.StandMessage, "overlay.stand.message", settings);
            PrimaryButton.Content = Loc.Reminder(settings.StandConfirm, "overlay.stand.confirm", settings);
            SecondaryButton.Content = Loc.Reminder(settings.StandSnooze, "overlay.stand.snooze", settings);
        }
        else
        {
            IconGlyph.Glyph = "\uE930";
            TitleText.Text = Loc.Reminder(settings.SitTitle, "overlay.sit.title", settings);
            SubtitleText.Text = Loc.Reminder(settings.SitMessage, "overlay.sit.message", settings);
            PrimaryButton.Content = Loc.Reminder(settings.SitConfirm, "overlay.sit.confirm", settings);
            SecondaryButton.Content = Loc.Reminder(settings.SitSnooze, "overlay.sit.snooze", settings);
        }
        SecondaryButton.Visibility = settings.AllowSnooze ? Visibility.Visible : Visibility.Collapsed;
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
