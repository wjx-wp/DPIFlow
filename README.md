# DPIFlow

Automatic per-monitor application scaling manager for Windows.

> Status: early MVP. The core is intentionally conservative: it never changes global Windows DPI. Windows Per-Monitor DPI remains the baseline; DPIFlow only applies opt-in per-app/per-monitor rules.

## Why

A 14-inch 1080p laptop panel and a 24-inch 1080p external display can have dramatically different pixel densities. Windows can assign different DPI scales to each monitor, but some applications also have their own content zoom. DPIFlow automates that second layer when a window moves between monitors.

## MVP features

- Windows 10/11 tray app, .NET Framework 4.8, no admin requirement.
- Event-driven window tracking using WinEvent hooks (foreground + move/resize end).
- Per-monitor identity and effective DPI detection with a cached monitor map refreshed only when display topology changes.
- Local JSON rules at `%LOCALAPPDATA%\DPIFlow\settings.json`.
- Quick-add the last foreground app across all connected monitors.
- `Observe` adapter: no modification; useful for apps that already support Per-Monitor DPI correctly.
- `ChromiumKeyboardZoom` adapter: lightweight fallback for Chrome/Edge using Ctrl+0 and zoom steps.
- Optional start-with-Windows setting under the current user.
- Single-instance protection and local diagnostic logging.
- GitHub Actions builds the Windows EXE on a Windows runner.
- The root `VERSION` file controls releases. When changes reach `main`, an unpublished version is built and published automatically with an EXE, SHA-256 file, and Browser Companion ZIP.

## Recommended setup

1. Let Windows manage baseline monitor scaling (for example external 100%, laptop 125%).
2. Run `DPIFlow.exe`.
3. Use Chrome/Edge, then open DPIFlow Settings from the tray.
4. Click **Quick-add last app**. On a significantly higher-DPI monitor, Chromium defaults to 90%; lower-DPI displays default to 100%.
5. Save. Window transitions are automatic after that.

## Chrome / Edge companion

The repository includes a Manifest V3 Browser Companion under `browser-extension/`. It uses official `windows`, `system.display`, and `tabs` extension APIs to identify which monitor contains a browser window and apply per-tab zoom. It reapplies rules after window moves, tab activation/moves, navigation, and display changes.

The companion and EXE currently keep independent settings. A future Native Messaging bridge can unify them. Without the companion, `ChromiumKeyboardZoom` remains available as a lightweight fallback.

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

- Native Messaging bridge for unified EXE/browser settings.
- Better adapter plugin model.
- Rule import/export and richer diagnostics.
- App-specific adapters (PDF readers, Electron apps, etc.).
- Automated integration tests for monitor/rule selection.

License: MIT.
