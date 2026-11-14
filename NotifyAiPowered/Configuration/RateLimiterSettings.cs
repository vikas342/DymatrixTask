namespace NotifyAiPowered.Configuration;

/// <summary>
/// Configuration settings for rate limiting outbound alerts.
/// </summary>
public class RateLimiterSettings
{
    public const string SectionName = "RateLimiter";

    /// <summary>
    /// Maximum number of messages permitted in the rolling window (Default: 10).
    /// </summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>
    /// Rolling window duration in seconds (Default: 60s).
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Number of segments in the sliding window for smooth transitions (Default: 6).
    /// </summary>
    public int SegmentsPerWindow { get; set; } = 6;
}

