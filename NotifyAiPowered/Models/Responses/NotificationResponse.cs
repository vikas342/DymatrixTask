using System;
using NotifyAiPowered.Models.Enums;

namespace NotifyAiPowered.Models.Responses;

/// <summary>
/// Response payload returned after processing a notification.
/// </summary>
public class NotificationResponse
{
    public string Status { get; set; } = string.Empty;

    public bool Forwarded { get; set; }

    public NotificationLevel Level { get; set; }

    public string? Message { get; set; }

    public string? IncidentType { get; set; }

    public string? GeneratedMessage { get; set; }

    public string? RecommendedAction { get; set; }

    public int? RetryAfterSeconds { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

