namespace NotifyAiPowered.Services.Interfaces;

/// <summary>
/// Result of attempting to acquire a rate limit permit.
/// </summary>
public record RateLimitResult(bool IsAllowed, int? RetryAfterSeconds = null);

/// <summary>
/// Contract for enforcing rolling rate limits on outbound notifications.
/// </summary>
public interface IRateLimiterService
{
    /// <summary>
    /// Attempts to acquire 1 permit for an outbound notification.
    /// </summary>
    RateLimitResult TryAcquire();
}

