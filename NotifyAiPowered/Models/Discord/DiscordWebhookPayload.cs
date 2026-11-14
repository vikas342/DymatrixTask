using System.Text.Json.Serialization;
using NotifyAiPowered.Models.Enums;

namespace NotifyAiPowered.Models.Discord;

/// <summary>
/// Discord webhook payload model.
/// Note: username and avatar_url are optional.
/// If omitted, Discord automatically uses the webhook's configured defaults.
/// </summary>
public class DiscordWebhookPayload
{
    [JsonPropertyName("username")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Username { get; set; }

    [JsonPropertyName("avatar_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonPropertyName("embeds")]
    public List<DiscordEmbed> Embeds { get; set; } = new();
}

public class DiscordEmbed
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public int Color { get; set; }

    [JsonPropertyName("fields")]
    public List<DiscordEmbedField> Fields { get; set; } = new();

    [JsonPropertyName("footer")]
    public DiscordEmbedFooter? Footer { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    /// <summary>
    /// Gets the Discord embed color integer for a given severity level.
    /// </summary>
    public static int GetColorForLevel(NotificationLevel level) => level switch
    {
        NotificationLevel.Warning => 16098851, // Amber #F59E0B
        NotificationLevel.Error => 15682852,   // Red #EF4444
        NotificationLevel.Critical => 8330525, // Dark Crimson #7F1D1D
        _ => 3760866                          // Blue #3962E2
    };
}

public class DiscordEmbedField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("inline")]
    public bool Inline { get; set; }
}

public class DiscordEmbedFooter
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

