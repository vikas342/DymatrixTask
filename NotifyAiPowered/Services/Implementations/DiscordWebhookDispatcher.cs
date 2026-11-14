using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NotifyAiPowered.Configuration;
using NotifyAiPowered.Models.Discord;
using NotifyAiPowered.Models.Enums;
using NotifyAiPowered.Models.Llm;
using NotifyAiPowered.Models.Requests;
using NotifyAiPowered.Services.Interfaces;

namespace NotifyAiPowered.Services.Implementations;

/// <summary>
/// Dispatches enriched alert messages to a Discord Webhook as rich embeds.
/// </summary>
public class DiscordWebhookDispatcher : INotificationDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly DiscordSettings _settings;
    private readonly ILogger<DiscordWebhookDispatcher> _logger;

    public DiscordWebhookDispatcher(
        HttpClient httpClient,
        IOptions<DiscordSettings> options,
        ILogger<DiscordWebhookDispatcher> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<bool> DispatchAsync(
        NotificationRequest request,
        LlmAnalysisResult aiAnalysis)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookUrl))
        {
            _logger.LogWarning("Discord Webhook URL is not configured. Outbound dispatch skipped.");
            return false;
        }

        var payload = BuildPayload(request, aiAnalysis);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_settings.WebhookUrl, payload);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully dispatched notification for '{Source}' to Discord (Status {StatusCode}).",
                    request.Source, response.StatusCode);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Discord Webhook returned non-success status code {StatusCode}: {ErrorBody}",
                response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch notification to Discord Webhook.");
            return false;
        }
    }

    private DiscordWebhookPayload BuildPayload(NotificationRequest request, LlmAnalysisResult aiAnalysis)
    {
        var emoji = GetEmojiForLevel(request.Level);
        var title = $"{emoji} [{request.Level}] {request.Source}: {request.Title}";
        if (title.Length > 256)
        {
            title = title[..253] + "...";
        }

        var embed = new DiscordEmbed
        {
            Title = title,
            Description = string.IsNullOrWhiteSpace(aiAnalysis.Summary) ? request.Message : aiAnalysis.Summary,
            Color = DiscordEmbed.GetColorForLevel(request.Level),
            Timestamp = request.Timestamp.ToString("o"),
            Footer = new DiscordEmbedFooter
            {
                Text = aiAnalysis.IsFallback
                    ? "NotifyAiPowered • Rule-based Fallback"
                    : "NotifyAiPowered • AI Incident Gateway"
            },
            Fields = new List<DiscordEmbedField>
            {
                new() { Name = "Source", Value = request.Source, Inline = true },
                new() { Name = "Severity", Value = request.Level.ToString(), Inline = true },
                new() { Name = "Incident Classification", Value = string.IsNullOrWhiteSpace(aiAnalysis.IncidentType) ? "Unknown" : aiAnalysis.IncidentType, Inline = false },
                new() { Name = "Recommended Action", Value = string.IsNullOrWhiteSpace(aiAnalysis.ActionRequired) ? "Investigate service logs." : aiAnalysis.ActionRequired, Inline = false }
            }
        };

        // if (request.Metadata != null && request.Metadata.Count > 0)
        // {
        //     var metaPreview = string.Join(", ", request.Metadata.Select(kv => $"{kv.Key}: {kv.Value}"));
        //     if (metaPreview.Length > 1024)
        //     {
        //         metaPreview = metaPreview[..1021] + "...";
        //     }
        //     embed.Fields.Add(new DiscordEmbedField
        //     {
        //         Name = "Metadata",
        //         Value = $"`{metaPreview}`",
        //         Inline = false
        //     });
        // }

        return new DiscordWebhookPayload
        {
            Username = string.IsNullOrWhiteSpace(_settings.BotUsername) ? null : _settings.BotUsername,
            AvatarUrl = string.IsNullOrWhiteSpace(_settings.AvatarUrl) ? null : _settings.AvatarUrl,
            Embeds = new List<DiscordEmbed> { embed }
        };
    }

    private static string GetEmojiForLevel(NotificationLevel level) => level switch
    {
        NotificationLevel.Warning => "⚠️",
        NotificationLevel.Error => "❌",
        NotificationLevel.Critical => "🚨",
        _ => "ℹ️"
    };
}

