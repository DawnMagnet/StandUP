<p align="center">
  <img src="Assets/Mascot/frame-01.png" width="128" alt="StandUP mascot" />
</p>

<h1 align="center">StandUP</h1>

<p align="center">
  A gentle full-screen reminder for Windows that helps you alternate between sitting and standing.
</p>

<p align="center">
  <a href="../../releases/latest"><b>Download the installer</b></a>
</p>

![StandUP main window](docs/screenshot.png)

## Features

- **Sit / stand cycle.** Set how long you sit and how long you stand. When the time is up, StandUP covers every
  monitor with a calm full-screen reminder, so you notice it without a jarring pop-up.
- **Snooze.** Postpone a reminder by a few minutes, or turn snoozing off entirely.
- **Screen lock aware.** Time spent on the lock screen counts as a break. If you were away long enough, the sitting
  timer starts over when you come back.
- **Your own words.** Customize the title, message and buttons of both reminders, with a live preview.
- **A friendly mascot** that stands up, sits down and waves at you, rendered by the compositor so it stays smooth.
- **Fluent design.** Light, dark or system theme, Mica or Acrylic window material, and Windows animation settings
  are respected.
- **Tray app.** Closing the window keeps StandUP running in the notification area. It can start with Windows.
- **English and Simplified Chinese.**

## Install

Download `StandUP-Setup-<version>.exe` from the [latest release](../../releases/latest) and run it. It installs for
the current user only and needs no administrator rights.

Requirements: Windows 10 version 2004 (build 19041) or later, x64, and the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). The Windows App SDK is bundled.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Then run:

```powershell
dotnet build -c Debug
```

The app is at `bin\Debug\net10.0-windows10.0.26100.0\win-x64\StandUP.exe`.

To build the installer, also install [Inno Setup 6](https://jrsoftware.org/isinfo.php), then:

```powershell
dotnet publish -c Release -o publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\StandUP.iss
```

The installer is written to `installer\Output\`.

## Project layout

| Path | Contents |
| --- | --- |
| `Services/` | Reminder engine, settings, statistics, localization, tray icon, motion helpers |
| `Views/` | Main window, pages and the full-screen reminder overlay |
| `Controls/` | The mascot, pulse rings and settings cards |
| `Assets/Mascot/` | Mascot artwork: the sit/stand frame atlas and the wave cut-outs |
| `installer/` | Inno Setup script |

Settings and daily statistics are stored in `%LOCALAPPDATA%\StandUP`.

## License

[MIT](LICENSE)
