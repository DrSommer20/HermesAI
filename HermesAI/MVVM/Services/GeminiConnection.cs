using HermesAI.MVVM.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace HermesAI.MVVM.Services
{
    /// <summary>
    /// Handles the connection to the Gemini API.
    /// 
    /// ARCHITECTURE DECISION: We're hitting the Gemini REST API directly (no SDK),
    /// because the official Google SDK doesn't expose an IChatClient that plays nice with MCP.
    /// Doing it this way gives us full control over the function calling loop.
    /// 
    /// DEVELOPMENT STEPS:
    /// 1. Basic streaming (streamGenerateContent + SSE) — already done
    /// 2. Function Calling (generateContent without streaming) — NEW for MCP integration
    /// 3. Tool Loop: Gemini → functionCall → execute tool → functionResponse → Gemini
    /// </summary>
    public class GeminiConnection : IAIConnection
    {
        private const string StreamEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:streamGenerateContent";
        private const string GenerateEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";
        private static readonly HttpClient _httpClient = new HttpClient();

        // Standard streaming without tools
        /// <summary>
        /// Streams a response from Gemini using Server-Sent Events (SSE) without any tool calling.
        /// </summary>
        public async IAsyncEnumerable<string> GetResponseStreamAsync(IEnumerable<ChatMessage> chatHistory, [EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
        {
            string apiKey = SecretManager.LoadApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                yield return "Fehler: Kein API-Key gefunden. Bitte in den Einstellungen eintragen.";
                yield break;
            }

            var formattedContents = chatHistory
                .Where(m => !m.IsToolMessage) // Don't send tool messages into the regular chat stream
                .Select(msg => new
                {
                    role = msg.IsMyMessage ? "user" : "model",
                    parts = new[] { new { text = msg.Text } }
                }).ToArray();

            var requestBody = new { contents = formattedContents };
            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var requestUrl = $"{StreamEndpoint}?alt=sse&key={apiKey}";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = content };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                yield return $"[API Fehler]: {response.StatusCode}\n{error}";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    continue;

                string jsonData = line.Substring(6);
                string chunkToReturn = null;

                try
                {
                    using var document = JsonDocument.Parse(jsonData);
                    var candidates = document.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        var parts = candidates[0].GetProperty("content").GetProperty("parts");
                        if (parts.GetArrayLength() > 0)
                        {
                            chunkToReturn = parts[0].GetProperty("text").GetString();
                        }
                    }
                }
                catch
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(chunkToReturn))
                {
                    yield return chunkToReturn;
                }
            }
        }

        //Function Calling (non-streaming, used for the tool loop)

        /// <summary>
        /// Sends a request to Gemini INCLUDING tool declarations.
        /// 
        /// ARCHITECTURE DECISION: We have to use the NON-streaming endpoint here 
        /// (generateContent) because we need to parse the entire response to figure 
        /// out if a functionCall came back.
        /// 
        /// PROBLEM: Gemini can return BOTH text AND functionCalls in a single response.
        /// We need to parse both and handle them properly.
        /// 
        /// Returns: GeminiResponse containing either text or FunctionCall data.
        /// </summary>
        public async Task<GeminiResponse> SendWithToolsAsync(
            List<object> conversationParts,
            List<object> toolDeclarations,
            CancellationToken cancellationToken = default)
        {
            string apiKey = SecretManager.LoadApiKey();
            if (string.IsNullOrEmpty(apiKey))
                return GeminiResponse.FromText("[Fehler: Kein API-Key]");

            var requestBody = new Dictionary<string, object>
            {
                ["contents"] = conversationParts,
                ["tools"] = new[] { new { function_declarations = toolDeclarations } }
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var requestUrl = $"{GenerateEndpoint}?key={apiKey}";

            try
            {
                var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return GeminiResponse.FromText($"[API Fehler]: {response.StatusCode}\n{responseBody}");

                using var document = JsonDocument.Parse(responseBody);
                var candidates = document.RootElement.GetProperty("candidates");

                if (candidates.GetArrayLength() == 0)
                    return GeminiResponse.FromText("[Keine Antwort von Gemini]");

                var firstCandidate = candidates[0];
                var parts = firstCandidate.GetProperty("content").GetProperty("parts");

                if (parts.GetArrayLength() == 0)
                    return GeminiResponse.FromText("[Leere Antwort]");

                var firstPart = parts[0];

                // Check if the response contains a functionCall
                if (firstPart.TryGetProperty("functionCall", out var functionCall))
                {
                    var funcName = functionCall.GetProperty("name").GetString() ?? "";
                    var funcArgs = new Dictionary<string, object?>();

                    if (functionCall.TryGetProperty("args", out var args))
                    {
                        foreach (var prop in args.EnumerateObject())
                        {
                            funcArgs[prop.Name] = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString(),
                                JsonValueKind.Number => prop.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => prop.Value.ToString()
                            };
                        }
                    }

                    string? funcId = null;
                    if (functionCall.TryGetProperty("id", out var idProp))
                        funcId = idProp.GetString();

                    return GeminiResponse.FromFunctionCall(funcName, funcArgs, funcId);
                }

                // Just regular text response
                if (firstPart.TryGetProperty("text", out var textProp))
                {
                    return GeminiResponse.FromText(textProp.GetString() ?? "");
                }

                return GeminiResponse.FromText("[Unbekanntes Antwortformat]");
            }
            catch (Exception ex)
            {
                return GeminiResponse.FromText($"[Verbindungsfehler: {ex.Message}]");
            }
        }

        // Streaming WITH Tools (for the final response after the tool loop)

        /// <summary>
        /// Streams the final response from Gemini after all tools have been executed.
        /// The conversationParts contain the entire chat history including all
        /// functionCall and functionResponse entries.
        /// </summary>
        public async IAsyncEnumerable<string> StreamWithToolHistoryAsync(
            List<object> conversationParts,
            List<object> toolDeclarations,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string apiKey = SecretManager.LoadApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                yield return "[Fehler: Kein API-Key]";
                yield break;
            }

            var requestBody = new Dictionary<string, object>
            {
                ["contents"] = conversationParts,
                ["tools"] = new[] { new { function_declarations = toolDeclarations } }
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var requestUrl = $"{StreamEndpoint}?alt=sse&key={apiKey}";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = content };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                yield return $"[API Fehler]: {response.StatusCode}\n{error}";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    continue;

                string jsonData = line.Substring(6);
                string chunkToReturn = null;

                try
                {
                    using var document = JsonDocument.Parse(jsonData);
                    var candidates = document.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        var parts = candidates[0].GetProperty("content").GetProperty("parts");
                        if (parts.GetArrayLength() > 0 && parts[0].TryGetProperty("text", out var textProp))
                        {
                            chunkToReturn = textProp.GetString();
                        }
                    }
                }
                catch { continue; }

                if (!string.IsNullOrEmpty(chunkToReturn))
                    yield return chunkToReturn;
            }
        }

        // Title generation 
        /// <summary>
        /// Asks Gemini to generate a short, punchy title for a new chat based on the first prompt.
        /// </summary>
        public async Task<string> GenerateTitleAsync(string prompt, System.Threading.CancellationToken cancellationToken = default)
        {
            var history = new List<ChatMessage> 
            { 
                new ChatMessage($"Erstelle einen extrem kurzen, passenden Titel (maximal 3 Wörter) für diese Anfrage. Antworte NUR mit dem Titel, ohne Anführungszeichen oder weitere Erklärungen: {prompt}", true) 
            };

            string title = "";
            try
            {
                await foreach (var chunk in GetResponseStreamAsync(history, cancellationToken))
                {
                    title += chunk;
                }
                
                title = title.Trim().Trim('"', '\'');
                if (title.Length > 30) title = title.Substring(0, 30) + "...";
                
                return string.IsNullOrWhiteSpace(title) ? "Neuer Chat" : title;
            }
            catch
            {
                return "Neuer Chat";
            }
        }
    }

    /// <summary>
    /// Encapsulates the response from Gemini.
    /// 
    /// ARCHITECTURE DECISION: Gemini can return either text OR a functionCall.
    /// This model makes the distinction explicit and type-safe.
    /// The ViewModel only needs to check: IsFunctionCall → execute tool, otherwise display text.
    /// </summary>
    public class GeminiResponse
    {
        public bool IsFunctionCall { get; private set; }
        public string Text { get; private set; } = "";
        public string FunctionName { get; private set; } = "";
        public Dictionary<string, object?> FunctionArgs { get; private set; } = new();
        public string? FunctionCallId { get; private set; }

        /// <summary>
        /// Factory method to create a response representing plain text.
        /// </summary>
        public static GeminiResponse FromText(string text)
            => new() { IsFunctionCall = false, Text = text };

        /// <summary>
        /// Factory method to create a response representing a tool/function call requested by Gemini.
        /// </summary>
        public static GeminiResponse FromFunctionCall(string name, Dictionary<string, object?> args, string? id = null)
            => new() { IsFunctionCall = true, FunctionName = name, FunctionArgs = args, FunctionCallId = id };
    }
}