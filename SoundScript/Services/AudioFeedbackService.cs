using System;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace SoundScript.Services
{
    public class AudioFeedbackService : IDisposable
    {
        private SoundPlayer? _startSoundPlayer;
        private SoundPlayer? _endSoundPlayer;
        private bool _wasSystemMuted = false;
        private int _originalVolume = 0;

        // Windows API for comprehensive volume control
        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [DllImport("winmm.dll")]
        private static extern int waveOutGetVolume(IntPtr hwo, out uint dwVolume);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // Enhanced Windows messages for volume control
        private const int WM_APPCOMMAND = 0x319;
        private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
        private const int APPCOMMAND_VOLUME_UP = 0xA0000;
        private const int APPCOMMAND_VOLUME_DOWN = 0x90000;

        // Additional volume control methods
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int VK_VOLUME_MUTE = 0xAD;
        private const int VK_VOLUME_DOWN = 0xAE;
        private const int VK_VOLUME_UP = 0xAF;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public AudioFeedbackService()
        {
            InitializeSoundPlayers();
        }

        private void InitializeSoundPlayers()
        {
            try
            {
                var startSoundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "soundstart.mp3");
                var endSoundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "soundend.mp3");

                // Note: SoundPlayer only supports WAV files, so we'll need to convert or use MediaPlayer
                // For now, let's use a simple approach with system sounds as fallback
                if (File.Exists(startSoundPath))
                {
                    // We'll use MediaPlayer for MP3 support
                }
                else
                {
                    // Fallback to system sounds
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing sound players: {ex.Message}");
            }
        }

        public async Task PlayStartSoundAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var startSoundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "soundstart.mp3");
                    if (File.Exists(startSoundPath))
                    {
                        // Use MediaPlayer for your custom MP3 - should play even when system is muted
                        var mediaPlayer = new System.Windows.Media.MediaPlayer();
                        mediaPlayer.Open(new Uri(startSoundPath));
                        mediaPlayer.Volume = 1.0; // Maximum volume to overcome mute
                        mediaPlayer.Play();
                        System.Diagnostics.Debug.WriteLine("Playing your start sound (should be audible even with system muted)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("soundstart.mp3 not found, no start sound");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing start sound: {ex.Message}");
            }
        }

        public async Task PlayEndSoundAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var endSoundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "soundend.mp3");
                    if (File.Exists(endSoundPath))
                    {
                        // Use MediaPlayer for your custom MP3 - system should be unmuted by now
                        var mediaPlayer = new System.Windows.Media.MediaPlayer();
                        mediaPlayer.Open(new Uri(endSoundPath));
                        mediaPlayer.Volume = 1.0; // Full volume
                        mediaPlayer.Play();
                        System.Diagnostics.Debug.WriteLine("Playing your end sound (system unmuted)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("soundend.mp3 not found, no end sound");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing end sound: {ex.Message}");
            }
        }

        public void MuteSpeakers()
        {
            try
            {
                if (_wasSystemMuted) 
                {
                    System.Diagnostics.Debug.WriteLine("System already muted, skipping");
                    return; // Already muted
                }

                // Store current volume before muting
                waveOutGetVolume(IntPtr.Zero, out uint currentVolume);
                _originalVolume = (int)currentVolume;

                System.Diagnostics.Debug.WriteLine($"MUTING ALL BACKGROUND AUDIO - Current volume: {currentVolume}");

                // Use the most reliable method: keyboard simulation
                // This will mute all applications like Spotify, YouTube, etc.
                keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
                keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                
                _wasSystemMuted = true;
                System.Diagnostics.Debug.WriteLine("✅ BACKGROUND AUDIO MUTED - Spotify/YouTube/etc should be silent now");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error muting speakers: {ex.Message}");
            }
        }

        public void UnmuteSpeakers()
        {
            try
            {
                if (!_wasSystemMuted) 
                {
                    System.Diagnostics.Debug.WriteLine("System not muted, nothing to unmute");
                    return; // Not muted, nothing to do
                }

                System.Diagnostics.Debug.WriteLine("UNMUTING BACKGROUND AUDIO - Restoring Spotify/YouTube/etc");

                // Use keyboard simulation to toggle mute off
                keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
                keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                _wasSystemMuted = false;
                System.Diagnostics.Debug.WriteLine("✅ BACKGROUND AUDIO RESTORED - All apps should have sound again");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unmuting speakers: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _startSoundPlayer?.Dispose();
            _endSoundPlayer?.Dispose();
            
            // Ensure speakers are unmuted when disposing
            if (_wasSystemMuted)
            {
                UnmuteSpeakers();
            }
        }
    }
} 