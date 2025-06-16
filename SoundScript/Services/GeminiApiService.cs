using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using SoundScript.Models;

namespace SoundScript.Services
{
    public class GeminiApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _settings;
        private const string GEMINI_API_BASE = "https://generativelanguage.googleapis.com/v1beta/models";

        public GeminiApiService(ApiSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpClient();
            _httpClient.Timeout = settings.RequestTimeout;
        }

        public async Task<string> PolishTextAsync(string rawText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return rawText;

            try
            {
                var prompt = CreatePolishingPrompt(rawText);
                var response = await CallGeminiApiAsync(prompt, cancellationToken);
                
                return ExtractPolishedText(response) ?? rawText;
            }
            catch (Exception ex)
            {
                // Log error but return original text as fallback
                System.Diagnostics.Debug.WriteLine($"Text polishing failed: {ex.Message}");
                return rawText;
            }
        }

        private string CreatePolishingPrompt(string rawText)
        {
            return $@"Add ONLY punctuation and capitalization to this text. KEEP EVERY SINGLE WORD including um, uh, ah, hm, like, you know, etc.

CRITICAL RULES:
- Keep ALL filler words (um, uh, ah, hm, like, you know, etc.)
- Keep ALL hesitations and natural speech patterns
- Add periods, commas, question marks where needed
- Capitalize first letters of sentences and proper nouns
- Do NOT change any words
- Do NOT add any words  
- Do NOT remove any words
- Do NOT fix grammar by changing words

Text: {rawText}

Return the exact same words with only punctuation and capitalization added:";
        }

        private async Task<string> CallGeminiApiAsync(string prompt, CancellationToken cancellationToken)
        {
            var url = $"{GEMINI_API_BASE}/{_settings.GeminiModel}:generateContent?key={_settings.GeminiApiKey}";
            
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1, // Low temperature for minimal changes
                    topK = 1,
                    topP = 0.8,
                    maxOutputTokens = 512, // Reduced for speed
                    stopSequences = new string[0]
                },
                safetySettings = new[]
                {
                    new
                    {
                        category = "HARM_CATEGORY_HARASSMENT",
                        threshold = "BLOCK_ONLY_HIGH"
                    },
                    new
                    {
                        category = "HARM_CATEGORY_HATE_SPEECH", 
                        threshold = "BLOCK_ONLY_HIGH"
                    },
                    new
                    {
                        category = "HARM_CATEGORY_SEXUALLY_EXPLICIT",
                        threshold = "BLOCK_ONLY_HIGH"
                    },
                    new
                    {
                        category = "HARM_CATEGORY_DANGEROUS_CONTENT",
                        threshold = "BLOCK_ONLY_HIGH"
                    }
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Gemini API error: {response.StatusCode} - {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        private string? ExtractPolishedText(string jsonResponse)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<GeminiResponse>(jsonResponse);
                
                return response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse Gemini response: {ex.Message}");
                return null;
            }
        }

        public async Task<string> PolishTextWithRetryAsync(string rawText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return rawText;

            for (int attempt = 1; attempt <= _settings.MaxRetries; attempt++)
            {
                try
                {
                    var result = await PolishTextAsync(rawText, cancellationToken);
                    
                    // If we got a meaningful result, return it
                    if (!string.IsNullOrWhiteSpace(result))
                        return result;
                        
                    // If this was the last attempt, return original text
                    if (attempt == _settings.MaxRetries)
                        return rawText;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Text polishing attempt {attempt} failed: {ex.Message}");
                    
                    // If this is the last attempt, return original text
                    if (attempt == _settings.MaxRetries)
                        return rawText;
                        
                    // Wait before retry (faster retry)
                    var delay = TimeSpan.FromMilliseconds(300 * attempt);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            return rawText; // Fallback to original text
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 