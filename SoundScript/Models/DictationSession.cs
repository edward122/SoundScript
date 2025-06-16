using System;

namespace SoundScript.Models
{
    public class DictationSession
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string RawText { get; set; } = string.Empty;
        public string PolishedText { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public int WordCount { get; set; }
        public double WordsPerMinute { get; set; }
        public double Confidence { get; set; }
        public string Status { get; set; } = "Completed"; // Completed, Failed, Cancelled
        public string ErrorMessage { get; set; } = string.Empty;
        
        // Additional stats
        public int CharacterCount { get; set; }
        public int SentenceCount { get; set; }
        public string Language { get; set; } = "en";
        public string ModelUsed { get; set; } = "whisper-1";
        
        // Audio data storage
        public byte[]? AudioData { get; set; }
        public bool HasAudioData => AudioData != null && AudioData.Length > 0;
        
        // Calculated properties
        public string FormattedDuration => Duration.TotalSeconds < 60 
            ? $"{Duration.TotalSeconds:F1}s" 
            : $"{Duration.Minutes}m {Duration.Seconds}s";
            
        public string FormattedTimestamp => Timestamp.ToString("MMM dd, yyyy 'at' h:mm tt");
        public string TimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - Timestamp;
                if (timeSpan.TotalMinutes < 1) return "Just now";
                if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}m ago";
                if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
                if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}d ago";
                return Timestamp.ToString("MMM dd");
            }
        }
        
        public string PreviewText => RawText;
        
        public void CalculateStats()
        {
            if (string.IsNullOrWhiteSpace(RawText))
            {
                WordCount = 0;
                CharacterCount = 0;
                SentenceCount = 0;
                WordsPerMinute = 0;
                return;
            }
            
            // Calculate word count
            WordCount = RawText.Split(new char[] { ' ', '\t', '\n', '\r' }, 
                StringSplitOptions.RemoveEmptyEntries).Length;
            
            // Calculate character count (excluding spaces)
            CharacterCount = RawText.Replace(" ", "").Length;
            
            // Calculate sentence count (rough estimate)
            SentenceCount = RawText.Split(new char[] { '.', '!', '?' }, 
                StringSplitOptions.RemoveEmptyEntries).Length;
            
            // Calculate WPM - improved calculation
            if (Duration.TotalMinutes > 0 && WordCount > 0)
            {
                WordsPerMinute = Math.Round(WordCount / Duration.TotalMinutes, 1);
                
                // Only cap at extremely unrealistic values (like 10,000+ WPM)
                if (WordsPerMinute > 10000)
                {
                    WordsPerMinute = 10000; // Cap at 10,000 WPM to prevent display issues
                }
            }
            else
            {
                WordsPerMinute = 0;
            }
        }
    }
} 