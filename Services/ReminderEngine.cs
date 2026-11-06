using System.Diagnostics;
using Microsoft.UI.Dispatching;

namespace StandUP.Services;

public enum Phase
{
    Sitting,
    StandPrompt,
    Standing,
    SitPrompt,
}

public sealed class ReminderEngine : IDisposable
{
    // Clamp per-tick deltas so time spent suspended/asleep is not counted.
    static readonly TimeSpan MaxTickDelta = TimeSpan.FromSeconds(5);

    readonly SettingsStore _settings;
    readonly DispatcherQueueTimer _timer;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    TimeSpan _last;
    DateTime _lockStartUtc;

    public Phase Phase { get; private set; } = Phase.Sitting;
    public TimeSpan Elapsed { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsPaused { get; private set; }
    public DailyStats Stats { get; } = DailyStats.Load();

    public event Action? Changed;
    public event Action<Phase>? PhaseChanged;

    public ReminderEngine(SettingsStore settings, DispatcherQueue queue)
    {
        _settings = settings;
        _last = _clock.Elapsed;
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public TimeSpan SitDuration => TimeSpan.FromMinutes(_settings.Current.SitMinutes);
    public TimeSpan StandDuration => TimeSpan.FromMinutes(_settings.Current.StandMinutes);
    TimeSpan SnoozeDuration => TimeSpan.FromMinutes(_settings.Current.SnoozeMinutes);

    public TimeSpan PhaseDuration => Phase switch
    {
        Phase.Sitting => SitDuration,
        Phase.Standing => StandDuration,
        _ => TimeSpan.Zero,
    };

    public TimeSpan Remaining => PhaseDuration == TimeSpan.Zero
        ? TimeSpan.Zero
        : TimeSpan.FromTicks(Math.Max(0, (PhaseDuration - Elapsed).Ticks));

    public bool IsPrompting => Phase is Phase.StandPrompt or Phase.SitPrompt;

    // Progress of the current timed phase in [0, 1].
    public double Progress => PhaseDuration > TimeSpan.Zero ? Math.Clamp(Elapsed / PhaseDuration, 0, 1) : 0;

    // Local time of the next full-screen reminder, or null when the timer is not running.
    public DateTime? NextReminderAt => IsLocked || IsPaused || IsPrompting ? null : DateTime.Now + Remaining;

    void Tick()
    {
        var now = _clock.Elapsed;
        var delta = now - _last;
        _last = now;
        if (delta > MaxTickDelta)
        {
            delta = MaxTickDelta;
        }

        // Session notifications can be missed or arrive late, so also poll the desktop state.
        // Never fire a prompt while the screen is locked.
        var desktopLocked = NativeMethods.IsInputDesktopLocked();
        if (desktopLocked && !IsLocked)
        {
            OnSessionLocked();
        }
        else if (!desktopLocked && IsLocked && DateTime.UtcNow - _lockStartUtc > TimeSpan.FromSeconds(3))
        {
            OnSessionUnlocked();
        }

        if (IsLocked)
        {
            // Only these modes keep the timer running while locked. A reminder that comes due
            // waits until the unlock, when the next tick raises it.
            if (!IsPaused && !IsPrompting && LockMode is LockBehavior.Continue or LockBehavior.Sitting)
            {
                Accrue(delta);
                Changed?.Invoke();
            }
            return;
        }

        if (!IsPaused && !IsPrompting)
        {
            Accrue(delta);
            if (Phase == Phase.Sitting && Elapsed >= SitDuration)
            {
                Enter(Phase.StandPrompt, TimeSpan.Zero);
                return;
            }
            if (Phase == Phase.Standing && Elapsed >= StandDuration)
            {
                Enter(Phase.SitPrompt, TimeSpan.Zero);
                return;
            }
        }

        Changed?.Invoke();
    }

    string LockMode => _settings.Current.LockMode;

    void Accrue(TimeSpan delta)
    {
        Stats.RollOver();
        if (Phase == Phase.Sitting)
        {
            Stats.SittingSeconds += delta.TotalSeconds;
        }
        else
        {
            Stats.StandingSeconds += delta.TotalSeconds;
        }
        Elapsed += delta;
    }

    void Enter(Phase phase, TimeSpan elapsed)
    {
        if (phase == Phase.Standing && Phase == Phase.StandPrompt)
        {
            Stats.RollOver();
            Stats.Breaks++;
        }
        Phase = phase;
        Elapsed = elapsed;
        Stats.Save();
        PhaseChanged?.Invoke(phase);
        Changed?.Invoke();
    }

    public void ConfirmStanding()
    {
        if (Phase == Phase.StandPrompt)
        {
            Enter(Phase.Standing, TimeSpan.Zero);
        }
    }

    public void ConfirmSitting()
    {
        if (Phase == Phase.SitPrompt)
        {
            Enter(Phase.Sitting, TimeSpan.Zero);
        }
    }

    public void Snooze()
    {
        switch (Phase)
        {
            case Phase.StandPrompt:
                Enter(Phase.Sitting, Max(TimeSpan.Zero, SitDuration - SnoozeDuration));
                break;
            case Phase.SitPrompt:
                Enter(Phase.Standing, Max(TimeSpan.Zero, StandDuration - SnoozeDuration));
                break;
        }
    }

    public void BreakNow() => Enter(Phase.StandPrompt, TimeSpan.Zero);

    public void Restart() => Enter(Phase.Sitting, TimeSpan.Zero);

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        Changed?.Invoke();
    }

    // What a lock means depends on AppSettings.LockMode (see LockBehavior):
    // standing - locking while a prompt is pending answers it, and a lock at least as long as the
    //            stand duration restarts the sit cycle; shorter locks keep the previous timing.
    // sitting  - locking ends a standing break and the locked time adds to the sitting timer.
    // continue - the current phase keeps counting while locked.
    // pause    - nothing counts while locked.
    // reset    - every unlock starts a fresh sitting period.
    public void OnSessionLocked()
    {
        if (IsLocked)
        {
            return;
        }

        IsLocked = true;
        _lockStartUtc = DateTime.UtcNow;
        switch (LockMode)
        {
            case LockBehavior.Standing when Phase == Phase.StandPrompt:
                Enter(Phase.Standing, TimeSpan.Zero);
                break;
            case LockBehavior.Standing or LockBehavior.Sitting when Phase == Phase.SitPrompt:
                Enter(Phase.Sitting, TimeSpan.Zero);
                break;
            case LockBehavior.Sitting when Phase == Phase.Standing:
                Enter(Phase.Sitting, TimeSpan.Zero);
                break;
        }
        Changed?.Invoke();
    }

    public void OnSessionUnlocked()
    {
        if (!IsLocked)
        {
            return;
        }

        IsLocked = false;
        _last = _clock.Elapsed;
        var lockDuration = DateTime.UtcNow - _lockStartUtc;

        if (LockMode == LockBehavior.Reset)
        {
            Enter(Phase.Sitting, TimeSpan.Zero);
            return;
        }
        if (LockMode != LockBehavior.Standing)
        {
            Changed?.Invoke();
            return;
        }

        if (Phase == Phase.Standing)
        {
            Stats.RollOver();
            Stats.StandingSeconds += Math.Min(lockDuration.TotalSeconds, Math.Max(0, (StandDuration - Elapsed).TotalSeconds));
            var standing = Elapsed + lockDuration;
            if (standing >= StandDuration)
            {
                Enter(Phase.Sitting, TimeSpan.Zero);
                return;
            }
            Elapsed = standing;
        }
        else if (lockDuration >= StandDuration)
        {
            Enter(Phase.Sitting, TimeSpan.Zero);
            return;
        }

        Changed?.Invoke();
    }

    static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public void Dispose()
    {
        _timer.Stop();
        Stats.Save();
    }
}
