using Microsoft.AspNetCore.Mvc;
using NotifyAiPowered.Models.Enums;
using NotifyAiPowered.Models.Requests;
using NotifyAiPowered.Models.Responses;
using NotifyAiPowered.Services.Interfaces;


namespace NotifyAiPowered.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IRateLimiterService _rateLimiter;
    private readonly ILlmAnalysisService _llmService;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
       IRateLimiterService rateLimiter,
       ILlmAnalysisService llmService,
       INotificationDispatcher dispatcher, ILogger<NotificationsController> logger)
    {
        _rateLimiter = rateLimiter;
        _llmService = llmService;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [HttpPost("SendNotification")]
    public async Task<IActionResult> Send([FromBody] NotificationRequest request)
    {
        // 1. Info: Acknowledge without forwarding
        if (request.Level < NotificationLevel.Warning)
        {
            _logger.LogInformation(
                "Notification from '{Source}' with level '{Level}' is below forwarding threshold. Acknowledged without forwarding.",
                request.Source, request.Level);

            return Ok(new NotificationResponse
            {
                Status = "Processed",
                Forwarded = false,
                Level = request.Level,
                Message = "Notification level is below forwarding threshold (Warning). Not forwarded."
            });
        }

        // 2. Check 10 messages/minute rate limit
        var rateLimit = _rateLimiter.TryAcquire();
        if (!rateLimit.IsAllowed)
        {
            var retrySec = rateLimit.RetryAfterSeconds ?? 60;
            Response.Headers.Append("Retry-After", retrySec.ToString());

            _logger.LogWarning(
                "Rate limit exceeded for forwarded notification from '{Source}' (Level: {Level}). Retry after {Seconds}s.",
                request.Source, request.Level, retrySec);

            return StatusCode(StatusCodes.Status429TooManyRequests, new NotificationResponse
            {
                Status = "RateLimited",
                Forwarded = false,
                Level = request.Level,
                RetryAfterSeconds = retrySec,
                Message = "Forwarding rate limit exceeded. A maximum of 10 forwarded notifications per minute is allowed."
            });
        }

        // 3. AI analysis & Discord dispatch
        var ai = await _llmService.AnalyzeIncidentAsync(request);
        var dispatched = await _dispatcher.DispatchAsync(request, ai);

        _logger.LogInformation(
          "Notification from '{Source}' (Level: {Level}) processed and forwarded. Dispatched: {Dispatched}. AI Classification: '{IncidentType}'.",
          request.Source, request.Level, dispatched, ai.IncidentType);

        // 4. Return result
        var statusMessage = ai.IsFallback
            ? "Notification forwarded to Discord successfully (AI service unavailable; local fallback summary used)."
            : "Notification enriched with AI and delivered to Discord successfully.";

        return Ok(new NotificationResponse
        {
            Status = "Forwarded",
            Forwarded = true,
            Level = request.Level,
            Message = statusMessage,
            IncidentType = ai.IncidentType,
            GeneratedMessage = ai.Summary,
            RecommendedAction = ai.ActionRequired
        });
    }
}
