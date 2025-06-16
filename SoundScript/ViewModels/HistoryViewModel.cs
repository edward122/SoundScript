using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SoundScript.Models;
using SoundScript.Services;
using SoundScript.Utils;

namespace SoundScript.ViewModels
{
    public class HistoryViewModel : INotifyPropertyChanged
    {
        private readonly HistoryService _historyService;
        private string _searchText = string.Empty;
        private HistoryStats _stats = new();
        private bool _isLoading = false;

        public ObservableCollection<DictationSession> Sessions { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public HistoryStats Stats
        {
            get => _stats;
            set => SetProperty(ref _stats, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand SearchCommand { get; }
        public ICommand ShowStatsCommand { get; }
        public ICommand ViewSessionCommand { get; }
        public ICommand DeleteSessionCommand { get; }
        public ICommand RefreshCommand { get; }

        public HistoryViewModel()
        {
            _historyService = new HistoryService();

            SearchCommand = new RelayCommand(async () => await SearchSessionsAsync());
            ShowStatsCommand = new RelayCommand(async () => await RefreshStatsAsync());
            ViewSessionCommand = new RelayCommand<DictationSession>(ViewSession);
            DeleteSessionCommand = new RelayCommand<DictationSession>(async session => await DeleteSessionAsync(session));
            RefreshCommand = new RelayCommand(async () => await LoadSessionsAsync());

            // Load initial data
            _ = Task.Run(async () => await LoadInitialDataAsync());
        }

        private async Task LoadInitialDataAsync()
        {
            await LoadSessionsAsync();
            await RefreshStatsAsync();
        }

        private async Task LoadSessionsAsync()
        {
            try
            {
                IsLoading = true;
                var sessions = await _historyService.GetRecentSessionsAsync(100);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Sessions.Clear();
                    foreach (var session in sessions)
                    {
                        Sessions.Add(session);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sessions: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SearchSessionsAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadSessionsAsync();
                return;
            }

            try
            {
                IsLoading = true;
                var sessions = await _historyService.SearchSessionsAsync(SearchText);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Sessions.Clear();
                    foreach (var session in sessions)
                    {
                        Sessions.Add(session);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching sessions: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshStatsAsync()
        {
            try
            {
                var stats = await _historyService.GetStatsAsync();
                Stats = stats;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading stats: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewSession(DictationSession? session)
        {
            if (session == null) return;

            var detailWindow = new SessionDetailWindow(session);
            detailWindow.ShowDialog();
        }

        private async Task DeleteSessionAsync(DictationSession? session)
        {
            if (session == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete this session?\n\n\"{session.PreviewText}\"",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _historyService.DeleteSessionAsync(session.Id);
                    Sessions.Remove(session);
                    await RefreshStatsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting session: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
    }

    // Simple session detail window for viewing full transcription
    public class SessionDetailWindow : Window
    {
        public SessionDetailWindow(DictationSession session)
        {
            Title = $"Session Details - {session.FormattedTimestamp}";
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 15, 15));

            var grid = new System.Windows.Controls.Grid();
            grid.Margin = new Thickness(20);

            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled
            };

            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = session.RawText,
                FontSize = 16,
                Foreground = System.Windows.Media.Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            scrollViewer.Content = textBlock;
            grid.Children.Add(scrollViewer);
            Content = grid;
        }
    }
} 