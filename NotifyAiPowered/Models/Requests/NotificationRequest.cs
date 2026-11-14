using System;
using System.ComponentModel.DataAnnotations;
using NotifyAiPowered.Models.Enums;

namespace NotifyAiPowered.Models.Requests;

/// <summary>
/// Inbound notification payload model.
/// </summary>
public class NotificationRequest
{

    /// <summary>
    /// Severity level of the notification (Info, Warning, Error, Critical).
    /// </summary>
    [Required(ErrorMessage = "The Level field is required.")]
    public NotificationLevel Level { get; set; }

    /// <summary>
    /// The originating service or subsystem producing the notification.
    /// </summary>
    [Required(ErrorMessage = "The Source field is required.")]
    public string Source { get; set; } = "XYZ Service";


    /// <summary>
    /// Short headline or subject of the notification.
    /// </summary>
    [Required(ErrorMessage = "The Title field is required.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed notification message or error description.
    /// </summary>
    [Required(ErrorMessage = "The Message field is required.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of when the event occurred (defaults to UtcNow if not provided).
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

}

