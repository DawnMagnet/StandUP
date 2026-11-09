using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using StandUP.Controls;
using StandUP.Services;
using Windows.Foundation;
using Windows.UI;

namespace StandUP.Views.Pages;

public sealed partial class HomePage : Page
{
    readonly ReminderEngine _engine = AppHost.Engine;
    readonly DispatcherQueueTimer _feedbackTimer;
    (Color From, Color To) _palette;
    (Phase Phase, bool Paused, bool Locked)? _lastState;
    bool? _compact;
    bool _concealing;
    double _progress;

    public HomePage()
    {
        InitializeComponent();
        SitSegment.Background = Palette.Gradient(Palette.Sit);
        StandSegment.Background = Palette.Gradient(Palette.Stand);
        SitDot.Fill = Palette.Solid(Palette.Sit.From);
        StandDot.Fill = Palette.Solid(Palette.Stand.From);
        MascotShadow.Fill = SoftShadow(Color.FromArgb(255, 6, 74, 43), 0.28);
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

    static RadialGradientBrush SoftShadow(Color color, double alpha)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop { Color = Palette.WithAlpha(color, alpha), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = Palette.WithAlpha(color, 0), Offset = 1 });
        return brush;
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
            HeroGlow.Fill = SoftShadow(palette.From, 0.16);
            PhaseChip.Background = Palette.Solid(Palette.WithAlpha(palette.From, 0.14));
            PhaseGlyph.Foreground = Palette.Solid(palette.From);
            PhaseText.Foreground = Palette.Solid(palette.From);
            ProgressFill.Background = Palette.Gradient(palette);
        }

        var standing = _engine.Phase is Phase.Standing or Phase.SitPrompt;
        CountdownText.Text = _engine.IsPrompting ? "--:--" : FormatSpan(_engine.Remaining);
        CountdownSuffix.Text = _engine switch
        {
            { IsLocked: true } or { IsPaused: true } or { IsPrompting: true } => string.Empty,
            _ => Loc.T(standing ? "home.until.sit" : "home.until.stand"),
        };

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

        var next = _engine.NextReminderAt?.ToString("t", culture) ?? string.Empty;
        HeadlineText.Text = _engine switch
        {
            { IsLocked: true } => Loc.T($"lock.{AppHost.Settings.Current.LockMode}.short"),
            { IsPaused: true } => Loc.T("home.desc.paused"),
            { IsPrompting: true } => Loc.T("home.desc.prompt"),
            { Phase: Phase.Standing } => Loc.F("home.next.standing", next),
            _ => Loc.F("home.next.sitting", next, AppHost.Settings.Current.StandMinutes),
        };

        _progress = _engine.IsPrompting ? 1 : _engine.Progress;
        UpdateProgressFill();
        ProgressDone.Text = Loc.F(standing ? "home.progress.stood" : "home.progress.sat", AppHost.Duration(_engine.Elapsed));
        ProgressTotal.Text = Loc.F("home.progress.total", (int)_engine.PhaseDuration.TotalMinutes);

        PauseGlyph.Glyph = _engine.IsPaused ? "\uE768" : "\uE769";
        PauseText.Text = Loc.T(_engine.IsPaused ? "btn.resume" : "btn.pause");
        BreakNowButton.IsEnabled = !_engine.IsPrompting;

        var stats = _engine.Stats;
        SetDuration(SitStatText, stats.Sitting);
        SetDuration(StandStatText, stats.Standing);
        SetValue(BreakStatText, stats.Breaks.ToString(CultureInfo.InvariantCulture), string.Empty);
        var total = stats.SittingSeconds + stats.StandingSeconds;
        var share = total > 0 ? stats.StandingSeconds / total : 0;
        SitShare.Width = new GridLength(total > 0 ? Math.Max(stats.SittingSeconds, total * 0.02) : 1, GridUnitType.Star);
        StandShare.Width = new GridLength(total > 0 ? Math.Max(stats.StandingSeconds, total * 0.02) : 1, GridUnitType.Star);
        SitSegment.Opacity = StandSegment.Opacity = total > 0 ? 1 : 0.25;
        ShareStatText.Text = $"{Math.Round(share * 100):0}%";

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
            }
            else if (!_engine.IsLocked && previous!.Value.Phase == Phase.SitPrompt && _engine.Phase == Phase.Sitting)
            {
                ShowFeedback("home.feedback.sitting", "\uE73E");
            }
        }
    }

    // A large number followed by its unit in the label size, e.g. "1 h 16 min".
    static void SetDuration(TextBlock target, TimeSpan span)
    {
        var minutes = (int)span.TotalMinutes;
        target.Inlines.Clear();
        if (minutes >= 60)
        {
            AddRuns(target, (minutes / 60).ToString(CultureInfo.InvariantCulture), Loc.T("unit.h"));
            target.Inlines.Add(new Run { Text = " " });
        }
        AddRuns(target, (minutes % 60 == 0 && minutes >= 60 ? 0 : minutes % 60).ToString(CultureInfo.InvariantCulture), Loc.T("unit.min"));
    }

    static void SetValue(TextBlock target, string value, string unit)
    {
        target.Inlines.Clear();
        AddRuns(target, value, unit);
    }

    static void AddRuns(TextBlock target, string value, string unit)
    {
        target.Inlines.Add(new Run { Text = value });
        if (unit.Length > 0)
        {
            target.Inlines.Add(new Run
            {
                Text = (Loc.Current == Loc.ZhCN ? string.Empty : " ") + unit,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Normal,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
        }
    }

    void OnProgressTrackSizeChanged(object sender, SizeChangedEventArgs e) => UpdateProgressFill();

    void UpdateProgressFill()
    {
        var width = ProgressTrack.ActualWidth;
        // Keep a visible pill even at the very start.
        ProgressFill.Width = width <= 0 ? 0 : Math.Max(ProgressTrack.ActualHeight, width * Math.Clamp(_progress, 0, 1));
        ProgressFill.Visibility = _progress > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void OnHeroSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 700;
        if (_compact == compact) return;
        _compact = compact;
        HeroContent.ColumnSpacing = compact ? 0 : 36;
        HeroContent.RowSpacing = compact ? 20 : 0;
        Grid.SetColumn(HeroDetails, compact ? 0 : 1);
        Grid.SetRow(HeroDetails, compact ? 1 : 0);
        Grid.SetColumnSpan(HeroDetails, compact ? 2 : 1);
        Grid.SetColumnSpan(MascotStage, compact ? 2 : 1);
        MascotStage.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        ActionButtons.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
        foreach (var button in ActionButtons.Children.OfType<Button>())
        {
            button.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
        }
        StatsRow.ColumnSpacing = compact ? 24 : 44;
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
