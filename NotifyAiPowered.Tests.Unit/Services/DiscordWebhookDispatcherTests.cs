using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NotifyAiPowered.Configuration;
using NotifyAiPowered.Models.Enums;
using NotifyAiPowered.Models.Llm;
using NotifyAiPowered.Models.Requests;
using NotifyAiPowered.Services.Implementations;
using Xunit;

namespace NotifyAiPowered.Tests.Unit.Services;

public class DiscordWebhookDispatcherTests
{
    private readonly Mock<ILogger<DiscordWebhookDispatcher>> _loggerMock = new();

    [Theory]
    [InlineData(NotificationLevel.Warning, 16098851)]
    [InlineData(NotificationLevel.Error, 15682852)]
    [InlineData(NotificationLevel.Critical, 8330525)]
    public async Task DispatchAsync_SetsCorrectSeverityColor(NotificationLevel level, int expectedColor)
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        var httpClient = new HttpClient(handlerMock.Object);
        var settings = Options.Create(new DiscordSettings
        {
            WebhookUrl = "https://discord.com/api/webhooks/test/123"
        });

        var dispatcher = new DiscordWebhookDispatcher(httpClient, settings, _loggerMock.Object);
        var request = new NotificationRequest
        {
            Source = "TestService",
            Level = level,
            Title = "Incident Alert",
            Message = "Test error detail"
        };
        var aiAnalysis = new LlmAnalysisResult
        {
            IncidentType = "Test Incident",
            Summary = "Test Summary",
            ActionRequired = "Check logs."
        };

        // Act
        var result = await dispatcher.DispatchAsync(request, aiAnalysis);

        // Assert
        result.Should().BeTrue();
        capturedRequest.Should().NotBeNull();

        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain($"\"color\":{expectedColor}");
        body.Should().Contain("TestService");
        body.Should().Contain(level.ToString());
    }

    [Fact]
    public async Task DispatchAsync_WhenWebhookUrlIsEmpty_ReturnsFalseWithoutCallingNetwork()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var httpClient = new HttpClient(handlerMock.Object);
        var settings = Options.Create(new DiscordSettings
        {
            WebhookUrl = "" // Empty URL
        });

        var dispatcher = new DiscordWebhookDispatcher(httpClient, settings, _loggerMock.Object);
        var request = new NotificationRequest
        {
            Source = "TestService",
            Level = NotificationLevel.Warning,
            Title = "Alert",
            Message = "Detail"
        };

        // Act
        var result = await dispatcher.DispatchAsync(request, new LlmAnalysisResult());

        // Assert
        result.Should().BeFalse();
        handlerMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WhenDiscordReturnsErrorStatus_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Invalid webhook token")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var settings = Options.Create(new DiscordSettings
        {
            WebhookUrl = "https://discord.com/api/webhooks/test/invalid"
        });

        var dispatcher = new DiscordWebhookDispatcher(httpClient, settings, _loggerMock.Object);
        var request = new NotificationRequest
        {
            Source = "TestService",
            Level = NotificationLevel.Warning,
            Title = "Alert",
            Message = "Detail"
        };

        // Act
        var result = await dispatcher.DispatchAsync(request, new LlmAnalysisResult());

        // Assert
        result.Should().BeFalse();
    }
}

