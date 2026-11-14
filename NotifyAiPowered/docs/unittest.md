# Unit Testing Plan & Matrix: NotifyAiPowered

## 1. Overview
This document specifies the unit testing architecture, test suites, mocking guidelines, and test cases for **NotifyAiPowered**. Unit tests are isolated from external networks and third-party dependencies using test doubles and mocks.

---

## 2. Testing Frameworks & Tools

| Library / Tool | Version | Purpose |
| :--- | :--- | :--- |
| **xUnit** | 2.9+ | Primary unit testing runner and framework |
| **Moq** | 4.20+ | Mocking interfaces (`ILlmAnalysisService`, `INotificationDispatcher`, `IRateLimiterService`, `HttpMessageHandler`) |
| **FluentAssertions** | 7.0+ | Readable and expressive assertion syntax |
| **Microsoft.Extensions.Options** | 9.0+ | In-memory options configuration wrappers (`Options.Create()`) |

---

## 3. Unit Test Suites & Test Cases Matrix

### 3.1. Controller Tests (`NotificationsControllerTests`)
Focuses on HTTP status code generation, request validation, and orchestrating service calls.

| Test Method | Input Scenario | Expected Outcome |
| :--- | :--- | :--- |
| `Post_WithInfoLevel_Returns200WithoutForwarding` | `NotificationRequest` with `Level = Info` | Returns `200 OK`, `forwarded: false`. Verifies `IRateLimiterService`, `ILlmAnalysisService`, and `INotificationDispatcher` are **never called**. |
| `Post_WithWarningLevel_WhenPermitGranted_Returns200WithForwarding` | `NotificationRequest` with `Level = Warning` | Returns `200 OK`, `forwarded: true`. Verifies rate limiter permit is acquired, LLM is invoked, and Discord dispatcher is executed. |
| `Post_WithErrorLevel_WhenPermitGranted_Returns200WithForwarding` | `NotificationRequest` with `Level = Error` | Returns `200 OK`, `forwarded: true`. Both LLM and Discord services called. |
| `Post_WithCriticalLevel_WhenPermitGranted_Returns200WithForwarding` | `NotificationRequest` with `Level = Critical` | Returns `200 OK`, `forwarded: true`. Both LLM and Discord services called. |
| `Post_WhenRateLimitExceeded_Returns429WithRetryAfter` | `NotificationRequest` with `Level = Warning`, Limiter returns `IsAcquired = false` | Returns `429 Too Many Requests`. Response body contains retry time, and `Retry-After` header is present. LLM and Discord are **not called**. |
| `Post_WhenInvalidModel_Returns400BadRequest` | Request with missing required fields (empty `Source` / `Message`) | Returns `400 Bad Request`. |

---

### 3.2. Rate Limiter Tests (`RateLimiterServiceTests`)
Focuses on enforcing the strict rule: **at most 10 forwarded messages per minute**.

| Test Method | Input Scenario | Expected Outcome |
| :--- | :--- | :--- |
| `TryAcquire_UpTo10Times_AllSucceed` | 10 consecutive calls to `TryAcquire()` within a 60-second window | All 10 return `IsAcquired = true` and `RetryAfter = null`. |
| `TryAcquire_OnEleventhCall_ReturnsFalseWithRetryAfter` | 11th consecutive call within the same 60-second window | 11th call returns `IsAcquired = false` and `RetryAfterSeconds > 0`. |
| `TryAcquire_AfterWindowSlides_AllowsNewPermit` | 10 calls made, simulated time advances past the sliding window segment | Subsequent calls succeed as expired permits roll out of the window. |
| `TryAcquire_ConcurrentCalls_MaintainsAccurateCount` | 20 parallel threads calling `TryAcquire()` simultaneously | Exactly 10 permits are acquired (`true`) and exactly 10 are rejected (`false`). |

---

### 3.3. AI Analysis Service Tests (`GeminiLlmServiceTests`)
Focuses on Google Gemini API interaction, prompt generation, parsing, and fallback safety.

| Test Method | Input Scenario | Expected Outcome |
| :--- | :--- | :--- |
| `AnalyzeIncidentAsync_WithValidApiKey_ConstructsCorrectPrompt` | `NotificationRequest` with title, message, source, and metadata | Formats an HTTP POST to Gemini with incident context and instructions. |
| `AnalyzeIncidentAsync_WhenGeminiReturnsJson_ParsesStructuredOutput` | Simulated Gemini HTTP 200 response with JSON markdown block | Returns `LlmAnalysisResult` with populated `IncidentType`, `Summary`, and `ActionRequired`. |
| `AnalyzeIncidentAsync_WhenApiKeyIsEmpty_UsesFallbackWithoutCallingNetwork` | Configuration has `ApiKey = ""` | Returns clean fallback `LlmAnalysisResult` immediately without throwing an exception or calling network. |
| `AnalyzeIncidentAsync_WhenHttpTimesOutOrErrors_ReturnsFallbackGracefully` | Simulated HTTP 500 / HttpRequestException from Gemini | Returns fallback `LlmAnalysisResult`. Critical notification flow continues uninterrupted. |

---

### 3.4. Discord Dispatcher Tests (`DiscordWebhookDispatcherTests`)
Focuses on Discord webhook formatting, embeds, and color styling.

| Test Method | Input Scenario | Expected Outcome |
| :--- | :--- | :--- |
| `DispatchAsync_WarningNotification_SetsAmberColorEmbed` | Severity = `Warning` | Generated Discord Embed `color` equals `16098851` (`#F59E0B`). |
| `DispatchAsync_ErrorNotification_SetsRedColorEmbed` | Severity = `Error` | Generated Discord Embed `color` equals `15682852` (`#EF4444`). |
| `DispatchAsync_CriticalNotification_SetsDarkCrimsonColorEmbed` | Severity = `Critical` | Generated Discord Embed `color` equals `8330525` (`#7F1D1D`). |
| `DispatchAsync_PopulatesEmbedFieldsCorrectly` | Payload with AI summary and recommendation | Embed fields contain `Source`, `Severity`, `Incident Type`, `AI Summary`, and `Recommended Action`. |
| `DispatchAsync_WhenDiscordReturns204_ReturnsSuccess` | Mocked Discord HTTP response `204 No Content` | Task completes successfully returning `true`. |
| `DispatchAsync_WhenDiscordReturns4xx_LogsAndReturnsFalse` | Mocked Discord HTTP response `400 Bad Request` | Returns `false` or raises managed custom exception; does not crash application. |

---

## 4. Mocking Guidelines & Strategy

### 4.1. Mocking HTTP Calls (`HttpMessageHandler`)
To test `GeminiLlmService` and `DiscordWebhookDispatcher` without making real network calls, use a mocked `HttpMessageHandler`:

```csharp
var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
handlerMock
    .Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage
    {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent("{ \"candidates\": [...] }", Encoding.UTF8, "application/json")
    });

var httpClient = new HttpClient(handlerMock.Object);
```

### 4.2. Mocking Time for Rate Limiter Tests
Use `Microsoft.Extensions.Time.Testing.FakeTimeProvider` (part of .NET 8/9 test extensions) or pass a controllable time provider to `SlidingWindowRateLimiterOptions.TimeProvider` to test time expiration deterministically without using fragile `Thread.Sleep()`.

