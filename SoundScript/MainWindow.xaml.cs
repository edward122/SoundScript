using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using SoundScript.ViewModels;
using SoundScript.Views;
using SoundScript.Models;

namespace SoundScript
{
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
        private readonly MainViewModel _viewModel;
        private bool _statsExpanded = true;
        private bool _analyticsExpanded = false;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            
            // Enable dark mode for title bar
            Utils.DarkModeHelper.EnableDarkMode(this);
            
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            
            // Initialize hotkeys after the window is loaded
            Loaded += MainWindow_Loaded;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize application: {ex.Message}\n\nDetails: {ex}", "Startup Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        
        try
        {
            if (_viewModel != null)
            {
                // Initialize hotkeys after window handle is created
                var success = _viewModel.InitializeHotkeys(this);
                if (!success)
                {
                    MessageBox.Show("Note: Global hotkey may not work optimally. Try running as administrator for better performance.", 
                                  "Hotkey Information", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error setting up hotkeys: {ex.Message}\n\nThe application will continue but hotkeys may not work.", 
                          "Hotkey Setup Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Warning);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = new SettingsWindow();
            if (settingsWindow.ShowDialog() == true)
            {
                // Settings were saved, no need to refresh as services are recreated
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open settings: {ex.Message}", "Settings Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StatsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _statsExpanded = !_statsExpanded;
            
            // Simple rotation of the toggle icon
            if (StatsToggleRotation != null)
            {
                StatsToggleRotation.Angle = _statsExpanded ? 0 : 180;
            }
            
            // Simple visibility toggle
            if (StatsContent != null)
            {
                StatsContent.Visibility = _statsExpanded ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error toggling stats: {ex.Message}");
            // Ensure stats remain visible if there's an error
            if (StatsContent != null)
            {
                StatsContent.Visibility = Visibility.Visible;
                _statsExpanded = true;
            }
        }
    }

    private void AnalyticsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _analyticsExpanded = !_analyticsExpanded;
            AnalyticsSection.Visibility = _analyticsExpanded ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error toggling analytics: {ex.Message}");
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        try
        {
            _viewModel?.Dispose();
        }
        catch (Exception ex)
        {
            // Log but don't show error during shutdown
            System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Ensure proper cleanup before closing
            _viewModel?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during closing: {ex.Message}");
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.InitializeHotkeys(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize hotkeys: {ex.Message}", "Initialization Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopySessionText_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is DictationSession session)
            {
                var textToCopy = !string.IsNullOrWhiteSpace(session.PolishedText) 
                    ? session.PolishedText 
                    : session.RawText;
                
                if (!string.IsNullOrWhiteSpace(textToCopy))
                {
                    Clipboard.SetText(textToCopy);
                    
                    // Visual feedback - briefly change button color
                    var originalBrush = button.Background;
                    button.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // Blue
                    
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    timer.Tick += (s, args) =>
                    {
                        button.Background = originalBrush;
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy text: {ex.Message}", "Copy Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DownloadAudio_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is DictationSession session)
            {
                if (session.AudioData == null || session.AudioData.Length == 0)
                {
                    MessageBox.Show("No audio data available for this session.", "Download Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Audio Recording",
                    Filter = "WAV Audio Files (*.wav)|*.wav|All Files (*.*)|*.*",
                    DefaultExt = "wav",
                    FileName = $"Recording_{session.Timestamp:yyyy-MM-dd_HH-mm-ss}.wav"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Convert raw PCM data to WAV format
                    var wavData = ConvertPcmToWav(session.AudioData);
                    File.WriteAllBytes(saveDialog.FileName, wavData);
                    
                    // Visual feedback - briefly change button color
                    var originalBrush = button.Background;
                    button.Background = new SolidColorBrush(Color.FromRgb(16, 124, 16)); // Green
                    
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    timer.Tick += (s, args) =>
                    {
                        button.Background = originalBrush;
                        timer.Stop();
                    };
                    timer.Start();
                    
                    MessageBox.Show($"Audio saved successfully to:\n{saveDialog.FileName}", "Download Complete", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to download audio: {ex.Message}", "Download Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private byte[] ConvertPcmToWav(byte[] pcmData)
    {
        // WAV file format constants
        const int sampleRate = 16000; // 16 kHz as used in AudioCaptureService
        const short bitsPerSample = 16;
        const short channels = 1; // Mono
        
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);
        
        // WAV header
        writer.Write("RIFF".ToCharArray()); // ChunkID
        writer.Write((uint)(36 + pcmData.Length)); // ChunkSize
        writer.Write("WAVE".ToCharArray()); // Format
        
        // fmt subchunk
        writer.Write("fmt ".ToCharArray()); // Subchunk1ID
        writer.Write((uint)16); // Subchunk1Size (16 for PCM)
        writer.Write((ushort)1); // AudioFormat (1 for PCM)
        writer.Write((ushort)channels); // NumChannels
        writer.Write((uint)sampleRate); // SampleRate
        writer.Write((uint)(sampleRate * channels * bitsPerSample / 8)); // ByteRate
        writer.Write((ushort)(channels * bitsPerSample / 8)); // BlockAlign
        writer.Write((ushort)bitsPerSample); // BitsPerSample
        
        // data subchunk
        writer.Write("data".ToCharArray()); // Subchunk2ID
        writer.Write((uint)pcmData.Length); // Subchunk2Size
        writer.Write(pcmData); // The actual sound data
        
        return memoryStream.ToArray();
    }
}
}