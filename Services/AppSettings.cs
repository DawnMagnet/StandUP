using System.Text.Json;

namespace StandUP.Services;

public sealed class AppSettings
{
    public int SitMinutes { get; set; } = 50;
    public int StandMinutes { get; set; } = 15;
    public int SnoozeMinutes { get; set; } = 5;
    public bool AllowSnooze { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public string Language { get; set; } = Loc.Auto;
    public string Theme { get; set; } = AppTheme.System;
    public string Backdrop { get; set; } = AppBackdrop.MicaAlt;

    // User-defined full-screen reminder text; empty means "use the localized default".
    public string StandTitle { get; set; } = string.Empty;
    public string StandMessage { get; set; } = string.Empty;
    public string StandConfirm { get; set; } = string.Empty;
    public string StandSnooze { get; set; } = string.Empty;
    public string SitTitle { get; set; } = string.Empty;
    public string SitMessage { get; set; } = string.Empty;
    public string SitConfirm { get; set; } = string.Empty;
    public string SitSnooze { get; set; } = string.Empty;

    internal void Clamp()
    {
        SitMinutes = Math.Clamp(SitMinutes, 1, 480);
        StandMinutes = Math.Clamp(StandMinutes, 1, 240);
        SnoozeMinutes = Math.Clamp(SnoozeMinutes, 1, 60);
        if (Language is not (Loc.Auto or Loc.ZhCN or Loc.EnUS))
        {
            Language = Loc.Auto;
        }
        if (Theme is not (AppTheme.System or AppTheme.Light or AppTheme.Dark))
        {
            Theme = AppTheme.System;
        }
        if (Backdrop is not (AppBackdrop.Mica or AppBackdrop.MicaAlt or AppBackdrop.Acrylic))
        {
            Backdrop = AppBackdrop.MicaAlt;
        }
        StandTitle ??= string.Empty;
        StandMessage ??= string.Empty;
        StandConfirm ??= string.Empty;
        StandSnooze ??= string.Empty;
        SitTitle ??= string.Empty;
        SitMessage ??= string.Empty;
        SitConfirm ??= string.Empty;
        SitSnooze ??= string.Empty;
    }
}

public static class AppTheme
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";
}

public static class AppBackdrop
{
    public const string Mica = "mica";
    public const string MicaAlt = "micaAlt";
    public const string Acrylic = "acrylic";
}

public sealed class SettingsStore
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StandUP");

    static string FilePath => Path.Combine(DataDirectory, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public static SettingsStore Load()
    {
        var store = new SettingsStore();
        try
        {
            if (File.Exists(FilePath))
            {
                store.Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            store.Current = new AppSettings();
        }
        store.Current.Clamp();
        return store;
    }

    // Raised after every save so that open pages and windows can reflect the new values.
    public event Action? Saved;

    public void Save()
    {
        Current.Clamp();
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
        Saved?.Invoke();
    }
}
