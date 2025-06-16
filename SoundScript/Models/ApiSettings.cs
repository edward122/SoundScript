using System;

namespace SoundScript.Models
{
    public class ApiSettings
    {
        public string OpenAiApiKey { get; set; } = string.Empty;
        public string GeminiApiKey { get; set; } = string.Empty;
        public string WhisperModel { get; set; } = "whisper-1";
        public string GeminiModel { get; set; } = "gemini-2.0-flash-exp";
        public bool UseStreamingTranscription { get; set; } = true;
        public int MaxRetries { get; set; } = 2;
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
        
        // Audio settings
        public int ChunkSizeSeconds { get; set; } = 3;
        public double SilenceThreshold { get; set; } = 0.01;
        
        // Performance settings
        public bool EnableRealTimeProcessing { get; set; } = true;
        public bool AutoPasteResults { get; set; } = true;
        public bool SkipPolishing { get; set; } = false;
        public bool UseParallelProcessing { get; set; } = true;
        
        public bool IsConfigured => !string.IsNullOrWhiteSpace(OpenAiApiKey) && 
                                   !string.IsNullOrWhiteSpace(GeminiApiKey);
    }
} 