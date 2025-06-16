using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;

namespace SoundScript.Services
{
    public class ClipboardService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_CONTROL = 0x11;
        private const int VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private IntPtr _previousWindow;

        public async Task<bool> CopyToClipboardAndPasteAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                // Store the current foreground window
                _previousWindow = GetForegroundWindow();

                // Copy to clipboard immediately
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Clipboard.SetText(text);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Clipboard set failed: {ex.Message}");
                        throw;
                    }
                });

                // Minimal delay for clipboard to be ready
                await Task.Delay(10);

                // Restore focus to previous window
                if (_previousWindow != IntPtr.Zero)
                {
                    SetForegroundWindow(_previousWindow);
                    await Task.Delay(20); // Minimal delay for window focus
                }

                // Send Ctrl+V immediately
                await Task.Run(() =>
                {
                    // Press Ctrl
                    keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                    
                    // Press V
                    keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                    
                    // Release V
                    keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    
                    // Release Ctrl
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard and paste operation failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CopyToClipboardAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Clipboard.SetText(text);
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard copy failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CopyTextOnlyAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                await CopyToClipboardAsync(text);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy operation failed: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> GetClipboardTextAsync()
        {
            try
            {
                return await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        return Clipboard.ContainsText() ? Clipboard.GetText() : null;
                    }
                    catch (COMException)
                    {
                        return null;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get clipboard text: {ex.Message}");
                return null;
            }
        }

        public void StorePreviousWindow()
        {
            _previousWindow = GetForegroundWindow();
        }

        public void RestorePreviousWindow()
        {
            if (_previousWindow != IntPtr.Zero)
            {
                SetForegroundWindow(_previousWindow);
            }
        }
    }
} 