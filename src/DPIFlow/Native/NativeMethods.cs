using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DPIFlow.Native
{
    internal static class NativeMethods
    {
        internal const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        internal const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        internal const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        internal const uint KEYEVENTF_KEYUP = 0x0002;
        internal const uint INPUT_KEYBOARD = 1;
        internal const ushort VK_CONTROL = 0x11;
        internal const ushort VK_0 = 0x30;
        internal const ushort VK_OEM_MINUS = 0xBD;
        internal const ushort VK_OEM_PLUS = 0xBB;

        internal delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint threadId, uint time);

        [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmod, WinEventDelegate callback, uint processId, uint threadId, uint flags);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumDisplayDevices(string device, uint devNum, ref DISPLAY_DEVICE displayDevice, uint flags);
        [DllImport("shcore.dll")] internal static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scale);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)] internal struct POINT { internal int X; internal int Y; internal POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] internal struct MONITORINFOEX { internal int cbSize; internal RECT rcMonitor; internal RECT rcWork; internal uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string szDevice; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct DISPLAY_DEVICE { internal int cb; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string DeviceName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string DeviceString; internal uint StateFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string DeviceID; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string DeviceKey; }
        [StructLayout(LayoutKind.Sequential)] internal struct INPUT { internal uint type; internal InputUnion U; }
        [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] internal MOUSEINPUT mi; [FieldOffset(0)] internal KEYBDINPUT ki; [FieldOffset(0)] internal HARDWAREINPUT hi; }
        [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT { internal int dx, dy; internal uint mouseData, dwFlags, time; internal UIntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT { internal ushort wVk, wScan; internal uint dwFlags, time; internal UIntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] internal struct HARDWAREINPUT { internal uint uMsg; internal ushort wParamL, wParamH; }

        internal static string ReadWindowTitle(IntPtr hwnd)
        {
            int length = GetWindowTextLength(hwnd);
            var buffer = new StringBuilder(Math.Max(length + 1, 256));
            GetWindowText(hwnd, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        internal static bool SendCtrlShortcut(ushort key)
        {
            var inputs = new[]
            {
                KeyInput(VK_CONTROL, false), KeyInput(key, false), KeyInput(key, true), KeyInput(VK_CONTROL, true)
            };
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) == inputs.Length;
        }

        private static INPUT KeyInput(ushort key, bool up)
        {
            return new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = key, dwFlags = up ? KEYEVENTF_KEYUP : 0 } } };
        }
    }
}
