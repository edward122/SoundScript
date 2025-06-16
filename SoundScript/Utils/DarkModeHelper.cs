using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SoundScript.Utils
{
    public static class DarkModeHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static void EnableDarkMode(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                var hwnd = helper.Handle;
                
                if (hwnd == IntPtr.Zero)
                {
                    window.SourceInitialized += (sender, e) =>
                    {
                        var newHelper = new WindowInteropHelper(window);
                        SetDarkMode(newHelper.Handle);
                    };
                }
                else
                {
                    SetDarkMode(hwnd);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enable dark mode: {ex.Message}");
            }
        }

        private static void SetDarkMode(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                int useImmersiveDarkMode = 1;
                
                // Try the newer attribute first (Windows 11)
                var result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, 
                    ref useImmersiveDarkMode, sizeof(int));
                
                // If that fails, try the older attribute (Windows 10)
                if (result != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, 
                        ref useImmersiveDarkMode, sizeof(int));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set dark mode: {ex.Message}");
            }
        }
    }
} 