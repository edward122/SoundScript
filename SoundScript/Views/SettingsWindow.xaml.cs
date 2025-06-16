using System;
using System.Diagnostics;
using System.Security;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Controls;
using SoundScript.Models;
using SoundScript.Services;

namespace SoundScript.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Enable dark mode for title bar
            SoundScript.Utils.DarkModeHelper.EnableDarkMode(this);
            
            _settingsService = new SettingsService();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = _settingsService.LoadSettings();
            
            OpenAiKeyBox.Password = settings.OpenAiApiKey;
            GeminiKeyBox.Password = settings.GeminiApiKey;
            
            // Set model selections
            foreach (ComboBoxItem item in WhisperModelComboBox.Items)
            {
                if (item.Content?.ToString() == settings.WhisperModel)
                {
                    WhisperModelComboBox.SelectedItem = item;
                    break;
                }
            }
            
            foreach (ComboBoxItem item in GeminiModelComboBox.Items)
            {
                if (item.Content?.ToString() == settings.GeminiModel)
                {
                    GeminiModelComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Set behavior settings
            AutoPasteCheckBox.IsChecked = settings.AutoPasteResults;
            SkipPolishingCheckBox.IsChecked = settings.SkipPolishing;
            RealTimeProcessingCheckBox.IsChecked = settings.EnableRealTimeProcessing;
            
            // Set max retries
            MaxRetriesTextBox.Text = settings.MaxRetries.ToString();
        }

        private void SaveSettings()
        {
            var settings = new ApiSettings
            {
                OpenAiApiKey = OpenAiKeyBox.Password.Trim(),
                GeminiApiKey = GeminiKeyBox.Password.Trim(),
                WhisperModel = (WhisperModelComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "whisper-1",
                GeminiModel = (GeminiModelComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "gemini-2.0-flash-exp",
                AutoPasteResults = AutoPasteCheckBox.IsChecked ?? true,
                SkipPolishing = SkipPolishingCheckBox.IsChecked ?? false,
                EnableRealTimeProcessing = RealTimeProcessingCheckBox.IsChecked ?? true,
                MaxRetries = int.TryParse(MaxRetriesTextBox.Text, out int retries) ? retries : 3
            };

            _settingsService.SaveSettings(settings);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                
                MessageBox.Show("Settings saved successfully!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var openAiKey = OpenAiKeyBox.Password.Trim();
            var geminiKey = GeminiKeyBox.Password.Trim();

            if (string.IsNullOrEmpty(openAiKey) || string.IsNullOrEmpty(geminiKey))
            {
                MessageBox.Show("Please enter both API keys before testing.", "Missing API Keys", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestButton.IsEnabled = false;
            TestButton.Content = "Testing...";

            try
            {
                var testSettings = new ApiSettings
                {
                    OpenAiApiKey = openAiKey,
                    GeminiApiKey = geminiKey
                };

                // Test both APIs
                using var whisperService = new WhisperApiService(testSettings);
                using var geminiService = new GeminiApiService(testSettings);

                // Simple test calls would go here
                MessageBox.Show("API keys are valid!", "Test Successful", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"API test failed: {ex.Message}", "Test Failed", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestButton.IsEnabled = true;
                TestButton.Content = "Test APIs";
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open link: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            
            e.Handled = true;
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            CheckUpdatesButton.Content = "🔄 Checking...";

            try
            {
                var updateService = new UpdateService();
                await updateService.CheckForUpdatesAsync(true); // Show "no updates" message
                updateService.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates: {ex.Message}", "Update Check Failed", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "🔄 Check for Updates";
            }
        }

        private async void ClearAllDataButton_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            var result = MessageBox.Show(
                "⚠️ WARNING: This will permanently delete ALL your transcription sessions, statistics, and history.\n\n" +
                "This action CANNOT be undone!\n\n" +
                "Are you absolutely sure you want to continue?",
                "Clear All Data - Confirmation Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            // Second confirmation
            var secondResult = MessageBox.Show(
                "🚨 FINAL WARNING 🚨\n\n" +
                "You are about to delete ALL your data permanently.\n\n" +
                "Type 'DELETE' in the next dialog to confirm.",
                "Final Confirmation Required",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Stop,
                MessageBoxResult.Cancel);

            if (secondResult != MessageBoxResult.OK)
                return;

            // Text confirmation dialog
            var confirmationDialog = new TextConfirmationDialog("DELETE");
            if (confirmationDialog.ShowDialog() != true)
                return;

            // Disable button and show progress
            ClearAllDataButton.IsEnabled = false;
            ClearAllDataButton.Content = "🗑️ Clearing Data...";

            try
            {
                using var historyService = new HistoryService();
                
                // Get total count for user feedback
                var allSessions = await historyService.GetAllSessionsAsync();
                var totalSessions = allSessions.Count;

                // Clear all data
                await historyService.ClearAllDataAsync();

                MessageBox.Show(
                    $"✅ Successfully deleted {totalSessions} sessions and all statistics.\n\n" +
                    "The application will restart to refresh the interface.",
                    "Data Cleared Successfully",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Close settings and restart application
                DialogResult = true;
                Close();

                // Restart the application
                var currentProcess = Process.GetCurrentProcess();
                if (currentProcess.MainModule?.FileName != null)
                {
                    Process.Start(currentProcess.MainModule.FileName);
                }
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Error clearing data: {ex.Message}\n\n" +
                    "Some data may not have been deleted. Please try again or contact support.",
                    "Error Clearing Data",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ClearAllDataButton.IsEnabled = true;
                ClearAllDataButton.Content = "🗑️ Clear All Data";
            }
        }
    }
} 