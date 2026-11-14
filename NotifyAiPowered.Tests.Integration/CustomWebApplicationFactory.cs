using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NotifyAiPowered.Models.Llm;
using NotifyAiPowered.Models.Requests;
using NotifyAiPowered.Services.Interfaces;

namespace NotifyAiPowered.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<ILlmAnalysisService> LlmMock { get; } = new();
    public Mock<INotificationDispatcher> DispatcherMock { get; } = new();

    public CustomWebApplicationFactory()
    {
        LlmMock.Setup(l => l.AnalyzeIncidentAsync(It.IsAny<NotificationRequest>()))
            .ReturnsAsync(new LlmAnalysisResult
            {
                IncidentType = "Integration Test Issue",
                Summary = "Integration test simulated summary.",
                ActionRequired = "Check integration test logs."
            });

        DispatcherMock.Setup(d => d.DispatchAsync(It.IsAny<NotificationRequest>(), It.IsAny<LlmAnalysisResult>()))
            .ReturnsAsync(true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Swap LLM and Discord dispatcher services with mocks to keep integration tests offline and deterministic
            services.AddScoped<ILlmAnalysisService>(_ => LlmMock.Object);
            services.AddScoped<INotificationDispatcher>(_ => DispatcherMock.Object);
        });
    }
}

