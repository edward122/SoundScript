using System;

namespace SoundScript.Models
{
    public class TranscriptionResult
    {
        public string RawText { get; set; } = string.Empty;
        public string PolishedText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsPartial { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public TranscriptionStatus Status { get; set; } = TranscriptionStatus.Processing;
        public string? ErrorMessage { get; set; }
        
        public bool IsSuccessful => Status == TranscriptionStatus.Completed && 
                                   !string.IsNullOrWhiteSpace(RawText);

        public void CalculateStats()
        {
            // Calculate WPM if we have duration
            if (Duration.TotalSeconds > 0 && !string.IsNullOrWhiteSpace(RawText))
            {
                var wordCount = RawText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                var wordsPerMinute = (int)Math.Round(wordCount / Duration.TotalMinutes);
                
                // Store stats for later use (we'll implement stats tracking in Week 3)
                System.Diagnostics.Debug.WriteLine($"Transcription stats: {wordCount} words, {wordsPerMinute} WPM");
            }
        }
    }

    public enum TranscriptionStatus
    {
        Processing,
        Transcribing,
        Polishing,
        Completed,
        Failed,
        Cancelled
    }

    public class WhisperResponse
    {
        public string Text { get; set; } = string.Empty;
        public WhisperSegment[]? Segments { get; set; }
        public string Language { get; set; } = "en";
        public double Duration { get; set; }
    }

    public class WhisperSegment
    {
        public int Id { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    public class GeminiResponse
    {
        public GeminiCandidate[]? Candidates { get; set; }
    }

    public class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }

    public class GeminiContent
    {
        public GeminiPart[]? Parts { get; set; }
    }

    public class GeminiPart
    {
        public string Text { get; set; } = string.Empty;
    }
} 