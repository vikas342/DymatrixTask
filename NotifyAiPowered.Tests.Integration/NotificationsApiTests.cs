using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NotifyAiPowered.Models.Enums;
using NotifyAiPowered.Models.Requests;
using NotifyAiPowered.Models.Responses;
using Xunit;

namespace NotifyAiPowered.Tests.Integration;

public class NotificationsApiTests
{
    [Fact]
    public async Task Post_InfoNotification_Returns200_NotForwarded()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var request = new NotificationRequest
        {
            Source = "InventoryService",
            Level = NotificationLevel.Info,
            Title = "Stock Replenished",
            Message = "SKU-9921 replenished by 50 units."
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/notifications/SendNotification", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<NotificationResponse>();
        content.Should().NotBeNull();
        content!.Status.Should().Be("Processed");
        content.Forwarded.Should().BeFalse();
        content.Level.Should().Be(NotificationLevel.Info);
    }

    [Fact]
    public async Task Post_WarningNotification_Returns200_ForwardedWithAiEnrichment()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var request = new NotificationRequest
        {
            Source = "PaymentService",
            Level = NotificationLevel.Warning,
            Title = "Stripe Timeout",
            Message = "Connection timed out."
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/notifications/SendNotification", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<NotificationResponse>();
        content.Should().NotBeNull();
        content!.Status.Should().Be("Forwarded");
        content.Forwarded.Should().BeTrue();
        content.Level.Should().Be(NotificationLevel.Warning);
        content.IncidentType.Should().Be("Integration Test Issue");
        content.GeneratedMessage.Should().Be("Integration test simulated summary.");
        content.RecommendedAction.Should().Be("Check integration test logs.");
    }

    [Fact]
    public async Task Post_InvalidPayload_Returns400BadRequest()
    {
        // Arrange: Missing required Source and Title
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var invalidPayload = new
        {
            Level = "Warning",
            Message = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/notifications/SendNotification", invalidPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ExceedingRateLimit_Returns429WithRetryAfterHeader()
    {
        // Arrange: Send 11 warning requests in rapid succession
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        HttpResponseMessage? lastResponse = null;

        for (int i = 0; i < 11; i++)
        {
            var request = new NotificationRequest
            {
                Source = "RateLimitProbe",
                Level = NotificationLevel.Warning,
                Title = $"Alert #{i}",
                Message = $"Simulating high volume alert #{i}"
            };

            lastResponse = await client.PostAsJsonAsync("/api/notifications/SendNotification", request);
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        // Assert: The 11th request must be 429 Too Many Requests
        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        lastResponse.Headers.Should().ContainKey("Retry-After");
        var body = await lastResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("RateLimited");
        body.Forwarded.Should().BeFalse();
        body.RetryAfterSeconds.Should().BeGreaterThan(0);
    }
}
