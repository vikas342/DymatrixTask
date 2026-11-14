namespace NotifyAiPowered.Configuration;

/// <summary>
/// Configuration settings for Google Gemini API integration.
/// </summary>
public class GeminiSettings
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// Google Gemini API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model identifier, e.g., "gemini-3.5-flash-lite".
    /// </summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>
    /// Base endpoint for Google Generative Language API.
    /// </summary>
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com";

    /// <summary>
    /// HTTP timeout in seconds for Gemini requests (default: 60s for LLM processing).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}

