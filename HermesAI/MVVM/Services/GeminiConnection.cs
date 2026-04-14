using HermesAI.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HermesAI.MVVM.Services
{
    public class GeminiConnection : IAIConnection
    {
        private string? _apiKey;
        private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent";

        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> GetResponseAsync(IEnumerable<ChatMessage> chatHistory)
        {
            if(_apiKey == null) _apiKey = SecretManager.LoadApiKey();

            if (string.IsNullOrEmpty(_apiKey)) return "Fehler: Es wurde noch kein API-Key hinterlegt. Bitte in den Einstellungen eintragen.";

            // Gemini erwartet "user" für deine Nachrichten und "model" für die KI-Antworten
            var formattedContents = chatHistory.Select(msg => new
            {
                role = msg.IsMyMessage ? "user" : "model",
                parts = new[] { new { text = msg.Text } }
            }).ToArray();

            var requestBody = new { contents = formattedContents };
            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var requestUrl = $"{Endpoint}?key={_apiKey}";
            var response = await _httpClient.PostAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"[API Fehler]: {response.StatusCode} - {error}";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);

            try
            {
                var textResponse = document.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                return textResponse ?? "Fehler: Leere Antwort erhalten.";
            }
            catch (Exception ex)
            {
                return $"[Parsing Fehler]: Konnte die Antwort nicht lesen. Details: {ex.Message}";
            }
        }
    }
}