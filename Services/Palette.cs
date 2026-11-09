using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace StandUP.Services;

// Phase colors shared by the dashboard, the title bar status pill and the full-screen reminders.
public static class Palette
{
    public static readonly (Color From, Color To) Sit = (Color.FromArgb(255, 108, 124, 245), Color.FromArgb(255, 139, 92, 246));
    public static readonly (Color From, Color To) Stand = (Color.FromArgb(255, 22, 163, 106), Color.FromArgb(255, 45, 201, 138));
    public static readonly (Color From, Color To) Prompt = (Color.FromArgb(255, 251, 146, 60), Color.FromArgb(255, 236, 72, 153));
    public static readonly (Color From, Color To) Idle = (Color.FromArgb(255, 148, 163, 184), Color.FromArgb(255, 100, 116, 139));

    public static (Color From, Color To) For(ReminderEngine engine) => engine switch
    {
        { IsLocked: true } or { IsPaused: true } => Idle,
        { IsPrompting: true } => Prompt,
        { Phase: Phase.Standing } => Stand,
        _ => Sit,
    };

    public static LinearGradientBrush Gradient((Color From, Color To) pair, double fromAlpha = 1, double toAlpha = 1)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
        };
        brush.GradientStops.Add(new GradientStop { Color = WithAlpha(pair.From, fromAlpha), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = WithAlpha(pair.To, toAlpha), Offset = 1 });
        return brush;
    }

    public static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(255 * Math.Clamp(alpha, 0, 1)), color.R, color.G, color.B);

    public static SolidColorBrush Solid(Color color) => new(color);

    public static Color White => Colors.White;
}
