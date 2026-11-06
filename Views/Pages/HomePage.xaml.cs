using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StandUP.Services;
using Windows.Foundation;

namespace StandUP.Views.Pages;

public sealed partial class HomePage : Page
{
    const double RingSize = 232;
    const double RingThickness = 16;

    readonly ReminderEngine _engine = AppHost.Engine;
    (Windows.UI.Color From, Windows.UI.Color To) _palette;

    public HomePage()
    {
        InitializeComponent();
        SitIconBack.Background = Palette.Gradient(Palette.Sit);
        StandIconBack.Background = Palette.Gradient(Palette.Stand);
        BreakIconBack.Background = Palette.Gradient(Palette.Prompt);
        RemainingText.Text = Loc.T("home.remaining");

        Loaded += (_, _) =>
        {
            _engine.Changed += Refresh;
            Refresh();
        };
        Unloaded += (_, _) => _engine.Changed -= Refresh;
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
            { IsLocked: true } => "home.desc.locked",
            { IsPaused: true } => "home.desc.paused",
            { IsPrompting: true } => "home.desc.prompt",
            { Phase: Phase.Standing } => "home.desc.standing",
            _ => "home.desc.sitting",
        });

        PauseGlyph.Glyph = _engine.IsPaused ? "\uE768" : "\uE769";
        PauseText.Text = Loc.T(_engine.IsPaused ? "btn.resume" : "btn.pause");
        BreakNowButton.IsEnabled = !_engine.IsPrompting;

        var stats = _engine.Stats;
        SitStatText.Text = AppHost.Duration(stats.Sitting);
        StandStatText.Text = AppHost.Duration(stats.Standing);
        BreakStatText.Text = stats.Breaks.ToString(CultureInfo.InvariantCulture);
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

    void OnPauseClick(object sender, RoutedEventArgs e) => _engine.TogglePause();

    void OnRestartClick(object sender, RoutedEventArgs e) => _engine.Restart();
}
