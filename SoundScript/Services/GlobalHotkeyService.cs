using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SoundScript.Services
{
    public class GlobalHotkeyService
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_WIN = 0x0008;
        private const int VK_LWIN = 0x5B; // Left Windows key
        private const int VK_CONTROL = 0x11;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_RWIN = 0x5C; // Right Windows key
        
        private IntPtr _windowHandle;
        private HwndSource? _source;
        private System.Windows.Threading.DispatcherTimer? _keyCheckTimer;
        private bool _isPressed = false;
        private bool _useTimerOnly = false;
        
        public event EventHandler? HotkeyPressed;
        public event EventHandler? HotkeyReleased;
        
        public bool IsRegistered { get; private set; }

        public bool RegisterHotkey(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                _windowHandle = helper.Handle;
                
                _source = HwndSource.FromHwnd(_windowHandle);
                _source?.AddHook(HwndHook);
                
                // Always use timer-based monitoring for better press/release detection
                // Traditional hotkey registration doesn't handle releases well
                _useTimerOnly = true;
                StartKeyMonitoring();
                IsRegistered = true;
                
                System.Diagnostics.Debug.WriteLine("GlobalHotkeyService: Using timer-based key monitoring");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GlobalHotkeyService registration error: {ex.Message}");
                // Fallback to key monitoring
                StartKeyMonitoring();
                IsRegistered = true;
                return true;
            }
        }

        private void StartKeyMonitoring()
        {
            _keyCheckTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30) // Faster polling for better responsiveness
            };
            _keyCheckTimer.Tick += CheckKeyState;
            _keyCheckTimer.Start();
            
            System.Diagnostics.Debug.WriteLine("GlobalHotkeyService: Key monitoring timer started");
        }

        private void CheckKeyState(object? sender, EventArgs e)
        {
            try
            {
                // Check for Ctrl key (either left or right)
                bool ctrlPressed = (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0 || 
                                  (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0 ||
                                  (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                
                // Check for Win key (either left or right)
                bool winPressed = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || 
                                 (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
                
                bool currentlyPressed = ctrlPressed && winPressed;
                
                if (currentlyPressed && !_isPressed)
                {
                    _isPressed = true;
                    System.Diagnostics.Debug.WriteLine("GlobalHotkeyService: Hotkey PRESSED");
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                }
                else if (!currentlyPressed && _isPressed)
                {
                    _isPressed = false;
                    System.Diagnostics.Debug.WriteLine("GlobalHotkeyService: Hotkey RELEASED");
                    HotkeyReleased?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GlobalHotkeyService CheckKeyState error: {ex.Message}");
            }
        }

        public void UnregisterHotkey()
        {
            try
            {
                if (IsRegistered && !_useTimerOnly)
                {
                    UnregisterHotKey(_windowHandle, HOTKEY_ID);
                }
                
                _keyCheckTimer?.Stop();
                _keyCheckTimer = null;
                _source?.RemoveHook(HwndHook);
                IsRegistered = false;
                _isPressed = false;
                
                System.Diagnostics.Debug.WriteLine("GlobalHotkeyService: Unregistered");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GlobalHotkeyService unregister error: {ex.Message}");
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            
            try
            {
                if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID && !_useTimerOnly)
                {
                    // Traditional hotkey was pressed (but we're using timer-only mode now)
                    System.Diagnostics.Debug.WriteLine("GlobalHotkeyService: Traditional hotkey detected (ignored in timer mode)");
                    handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GlobalHotkeyService HwndHook error: {ex.Message}");
            }
            
            return IntPtr.Zero;
        }

        public bool IsCurrentlyPressed()
        {
            return _isPressed;
        }
    }
} 