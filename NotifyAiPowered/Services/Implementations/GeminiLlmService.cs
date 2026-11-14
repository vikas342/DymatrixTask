using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NotifyAiPowered.Configuration;
using NotifyAiPowered.Models.Llm;
using NotifyAiPowered.Models.Requests;
using NotifyAiPowered.Services.Interfaces;

namespace NotifyAiPowered.Services.Implementations;

/// <summary>
/// Google Gemini API implementation of ILlmAnalysisService.
/// Analyzes notifications, categorizes errors, and crafts human-readable summaries.
/// Resiliently falls back to a deterministic summary if API key is absent or network fails.
/// </summary>
public partial class GeminiLlmService : ILlmAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiLlmService> _logger;

    public GeminiLlmService(
        HttpClient httpClient,
        IOptions<GeminiSettings> options,
        ILogger<GeminiLlmService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<LlmAnalysisResult> AnalyzeIncidentAsync(
        NotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogInformation("Gemini API key is not configured; using deterministic fallback analysis.");
            return GenerateFallback(request);
        }

        try
        {
            var prompt = BuildPrompt(request);
            var model = string.IsNullOrWhiteSpace(_settings.Model) ? "gemini-3.5-flash-lite" : _settings.Model;
            var endpoint = $"{_settings.Endpoint.TrimEnd('/')}/v1beta/models/{model}:generateContent?key={_settings.ApiKey}";

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
                    responseMimeType = "application/json",
                    temperature = 0.2
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini API returned error status {StatusCode}: {ErrorBody}. Using fallback analysis.",
                    response.StatusCode, errorBody);
                return GenerateFallback(request);
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var parsed = ParseGeminiResponse(jsonResponse);
            if (parsed != null)
            {
                parsed.IsFallback = false;
                return parsed;
            }

            _logger.LogWarning("Could not parse structured output from Gemini response; using fallback analysis.");
            return GenerateFallback(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while communicating with Gemini API. Falling back to local analysis.");
            return GenerateFallback(request);
        }
    }

    private static string BuildPrompt(NotificationRequest request)
    {


        return $$"""
You are an expert Site Reliability Engineer and automated alert triage system.
Analyze the following alert notification and respond strictly with a valid JSON object matching the requested schema.

Alert Details:
- Source Service: {{request.Source}}
- Severity Level: {{request.Level}}
- Title: {{request.Title}}
- Message: {{request.Message}}

Required JSON Schema:
{
  "incidentType": "Brief 2-4 word classification of the issue (e.g., 'Database Connection Timeout', 'Payment Gateway Degradation', 'Authentication Token Mismatch')",
  "summary": "1-2 sentence executive summary of what happened and its user/system impact.",
  "actionRequired": "1 concise sentence with the immediate troubleshooting or remediation action recommended."
}

Do not include markdown fences or any explanation outside the JSON object.
""";
    }

    private static LlmAnalysisResult? ParseGeminiResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                return null;
            }

            var text = parts[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Strip markdown code fences if Gemini added ```json ... ```
            var cleanText = CleanMarkdownJson(text);

            using var innerDoc = JsonDocument.Parse(cleanText);
            var innerRoot = innerDoc.RootElement;

            var incidentType = innerRoot.TryGetProperty("incidentType", out var itProp) ? itProp.GetString() : null;
            var summary = innerRoot.TryGetProperty("summary", out var sProp) ? sProp.GetString() : null;
            var actionRequired = innerRoot.TryGetProperty("actionRequired", out var aProp) ? aProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(incidentType) && string.IsNullOrWhiteSpace(summary))
            {
                return null;
            }

            return new LlmAnalysisResult
            {
                IncidentType = incidentType ?? "Unclassified Incident",
                Summary = summary ?? text,
                ActionRequired = actionRequired ?? "Review application logs for details."
            };
        }
        catch
        {
            return null;
        }
    }

    private static string CleanMarkdownJson(string text)
    {
        var trimmed = text.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed.Substring(start, end - start + 1);
        }
        return trimmed;
    }

    private static LlmAnalysisResult GenerateFallback(NotificationRequest request)
    {
        var incidentType = $"{request.Source} {request.Level} Alert";
        var summary = $"Alert from {request.Source}: {request.Title}. Details: {request.Message}";
        var action = $"Check telemetry, logs, and diagnostic dashboards for service: {request.Source}.";

        return new LlmAnalysisResult
        {
            IncidentType = incidentType,
            Summary = summary,
            ActionRequired = action,
            IsFallback = true
        };
    }

    [GeneratedRegex(@"^```(?:json)?\s*([\s\S]*?)\s*```$", RegexOptions.Compiled)]
    private static partial Regex JsonFenceRegex();
}

