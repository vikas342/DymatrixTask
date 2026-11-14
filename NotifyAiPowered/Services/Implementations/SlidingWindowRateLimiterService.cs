using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using NotifyAiPowered.Configuration;
using NotifyAiPowered.Services.Interfaces;

namespace NotifyAiPowered.Services.Implementations;

/// <summary>
/// Enforces a maximum of N forwarded messages per sliding window (e.g., 10 messages / minute).
/// Registered as a Singleton to maintain state across requests.
/// </summary>
public class SlidingWindowRateLimiterService : IRateLimiterService, IDisposable
{
    private readonly SlidingWindowRateLimiter _limiter;
    private readonly int _windowSeconds;

    public SlidingWindowRateLimiterService(IOptions<RateLimiterSettings> options)
        : this(options.Value, null)
    {
    }

    /// <summary>
    /// Internal constructor allowing custom TimeProvider for unit testing without waiting real wall-clock time.
    /// </summary>
    public SlidingWindowRateLimiterService(RateLimiterSettings settings, TimeProvider? timeProvider = null)
    {
        _windowSeconds = settings.WindowSeconds > 0 ? settings.WindowSeconds : 60;
        var permitLimit = settings.PermitLimit > 0 ? settings.PermitLimit : 10;
        var segments = settings.SegmentsPerWindow > 0 ? settings.SegmentsPerWindow : 6;

        var options = new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(_windowSeconds),
            SegmentsPerWindow = segments,
            QueueLimit = 0
        };

        _limiter = new SlidingWindowRateLimiter(options);
    }

    public RateLimitResult TryAcquire()
    {
        var lease = _limiter.AttemptAcquire(permitCount: 1);
        if (lease.IsAcquired)
        {
            return new RateLimitResult(true);
        }

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retrySpan))
        {
            var seconds = (int)Math.Max(1, Math.Ceiling(retrySpan.TotalSeconds));
            return new RateLimitResult(false, seconds);
        }

        return new RateLimitResult(false, _windowSeconds);
    }

    public void Dispose()
    {
        _limiter.Dispose();
        GC.SuppressFinalize(this);
    }
}

