namespace NotifyAiPowered.Configuration;

/// <summary>
/// Configuration settings for Discord Webhook dispatching.
/// </summary>
public class DiscordSettings
{
    public const string SectionName = "Discord";

    /// <summary>
    /// The incoming Discord Webhook URL (Mandatory).
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional custom bot display name. If left empty, Discord uses webhook default.
    /// </summary>
    public string? BotUsername { get; set; }

    /// <summary>
    /// Optional custom bot avatar URL. If left empty, Discord uses webhook default.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// HTTP timeout in seconds for Discord requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}

