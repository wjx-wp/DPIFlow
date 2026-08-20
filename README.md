# DPIFlow

Automatic per-monitor application scaling manager for Windows.

> Status: early MVP. The core is intentionally conservative: it never changes global Windows DPI. Windows Per-Monitor DPI remains the baseline; DPIFlow only applies opt-in per-app/per-monitor rules.

## Why

A 14-inch 1080p laptop panel and a 24-inch 1080p external display can have dramatically different pixel densities. Windows can assign different DPI scales to each monitor, but some applications also have their own content zoom. DPIFlow automates that second layer when a window moves between monitors.

## MVP features

- Windows 10/11 tray app, .NET Framework 4.8, no admin requirement.
- Event-driven window tracking using WinEvent hooks (foreground + move/resize end).
- Per-monitor identity and effective DPI detection.
- Local JSON rules at `%LOCALAPPDATA%\DPIFlow\settings.json`.
- Quick-add the last foreground app across all connected monitors.
- `Observe` adapter: no modification; useful for apps that already support Per-Monitor DPI correctly.
- `ChromiumKeyboardZoom` adapter: lightweight fallback for Chrome/Edge using Ctrl+0 and zoom steps.
- Optional start-with-Windows setting under the current user.
- GitHub Actions builds the Windows EXE; tags matching `v*` publish a Release with SHA-256.

## Recommended setup

1. Let Windows manage baseline monitor scaling (for example external 100%, laptop 125%).
2. Run `DPIFlow.exe`.
3. Use Chrome/Edge, then open DPIFlow Settings from the tray.
4. Click **Quick-add last app**. On a significantly higher-DPI monitor, Chromium defaults to 90%; lower-DPI displays default to 100%.
5. Save. Window transitions are automatic after that.

## Important Chromium note

The keyboard adapter is deliberately a fallback and is best for a single active browser window. Chrome stores normal page zoom in ways that are not truly per-monitor. A companion Manifest V3 extension is planned/being developed for correct per-tab, per-window monitor-aware zoom using official Chrome extension APIs.

## Build

Open `DPIFlow.sln` in Visual Studio 2022 with the .NET Framework 4.8 targeting pack, or run:

```powershell
msbuild DPIFlow.sln /p:Configuration=Release /p:Platform="Any CPU"
```

Output: `src\DPIFlow\bin\Release\DPIFlow.exe`

## Safety principles

- No global DPI registry rewriting.
- No administrator requirement for normal operation.
- Rules are opt-in; unknown apps are untouched.
- Settings remain local.
- Adapter failures should fail closed (do nothing) rather than alter system display configuration.

## Roadmap

- Companion Chrome/Edge extension with per-tab zoom.
- Better adapter plugin model.
- Rule import/export and diagnostics log.
- App-specific adapters (PDF readers, Electron apps, etc.).
- Automated integration tests for monitor/rule selection.

License: MIT.
