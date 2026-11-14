# System Architecture: NotifyAiPowered

## 1. Overview
The **NotifyAiPowered** system is designed using clean architecture and modular component design principles on **ASP.NET Core (.NET 9)**. It cleanly decouples API ingestion, business filtering, rate limiting, AI enrichment, and external dispatching.

---

## 2. Technology Stack

| Layer / Concern | Technology | Purpose |
| :--- | :--- | :--- |
| **Runtime & Framework** | .NET 9 SDK / ASP.NET Core Web API | Core execution runtime and RESTful API framework |
| **Language** | C# 13 | Primary programming language |
| **Rate Limiter** | `System.Threading.RateLimiting` | Native rolling sliding-window rate limiting engine |
| **AI Integration** | Google Gemini API (REST / SDK) | Intelligent classification and alert generation |
| **Outbound Messaging** | Discord Webhooks API | Real-time chat notification channel with rich embeds |
| **API Documentation** | OpenAPI 3.0 & Swashbuckle (Swagger) | Interactive API exploration and testing |
| **Configuration** | Microsoft Extensions Options (`IOptions<T>`) | Strongly typed, environment-driven configuration |
| **Unit & Integration Tests** | xUnit, Moq, FluentAssertions, `WebApplicationFactory` | Test automation and coverage |

---

## 3. High-Level Architecture Diagram

```
                 ┌────────────────────────────────────────────────────────┐
                 │                Inbound HTTP Request                    │
                 └──────────────────────────┬─────────────────────────────┘
                                            │
                                            ▼
                          ┌───────────────────────────────────┐
                          │    NotificationsController        │
                          │   (Route: /api/notifications)     │
                          └─────────────────┬─────────────────┘
                                            │
                                            ▼
                       ┌────────────────────────────────────────┐
                       │       Severity Level Evaluator         │
                       │     (Check: level >= Warning)          │
                       └────────────┬───────────────────────┬───┘
                                    │ (Level < Warning)     │ (Level >= Warning)
                                    ▼                       ▼
                       ┌──────────────────────┐    ┌───────────────────────────────────┐
                       │ Log & Return 200 OK  │    │      IRateLimiterService          │
                       │   (No forwarding)    │    │ (Sliding Window: max 10/min)      │
                       └──────────────────────┘    └────────┬──────────────────────┬───┘
                                                            │ (Limit exceeded)     │ (Allowed)
                                                            ▼                      ▼
                                                   ┌─────────────────┐   ┌───────────────────────────┐
                                                   │ Return HTTP 429 │   │    ILlmAnalysisService    │
                                                   │  (Retry-After)  │   │  (Google Gemini Service)  │
                                                   └─────────────────┘   └─────────────┬─────────────┘
                                                                                       │
                                                                                       ▼
                                                                         ┌───────────────────────────┐
                                                                         │  INotificationDispatcher  │
                                                                         │ (Discord Webhook Client)  │
                                                                         └─────────────┬─────────────┘
                                                                                       │
                                                                                       ▼
                                                                         ┌───────────────────────────┐
                                                                         │  Discord Channel (Embed)  │
                                                                         └───────────────────────────┘
```

---

## 4. Solution & Directory Structure

```text
DymatrixTask/
│
├── NotifyAiPowered/                        # Web API Project (.NET 9)
│   ├── docs/                               # Project documentation
│   │   ├── project.md                      # High-level project summary
│   │   ├── architecture.md                 # System architecture & component design
│   │   ├── flow.md                         # Sequence diagrams & request lifecycle
│   │   ├── testing.md                      # Payload & integration testing guide
│   │   └── unittest.md                     # Unit test matrix & boundary tests
│   ├── Controllers/
│   │   └── NotificationsController.cs     # Main HTTP controller
│   ├── Models/
│   │   ├── Enums/
│   │   │   └── NotificationLevel.cs       # Info, Warning, Error, Critical
│   │   ├── Requests/
│   │   │   └── NotificationRequest.cs     # Inbound payload schema
│   │   ├── Responses/
│   │   │   └── NotificationResponse.cs    # Output status response
│   │   ├── Llm/
│   │   │   └── LlmAnalysisResult.cs       # Structured AI output model
│   │   └── Discord/
│   │       └── DiscordWebhookPayload.cs   # Discord embed serialization models
│   ├── Services/
│   │   ├── Interfaces/
│   │   │   ├── ILlmAnalysisService.cs     # AI enrichment contract
│   │   │   ├── INotificationDispatcher.cs # Channel dispatch contract
│   │   │   └── IRateLimiterService.cs     # Rate limit verification contract
│   │   └── Implementations/
│   │       ├── GeminiLlmService.cs        # Gemini API client with fallback
│   │       ├── DiscordWebhookDispatcher.cs # Discord HTTP client
│   │       └── SlidingWindowRateLimiterService.cs # 10 msgs/min in-memory limiter
│   ├── Configuration/
│   │   ├── GeminiSettings.cs              # Gemini API keys & model settings
│   │   ├── DiscordSettings.cs             # Discord Webhook URL & identity
│   │   └── RateLimiterSettings.cs         # Rate limiting parameters (10 msgs/60s)
│   ├── appsettings.json                   # Base configuration
│   ├── appsettings.Development.json       # Development overrides
│   ├── Program.cs                         # Application composition & middleware
│   └── NotifyAiPowered.csproj             # Project file and package references
│
├── NotifyAiPowered.Tests.Unit/            # Unit Test Project
│   ├── Controllers/
│   └── Services/
│
└── NotifyAiPowered.Tests.Integration/     # Integration Test Project
    ├── CustomWebApplicationFactory.cs
    └── NotificationsApiTests.cs
```

---

## 5. Component Breakdown

### 5.1. Notifications Controller (`NotificationsController`)
- Handles incoming HTTP `POST /api/notifications` requests.
- Validates model constraints (non-empty source, valid severity level, title, and message).
- Coordinates execution between the severity checker, rate limiter, LLM service, and external dispatcher.

### 5.2. Rate Limiting Service (`IRateLimiterService`)
- Enforces the business constraint: **at most 10 forwarded messages per minute**.
- Utilizes `System.Threading.RateLimiting.SlidingWindowRateLimiter`:
  - `PermitLimit = 10`
  - `Window = TimeSpan.FromMinutes(1)`
  - `SegmentsPerWindow = 6` (for smooth 10-second rolling transitions)
- When a lease cannot be acquired, returns a metadata result containing the calculated wait time for the `Retry-After` header.

### 5.3. AI Incident Analysis Service (`ILlmAnalysisService`)
- Communicates with **Google Gemini API** (`gemini-3.8-flash`).
- Sends notification context using structured instructions to produce a JSON response containing:
  - `IncidentType`: Category of error (e.g. Infrastructure, Database, Authentication, Network).
  - `Summary`: Human-readable 1-2 sentence description.
  - `ActionRequired`: Recommended immediate remediation step.
- **Resilience Fallback:** If the Gemini API key is missing or the external API call fails, the service falls back to a deterministic template analyzer so that no operational alert is lost.

### 5.4. External Dispatcher Service (`INotificationDispatcher`)
- Builds and posts JSON payloads to a configured **Discord Webhook**.
- Formats messages using Discord **Embeds**:
  - Color borders:
    - `Warning`: `#F59E0B` (Amber)
    - `Error`: `#EF4444` (Red)
    - `Critical`: `#7F1D1D` (Dark Crimson)
  - Fields for Source, Severity, Classification, AI Summary, Recommended Action, and Timestamp.

---

## 6. Dependency Injection & Service Lifecycles

| Interface | Implementation | Lifecycle | Rationale |
| :--- | :--- | :--- | :--- |
| `IRateLimiterService` | `SlidingWindowRateLimiterService` | **Singleton** | Maintains persistent sliding window state across concurrent HTTP requests. |
| `ILlmAnalysisService` | `GeminiLlmService` | **Scoped / Transient** | Consumes configured `HttpClient` factory. |
| `INotificationDispatcher` | `DiscordWebhookDispatcher` | **Scoped / Transient** | Consumes configured `HttpClient` factory. |
| `HttpClient` (Gemini) | Typed/Named Client | **Singleton pool** | Reuses sockets via `IHttpClientFactory`. |
| `HttpClient` (Discord) | Typed/Named Client | **Singleton pool** | Reuses sockets via `IHttpClientFactory`. |

---

## 7. Configuration Architecture

Configuration is bound via the **Options Pattern** (`IOptions<T>`):
```json
{
  "RateLimiting": {
    "PermitLimit": 10,
    "WindowMinutes": 1
  },
  "Gemini": {
    "ApiKey": "",
    "Model": "gemini-3.8-flash",
    "TimeoutSeconds": 10
  },
  "Discord": {
    "WebhookUrl": "",
    "BotUsername": "NotifyAI Incident Bot"
  }
}
```
All secrets (e.g., `Gemini:ApiKey`, `Discord:WebhookUrl`) can be overridden seamlessly via environment variables (`Gemini__ApiKey`, `Discord__WebhookUrl`).

