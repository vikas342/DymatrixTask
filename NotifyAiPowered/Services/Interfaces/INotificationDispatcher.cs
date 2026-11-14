using NotifyAiPowered.Models.Llm;
using NotifyAiPowered.Models.Requests;

namespace NotifyAiPowered.Services.Interfaces;

/// <summary>
/// Service contract for dispatching formatted alerts to external communication channels.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Dispatches an enriched notification to the external channel (Discord).
    /// </summary>
    Task<bool> DispatchAsync(NotificationRequest request, LlmAnalysisResult aiAnalysis);
}

