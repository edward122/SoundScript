using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using SoundScript.Models;

namespace SoundScript.Services
{
    public class WhisperApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _settings;
        private const string WHISPER_API_URL = "https://api.openai.com/v1/audio/transcriptions";

        public WhisperApiService(ApiSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.OpenAiApiKey}");
            _httpClient.Timeout = settings.RequestTimeout;
            
            // Optimize for speed
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SoundScript/1.0");
        }

        public async Task<TranscriptionResult> TranscribeAudioAsync(byte[] audioData, CancellationToken cancellationToken = default)
        {
            var result = new TranscriptionResult
            {
                Status = TranscriptionStatus.Transcribing,
                Timestamp = DateTime.Now
            };

            try
            {
                // Convert audio to WAV format
                var wavData = ConvertToWav(audioData);
                
                using var form = new MultipartFormDataContent();
                using var audioContent = new ByteArrayContent(wavData);
                
                audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                form.Add(audioContent, "file", "audio.wav");
                form.Add(new StringContent(_settings.WhisperModel), "model");
                form.Add(new StringContent("json"), "response_format"); // Use simple JSON for speed
                form.Add(new StringContent("en"), "language");
                
                // Add temperature for more consistent results
                form.Add(new StringContent("0.0"), "temperature"); // Most deterministic
                
                // Add prompt for capturing ALL speech including filler words
                form.Add(new StringContent("Transcribe everything exactly as spoken, including all filler words like um, uh, ah, hm, you know, like, etc. Include all hesitations, pauses, and natural speech patterns."), "prompt");

                var response = await _httpClient.PostAsync(WHISPER_API_URL, form, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var whisperResponse = JsonConvert.DeserializeObject<WhisperResponse>(jsonResponse);
                    
                    if (whisperResponse != null && !string.IsNullOrWhiteSpace(whisperResponse.Text))
                    {
                        result.RawText = whisperResponse.Text.Trim();
                        result.Duration = TimeSpan.FromSeconds(whisperResponse.Duration);
                        result.Confidence = 0.9; // Default high confidence for simple JSON response
                        result.Status = TranscriptionStatus.Completed;
                    }
                    else
                    {
                        result.Status = TranscriptionStatus.Failed;
                        result.ErrorMessage = "No transcription text received from Whisper API";
                    }
                }
                else
                {
                    result.Status = TranscriptionStatus.Failed;
                    var errorContent = await response.Content.ReadAsStringAsync();
                    result.ErrorMessage = $"Whisper API error: {response.StatusCode} - {errorContent}";
                }
            }
            catch (TaskCanceledException)
            {
                result.Status = TranscriptionStatus.Cancelled;
                result.ErrorMessage = "Transcription request was cancelled";
            }
            catch (Exception ex)
            {
                result.Status = TranscriptionStatus.Failed;
                result.ErrorMessage = $"Transcription error: {ex.Message}";
            }

            return result;
        }

        private byte[] ConvertToWav(byte[] audioData)
        {
            // Create a simple WAV header for 16kHz, 16-bit, mono PCM
            var sampleRate = 16000;
            var bitsPerSample = 16;
            var channels = 1;
            var byteRate = sampleRate * channels * bitsPerSample / 8;
            var blockAlign = channels * bitsPerSample / 8;
            
            var header = new byte[44];
            var dataSize = audioData.Length;
            var fileSize = 36 + dataSize;
            
            // RIFF header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
            BitConverter.GetBytes(fileSize).CopyTo(header, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(header, 8);
            
            // fmt chunk
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(header, 12);
            BitConverter.GetBytes(16).CopyTo(header, 16); // chunk size
            BitConverter.GetBytes((short)1).CopyTo(header, 20); // PCM format
            BitConverter.GetBytes((short)channels).CopyTo(header, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(header, 24);
            BitConverter.GetBytes(byteRate).CopyTo(header, 28);
            BitConverter.GetBytes((short)blockAlign).CopyTo(header, 32);
            BitConverter.GetBytes((short)bitsPerSample).CopyTo(header, 34);
            
            // data chunk
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(header, 36);
            BitConverter.GetBytes(dataSize).CopyTo(header, 40);
            
            // Combine header and data
            var wavData = new byte[header.Length + audioData.Length];
            header.CopyTo(wavData, 0);
            audioData.CopyTo(wavData, header.Length);
            
            return wavData;
        }

        private double CalculateConfidence(WhisperSegment[]? segments)
        {
            if (segments == null || segments.Length == 0)
                return 0.9; // Default high confidence

            double totalConfidence = 0;
            foreach (var segment in segments)
            {
                totalConfidence += segment.Confidence;
            }

            return totalConfidence / segments.Length;
        }

        public async Task<TranscriptionResult> TranscribeAudioWithRetryAsync(byte[] audioData, CancellationToken cancellationToken = default)
        {
            TranscriptionResult? lastResult = null;
            
            for (int attempt = 1; attempt <= _settings.MaxRetries; attempt++)
            {
                try
                {
                    lastResult = await TranscribeAudioAsync(audioData, cancellationToken);
                    
                    if (lastResult.IsSuccessful)
                        return lastResult;
                    
                    // Wait before retry (shorter delays for speed)
                    if (attempt < _settings.MaxRetries)
                    {
                        var delay = TimeSpan.FromMilliseconds(500 * attempt); // Faster retry
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    lastResult = new TranscriptionResult
                    {
                        Status = TranscriptionStatus.Failed,
                        ErrorMessage = $"Attempt {attempt} failed: {ex.Message}"
                    };
                }
            }

            return lastResult ?? new TranscriptionResult
            {
                Status = TranscriptionStatus.Failed,
                ErrorMessage = "All transcription attempts failed"
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 