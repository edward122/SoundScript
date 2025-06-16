using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SoundScript.Models;

namespace SoundScript.Services
{
    public class SettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "SoundScript");
        
        private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "settings.json");
        private static readonly string EncryptedSettingsFile = Path.Combine(SettingsDirectory, "settings.enc");

        private ApiSettings? _cachedSettings;

        public ApiSettings LoadSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            try
            {
                EnsureSettingsDirectoryExists();

                // Try to load encrypted settings first
                if (File.Exists(EncryptedSettingsFile))
                {
                    _cachedSettings = LoadEncryptedSettings();
                }
                // Fall back to plain JSON (for compatibility)
                else if (File.Exists(SettingsFile))
                {
                    _cachedSettings = LoadPlainSettings();
                    
                    // Migrate to encrypted storage
                    if (_cachedSettings != null)
                    {
                        SaveSettings(_cachedSettings);
                        File.Delete(SettingsFile); // Remove plain text file
                    }
                }

                return _cachedSettings ?? CreateDefaultSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                return CreateDefaultSettings();
            }
        }

        public void SaveSettings(ApiSettings settings)
        {
            try
            {
                EnsureSettingsDirectoryExists();
                
                _cachedSettings = settings;
                
                // Save encrypted settings
                SaveEncryptedSettings(settings);
                
                // Remove plain text file if it exists
                if (File.Exists(SettingsFile))
                {
                    File.Delete(SettingsFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                throw;
            }
        }

        private ApiSettings? LoadEncryptedSettings()
        {
            try
            {
                var encryptedData = File.ReadAllBytes(EncryptedSettingsFile);
                var decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decryptedData);
                
                return JsonConvert.DeserializeObject<ApiSettings>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load encrypted settings: {ex.Message}");
                return null;
            }
        }

        private ApiSettings? LoadPlainSettings()
        {
            try
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonConvert.DeserializeObject<ApiSettings>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load plain settings: {ex.Message}");
                return null;
            }
        }

        private void SaveEncryptedSettings(ApiSettings settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            var data = Encoding.UTF8.GetBytes(json);
            var encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            
            File.WriteAllBytes(EncryptedSettingsFile, encryptedData);
        }

        private void EnsureSettingsDirectoryExists()
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }
        }

        private ApiSettings CreateDefaultSettings()
        {
            return new ApiSettings
            {
                WhisperModel = "whisper-1",
                GeminiModel = "gemini-2.0-flash-exp",
                UseStreamingTranscription = true,
                MaxRetries = 3,
                RequestTimeout = TimeSpan.FromSeconds(30),
                ChunkSizeSeconds = 5,
                SilenceThreshold = 0.01,
                EnableRealTimeProcessing = true,
                AutoPasteResults = true
            };
        }

        public void UpdateApiKey(string provider, string apiKey)
        {
            var settings = LoadSettings();
            
            switch (provider.ToLowerInvariant())
            {
                case "openai":
                case "whisper":
                    settings.OpenAiApiKey = apiKey ?? string.Empty;
                    break;
                    
                case "gemini":
                case "google":
                    settings.GeminiApiKey = apiKey ?? string.Empty;
                    break;
                    
                default:
                    throw new ArgumentException($"Unknown API provider: {provider}");
            }
            
            SaveSettings(settings);
        }

        public bool HasValidApiKeys()
        {
            var settings = LoadSettings();
            return settings.IsConfigured;
        }

        public void ResetSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                    File.Delete(SettingsFile);
                    
                if (File.Exists(EncryptedSettingsFile))
                    File.Delete(EncryptedSettingsFile);
                    
                _cachedSettings = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reset settings: {ex.Message}");
            }
        }

        public string GetSettingsDirectory()
        {
            return SettingsDirectory;
        }

        public bool IsFirstRun()
        {
            return !File.Exists(SettingsFile) && !File.Exists(EncryptedSettingsFile);
        }
    }
} 