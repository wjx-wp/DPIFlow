using DPIFlow.Models;
using DPIFlow.Native;
using System;
using System.Linq;
using System.Threading;

namespace DPIFlow.Adapters
{
    internal sealed class AdapterResult
    {
        internal bool Success { get; private set; }
        internal bool Deferred { get; private set; }
        internal string Message { get; private set; }
        internal static AdapterResult Ok(string message) { return new AdapterResult { Success = true, Message = message }; }
        internal static AdapterResult Defer(string message) { return new AdapterResult { Deferred = true, Message = message }; }
        internal static AdapterResult Fail(string message) { return new AdapterResult { Message = message }; }
    }

    internal interface IAppAdapter { AdapterResult Apply(WindowInfo window, ApplicationRule rule); }

    internal sealed class ObserveAdapter : IAppAdapter
    {
        public AdapterResult Apply(WindowInfo window, ApplicationRule rule) { return AdapterResult.Ok("Observed; Windows/app handles DPI natively."); }
    }

    internal sealed class ChromiumKeyboardZoomAdapter : IAppAdapter
    {
        private static readonly int[] ZoomSteps = { 25, 33, 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200, 250, 300, 400, 500 };
        public AdapterResult Apply(WindowInfo window, ApplicationRule rule)
        {
            if (!string.Equals(window.ProcessName, "chrome.exe", StringComparison.OrdinalIgnoreCase) && !string.Equals(window.ProcessName, "msedge.exe", StringComparison.OrdinalIgnoreCase))
                return AdapterResult.Fail("ChromiumKeyboardZoom currently supports chrome.exe and msedge.exe only.");
            if (NativeMethods.GetForegroundWindow() != window.Hwnd)
                return AdapterResult.Defer("Browser is not foreground; will retry on focus.");
            int target = ZoomSteps.OrderBy(x => Math.Abs(x - rule.TargetPercent)).First();
            int baseIndex = Array.IndexOf(ZoomSteps, 100);
            int targetIndex = Array.IndexOf(ZoomSteps, target);
            if (!NativeMethods.SendCtrlShortcut(NativeMethods.VK_0)) return AdapterResult.Fail("Ctrl+0 injection failed.");
            Thread.Sleep(35);
            ushort key = targetIndex < baseIndex ? NativeMethods.VK_OEM_MINUS : NativeMethods.VK_OEM_PLUS;
            int count = Math.Abs(targetIndex - baseIndex);
            for (int i = 0; i < count; i++) { if (!NativeMethods.SendCtrlShortcut(key)) return AdapterResult.Fail("Zoom key injection failed."); Thread.Sleep(35); }
            return AdapterResult.Ok("Chromium zoom set to " + target + "% (keyboard fallback).");
        }
    }

    internal static class AdapterFactory
    {
        internal static IAppAdapter Create(string name)
        {
            if (string.Equals(name, "ChromiumKeyboardZoom", StringComparison.OrdinalIgnoreCase)) return new ChromiumKeyboardZoomAdapter();
            return new ObserveAdapter();
        }
    }
}
