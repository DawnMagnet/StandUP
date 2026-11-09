using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StandUP.Controls;
using StandUP.Services;
using Windows.Foundation;

namespace StandUP.Views.Pages;

public sealed partial class HomePage : Page
{
    const double RingSize = 232;
    const double RingThickness = 16;

    readonly ReminderEngine _engine = AppHost.Engine;
    readonly DispatcherQueueTimer _feedbackTimer;
    (Windows.UI.Color From, Windows.UI.Color To) _palette;
    (Phase Phase, bool Paused, bool Locked)? _lastState;
    bool? _compact;
    bool _concealing;

    public HomePage()
    {
        InitializeComponent();
        SitIconBack.Background = Palette.Gradient(Palette.Sit);
        StandIconBack.Background = Palette.Gradient(Palette.Stand);
        BreakIconBack.Background = Palette.Gradient(Palette.Prompt);
        RemainingText.Text = Loc.T("home.remaining");
        _feedbackTimer = DispatcherQueue.CreateTimer();
        _feedbackTimer.Interval = TimeSpan.FromSeconds(4);
        _feedbackTimer.IsRepeating = false;
        _feedbackTimer.Tick += (_, _) =>
        {
            _concealing = true;
            UiMotion.Conceal(ActionFeedback, 8, () =>
            {
                // A newer message may have arrived while this one was leaving.
                if (!_concealing) return;
                _concealing = false;
                ActionFeedback.Visibility = Visibility.Collapsed;
            });
        };

        Loaded += (_, _) =>
        {
            _lastState = null;
            _engine.Changed += Refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            _engine.Changed -= Refresh;
            _feedbackTimer.Stop();
            ActionFeedback.Visibility = Visibility.Collapsed;
        };
    }

    public static string FormatSpan(TimeSpan span)
    {
        var total = (int)Math.Ceiling(span.TotalSeconds);
        return total >= 3600
            ? $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}"
            : $"{total / 60:00}:{total % 60:00}";
    }

    void Refresh()
    {
        var state = (_engine.Phase, _engine.IsPaused, _engine.IsLocked);
        var previous = _lastState;
        var stateChanged = previous.HasValue && previous.Value != state;
        _lastState = state;

        var now = DateTime.Now;
        GreetingText.Text = Loc.T(now.Hour switch
        {
            < 12 => "home.morning",
            < 18 => "home.afternoon",
            _ => "home.evening",
        });
        var culture = CultureInfo.GetCultureInfo(Loc.Current);
        DateText.Text = now.ToString(culture.DateTimeFormat.LongDatePattern, culture);

        var palette = Palette.For(_engine);
        if (palette != _palette)
        {
            _palette = palette;
            RingArc.Stroke = Palette.Gradient(palette);
            HeroGlow.Background = Palette.Gradient(palette, 0.20, 0.0);
            PhaseChip.Background = Palette.Solid(Palette.WithAlpha(palette.From, 0.18));
            PhaseGlyph.Foreground = Palette.Solid(palette.From);
        }

        RingArc.Data = Arc(_engine.IsPrompting ? 1 : _engine.Progress);
        CountdownText.Text = _engine.IsPrompting ? "--:--" : FormatSpan(_engine.Remaining);

        PhaseText.Text = Loc.T(_engine switch
        {
            { IsLocked: true } => "pill.locked",
            { IsPaused: true } => "pill.paused",
            { IsPrompting: true } => "pill.prompt",
            { Phase: Phase.Standing } => "pill.standing",
            _ => "pill.sitting",
        });
        PhaseGlyph.Glyph = _engine switch
        {
            { IsLocked: true } => "\uE72E",
            { IsPaused: true } => "\uE769",
            { IsPrompting: true } => "\uEA8F",
            { Phase: Phase.Standing } => "\uE805",
            _ => "\uE7F4",
        };

        var next = _engine.NextReminderAt?.ToString("t", culture);
        HeadlineText.Text = _engine switch
        {
            { IsLocked: true } => Loc.T("home.lockedTitle"),
            { IsPaused: true } => Loc.T("home.pausedTitle"),
            { IsPrompting: true } => Loc.T("home.promptTitle"),
            { Phase: Phase.Standing } => Loc.F("home.nextSit", next ?? string.Empty),
            _ => Loc.F("home.nextStand", next ?? string.Empty),
        };
        DescriptionText.Text = Loc.T(_engine switch
        {
            { IsLocked: true } => $"lock.{AppHost.Settings.Current.LockMode}.short",
            { IsPaused: true } => "home.desc.paused",
            { IsPrompting: true } => "home.desc.prompt",
            { Phase: Phase.Standing } => "home.desc.standing",
            _ => "home.desc.sitting",
        });

        PauseGlyph.Glyph = _engine.IsPaused ? "\uE768" : "\uE769";
        PauseText.Text = Loc.T(_engine.IsPaused ? "btn.resume" : "btn.pause");
        BreakNowButton.IsEnabled = !_engine.IsPrompting;

        LockInfo.Message = Loc.T($"lock.{AppHost.Settings.Current.LockMode}.desc")
            + (Loc.Current == Loc.ZhCN ? string.Empty : " ") + Loc.T("home.trayNote");

        var stats = _engine.Stats;
        SitStatText.Text = AppHost.Duration(stats.Sitting);
        StandStatText.Text = AppHost.Duration(stats.Standing);
        BreakStatText.Text = stats.Breaks.ToString(CultureInfo.InvariantCulture);

        HeroMascot.State = _engine switch
        {
            { IsLocked: true } or { IsPaused: true } => MascotState.Paused,
            { Phase: Phase.StandPrompt } => MascotState.Prompt,
            { Phase: Phase.Standing or Phase.SitPrompt } => MascotState.Standing,
            _ => MascotState.Sitting,
        };

        if (stateChanged)
        {
            UiMotion.Reveal(HeroDetails, 8);
            if (!_engine.IsLocked && previous!.Value.Phase == Phase.StandPrompt && _engine.Phase == Phase.Standing)
            {
                ShowFeedback("home.feedback.standing", "\uE73E");
                UiMotion.Emphasize(BreakIconBack);
            }
            else if (!_engine.IsLocked && previous!.Value.Phase == Phase.SitPrompt && _engine.Phase == Phase.Sitting)
            {
                ShowFeedback("home.feedback.sitting", "\uE73E");
            }
        }
    }

    void OnHeroSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 740;
        if (_compact == compact) return;
        _compact = compact;
        HeroContent.ColumnSpacing = compact ? 0 : 32;
        HeroContent.RowSpacing = compact ? 24 : 0;
        Grid.SetColumn(HeroDetails, compact ? 0 : 1);
        Grid.SetRow(HeroDetails, compact ? 1 : 0);
        Grid.SetColumnSpan(HeroDetails, compact ? 2 : 1);
        Grid.SetColumnSpan(TimerVisual, compact ? 2 : 1);
        TimerVisual.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        ActionButtons.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
        foreach (var button in ActionButtons.Children.OfType<Button>())
        {
            button.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
        }
    }

    void ShowFeedback(string key, string glyph)
    {
        FeedbackText.Text = Loc.T(key);
        FeedbackGlyph.Glyph = glyph;
        FeedbackGlyph.Foreground = Palette.Solid(Palette.For(_engine).From);
        var wasVisible = ActionFeedback.Visibility == Visibility.Visible && !_concealing;
        _concealing = false;
        ActionFeedback.Visibility = Visibility.Visible;
        if (!wasVisible)
        {
            UiMotion.Reveal(ActionFeedback, 12);
        }
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

    static Geometry? Arc(double progress)
    {
        if (progress <= 0.0005)
        {
            return null;
        }
        var radius = (RingSize - RingThickness) / 2;
        var center = RingSize / 2;
        if (progress >= 0.9995)
        {
            return new EllipseGeometry { Center = new Point(center, center), RadiusX = radius, RadiusY = radius };
        }
        var angle = progress * 2 * Math.PI;
        var figure = new PathFigure { StartPoint = new Point(center, center - radius), IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(center + radius * Math.Sin(angle), center - radius * Math.Cos(angle)),
            Size = new Size(radius, radius),
            IsLargeArc = angle > Math.PI,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    void OnBreakNowClick(object sender, RoutedEventArgs e) => _engine.BreakNow();

    void OnPauseClick(object sender, RoutedEventArgs e)
    {
        _engine.TogglePause();
        ShowFeedback(_engine.IsPaused ? "home.feedback.paused" : "home.feedback.resumed", _engine.IsPaused ? "\uE769" : "\uE768");
    }

    void OnRestartClick(object sender, RoutedEventArgs e)
    {
        _engine.Restart();
        ShowFeedback("home.feedback.restarted", "\uE72C");
    }
}
