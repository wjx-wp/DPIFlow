# DPIFlow architecture

DPIFlow separates **monitor detection**, **window events**, **rule matching**, and **application adapters** so that supporting a new application does not require rewriting the core.

## Event path

1. `WindowWatcher` registers out-of-context WinEvent hooks for foreground changes and move/resize completion.
2. `WindowInspector` resolves the HWND to a process and the monitor containing most of the window.
3. `MonitorService` maps the Win32 display device to a stable-ish hardware match key and effective DPI.
4. The first matching `ApplicationRule` is selected.
5. `AdapterFactory` creates the adapter and applies the rule.
6. A per-HWND signature prevents repeated work while the window remains on the same monitor/rule.

## Safety

The core intentionally does **not** rewrite global Windows DPI registry values. Native Per-Monitor DPI remains the system baseline. DPIFlow only performs adapter-specific actions for explicit rules.

## Adapter direction

- `Observe`: diagnostic/no-op for apps already behaving correctly.
- `ChromiumKeyboardZoom`: no-install fallback for Chrome/Edge; limited by Chromium's normal page-zoom persistence model.
- Browser Companion: preferred Chromium path. It uses official extension APIs and per-tab zoom.
- Future adapters should implement one narrow application behavior and fail closed.

## Browser companion

The Manifest V3 companion is deliberately independent of native IPC in the first release. It determines the browser window's display by maximum rectangle intersection, then applies a per-tab zoom value. Explicit per-display values are stored in `chrome.storage.local`; otherwise it suggests 90% when a display's DPI is at least 1.25× the lowest-DPI active display, and 100% otherwise.

A future native-messaging bridge can unify EXE and browser settings without changing the core rule engine.
