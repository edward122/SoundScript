using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SoundScript.ViewModels;

namespace SoundScript.Views
{
    public partial class FloatingWaveformWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _hideTimer;
        private readonly List<Rectangle> _waveformBars;
        private readonly Random _random;
        private DateTime _recordingStartTime;
        private bool _isHidden;
        private bool _isExpanded = false;
        private readonly DispatcherTimer _activityTimer;
        private double[] _audioLevels = new double[15]; // Store recent audio levels

        public MainViewModel? ViewModel { get; set; }

        public FloatingWaveformWindow()
        {
            InitializeComponent();
            
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += Timer_Tick;
            
            _hideTimer = new DispatcherTimer();
            _hideTimer.Tick += HideTimer_Tick;
            
            _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _activityTimer.Tick += ActivityTimer_Tick;
            
            _waveformBars = new List<Rectangle>();
            _random = new Random();
            
            InitializeWaveform();
            PositionWindow();
            
            // Ensure bars are properly distributed initially
            Loaded += (s, e) => RedistributeWaveformBars();
            
            // Enable dark mode
            Utils.DarkModeHelper.EnableDarkMode(this);
        }

        private void InitializeWaveform()
        {
            // Create minimal waveform bars
            const int barCount = 15;
            const double barWidth = 2.5;
            const double barSpacing = 1.5;

            for (int i = 0; i < barCount; i++)
            {
                var bar = new Rectangle
                {
                    Width = barWidth,
                    Height = 2,
                    Fill = new SolidColorBrush(Color.FromRgb(100, 100, 100)), // Dim gray when inactive
                    RadiusX = 1,
                    RadiusY = 1
                };

                Canvas.SetLeft(bar, i * (barWidth + barSpacing));
                Canvas.SetBottom(bar, 0);
                
                WaveformCanvas.Children.Add(bar);
                _waveformBars.Add(bar);
            }
        }

        private void PositionWindow()
        {
            // Position at bottom center of screen, above taskbar
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var workingHeight = SystemParameters.WorkArea.Height;
            
            Left = (screenWidth - Width) / 2;
            Top = workingHeight - Height - 10; // 10px above taskbar
        }

        public void StartRecording()
        {
            if (_isHidden) return;
            
            _recordingStartTime = DateTime.Now;
            _timer.Start();
            _activityTimer.Start();
            
            // Start in minimized state
            Width = 80;
            Height = 30;
            _isExpanded = false;
            
            Show();
            // Don't activate to prevent stealing focus from current application
        }

        public void StopRecording()
        {
            _timer.Stop();
            _activityTimer.Stop();
            
            // Reset waveform to inactive state
            foreach (var bar in _waveformBars)
            {
                bar.Height = 2;
                bar.Fill = new SolidColorBrush(Color.FromRgb(100, 100, 100)); // Dim gray
            }
            
            // Reset to minimized state
            CollapseToMinimized();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Just update waveform - no timer display needed
            UpdateWaveform();
        }

        public void UpdateAudioLevels(float[] audioSamples)
        {
            // Calculate RMS (Root Mean Square) for audio level
            if (audioSamples == null || audioSamples.Length == 0) return;
            
            double sum = 0;
            for (int i = 0; i < audioSamples.Length; i++)
            {
                sum += audioSamples[i] * audioSamples[i];
            }
            var rms = Math.Sqrt(sum / audioSamples.Length);
            var level = Math.Min(1.0, rms * 10); // Scale and clamp to 0-1
            
            // Shift audio levels array and add new level
            for (int i = 0; i < _audioLevels.Length - 1; i++)
            {
                _audioLevels[i] = _audioLevels[i + 1];
            }
            _audioLevels[_audioLevels.Length - 1] = level;
        }

        private void UpdateWaveform()
        {
            var hasActivity = false;
            var maxLevel = 0.0;
            
            for (int i = 0; i < _waveformBars.Count; i++)
            {
                var bar = _waveformBars[i];
                
                // Use real audio levels if available, otherwise simulate
                double level;
                if (i < _audioLevels.Length)
                {
                    level = _audioLevels[i];
                }
                else
                {
                    // Fallback simulation for bars beyond audio data
                    var baseLevel = Math.Sin(DateTime.Now.Millisecond * 0.01 + i * 0.3) * 0.3 + 0.3;
                    var randomVariation = _random.NextDouble() * 0.2;
                    level = Math.Max(0.05, Math.Min(0.8, baseLevel + randomVariation));
                }
                
                maxLevel = Math.Max(maxLevel, level);
                
                // Scale height based on level (2px minimum, up to 16px for compact design)
                var newHeight = 2 + (level * 14);
                bar.Height = newHeight;
                
                // Check for significant activity
                if (level > 0.3) hasActivity = true;
                
                // Color based on intensity - more subtle colors
                if (level > 0.7)
                {
                    bar.Fill = new SolidColorBrush(Color.FromRgb(0, 200, 100)); // Green for loud
                }
                else if (level > 0.4)
                {
                    bar.Fill = new SolidColorBrush(Color.FromRgb(0, 150, 200)); // Blue for medium
                }
                else if (level > 0.1)
                {
                    bar.Fill = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // Light gray for quiet
                }
                else
                {
                    bar.Fill = new SolidColorBrush(Color.FromRgb(100, 100, 100)); // Dim gray for silence
                }
                
                // Position from bottom
                Canvas.SetBottom(bar, 0);
            }
            
            // Expand if there's significant audio activity
            if (hasActivity && !_isExpanded)
            {
                ExpandToShowWaveform();
            }
            else if (hasActivity && _isExpanded)
            {
                // Reset activity timer if still active
                _activityTimer.Stop();
                _activityTimer.Start();
            }
        }

        private void HideForHour_Click(object sender, RoutedEventArgs e)
        {
            HideForDuration(TimeSpan.FromHours(1));
        }

        private void HideForHalfHour_Click(object sender, RoutedEventArgs e)
        {
            HideForDuration(TimeSpan.FromMinutes(30));
        }

        private void HideForTenMinutes_Click(object sender, RoutedEventArgs e)
        {
            HideForDuration(TimeSpan.FromMinutes(10));
        }

        private void HideForDuration(TimeSpan duration)
        {
            _isHidden = true;
            Hide();
            
            _hideTimer.Interval = duration;
            _hideTimer.Start();
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();
            _isHidden = false;
            
            // Show again if currently recording
            if (ViewModel?.IsRecording == true)
            {
                Show();
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open settings: {ex.Message}", "Settings Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _isHidden = true;
            Hide();
        }

        private void ActivityTimer_Tick(object? sender, EventArgs e)
        {
            // No activity for 2 seconds, collapse back to minimized
            _activityTimer.Stop();
            CollapseToMinimized();
        }

        private void ExpandToShowWaveform()
        {
            if (_isExpanded) return;
            
            _isExpanded = true;
            
            // Calculate new dimensions (1.2x scale)
            var newWidth = 80 * 1.2; // 96px
            var newHeight = 30 * 1.2; // 36px
            
            // Store current position - only adjust left (for horizontal expansion) and top (for upward expansion)
            var currentLeft = Left;
            var currentTop = Top;
            var currentWidth = Width;
            var currentHeight = Height;
            
            // Animate to expanded state
            var expandStoryboard = new System.Windows.Media.Animation.Storyboard();
            
            // Animate width expansion
            var widthAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = newWidth,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = 0.1 }
            };
            
            // Animate height expansion
            var heightAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = newHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = 0.1 }
            };
            
            // Animate position - expand left/right (center horizontally) and upward only
            var leftAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = currentLeft - (newWidth - currentWidth) / 2, // Center horizontally
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = 0.1 }
            };
            
            var topAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = currentTop - (newHeight - currentHeight), // Expand upward only (bottom stays fixed)
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = 0.1 }
            };
            
            System.Windows.Media.Animation.Storyboard.SetTarget(widthAnimation, this);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(widthAnimation, new PropertyPath("Width"));
            System.Windows.Media.Animation.Storyboard.SetTarget(heightAnimation, this);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(heightAnimation, new PropertyPath("Height"));
            System.Windows.Media.Animation.Storyboard.SetTarget(leftAnimation, this);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(leftAnimation, new PropertyPath("Left"));
            System.Windows.Media.Animation.Storyboard.SetTarget(topAnimation, this);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(topAnimation, new PropertyPath("Top"));
            
            expandStoryboard.Children.Add(widthAnimation);
            expandStoryboard.Children.Add(heightAnimation);
            expandStoryboard.Children.Add(leftAnimation);
            expandStoryboard.Children.Add(topAnimation);
            
            expandStoryboard.Completed += (s, e) =>
            {
                // Update canvas size for expanded state and redistribute bars
                WaveformCanvas.Width = newWidth - 16; // Account for padding
                WaveformCanvas.Height = newHeight - 8; // Account for padding
                RedistributeWaveformBars();
            };
            
            expandStoryboard.Begin();
        }

        private void CollapseToMinimized()
        {
            if (!_isExpanded) return;
            
            _isExpanded = false;
            
            // Calculate position for collapse - reverse the expansion
            var currentLeft = Left;
            var currentTop = Top;
            var currentWidth = Width;
            var currentHeight = Height;
            var newWidth = 80.0;
            var newHeight = 30.0;
            
            // Animate back to minimized state
            var collapseStoryboard = new System.Windows.Media.Animation.Storyboard();
            
            var widthAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = newWidth,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase()
            };
            
            var heightAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = newHeight,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase()
            };
            
            // Animate position - collapse horizontally to center, and downward (bottom stays fixed)
            var leftAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = currentLeft + (currentWidth - newWidth) / 2, // Center horizontally
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase()
            };
            
            var topAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = currentTop + (currentHeight - newHeight), // Collapse downward (bottom stays fixed)
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase()
            };
            
            System.Windows.Media.Animation.Storyboard.SetTarget(widthAnimation, this);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(widthAnimation, new PropertyPath("Width"));
            System.Windows.Media.Animation.Storyboard.SetTarget(heightAnimation, this);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(heightAnimation, new PropertyPath("Height"));
            System.Windows.Media.Animation.Storyboard.SetTarget(leftAnimation, this);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(leftAnimation, new PropertyPath("Left"));
            System.Windows.Media.Animation.Storyboard.SetTarget(topAnimation, this);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(topAnimation, new PropertyPath("Top"));
            
            collapseStoryboard.Children.Add(widthAnimation);
            collapseStoryboard.Children.Add(heightAnimation);
            collapseStoryboard.Children.Add(leftAnimation);
            collapseStoryboard.Children.Add(topAnimation);
            
            collapseStoryboard.Completed += (s, e) =>
            {
                // Reset canvas size for minimized state and redistribute bars
                WaveformCanvas.Width = 60;
                WaveformCanvas.Height = 20;
                RedistributeWaveformBars();
            };
            
            collapseStoryboard.Begin();
        }

        private void RedistributeWaveformBars()
        {
            if (_waveformBars.Count == 0) return;
            
            var canvasWidth = WaveformCanvas.Width;
            var barCount = _waveformBars.Count;
            var totalSpacing = canvasWidth / barCount;
            var barWidth = Math.Max(1.5, totalSpacing * 0.6); // 60% of space for bars, 40% for spacing
            var spacing = (canvasWidth - (barWidth * barCount)) / (barCount - 1);
            
            for (int i = 0; i < barCount; i++)
            {
                var bar = _waveformBars[i];
                bar.Width = barWidth;
                Canvas.SetLeft(bar, i * (barWidth + spacing));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _hideTimer?.Stop();
            _activityTimer?.Stop();
            base.OnClosed(e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Prevent window from stealing focus
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
        }

        // Win32 API for preventing focus stealing
        private const int HWND_TOPMOST = -1;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int TOPMOST_FLAGS = SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int width, int height, uint uFlags);
    }
} 