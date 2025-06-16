using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SoundScript.Models;
using SoundScript.Services;
using SoundScript.Utils;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;

namespace SoundScript.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly AudioCaptureService _audioCaptureService;
        private readonly WhisperApiService _whisperService;
        private readonly GeminiApiService _geminiService;
        private readonly ClipboardService _clipboardService;
        private readonly SettingsService _settingsService;
        private readonly HistoryService _historyService;
        private readonly AudioFeedbackService _audioFeedbackService;
        private readonly UpdateService _updateService;
        private Views.FloatingWaveformWindow? _floatingWindow;

        private string _statusText = "Ready - Press Ctrl+Win to start dictation";
        private string _transcriptionText = string.Empty;
        private bool _isRecording = false;
        private bool _isProcessing = false;
        private int _activeProcessingCount = 0;
        private CancellationTokenSource? _processingCancellation;
        private DateTime _recordingStartTime;

        // Stats
        private int _totalSessions = 0;
        private TimeSpan _totalRecordingTime = TimeSpan.Zero;
        private double _averageAccuracy = 95.0;
        private int _totalWords = 0;
        private double _averageWPM = 0.0;

        // History
        private ObservableCollection<DictationSession> _todaySessions = new();
        private ObservableCollection<DictationSession> _yesterdaySessions = new();
        private bool _hasYesterdaySessions = false;
        private int _loadedSessionsCount = 0;

        // Analytics properties
        private ObservableCollection<WordCount> _topWords = new();
        private int _todaySessionCount = 0;
        private int _weekSessionCount = 0;
        private double _wpmProgressWidth = 100;

        private int _sessionCount;
        public int SessionCount
        {
            get => _sessionCount;
            set => SetProperty(ref _sessionCount, value);
        }

        private TimeSpan _totalTime;
        public TimeSpan TotalTime
        {
            get => _totalTime;
            set => SetProperty(ref _totalTime, value);
        }

        public MainViewModel()
        {
            _settingsService = new SettingsService();
            _historyService = new HistoryService();
            var settings = _settingsService.LoadSettings();

            _hotkeyService = new GlobalHotkeyService();
            _audioCaptureService = new AudioCaptureService();
            _whisperService = new WhisperApiService(settings);
            _geminiService = new GeminiApiService(settings);
            _clipboardService = new ClipboardService();
            _audioFeedbackService = new AudioFeedbackService();
            _updateService = new UpdateService();

            // Initialize floating waveform window
            _floatingWindow = new Views.FloatingWaveformWindow();
            _floatingWindow.ViewModel = this;

            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.HotkeyReleased += OnHotkeyReleased;
            
            // Connect audio data to floating window for real-time visualization
            _audioCaptureService.AudioDataReceived += OnAudioDataReceived;

            OpenSettingsCommand = new RelayCommand(OpenSettings);
            ClearTranscriptionCommand = new RelayCommand(ClearTranscription);
            LoadMoreSessionsCommand = new AsyncRelayCommand(LoadMoreSessionsAsync);

            // Load initial data
            _ = Task.Run(LoadInitialDataAsync);
            
            // Check for updates on startup (silent)
            _ = Task.Run(async () => await _updateService.CheckForUpdatesAsync(false));
        }

        public bool InitializeHotkeys(Window window)
        {
            return _hotkeyService.RegisterHotkey(window);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string TranscriptionText
        {
            get => _transcriptionText;
            set => SetProperty(ref _transcriptionText, value);
        }

        public bool IsRecording
        {
            get => _isRecording;
            set => SetProperty(ref _isRecording, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public int TotalSessions
        {
            get => _totalSessions;
            set => SetProperty(ref _totalSessions, value);
        }

        public string TotalRecordingTime => _totalRecordingTime.ToString(@"hh\:mm\:ss");

        public double AverageAccuracy
        {
            get => _averageAccuracy;
            set => SetProperty(ref _averageAccuracy, value);
        }

        public int TotalWords
        {
            get => _totalWords;
            set => SetProperty(ref _totalWords, value);
        }

        public double AverageWPM
        {
            get => _averageWPM;
            set => SetProperty(ref _averageWPM, value);
        }

        // History Properties
        public ObservableCollection<DictationSession> TodaySessions
        {
            get => _todaySessions;
            set => SetProperty(ref _todaySessions, value);
        }

        public ObservableCollection<DictationSession> YesterdaySessions
        {
            get => _yesterdaySessions;
            set => SetProperty(ref _yesterdaySessions, value);
        }

        public bool HasYesterdaySessions
        {
            get => _hasYesterdaySessions;
            set => SetProperty(ref _hasYesterdaySessions, value);
        }

        // Analytics Properties
        public ObservableCollection<WordCount> TopWords
        {
            get => _topWords;
            set => SetProperty(ref _topWords, value);
        }

        public int TodaySessionCount
        {
            get => _todaySessionCount;
            set => SetProperty(ref _todaySessionCount, value);
        }

        public int WeekSessionCount
        {
            get => _weekSessionCount;
            set => SetProperty(ref _weekSessionCount, value);
        }

        public double WPMProgressWidth
        {
            get => _wpmProgressWidth;
            set => SetProperty(ref _wpmProgressWidth, value);
        }

        public ICommand OpenSettingsCommand { get; }
        public ICommand ClearTranscriptionCommand { get; }
        public ICommand LoadMoreSessionsCommand { get; }

        private async void OnHotkeyPressed(object? sender, EventArgs e)
        {
            if (IsRecording) return; // Only block if already recording, not processing

            try
            {
                IsRecording = true;
                _recordingStartTime = DateTime.Now;
                
                // Show concurrent processing status if applicable
                if (_activeProcessingCount > 0)
                {
                    StatusText = $"🎤 Recording... ({_activeProcessingCount} processing) - Release Ctrl+Win to stop";
                }
                else
                {
                    StatusText = "🎤 Recording... (Release Ctrl+Win to stop)";
                }
                
                // IMMEDIATE mute for reliable state management
                _audioFeedbackService.MuteSpeakers(); // Mute background audio RIGHT NOW
                
                // Play start sound (will play over the mute since it's our app)
                _ = _audioFeedbackService.PlayStartSoundAsync();
                
                System.Diagnostics.Debug.WriteLine($"🔇 IMMEDIATE: Background muted → Recording active (concurrent processing: {_activeProcessingCount})");
                
                _audioCaptureService.StartRecording();
                
                // Show floating waveform
                _floatingWindow?.StartRecording();
            }
            catch (Exception ex)
            {
                StatusText = $"Recording failed: {ex.Message}";
                IsRecording = false;
                _audioFeedbackService.UnmuteSpeakers(); // Ensure speakers are unmuted on error
            }
        }

        private void OnAudioDataReceived(object? sender, byte[] audioData)
        {
            // Convert byte array to float array for waveform visualization
            if (audioData.Length >= 2 && _floatingWindow != null)
            {
                var floatSamples = new float[audioData.Length / 2]; // 16-bit audio = 2 bytes per sample
                for (int i = 0; i < floatSamples.Length; i++)
                {
                    // Convert 16-bit PCM to float (-1.0 to 1.0)
                    short sample = BitConverter.ToInt16(audioData, i * 2);
                    floatSamples[i] = sample / 32768f;
                }
                
                // Update floating window with real audio levels
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    _floatingWindow.UpdateAudioLevels(floatSamples);
                });
            }
        }

        private async void OnHotkeyReleased(object? sender, EventArgs e)
        {
            if (!_isRecording) return;

            try
            {
                IsRecording = false;
                
                // Increment processing counter for concurrent tracking
                _activeProcessingCount++;
                UpdateProcessingStatus();

                // CRITICAL: Always unmute background audio first (prevents state glitches)
                try
                {
                    _audioFeedbackService.UnmuteSpeakers();
                    System.Diagnostics.Debug.WriteLine("🔊 UNMUTED: Background audio restored immediately");
                }
                catch (Exception unmuteEx)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Error unmuting speakers: {unmuteEx.Message}");
                    // Force unmute as fallback
                    try { _audioFeedbackService.UnmuteSpeakers(); } catch { }
                }
                
                await Task.Delay(100); // Brief pause to ensure unmute takes effect
                _ = _audioFeedbackService.PlayEndSoundAsync();

                var recordingDuration = DateTime.Now - _recordingStartTime;
                var audioData = _audioCaptureService.StopRecording();
                
                // Hide floating waveform
                _floatingWindow?.StopRecording();

                if (audioData?.Length > 0 && recordingDuration > TimeSpan.Zero)
                {
                    // Process asynchronously without blocking new recordings
                    _ = Task.Run(async () =>
                    {
                        var cancellationSource = new CancellationTokenSource();
                        try
                        {
                            await ProcessAudioAsync(audioData, recordingDuration, cancellationSource.Token);
                        }
                        finally
                        {
                            // Decrement processing counter when done
                            _activeProcessingCount--;
                            Application.Current.Dispatcher.Invoke(UpdateProcessingStatus);
                            cancellationSource.Dispose();
                        }
                    });
                }
                else
                {
                    _activeProcessingCount--;
                    UpdateProcessingStatus();
                }
            }
            catch (Exception ex)
            {
                _activeProcessingCount--;
                UpdateProcessingStatus();
                
                // CRITICAL: Always unmute on any error to prevent stuck mute state
                try
                {
                    _audioFeedbackService.UnmuteSpeakers();
                    System.Diagnostics.Debug.WriteLine("🔊 EMERGENCY UNMUTE: Speakers restored after error");
                }
                catch (Exception unmuteEx)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Failed to unmute after error: {unmuteEx.Message}");
                }
            }
        }

        private void UpdateProcessingStatus()
        {
            if (_activeProcessingCount > 0)
            {
                IsProcessing = true;
                StatusText = _activeProcessingCount == 1 
                    ? "⏹️ Processing audio..." 
                    : $"⏹️ Processing {_activeProcessingCount} recordings...";
            }
            else
            {
                IsProcessing = false;
                StatusText = "Ready - Press Ctrl+Win to start dictation";
            }
        }

        private async Task ProcessAudioAsync(byte[] audioData, TimeSpan recordingDuration, CancellationToken cancellationToken)
        {
            var session = new DictationSession
            {
                Timestamp = DateTime.Now,
                Duration = recordingDuration,
                Status = "Processing",
                AudioData = audioData // Store the audio data for download feature
            };

            try
            {
                var settings = _settingsService.LoadSettings();
                
                // Start transcription immediately
                StatusText = "🔄 Transcribing...";
                var transcriptionTask = _whisperService.TranscribeAudioWithRetryAsync(audioData, cancellationToken);
                
                // Wait for transcription
                var transcriptionResult = await transcriptionTask;
                
                if (transcriptionResult.IsSuccessful && !string.IsNullOrWhiteSpace(transcriptionResult.RawText))
                {
                    var rawText = transcriptionResult.RawText;
                    TranscriptionText = rawText;
                    
                    // Update session with transcription data
                    session.RawText = rawText;
                    session.Confidence = transcriptionResult.Confidence;
                    session.ModelUsed = settings.WhisperModel;
                    
                    // Process text polishing and clipboard operations in parallel for speed
                    Task<string> polishingTask;
                    Task<bool> clipboardTask;
                    
                    if (settings.SkipPolishing)
                    {
                        // Skip polishing for maximum speed
                        polishingTask = Task.FromResult(rawText);
                    }
                    else
                    {
                        StatusText = "✨ Polishing text...";
                        polishingTask = _geminiService.PolishTextWithRetryAsync(rawText, cancellationToken);
                    }
                    
                    // Start clipboard operation immediately with raw text
                    if (settings.AutoPasteResults)
                    {
                        clipboardTask = _clipboardService.CopyToClipboardAndPasteAsync(rawText);
                    }
                    else
                    {
                        clipboardTask = _clipboardService.CopyToClipboardAsync(rawText);
                    }
                    
                    // Wait for both operations
                    var polishedText = await polishingTask;
                    var clipboardSuccess = await clipboardTask;
                    
                    // Update with polished text if different and polishing was enabled
                    if (!settings.SkipPolishing && !string.IsNullOrWhiteSpace(polishedText) && polishedText != rawText)
                    {
                        TranscriptionText = polishedText;
                        session.PolishedText = polishedText;
                        
                        // Update clipboard with polished text if auto-paste is enabled
                        if (settings.AutoPasteResults && clipboardSuccess)
                        {
                            // Quick update to clipboard with polished text
                            await _clipboardService.CopyToClipboardAsync(polishedText);
                        }
                    }
                    else
                    {
                        session.PolishedText = rawText;
                    }
                    
                    // Mark session as completed
                    session.Status = "Completed";
                    
                    // Save session to history
                    try
                    {
                        await _historyService.SaveSessionAsync(session);
                        
                        // Refresh history and stats
                        await LoadRecentSessionsAsync();
                        var stats = await _historyService.GetStatsAsync();
                        TotalSessions = stats.TotalSessions;
                        TotalWords = stats.TotalWords;
                        AverageWPM = stats.AverageWPM;
                        _totalRecordingTime = stats.TotalRecordingTime;
                        OnPropertyChanged(nameof(TotalRecordingTime));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
                    }
                    
                    // Update stats
                    TotalSessions++;
                    _totalRecordingTime = _totalRecordingTime.Add(recordingDuration);
                    OnPropertyChanged(nameof(TotalRecordingTime));
                    
                    StatusText = "Ready - Press Ctrl+Win to start dictation";
                }
                else
                {
                    session.Status = "Failed";
                    session.ErrorMessage = transcriptionResult.ErrorMessage ?? "Unknown transcription error";
                    StatusText = $"❌ Transcription failed: {transcriptionResult.ErrorMessage}";
                    
                    // Save failed session
                    try
                    {
                        await _historyService.SaveSessionAsync(session);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to save failed session: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                session.Status = "Cancelled";
                session.ErrorMessage = "Processing was cancelled";
                StatusText = "❌ Processing cancelled";
                
                // Save cancelled session
                try
                {
                    await _historyService.SaveSessionAsync(session);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save cancelled session: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                session.Status = "Failed";
                session.ErrorMessage = ex.Message;
                StatusText = $"❌ Error: {ex.Message}";
                
                // Save error session
                try
                {
                    await _historyService.SaveSessionAsync(session);
                }
                catch (Exception saveEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save error session: {saveEx.Message}");
                }
            }
            finally
            {
                // Processing state is now managed by the counter system in OnHotkeyReleased
                // Individual processing tasks don't control the global IsProcessing state
            }
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                // Load stats
                var stats = await _historyService.GetStatsAsync();
                TotalSessions = stats.TotalSessions;
                TotalWords = stats.TotalWords;
                AverageWPM = stats.AverageWPM;
                _totalRecordingTime = stats.TotalRecordingTime;
                _averageAccuracy = stats.AverageConfidence * 100; // Convert to percentage
                
                OnPropertyChanged(nameof(TotalRecordingTime));
                OnPropertyChanged(nameof(AverageAccuracy));

                // Load recent sessions
                await LoadRecentSessionsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load initial data: {ex.Message}");
            }
        }

        private async Task LoadRecentSessionsAsync()
        {
            try
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                
                var todaySessions = await _historyService.GetSessionsByDateAsync(today);
                var yesterdaySessions = await _historyService.GetSessionsByDateAsync(yesterday);
                
                TodaySessions = new ObservableCollection<DictationSession>(todaySessions);
                YesterdaySessions = new ObservableCollection<DictationSession>(yesterdaySessions);
                HasYesterdaySessions = yesterdaySessions.Any();
                _loadedSessionsCount = todaySessions.Count + yesterdaySessions.Count;
                
                // Calculate global stats with better accuracy
                var allSessions = await _historyService.GetAllSessionsAsync();
                TotalWords = allSessions.Sum(s => s.WordCount);
                
                // Calculate average WPM more accurately
                var validSessions = allSessions.Where(s => s.WordsPerMinute > 0).ToList();
                if (validSessions.Any())
                {
                    AverageWPM = Math.Round(validSessions.Average(s => s.WordsPerMinute), 1);
                }
                else
                {
                    AverageWPM = 0;
                }
                
                SessionCount = allSessions.Count;
                TotalTime = TimeSpan.FromTicks(allSessions.Sum(s => s.Duration.Ticks));
                
                // Calculate analytics
                CalculateAnalytics(allSessions, todaySessions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading recent sessions: {ex.Message}");
            }
        }

        private void CalculateAnalytics(List<DictationSession> allSessions, List<DictationSession> todaySessions)
        {
            try
            {
                // Today's session count
                TodaySessionCount = todaySessions.Count;
                
                // Week's session count
                var weekAgo = DateTime.Today.AddDays(-7);
                WeekSessionCount = allSessions.Count(s => s.Timestamp >= weekAgo);
                
                // Calculate WPM progress width (percentage of target 180 WPM)
                var targetWPM = 180.0;
                WPMProgressWidth = Math.Min(100, (AverageWPM / targetWPM) * 100);
                
                // Calculate most used words
                var allWords = new Dictionary<string, int>();
                foreach (var session in allSessions.Take(50)) // Last 50 sessions
                {
                    if (!string.IsNullOrEmpty(session.RawText))
                    {
                        var words = session.RawText.ToLower()
                            .Split(new char[] { ' ', '.', ',', '!', '?', ';', ':', '\n', '\r' }, 
                                   StringSplitOptions.RemoveEmptyEntries)
                            .Where(w => w.Length > 3) // Only words longer than 3 characters
                            .Where(w => !IsCommonWord(w)); // Filter out common words
                        
                        foreach (var word in words)
                        {
                            allWords[word] = allWords.GetValueOrDefault(word, 0) + 1;
                        }
                    }
                }
                
                TopWords = new ObservableCollection<WordCount>(
                    allWords.OrderByDescending(kvp => kvp.Value)
                           .Take(8)
                           .Select(kvp => new WordCount { Word = kvp.Key, Count = kvp.Value })
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating analytics: {ex.Message}");
            }
        }
        
        private bool IsCommonWord(string word)
        {
            var commonWords = new HashSet<string>
            {
                "the", "and", "that", "have", "for", "not", "with", "you", "this", "but", "his", "from", "they",
                "she", "her", "been", "than", "its", "who", "oil", "sit", "now", "find", "long", "down", "day",
                "did", "get", "has", "him", "old", "see", "two", "way", "may", "say", "each", "which", "their",
                "time", "will", "about", "would", "there", "could", "other", "after", "first", "well", "water",
                "been", "call", "who", "oil", "sit", "now", "find", "long", "down", "day", "did", "get", "has"
            };
            return commonWords.Contains(word);
        }

        private void OpenSettings()
        {
            var settingsWindow = new Views.SettingsWindow();
            settingsWindow.ShowDialog();
        }

        private void ClearTranscription()
        {
            TranscriptionText = string.Empty;
            StatusText = "Ready - Press Ctrl+Win to start dictation";
        }

        private async Task LoadMoreSessionsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== LoadMoreSessionsAsync called ===");
                System.Diagnostics.Debug.WriteLine($"Current loaded count: {_loadedSessionsCount}");
                System.Diagnostics.Debug.WriteLine($"Today sessions count: {TodaySessions.Count}");
                System.Diagnostics.Debug.WriteLine($"Yesterday sessions count: {YesterdaySessions.Count}");
                
                // First, let's get total count from database
                var allSessions = await _historyService.GetAllSessionsAsync();
                System.Diagnostics.Debug.WriteLine($"Total sessions in database: {allSessions.Count}");
                
                var moreSessions = await _historyService.GetMoreSessionsAsync(_loadedSessionsCount, 20);
                System.Diagnostics.Debug.WriteLine($"Retrieved {moreSessions.Count} more sessions from database (skip: {_loadedSessionsCount}, take: 20)");
                
                if (moreSessions.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No more sessions to load - reached end of database");
                    StatusText = "No more sessions to load";
                    return;
                }
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var today = DateTime.Today;
                    var yesterday = today.AddDays(-1);
                    int addedCount = 0;
                    
                    System.Diagnostics.Debug.WriteLine($"Processing {moreSessions.Count} sessions...");
                    
                    foreach (var session in moreSessions)
                    {
                        var sessionDate = session.Timestamp.Date;
                        var preview = session.RawText?.Length > 50 ? session.RawText.Substring(0, 50) + "..." : session.RawText ?? "";
                        System.Diagnostics.Debug.WriteLine($"Session ID {session.Id} from {sessionDate:yyyy-MM-dd} at {session.Timestamp:HH:mm}: {preview}");
                        
                        if (sessionDate == today)
                        {
                            // Add to today's sessions if not already there
                            if (!TodaySessions.Any(s => s.Id == session.Id))
                            {
                                TodaySessions.Add(session);
                                addedCount++;
                                System.Diagnostics.Debug.WriteLine($"  -> Added to Today's collection");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  -> Already in Today's collection");
                            }
                        }
                        else if (sessionDate == yesterday)
                        {
                            // Add to yesterday's sessions if not already there
                            if (!YesterdaySessions.Any(s => s.Id == session.Id))
                            {
                                YesterdaySessions.Add(session);
                                addedCount++;
                                System.Diagnostics.Debug.WriteLine($"  -> Added to Yesterday's collection");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  -> Already in Yesterday's collection");
                            }
                        }
                        else
                        {
                            // Add older sessions to yesterday's collection for now
                            if (!YesterdaySessions.Any(s => s.Id == session.Id))
                            {
                                YesterdaySessions.Add(session);
                                addedCount++;
                                System.Diagnostics.Debug.WriteLine($"  -> Added older session to Yesterday's collection");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  -> Older session already in Yesterday's collection");
                            }
                        }
                    }
                    
                    HasYesterdaySessions = YesterdaySessions.Count > 0;
                    _loadedSessionsCount += moreSessions.Count;
                    
                    System.Diagnostics.Debug.WriteLine($"=== Load More Complete ===");
                    System.Diagnostics.Debug.WriteLine($"Added {addedCount} new sessions to UI");
                    System.Diagnostics.Debug.WriteLine($"Total loaded count now: {_loadedSessionsCount}");
                    System.Diagnostics.Debug.WriteLine($"Today sessions: {TodaySessions.Count}, Yesterday sessions: {YesterdaySessions.Count}");
                    
                    StatusText = $"Loaded {addedCount} more sessions";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load more sessions: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                StatusText = $"Error loading sessions: {ex.Message}";
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Dispose()
        {
            try
            {
                _processingCancellation?.Cancel();
                _processingCancellation?.Dispose();
                _hotkeyService?.UnregisterHotkey();
                _audioCaptureService?.Dispose();
                _whisperService?.Dispose();
                _geminiService?.Dispose();
                _historyService?.Dispose();
                _audioFeedbackService?.Dispose();
                
                // Properly close and dispose floating window
                if (_floatingWindow != null)
                {
                    _floatingWindow.Close();
                    _floatingWindow = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during disposal: {ex.Message}");
            }
        }
    }
} 









