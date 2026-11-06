using StandUP.Views;

namespace StandUP.Services;

// Shared application services for pages, which WinUI constructs through Frame navigation.
public static class AppHost
{
    public static ReminderEngine Engine { get; set; } = null!;
    public static SettingsStore Settings { get; set; } = null!;
    public static Action<Phase> Preview { get; set; } = _ => { };
    public static Action Exit { get; set; } = () => { };
    public static string IconPath { get; set; } = string.Empty;
    public static MainWindow? MainWindow { get; set; }

    public static string Version =>
        typeof(AppHost).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static string Duration(TimeSpan span)
    {
        var minutes = (int)span.TotalMinutes;
        return minutes >= 60 ? Loc.F("fmt.hm", minutes / 60, minutes % 60) : Loc.F("fmt.m", minutes);
    }
}
