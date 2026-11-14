using FluentAssertions;
using Microsoft.Extensions.Options;
using NotifyAiPowered.Configuration;
using NotifyAiPowered.Services.Implementations;
using Xunit;

namespace NotifyAiPowered.Tests.Unit.Services;

public class RateLimiterServiceTests
{
    [Fact]
    public void TryAcquire_First10Calls_AllSucceed()
    {
        // Arrange
        var settings = Options.Create(new RateLimiterSettings
        {
            PermitLimit = 10,
            WindowSeconds = 60,
            SegmentsPerWindow = 6
        });

        using var service = new SlidingWindowRateLimiterService(settings);

        // Act & Assert
        for (int i = 1; i <= 10; i++)
        {
            var result = service.TryAcquire();
            result.IsAllowed.Should().BeTrue($"Permit #{i} should be granted within the 10/min limit.");
            result.RetryAfterSeconds.Should().BeNull();
        }
    }

    [Fact]
    public void TryAcquire_EleventhCall_IsRejectedWithRetryAfter()
    {
        // Arrange
        var settings = Options.Create(new RateLimiterSettings
        {
            PermitLimit = 10,
            WindowSeconds = 60,
            SegmentsPerWindow = 6
        });

        using var service = new SlidingWindowRateLimiterService(settings);

        // Consume 10 permits
        for (int i = 0; i < 10; i++)
        {
            service.TryAcquire();
        }

        // Act: 11th attempt
        var result = service.TryAcquire();

        // Assert
        result.IsAllowed.Should().BeFalse("The 11th call within 60s must exceed the 10/min limit.");
        result.RetryAfterSeconds.Should().NotBeNull();
        result.RetryAfterSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryAcquire_ConcurrentAccess_EnforcesStrictLimit()
    {
        // Arrange: 10 permit limit
        var settings = Options.Create(new RateLimiterSettings
        {
            PermitLimit = 10,
            WindowSeconds = 60,
            SegmentsPerWindow = 6
        });

        using var service = new SlidingWindowRateLimiterService(settings);

        int totalAttempts = 30;
        int successfulAcquires = 0;
        int rejectedAcquires = 0;

        // Act: 30 parallel attempts
        Parallel.For(0, totalAttempts, _ =>
        {
            var result = service.TryAcquire();
            if (result.IsAllowed)
            {
                Interlocked.Increment(ref successfulAcquires);
            }
            else
            {
                Interlocked.Increment(ref rejectedAcquires);
            }
        });

        // Assert: Exactly 10 permits granted, exactly 20 rejected
        successfulAcquires.Should().Be(10);
        rejectedAcquires.Should().Be(20);
    }
}

