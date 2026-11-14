using System.Text.Json.Serialization;

namespace NotifyAiPowered.Models.Enums;

/// <summary>
/// Notification severity levels.
/// Only 4 levels supported: Info, Warning, Error, Critical.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationLevel
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

