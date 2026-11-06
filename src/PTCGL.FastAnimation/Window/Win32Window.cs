using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PTCGL.FastAnimation.Window
{
    internal static class Win32Window
    {
        internal const int MinClientWidth = 640;
        internal const int MinClientHeight = 360;

        internal const uint WsThickFrame = 0x00040000;
        internal const uint WsMaximizeBox = 0x00010000;

        private const int GwlWndProc = -4;
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const uint SwpFrameChanged =
            0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020;
        private const int LogPixelsX = 88;
        private const int ChromeSlack = 512;

        private static readonly EnumWindowsProc EnumCallback = OnEnumWindow;
        private static readonly StringBuilder ClassName = new StringBuilder(256);
        private static IntPtr _preferred;
        private static IntPtr _fallback;
        private static long _preferredArea;
        private static long _fallbackArea;
        private static uint _processId;
        private static bool _noWindowDpi;
        private static bool _noSystemDpi;
        private static bool _noAdjustForDpi;

        internal static IntPtr FindUnityWindow()
        {
            _preferred = IntPtr.Zero;
            _fallback = IntPtr.Zero;
            _preferredArea = -1;
            _fallbackArea = -1;
            _processId = (uint)Process.GetCurrentProcess().Id;
            EnumWindows(EnumCallback, IntPtr.Zero);
            return _preferred != IntPtr.Zero ? _preferred : _fallback;
        }

        internal static uint GetStyle(IntPtr hwnd)
        {
            return Low32(GetWindowLongPtr(hwnd, GwlStyle));
        }

        internal static uint SetStyle(IntPtr hwnd, uint style)
        {
            SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(unchecked((int)style)));
            return GetStyle(hwnd);
        }

        internal static bool TryNotifyFrameChanged(IntPtr hwnd)
        {
            return SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpFrameChanged);
        }

        internal static IntPtr GetWndProc(IntPtr hwnd)
        {
            return GetWindowLongPtr(hwnd, GwlWndProc);
        }

        internal static IntPtr SetWndProc(IntPtr hwnd, IntPtr wndProc)
        {
            SetLastError(0);
            return SetWindowLongPtr(hwnd, GwlWndProc, wndProc);
        }

        internal static bool TryGetMinimumTrackSize(IntPtr hwnd, out int width, out int height)
        {
            width = 0;
            height = 0;
            var style = GetStyle(hwnd);
            var exStyle = Low32(GetWindowLongPtr(hwnd, GwlExStyle));
            var menu = GetMenu(hwnd) != IntPtr.Zero;
            var dpi = GetDpi(hwnd);
            var client = new WinRect
            {
                Left = 0,
                Top = 0,
                Right = MinClientWidth,
                Bottom = MinClientHeight
            };

            var adjusted = TryAdjust(ref client, style, menu, exStyle, dpi);
            var measured = TryMeasureChrome(hwnd, out var extraWidth, out var extraHeight);
            var haveWidth = false;
            var haveHeight = false;

            if (adjusted)
            {
                haveWidth = ConsiderOuter(client.Right - client.Left, MinClientWidth, ref width);
                haveHeight = ConsiderOuter(client.Bottom - client.Top, MinClientHeight, ref height);
            }

            if (measured)
            {
                haveWidth |= ConsiderOuter(MinClientWidth + extraWidth, MinClientWidth, ref width);
                haveHeight |= ConsiderOuter(MinClientHeight + extraHeight, MinClientHeight, ref height);
            }

            return haveWidth && haveHeight;
        }

        private static bool ConsiderOuter(int candidate, int clientMinimum, ref int selected)
        {
            if (candidate < clientMinimum || candidate > clientMinimum + ChromeSlack)
                return false;
            if (candidate > selected)
                selected = candidate;
            return true;
        }

        private static bool OnEnumWindow(IntPtr hwnd, IntPtr lParam)
        {
            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId != _processId || !IsWindowVisible(hwnd))
                return true;

            var className = ReadClass(hwnd);
            if (className.Length == 0 || className == "ConsoleWindowClass")
                return true;

            if (!GetClientRect(hwnd, out var client))
                return true;

            var area = (long)Math.Max(0, client.Right - client.Left) * Math.Max(0, client.Bottom - client.Top);
            if (className == "UnityWndClass")
                Consider(ref _preferred, ref _preferredArea, hwnd, area);
            else
                Consider(ref _fallback, ref _fallbackArea, hwnd, area);
            return true;
        }

        private static void Consider(ref IntPtr best, ref long bestArea, IntPtr hwnd, long area)
        {
            if (best == IntPtr.Zero || area > bestArea)
            {
                best = hwnd;
                bestArea = area;
            }
        }

        private static string ReadClass(IntPtr hwnd)
        {
            ClassName.Length = 0;
            return GetClassName(hwnd, ClassName, ClassName.Capacity) == 0
                ? string.Empty
                : ClassName.ToString();
        }

        private static bool TryMeasureChrome(IntPtr hwnd, out int extraWidth, out int extraHeight)
        {
            extraWidth = 0;
            extraHeight = 0;
            if (!GetWindowRect(hwnd, out var window) || !GetClientRect(hwnd, out var client))
                return false;

            extraWidth = (window.Right - window.Left) - (client.Right - client.Left);
            extraHeight = (window.Bottom - window.Top) - (client.Bottom - client.Top);
            return extraWidth >= 0 && extraHeight >= 0 && extraWidth <= ChromeSlack && extraHeight <= ChromeSlack;
        }

        private static bool TryAdjust(ref WinRect rect, uint style, bool menu, uint exStyle, uint dpi)
        {
            var original = rect;
            if (!_noAdjustForDpi)
            {
                try
                {
                    if (AdjustWindowRectExForDpi(ref rect, style, menu, exStyle, dpi))
                        return true;
                }
                catch (EntryPointNotFoundException)
                {
                    _noAdjustForDpi = true;
                }
                catch (DllNotFoundException)
                {
                    _noAdjustForDpi = true;
                }
            }

            rect = original;
            return AdjustWindowRectEx(ref rect, style, menu, exStyle);
        }

        private static uint GetDpi(IntPtr hwnd)
        {
            if (!_noWindowDpi)
            {
                try
                {
                    var dpi = GetDpiForWindow(hwnd);
                    if (dpi != 0)
                        return dpi;
                }
                catch (EntryPointNotFoundException)
                {
                    _noWindowDpi = true;
                }
                catch (DllNotFoundException)
                {
                    _noWindowDpi = true;
                }
            }

            if (!_noSystemDpi)
            {
                try
                {
                    var dpi = GetDpiForSystem();
                    if (dpi != 0)
                        return dpi;
                }
                catch (EntryPointNotFoundException)
                {
                    _noSystemDpi = true;
                }
                catch (DllNotFoundException)
                {
                    _noSystemDpi = true;
                }
            }

            var dc = GetDC(hwnd);
            if (dc == IntPtr.Zero)
                return 96;
            try
            {
                var dpi = GetDeviceCaps(dc, LogPixelsX);
                return dpi > 0 ? (uint)dpi : 96;
            }
            finally
            {
                ReleaseDC(hwnd, dc);
            }
        }

        private static uint Low32(IntPtr value)
        {
            return unchecked((uint)value.ToInt64());
        }

        private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, index)
                : new IntPtr(GetWindowLong32(hwnd, index));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
        {
            // 32-bit LARGEADDRESSAWARE pointers do not fit in a signed Int32 conversion.
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hwnd, index, value)
                : new IntPtr(SetWindowLong32(hwnd, index, unchecked((int)value.ToInt64())));
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hwnd, out WinRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out WinRect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMenu(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hwnd,
            IntPtr hwndInsertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AdjustWindowRectEx(ref WinRect rect, uint style, bool menu, uint exStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AdjustWindowRectExForDpi(
            ref WinRect rect,
            uint style,
            bool menu,
            uint exStyle,
            uint dpi);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr dc, int index);

        [DllImport("user32.dll")]
        internal static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallWindowProc(
            IntPtr previous,
            IntPtr hwnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool RedrawWindow(IntPtr hwnd, IntPtr updateRect, IntPtr updateRegion, uint flags);

        [DllImport("kernel32.dll")]
        internal static extern void SetLastError(uint error);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WinPoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WinRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MinMaxInfo
        {
            public WinPoint Reserved;
            public WinPoint MaxSize;
            public WinPoint MaxPosition;
            public WinPoint MinTrackSize;
            public WinPoint MaxTrackSize;
        }
    }
}
