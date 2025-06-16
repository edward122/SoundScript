using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SoundScript.Services
{
    public class UpdateService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string GITHUB_API_URL = "https://api.github.com/repos/edward122/SoundScript/releases/latest";
        private const string UPDATE_CHECK_INTERVAL_HOURS = "24"; // Check once per day
        
        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SoundScript-UpdateChecker");
        }

        public async Task<bool> CheckForUpdatesAsync(bool showNoUpdateMessage = false)
        {
            try
            {
                // Check if we should check for updates (rate limiting)
                if (!ShouldCheckForUpdates() && !showNoUpdateMessage)
                    return false;

                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);
                
                if (release?.tag_name == null)
                    return false;

                var currentVersion = GetCurrentVersion();
                var latestVersion = release.tag_name.TrimStart('v');
                
                if (IsNewerVersion(latestVersion, currentVersion))
                {
                    var result = MessageBox.Show(
                        $"🎉 New version available!\n\n" +
                        $"Current: v{currentVersion}\n" +
                        $"Latest: v{latestVersion}\n\n" +
                        $"Changes:\n{release.body}\n\n" +
                        $"Would you like to download the update?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Open download page
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = release.html_url,
                            UseShellExecute = true
                        });
                        return true;
                    }
                }
                else if (showNoUpdateMessage)
                {
                    MessageBox.Show(
                        $"✅ You're up to date!\n\nCurrent version: v{currentVersion}",
                        "No Updates Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                // Update last check time
                UpdateLastCheckTime();
                return false;
            }
            catch (Exception ex)
            {
                if (showNoUpdateMessage)
                {
                    MessageBox.Show(
                        $"❌ Could not check for updates:\n{ex.Message}",
                        "Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return false;
            }
        }

        private string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version?.Major}.{version?.Minor}.{version?.Build}";
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var latestParts = latest.Split('.');
                var currentParts = current.Split('.');
                
                for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
                {
                    var latestPart = i < latestParts.Length ? int.Parse(latestParts[i]) : 0;
                    var currentPart = i < currentParts.Length ? int.Parse(currentParts[i]) : 0;
                    
                    if (latestPart > currentPart) return true;
                    if (latestPart < currentPart) return false;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldCheckForUpdates()
        {
            try
            {
                var lastCheckFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SoundScript",
                    "last_update_check.txt");

                if (!File.Exists(lastCheckFile))
                    return true;

                var lastCheck = DateTime.Parse(File.ReadAllText(lastCheckFile));
                return DateTime.Now.Subtract(lastCheck).TotalHours >= double.Parse(UPDATE_CHECK_INTERVAL_HOURS);
            }
            catch
            {
                return true;
            }
        }

        private void UpdateLastCheckTime()
        {
            try
            {
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SoundScript");
                
                Directory.CreateDirectory(appDataDir);
                
                var lastCheckFile = Path.Combine(appDataDir, "last_update_check.txt");
                File.WriteAllText(lastCheckFile, DateTime.Now.ToString("O"));
            }
            catch
            {
                // Ignore errors
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class GitHubRelease
    {
        public string? tag_name { get; set; }
        public string? name { get; set; }
        public string? body { get; set; }
        public string? html_url { get; set; }
        public bool prerelease { get; set; }
        public DateTime published_at { get; set; }
    }
} 