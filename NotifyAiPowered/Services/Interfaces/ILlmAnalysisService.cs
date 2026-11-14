using NotifyAiPowered.Models.Llm;
using NotifyAiPowered.Models.Requests;

namespace NotifyAiPowered.Services.Interfaces;

/// <summary>
/// Service contract for AI-powered incident classification and alert generation.
/// </summary>
public interface ILlmAnalysisService
{
    /// <summary>
    /// Analyzes the notification payload and returns a structured AI summary.
    /// Falls back gracefully to rule-based analysis if external AI is unavailable.
    /// </summary>
    Task<LlmAnalysisResult> AnalyzeIncidentAsync(NotificationRequest request);
}

