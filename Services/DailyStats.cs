using System.Text.Json;

namespace StandUP.Services;

// Activity totals for the current local day. Persisted so that a restart keeps today's numbers.
public sealed class DailyStats
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static string FilePath => Path.Combine(SettingsStore.DataDirectory, "stats.json");

    public DateOnly Day { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public double SittingSeconds { get; set; }
    public double StandingSeconds { get; set; }
    public int Breaks { get; set; }

    public TimeSpan Sitting => TimeSpan.FromSeconds(SittingSeconds);
    public TimeSpan Standing => TimeSpan.FromSeconds(StandingSeconds);

    public static DailyStats Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var stats = JsonSerializer.Deserialize<DailyStats>(File.ReadAllText(FilePath));
                if (stats is not null)
                {
                    stats.RollOver();
                    return stats;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return new DailyStats();
    }

    // Resets the totals when the local date has changed since they were recorded.
    public void RollOver()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (Day != today)
        {
            Day = today;
            SittingSeconds = 0;
            StandingSeconds = 0;
            Breaks = 0;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DataDirectory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
