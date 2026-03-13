using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Blocks Steam overlay from activating on Shift+Tab by installing a low-level
    /// keyboard hook that suppresses the key combination when the game window is focused.
    /// Only active when running under Steam (steam_api64.dll detected).
    ///
    /// How it works:
    /// - WH_KEYBOARD_LL intercepts WM_KEYDOWN/WM_KEYUP at the OS message level
    /// - Steam's overlay hooks the same message pipeline, so suppressing here prevents activation
    /// - Unity's Input.GetKeyDown uses Raw Input (WM_INPUT), a separate path unaffected by LL hooks
    /// - Result: Steam doesn't see Shift+Tab, but Unity (and our mod) still processes it normally
    /// </summary>
    public static class SteamOverlayBlocker
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int VK_TAB = 0x09;
        private const int VK_SHIFT = 0x10;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private static IntPtr _hookId = IntPtr.Zero;
        // Must be stored as a field to prevent GC from collecting the delegate
        private static LowLevelKeyboardProc _hookCallback;
        private static IntPtr _gameWindowHandle = IntPtr.Zero;
        private static bool _isInstalled;

        public static bool IsActive => _isInstalled;

        /// <summary>
        /// Install the keyboard hook if running under Steam.
        /// Safe to call multiple times - subsequent calls are no-ops.
        /// </summary>
        public static void Install()
        {
            if (_isInstalled) return;

            if (!IsSteamLoaded())
            {
                MelonLogger.Msg("[SteamOverlayBlocker] Steam not detected, skipping");
                return;
            }

            CacheGameWindow();

            // Pin delegate as field to prevent GC collection
            _hookCallback = HookCallback;

            using (var process = Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookCallback,
                    GetModuleHandle(module.ModuleName), 0);
            }

            if (_hookId == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                MelonLogger.Warning($"[SteamOverlayBlocker] Failed to install hook (error {error})");
                _hookCallback = null;
                return;
            }

            _isInstalled = true;
            MelonLogger.Msg("[SteamOverlayBlocker] Installed - Shift+Tab blocked from Steam overlay");
        }

        /// <summary>
        /// Remove the keyboard hook. Called on application quit.
        /// </summary>
        public static void Uninstall()
        {
            if (!_isInstalled) return;

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            _isInstalled = false;
            _hookCallback = null;
            MelonLogger.Msg("[SteamOverlayBlocker] Uninstalled");
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                if (hookStruct.vkCode == VK_TAB)
                {
                    int msg = wParam.ToInt32();
                    bool isKeyEvent = msg == WM_KEYDOWN || msg == WM_KEYUP
                                   || msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP;

                    if (isKeyEvent && IsShiftHeld() && IsGameWindowFocused())
                    {
                        // Suppress Shift+Tab from reaching Steam's overlay hook
                        // Unity's Input.GetKeyDown still works via Raw Input (WM_INPUT)
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static bool IsShiftHeld()
        {
            // GetKeyState returns high bit set when key is down
            return (GetKeyState(VK_SHIFT) & 0x8000) != 0;
        }

        private static bool IsGameWindowFocused()
        {
            // Refresh cached handle if it was zero (process may not have had a window yet)
            if (_gameWindowHandle == IntPtr.Zero)
                CacheGameWindow();

            IntPtr foreground = GetForegroundWindow();
            return foreground != IntPtr.Zero && foreground == _gameWindowHandle;
        }

        private static void CacheGameWindow()
        {
            using (var process = Process.GetCurrentProcess())
            {
                _gameWindowHandle = process.MainWindowHandle;
            }
        }

        private static bool IsSteamLoaded()
        {
            try
            {
                foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                {
                    string name = module.ModuleName;
                    if (name.Equals("steam_api64.dll", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("steam_api.dll", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("GameOverlayRenderer64.dll", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("GameOverlayRenderer.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        MelonLogger.Msg($"[SteamOverlayBlocker] Steam detected ({name})");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SteamOverlayBlocker] Error checking modules: {ex.Message}");
            }

            return false;
        }
    }
}
