using Microsoft.UI.Windowing;
using StandUP.Services;

namespace StandUP.Views;

internal sealed class OverlayManager
{
    readonly ReminderEngine _engine;
    readonly SettingsStore _settings;
    readonly List<OverlayWindow> _windows = [];

    public OverlayManager(ReminderEngine engine, SettingsStore settings)
    {
        _engine = engine;
        _settings = settings;
        engine.PhaseChanged += OnPhaseChanged;
    }

    void OnPhaseChanged(Phase phase)
    {
        CloseAll();
        if (phase is Phase.StandPrompt or Phase.SitPrompt)
        {
            try
            {
                Show(phase);
            }
            catch (Exception ex)
            {
                App.LogCrash(ex);
                throw;
            }
        }
    }

    public void Preview(Phase phase)
    {
        if (_engine.IsPrompting || _engine.IsLocked)
        {
            return;
        }
        CloseAll();
        Show(phase, CloseAll);
    }

    void Show(Phase phase, Action? closePreview = null)
    {
        var primary = DisplayArea.Primary;
        OverlayWindow? primaryWindow = null;
        var areas = DisplayArea.FindAll();
        for (var i = 0; i < areas.Count; i++)
        {
            var area = areas[i];
            var window = new OverlayWindow(area, _engine, _settings.Current, phase, closePreview);
            _windows.Add(window);
            window.Activate();
            if (area.DisplayId.Value == primary.DisplayId.Value)
            {
                primaryWindow = window;
            }
        }
        (primaryWindow ?? _windows.FirstOrDefault())?.BringToFront();
    }

    public void CloseAll()
    {
        foreach (var window in _windows)
        {
            window.AllowClose = true;
            window.Close();
        }
        _windows.Clear();
    }
}
