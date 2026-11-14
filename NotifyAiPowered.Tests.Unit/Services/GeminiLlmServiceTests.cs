using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NotifyAiPowered.Configuration;
using NotifyAiPowered.Models.Enums;
using NotifyAiPowered.Models.Requests;
using NotifyAiPowered.Services.Implementations;
using Xunit;

namespace NotifyAiPowered.Tests.Unit.Services;

public class GeminiLlmServiceTests
{
    private readonly Mock<ILogger<GeminiLlmService>> _loggerMock = new();

    [Fact]
    public async Task AnalyzeIncidentAsync_WhenApiKeyIsEmpty_ReturnsFallbackWithoutCallingHttp()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var httpClient = new HttpClient(handlerMock.Object);

        var settings = Options.Create(new GeminiSettings
        {
            ApiKey = "" // Empty key
        });

        var service = new GeminiLlmService(httpClient, settings, _loggerMock.Object);
        var request = new NotificationRequest
        {
            Source = "OrderService",
            Level = NotificationLevel.Warning,
            Title = "High CPU",
            Message = "CPU utilization exceeded 90%."
        };

        // Act
        var result = await service.AnalyzeIncidentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsFallback.Should().BeTrue();
        result.IncidentType.Should().Contain("OrderService");
        result.Summary.Should().Contain("High CPU");
        result.ActionRequired.Should().Contain("OrderService");

        // Verify no HTTP calls made
        handlerMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeIncidentAsync_WhenGeminiReturnsValidJson_ParsesCorrectly()
    {
        // Arrange
        var geminiResponseJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{\n  \"incidentType\": \"Database Connection Pool Exhaustion\",\n  \"summary\": \"Postgres connection pool reached 100% capacity causing query timeouts.\",\n  \"actionRequired\": \"Terminate idle connection sessions and scale pool capacity.\"\n}"
                  }
                ]
              }
            }
          ]
        }
        """;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(geminiResponseJson, System.Text.Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var settings = Options.Create(new GeminiSettings
        {
            ApiKey = "test-api-key",
            Model = "gemini-3.5-flash-lite"
        });

        var service = new GeminiLlmService(httpClient, settings, _loggerMock.Object);
        var request = new NotificationRequest
        {
            Source = "PostgresService",
            Level = NotificationLevel.Critical,
            Title = "Pool Exhausted",
            Message = "Connections 500/500."
        };

        // Act
        var result = await service.AnalyzeIncidentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsFallback.Should().BeFalse();
        result.IncidentType.Should().Be("Database Connection Pool Exhaustion");
        result.Summary.Should().Contain("Postgres connection pool reached 100% capacity");
        result.ActionRequired.Should().Be("Terminate idle connection sessions and scale pool capacity.");
    }

    [Fact]
    public async Task AnalyzeIncidentAsync_WhenGeminiReturnsMarkdownCodeFences_StripsAndParsesCorrectly()
    {
        // Arrange
        var fencedText = "```json\n{\n  \"incidentType\": \"Payment Gateway Degradation\",\n  \"summary\": \"Elevated latencies on Stripe API.\",\n  \"actionRequired\": \"Monitor upstream dashboard.\"\n}\n```";

        var geminiResponseJson = $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{{fencedText.Replace("\n", "\\n").Replace("\"", "\\\"")}}"
                  }
                ]
              }
            }
          ]
        }
        """;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(geminiResponseJson, System.Text.Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var settings = Options.Create(new GeminiSettings
        {
            ApiKey = "test-api-key"
        });

        var service = new GeminiLlmService(httpClient, settings, _loggerMock.Object);
        var request = new NotificationRequest
        {
            Source = "BillingService",
            Level = NotificationLevel.Warning,
            Title = "Slow Stripe",
            Message = "Latency 3s"
        };

        // Act
        var result = await service.AnalyzeIncidentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsFallback.Should().BeFalse();
        result.IncidentType.Should().Be("Payment Gateway Degradation");
        result.Summary.Should().Be("Elevated latencies on Stripe API.");
    }

    [Fact]
    public async Task AnalyzeIncidentAsync_WhenHttpErrorOccurs_FallsBackGracefully()
    {
        // Arrange: HTTP 500 from Gemini
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server Error")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var settings = Options.Create(new GeminiSettings
        {
            ApiKey = "test-api-key"
        });

        var service = new GeminiLlmService(httpClient, settings, _loggerMock.Object);
        var request = new NotificationRequest
        {
            Source = "InventoryService",
            Level = NotificationLevel.Error,
            Title = "Sync Failed",
            Message = "Inventory sync failed."
        };

        // Act
        var result = await service.AnalyzeIncidentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsFallback.Should().BeTrue();
        result.Summary.Should().Contain("Sync Failed");
    }
}

