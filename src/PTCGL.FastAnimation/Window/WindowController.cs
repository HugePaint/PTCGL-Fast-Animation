using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PTCGL.FastAnimation.Window
{
    internal static class WindowBootstrap
    {
        internal static void Start(Plugin plugin)
        {
            if (!Plugin.Cfg.WindowEnabled.Value)
                return;

            if (Application.platform != RuntimePlatform.WindowsPlayer
                && Application.platform != RuntimePlatform.WindowsEditor)
            {
                Plugin.Log.LogWarning("[Window] resizable frame is Windows-only; leaving the window unchanged.");
                return;
            }

            plugin.gameObject.AddComponent<WindowController>();
        }
    }

    internal sealed class WindowController : MonoBehaviour
    {
        private ResizableFrame? _frame;
        private string? _lastWarning;
        private float _suppressWarningsUntil;
        private float _missingSince = -1f;
        private float _retryAt;
        private float _styleRetryAt;
        private IntPtr _retryHwnd;
        private bool _loggedFullscreen;

        private void Awake()
        {
            _frame = new ResizableFrame();
        }

        private void Update()
        {
            if (_frame == null || _frame.InCallback)
                return;

            try
            {
                var fault = _frame.TakeFault();
                if (fault != null)
                    Warn(fault);
                Tick();
            }
            catch (Exception ex)
            {
                Warn("setup failed: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            if (_frame == null || !_frame.IsAttached)
                return;

            try
            {
                if (!_frame.Detach())
                    Plugin.Log.LogWarning("[Window] failed to remove the resizable frame.");
                Plugin.Log.LogInfo("[Window] detached. shutdown.");
            }
            catch (Exception)
            {
                // Unity is already tearing the player down. Leave the process exit to drop the hook.
            }
        }

        private void Tick()
        {
            var frame = _frame;
            if (frame == null)
                return;

            var windowed = Screen.fullScreenMode == FullScreenMode.Windowed;
            if (!windowed)
            {
                _missingSince = -1f;
                _retryHwnd = IntPtr.Zero;
                if (frame.IsAttached)
                    Release(frame, "fullscreen");
                else if (!_loggedFullscreen)
                {
                    _loggedFullscreen = true;
                    Plugin.Log.LogInfo("[Window] fullscreen; frame stays off.");
                }

                return;
            }

            _loggedFullscreen = false;
            var hwnd = Win32Window.FindUnityWindow();
            if (hwnd == IntPtr.Zero)
            {
                if (frame.IsAttached)
                    Release(frame, "hwnd unavailable");

                if (_missingSince < 0f)
                    _missingSince = Time.unscaledTime;
                else if (Time.unscaledTime - _missingSince >= 2f)
                    Warn("Unity window handle is not available.");
                return;
            }

            _missingSince = -1f;
            if (frame.IsAttached && frame.Hwnd != hwnd)
                Release(frame, "hwnd recreated");

            if (!frame.IsAttached)
            {
                if (hwnd == _retryHwnd && Time.unscaledTime < _retryAt)
                    return;

                if (frame.TryAttach(hwnd, out var error))
                {
                    _retryHwnd = IntPtr.Zero;
                    Plugin.Log.LogInfo("[Window] attached. hwnd=" + FormatHwnd(hwnd) + " clientMin=640x360.");
                    return;
                }

                _retryHwnd = hwnd;
                _retryAt = Time.unscaledTime + 1f;
                Warn(error ?? "setup failed.");
                return;
            }

            if (Time.unscaledTime < _styleRetryAt)
                return;

            if (!frame.EnsureOwnedStyles())
            {
                _styleRetryAt = Time.unscaledTime + 1f;
                Warn("failed to keep the resizable frame on the window.");
            }
        }

        private void Release(ResizableFrame frame, string reason)
        {
            if (!frame.Detach())
                Warn("failed to remove the resizable frame.");
            Plugin.Log.LogInfo("[Window] detached. " + reason + ".");
        }

        private void Warn(string message)
        {
            var now = Time.unscaledTime;
            if (message == _lastWarning && now < _suppressWarningsUntil)
                return;

            _lastWarning = message;
            _suppressWarningsUntil = now + 5f;
            Plugin.Log.LogWarning("[Window] " + message);
        }

        private static string FormatHwnd(IntPtr hwnd)
        {
            var value = unchecked((ulong)hwnd.ToInt64());
            if (IntPtr.Size == 4)
                value &= 0xFFFFFFFF;
            return "0x" + value.ToString("X");
        }
    }

    internal sealed class ResizableFrame
    {
        private const uint WmGetMinMaxInfo = 0x0024;
        private const uint WmSize = 0x0005;
        private const uint WmNcDestroy = 0x0082;
        private const uint WmEnterSizeMove = 0x0231;
        private const uint WmExitSizeMove = 0x0232;
        private const uint RdwInvalidate = 0x0001;
        private const uint RdwAllChildren = 0x0080;
        private const uint RdwUpdateNow = 0x0100;

        private readonly WndProc _callback;
        private readonly IntPtr _callbackPtr;
        private IntPtr _hwnd;
        private IntPtr _previousProc;
        private uint _ownedBits;
        private int _callbackDepth;
        private bool _interactiveSizing;
        private bool _redrawing;
        private string? _fault;

        internal ResizableFrame()
        {
            _callback = OnMessage;
            _callbackPtr = Marshal.GetFunctionPointerForDelegate(_callback);
        }

        internal bool IsAttached => _hwnd != IntPtr.Zero;

        internal bool InCallback => _callbackDepth > 0;

        internal IntPtr Hwnd => _hwnd;

        internal string? TakeFault()
        {
            var fault = _fault;
            _fault = null;
            return fault;
        }

        internal bool TryAttach(IntPtr hwnd, out string? error)
        {
            error = null;
            if (hwnd == IntPtr.Zero || !Win32Window.IsWindow(hwnd))
            {
                error = "window handle is invalid.";
                return false;
            }

            if (_hwnd == hwnd)
                return true;

            if (!Detach())
            {
                error = "failed to remove the previous resizable frame.";
                return false;
            }

            var before = Win32Window.GetStyle(hwnd);
            var desired = before | Win32Window.WsThickFrame | Win32Window.WsMaximizeBox;
            if (desired != before)
            {
                var applied = Win32Window.SetStyle(hwnd, desired);
                if ((applied & Win32Window.WsThickFrame) == 0 || (applied & Win32Window.WsMaximizeBox) == 0)
                {
                    RestoreStyle(hwnd, before, desired);
                    error = "resizable frame styles were rejected.";
                    return false;
                }
            }

            if (!Win32Window.TryNotifyFrameChanged(hwnd))
            {
                RestoreStyle(hwnd, before, desired);
                error = "SetWindowPos frame change failed.";
                return false;
            }

            var previous = Win32Window.GetWndProc(hwnd);
            if (previous == IntPtr.Zero)
            {
                RestoreStyle(hwnd, before, desired);
                error = "the window has no procedure to subclass.";
                return false;
            }

            _ownedBits = (Win32Window.WsThickFrame | Win32Window.WsMaximizeBox) & ~before;
            _previousProc = previous;
            _hwnd = hwnd;
            var swapped = Win32Window.SetWndProc(hwnd, _callbackPtr);
            if (swapped == IntPtr.Zero)
            {
                var nativeError = Marshal.GetLastWin32Error();
                _hwnd = IntPtr.Zero;
                _previousProc = IntPtr.Zero;
                _ownedBits = 0;
                RestoreStyle(hwnd, before, desired);
                error = "WndProc subclass failed (" + nativeError + ").";
                return false;
            }

            _previousProc = swapped;
            return true;
        }

        internal bool Detach()
        {
            if (_hwnd == IntPtr.Zero)
                return true;

            var hwnd = _hwnd;
            var previous = _previousProc;
            var owned = _ownedBits;
            var alive = Win32Window.IsWindow(hwnd);
            if (alive && previous != IntPtr.Zero && Win32Window.SetWndProc(hwnd, previous) == IntPtr.Zero)
                return false;

            _hwnd = IntPtr.Zero;
            _previousProc = IntPtr.Zero;
            _ownedBits = 0;
            _interactiveSizing = false;

            if (!alive || owned == 0 || !Win32Window.IsWindow(hwnd))
                return true;

            var style = Win32Window.GetStyle(hwnd);
            var restored = style & ~owned;
            if (restored != style)
                Win32Window.SetStyle(hwnd, restored);

            var cleared = (Win32Window.GetStyle(hwnd) & owned) == 0;
            if (restored != style)
                Win32Window.TryNotifyFrameChanged(hwnd);
            return cleared;
        }

        internal bool EnsureOwnedStyles()
        {
            if (_hwnd == IntPtr.Zero || !Win32Window.IsWindow(_hwnd))
                return false;

            var style = Win32Window.GetStyle(_hwnd);
            var missing = (Win32Window.WsThickFrame | Win32Window.WsMaximizeBox) & ~style;
            if (missing == 0)
                return true;

            var applied = Win32Window.SetStyle(_hwnd, style | missing);
            if ((applied & missing) != missing || !Win32Window.TryNotifyFrameChanged(_hwnd))
            {
                Win32Window.SetStyle(_hwnd, style);
                Win32Window.TryNotifyFrameChanged(_hwnd);
                return false;
            }

            _ownedBits |= missing;
            return true;
        }

        // Win32 calls only. Unity APIs are not safe on this callback.
        private IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var previous = _previousProc;
            _callbackDepth++;
            try
            {
                if (msg == WmGetMinMaxInfo && lParam != IntPtr.Zero)
                    return OnGetMinMaxInfo(previous, hwnd, msg, wParam, lParam);

                var result = Call(previous, hwnd, msg, wParam, lParam);
                if (msg == WmEnterSizeMove)
                    _interactiveSizing = true;
                else if (msg == WmExitSizeMove)
                    _interactiveSizing = false;
                else if (msg == WmSize)
                    TryLiveRedraw(hwnd);
                else if (msg == WmNcDestroy && hwnd == _hwnd)
                    DropDestroyedWindow();
                return result;
            }
            catch (Exception ex)
            {
                NoteFault(ex.GetType().Name + ": " + ex.Message);
                try
                {
                    return Call(previous, hwnd, msg, wParam, lParam);
                }
                catch (Exception nested)
                {
                    NoteFault(nested.GetType().Name + ": " + nested.Message);
                    return IntPtr.Zero;
                }
            }
            finally
            {
                _callbackDepth--;
            }
        }

        private IntPtr OnGetMinMaxInfo(IntPtr previous, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var result = Call(previous, hwnd, msg, wParam, lParam);
            var info = Marshal.PtrToStructure<Win32Window.MinMaxInfo>(lParam);
            if (info.MaxSize.X <= 0 || info.MaxSize.Y <= 0 || info.MaxTrackSize.X <= 0 || info.MaxTrackSize.Y <= 0)
            {
                Win32Window.DefWindowProc(hwnd, msg, wParam, lParam);
                info = Marshal.PtrToStructure<Win32Window.MinMaxInfo>(lParam);
            }

            if (!Win32Window.TryGetMinimumTrackSize(hwnd, out var width, out var height))
            {
                NoteFault("minimum client size was not applied.");
                return result;
            }

            info.MinTrackSize.X = width;
            info.MinTrackSize.Y = height;
            Marshal.StructureToPtr(info, lParam, false);
            return IntPtr.Zero;
        }

        private void TryLiveRedraw(IntPtr hwnd)
        {
            if (!_interactiveSizing || _redrawing)
                return;

            _redrawing = true;
            try
            {
                Win32Window.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RdwInvalidate | RdwAllChildren | RdwUpdateNow);
            }
            finally
            {
                _redrawing = false;
            }
        }

        private void DropDestroyedWindow()
        {
            _hwnd = IntPtr.Zero;
            _previousProc = IntPtr.Zero;
            _ownedBits = 0;
            _interactiveSizing = false;
        }

        private static IntPtr Call(IntPtr previous, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return previous == IntPtr.Zero
                ? Win32Window.DefWindowProc(hwnd, msg, wParam, lParam)
                : Win32Window.CallWindowProc(previous, hwnd, msg, wParam, lParam);
        }

        private void NoteFault(string message)
        {
            if (_fault == null)
                _fault = message;
        }

        private static void RestoreStyle(IntPtr hwnd, uint before, uint desired)
        {
            if (desired == before)
                return;

            Win32Window.SetStyle(hwnd, before);
            Win32Window.TryNotifyFrameChanged(hwnd);
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
