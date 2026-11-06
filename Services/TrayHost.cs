using System.Runtime.InteropServices;
using static StandUP.Services.NativeMethods;

namespace StandUP.Services;

internal sealed class TrayHost : IDisposable
{
    const uint WM_TRAY = WM_APP + 1;
    const uint TrayId = 1;
    const string ClassName = "StandUP.MessageWindow";

    const uint CmdOpen = 1;
    const uint CmdBreakNow = 2;
    const uint CmdRestart = 3;
    const uint CmdPause = 4;
    const uint CmdExit = 5;

    readonly WndProc _wndProc;
    readonly Func<bool> _isPaused;
    readonly string _iconPath;
    IntPtr _hwnd;
    IntPtr _icon;
    uint _taskbarCreated;
    string _tip = "StandUP";

    public event Action? OpenRequested;
    public event Action? BreakNowRequested;
    public event Action? RestartRequested;
    public event Action? PauseToggleRequested;
    public event Action? ExitRequested;
    public event Action? SessionLocked;
    public event Action? SessionUnlocked;

    public TrayHost(string iconPath, Func<bool> isPaused)
    {
        _iconPath = iconPath;
        _isPaused = isPaused;
        _wndProc = WindowProc;

        var instance = GetModuleHandleW(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = instance,
            lpszClassName = ClassName,
        };
        if (RegisterClassExW(ref wc) == 0)
        {
            throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
        }

        // A hidden top-level window (not message-only) so it receives TaskbarCreated broadcasts.
        const uint WS_POPUP = 0x80000000;
        const uint WS_EX_TOOLWINDOW = 0x80;
        _hwnd = CreateWindowExW(WS_EX_TOOLWINDOW, ClassName, "StandUP", WS_POPUP, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }

        _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
        if (!WTSRegisterSessionNotification(_hwnd, 0))
        {
            App.LogCrash(new InvalidOperationException($"WTSRegisterSessionNotification failed: {Marshal.GetLastWin32Error()}"));
        }

        _icon = LoadImageW(IntPtr.Zero, _iconPath, IMAGE_ICON,
            GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE);
        AddIcon();
    }

    public void SetTooltip(string tip)
    {
        if (tip == _tip)
        {
            return;
        }
        _tip = tip;
        var data = CreateData();
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    NOTIFYICONDATA CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = TrayId,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        uCallbackMessage = WM_TRAY,
        hIcon = _icon,
        szTip = _tip.Length > 120 ? _tip[..120] : _tip,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    void AddIcon()
    {
        var data = CreateData();
        Shell_NotifyIconW(NIM_ADD, ref data);
    }

    IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAY)
        {
            switch ((uint)(lParam.ToInt64() & 0xFFFF))
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    OpenRequested?.Invoke();
                    break;
                case WM_RBUTTONUP:
                    ShowMenu();
                    break;
            }
            return IntPtr.Zero;
        }

        if (msg == WM_WTSSESSION_CHANGE)
        {
            switch ((int)wParam.ToInt64())
            {
                case WTS_SESSION_LOCK:
                case WTS_CONSOLE_DISCONNECT:
                case WTS_REMOTE_DISCONNECT:
                    SessionLocked?.Invoke();
                    break;
                case WTS_SESSION_UNLOCK:
                case WTS_CONSOLE_CONNECT:
                case WTS_REMOTE_CONNECT:
                case WTS_SESSION_LOGON:
                    SessionUnlocked?.Invoke();
                    break;
            }
            return IntPtr.Zero;
        }

        if (_taskbarCreated != 0 && msg == _taskbarCreated)
        {
            AddIcon();
            return IntPtr.Zero;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    void ShowMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenuW(menu, MF_STRING, CmdOpen, Loc.T("tray.open"));
        AppendMenuW(menu, MF_SEPARATOR, 0, null);
        AppendMenuW(menu, MF_STRING, CmdBreakNow, Loc.T("tray.breakNow"));
        AppendMenuW(menu, MF_STRING, CmdRestart, Loc.T("tray.restart"));
        AppendMenuW(menu, MF_STRING, CmdPause, Loc.T(_isPaused() ? "tray.resume" : "tray.pause"));
        AppendMenuW(menu, MF_SEPARATOR, 0, null);
        AppendMenuW(menu, MF_STRING, CmdExit, Loc.T("tray.exit"));

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd);
        var cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);

        switch (cmd)
        {
            case CmdOpen: OpenRequested?.Invoke(); break;
            case CmdBreakNow: BreakNowRequested?.Invoke(); break;
            case CmdRestart: RestartRequested?.Invoke(); break;
            case CmdPause: PauseToggleRequested?.Invoke(); break;
            case CmdExit: ExitRequested?.Invoke(); break;
        }
    }

    public void Dispose()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var data = CreateData();
        Shell_NotifyIconW(NIM_DELETE, ref data);
        WTSUnRegisterSessionNotification(_hwnd);
        DestroyWindow(_hwnd);
        _hwnd = IntPtr.Zero;
        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }
}
