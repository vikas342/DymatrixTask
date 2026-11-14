using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NotifyAiPowered.Controllers;
using NotifyAiPowered.Models.Enums;
using NotifyAiPowered.Models.Llm;
using NotifyAiPowered.Models.Requests;
using NotifyAiPowered.Models.Responses;
using NotifyAiPowered.Services.Interfaces;
using Xunit;

namespace NotifyAiPowered.Tests.Unit.Controllers;

public class NotificationsControllerTests
{
    private readonly Mock<IRateLimiterService> _rateLimiterMock = new();
    private readonly Mock<ILlmAnalysisService> _llmServiceMock = new();
    private readonly Mock<INotificationDispatcher> _dispatcherMock = new();
    private readonly Mock<ILogger<NotificationsController>> _loggerMock = new();
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _controller = new NotificationsController(
            _rateLimiterMock.Object,
            _llmServiceMock.Object,
            _dispatcherMock.Object,
            _loggerMock.Object);

        // Setup mock HttpContext for headers
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Send_WithInfoLevel_Returns200WithoutForwarding()
    {
        // Arrange
        var request = new NotificationRequest
        {
            Source = "OrderService",
            Level = NotificationLevel.Info,
            Title = "Order Placed",
            Message = "Order #123 placed successfully."
        };

        // Act
        var result = await _controller.Send(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<NotificationResponse>().Subject;

        response.Status.Should().Be("Processed");
        response.Forwarded.Should().BeFalse();
        response.Level.Should().Be(NotificationLevel.Info);

        // Verify downstream services were NEVER invoked
        _rateLimiterMock.Verify(r => r.TryAcquire(), Times.Never);
        _llmServiceMock.Verify(l => l.AnalyzeIncidentAsync(It.IsAny<NotificationRequest>()), Times.Never);
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<NotificationRequest>(), It.IsAny<LlmAnalysisResult>()), Times.Never);
    }

    [Theory]
    [InlineData(NotificationLevel.Warning)]
    [InlineData(NotificationLevel.Error)]
    [InlineData(NotificationLevel.Critical)]
    public async Task Send_WithElevatedSeverity_WhenPermitGranted_EnrichesAndForwards(NotificationLevel level)
    {
        // Arrange
        var request = new NotificationRequest
        {
            Source = "PaymentService",
            Level = level,
            Title = "Payment Error",
            Message = "Payment timed out."
        };

        _rateLimiterMock
            .Setup(r => r.TryAcquire())
            .Returns(new RateLimitResult(true));

        var expectedAnalysis = new LlmAnalysisResult
        {
            IncidentType = "Gateway Timeout",
            Summary = "Payment service experienced an upstream timeout.",
            ActionRequired = "Check gateway status page."
        };

        _llmServiceMock
            .Setup(l => l.AnalyzeIncidentAsync(request))
            .ReturnsAsync(expectedAnalysis);

        _dispatcherMock
            .Setup(d => d.DispatchAsync(request, expectedAnalysis))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Send(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<NotificationResponse>().Subject;

        response.Status.Should().Be("Forwarded");
        response.Forwarded.Should().BeTrue();
        response.Level.Should().Be(level);
        response.IncidentType.Should().Be(expectedAnalysis.IncidentType);
        response.GeneratedMessage.Should().Be(expectedAnalysis.Summary);
        response.RecommendedAction.Should().Be(expectedAnalysis.ActionRequired);

        _rateLimiterMock.Verify(r => r.TryAcquire(), Times.Once);
        _llmServiceMock.Verify(l => l.AnalyzeIncidentAsync(request), Times.Once);
        _dispatcherMock.Verify(d => d.DispatchAsync(request, expectedAnalysis), Times.Once);
    }

    [Fact]
    public async Task Send_WhenRateLimitExceeded_Returns429WithRetryAfterHeader()
    {
        // Arrange
        var request = new NotificationRequest
        {
            Source = "AuthService",
            Level = NotificationLevel.Warning,
            Title = "High Failure Rate",
            Message = "Multiple failed login attempts."
        };

        _rateLimiterMock
            .Setup(r => r.TryAcquire())
            .Returns(new RateLimitResult(false, 45));

        // Act
        var result = await _controller.Send(request);

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);

        var response = statusResult.Value.Should().BeOfType<NotificationResponse>().Subject;
        response.Status.Should().Be("RateLimited");
        response.Forwarded.Should().BeFalse();
        response.RetryAfterSeconds.Should().Be(45);

        _controller.Response.Headers.Should().ContainKey("Retry-After");
        _controller.Response.Headers["Retry-After"].ToString().Should().Be("45");

        // Outbound calls must NOT be made
        _llmServiceMock.Verify(l => l.AnalyzeIncidentAsync(It.IsAny<NotificationRequest>()), Times.Never);
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<NotificationRequest>(), It.IsAny<LlmAnalysisResult>()), Times.Never);
    }
}
