# NotifyAiPowered - AI-Enriched Incident Notification Service

[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/Tests-21%20Passed-brightgreen.svg)]()
[![License](https://img.shields.io/badge/License-MIT-blue.svg)]()

**NotifyAiPowered** is an intelligent, high-resilience notification gateway built on **ASP.NET Core 9 (.NET 9 / C# 13)**. It receives system events, filters out low-severity noise, enforces a strict sliding-window rate limit, enriches incidents with **Google Gemini AI**, and delivers color-coded, actionable incident cards to **Discord** via webhooks.

---

## Table of Contents

1. [Core Features & System Highlights](#core-features--system-highlights)
2. [End-to-End System Workflow](#end-to-end-system-workflow)
3. [Local Environment Setup & Configuration](#local-environment-setup--configuration)
4. [Running the Application](#running-the-application)
5. [Testing the API (cURL & PowerShell Samples)](#testing-the-api-curl--powershell-samples)
6. [Testing Strategy (Unit & Integration Tests)](#testing-strategy-unit--integration-tests)
7. [Key Architectural Decisions (Why This Approach?)](#key-architectural-decisions-why-this-approach)
8. [Response DTO Reference](#response-dto-reference)

---

## 1. Core Features & System Highlights

- **4 Severity Levels**: `Info` (0), `Warning` (1), `Error` (2), `Critical` (3).
- **Severity Filtering**: `Info` notifications are acknowledged (`200 OK`) and logged locally, but **never forwarded** to Discord. This prevents developer alert fatigue.
- **Sliding Window Rate Limiter**: Strictly enforces a maximum of **10 forwarded alerts per rolling 60-second window**. Excess requests return `429 Too Many Requests` with a standard `Retry-After: <seconds>` HTTP header.
- **AI Incident Enrichment**: Powered by Google Gemini (`gemini-3.5-flash-lite`), each forwarded alert is enriched with:
  - An **Incident Type** classification (e.g., `Payment Service Failure`).
  - A **1-Sentence Executive Summary** explaining business/user impact.
  - A **Recommended Action** for the on-call engineer (e.g., triage steps, logs to check).
- **Zero-Downtime Fallback Pattern**: If Google Gemini is unreachable, rate-limited, or times out, the service **never fails or drops an alert**. It automatically falls back to a rule-based summary (`IsFallback = true`).
- **Rich Discord Embeds**: Alerts are delivered with color-coded severity borders:
  - **Warning**: Amber (`#F59E0B`)
  - **Error**: Red (`#EF4444`)
  - **Critical**: Dark Crimson (`#7F1D1D`)
- **100% Automated Test Coverage**: 21 automated tests (17 Unit Tests + 4 Full Integration Tests with `WebApplicationFactory`).

---

## 2. End-to-End System Workflow

```
[Inbound HTTP POST]
        │
        ▼
1. NotificationsController
        │
        ├─► [Severity == Info?] ──► YES ──► Returns 200 OK (Logged locally, never forwarded)
        │
        ▼ NO (Warning, Error, Critical)
2. IRateLimiterService (Sliding Window: 10 / min)
        │
        ├─► [Quota Exceeded?] ───► YES ──► Returns 429 Too Many Requests (+ Retry-After header)
        │
        ▼ NO (Permit Granted)
3. ILlmAnalysisService (Google Gemini AI)
        │
        ├─► Calls Gemini (gemini-3.5-flash-lite) for summary & triage steps
        └─► [On Timeout / Exception] ────► Local Rule-Based Fallback Generated
        │
        ▼
4. INotificationDispatcher (Discord Webhook)
        │
        ├─► Maps severity to Discord hex color
        ├─► Assembles Discord embed card
        └─► Posts payload to Discord Webhook
        │
        ▼
5. Response Returned to Client
        └─► Returns 200 OK with full AI enrichment payload
```

---

## 3. Local Environment Setup & Configuration

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (verify with `dotnet --version`).
- Any modern terminal (PowerShell, Bash, or Command Prompt).
- _(Optional)_ VS Code or Visual Studio 2022 / 2025.

### Configuration (`appsettings.json` or `appsettings.Development.json`)

To run the application locally, configure the following settings in `NotifyAiPowered/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "RateLimiter": {
    "PermitLimit": 10,
    "WindowSeconds": 60,
    "SegmentsPerWindow": 6
  },
  "Gemini": {
    "ApiKey": "<YOUR_GEMINI_API_KEY>",
    "Model": "gemini-3.5-flash-lite",
    "Endpoint": "https://generativelanguage.googleapis.com",
    "TimeoutSeconds": 60
  },
  "Discord": {
    "WebhookUrl": "<YOUR_DISCORD_WEBHOOK_URL>",
    "BotUsername": "NotifyAI Incident Bot",
    "TimeoutSeconds": 10
  }
}
```

### Where to Obtain Keys:

1. **Google Gemini API Key**:
   - Go to [Google AI Studio](https://aistudio.google.com/).
   - Click **Get API key** and create a free key.
   - Paste it into `"Gemini": { "ApiKey": "..." }`.
   - _(Note: If left empty, the application will automatically activate its local fallback generator without failing)._
2. **Discord Webhook URL**:
   - In Discord, go to **Server Settings** > **Integrations** > **Webhooks**.
   - Click **New Webhook**, select your alert channel, and click **Copy Webhook URL**.
   - Paste it into `"Discord": { "WebhookUrl": "..." }`.
   - _(Note: If left empty, the application skips Discord dispatch and returns a success response)._

---

## 4. Running the Application

### Option A: Using the .NET CLI

Open a terminal in the solution directory and run:

```powershell
dotnet run --project .\NotifyAiPowered
```

The application will start listening on:

- **HTTP:** `http://localhost:5052`
- **HTTPS:** `https://localhost:7233`
- **Swagger UI:** Automatically available at `http://localhost:5052/swagger`

---

### Option B: Running from VS Code

1. Open the project folder in VS Code.
2. Press **`F5`** (Start Debugging) or **`Ctrl + F5`** (Run Without Debugging).
3. VS Code builds the project, launches the server, and **automatically opens your default browser directly to the interactive Swagger UI**.

---

## 5. Testing the API (cURL & PowerShell Samples)

### Endpoint Details

- **Method:** `POST`
- **Path:** `/api/notifications` _(or `/api/notifications/SendNotification`)_
- **Headers:** `Content-Type: application/json`

---

### Sample 1: Elevated Error (Enriched by AI & Sent to Discord)

#### cURL:

```bash
curl -X 'POST' \
  'http://localhost:5052/api/notifications/SendNotification' \
  -H 'Content-Type: application/json' \
  -d '{
    "level": "Error",
    "source": "Payment Service",
    "title": "Stripe Gateway Timeout",
    "message": "Payment service is failing to complete charges. Receiving HTTP 500 from Stripe API.",
    "timestamp": "2026-11-14T10:00:00Z"
  }'
```

#### PowerShell:

```powershell
Invoke-RestMethod -Uri "http://localhost:5052/api/notifications/SendNotification" `
  -Method POST `
  -ContentType "application/json" `
  -Body '{
    "level": "Error",
    "source": "Payment Service",
    "title": "Stripe Gateway Timeout",
    "message": "Payment service is failing to complete charges. Receiving HTTP 500 from Stripe API.",
    "timestamp": "2026-11-14T10:00:00Z"
  }'
```

#### Expected 200 OK Response:

```json
{
  "status": "Forwarded",
  "forwarded": true,
  "level": "Error",
  "message": "Notification enriched with AI and delivered to Discord successfully.",
  "incidentType": "Payment Service Failure",
  "generatedMessage": "The payment service is receiving 500 errors from the Stripe API, causing user transactions to fail.",
  "recommendedAction": "Verify Stripe API status, check gateway timeout thresholds, and inspect transaction error logs.",
  "retryAfterSeconds": null,
  "timestamp": "2026-11-14T10:00:01.428Z"
}
```

---

### Sample 2: Info Alert (Filtered & Acknowledged, Never Forwarded)

#### cURL:

```bash
curl -X 'POST' \
  'http://localhost:5052/api/notifications/SendNotification' \
  -H 'Content-Type: application/json' \
  -d '{
    "level": "Info",
    "source": "Inventory Service",
    "title": "Catalog Sync",
    "message": "Routine daily catalog sync completed successfully.",
    "timestamp": "2026-11-14T10:00:00Z"
  }'
```

#### Expected 200 OK Response:

```json
{
  "status": "Processed",
  "forwarded": false,
  "level": "Info",
  "message": "Notification level is below forwarding threshold (Warning). Not forwarded.",
  "incidentType": null,
  "generatedMessage": null,
  "recommendedAction": null,
  "retryAfterSeconds": null,
  "timestamp": "2026-11-14T10:00:00.012Z"
}
```

---

### Sample 3: Rate Limiting in Action (Exceeding 10 messages/minute)

If you send 11 `Warning` or `Error` requests within 60 seconds:

- **HTTP Status Code:** `429 Too Many Requests`
- **Response Header:** `Retry-After: 48`

#### Expected 429 Response:

```json
{
  "status": "RateLimited",
  "forwarded": false,
  "level": "Warning",
  "message": "Forwarding rate limit exceeded. A maximum of 10 forwarded notifications per minute is allowed.",
  "incidentType": null,
  "generatedMessage": null,
  "recommendedAction": null,
  "retryAfterSeconds": 48,
  "timestamp": "2026-11-14T10:00:15.118Z"
}
```

---

## 6. Testing Strategy (Unit & Integration Tests)

The solution contains **21 automated tests** verifying every requirement, edge case, and failure mode.

```
NotifyAiPowered.sln
 ├── NotifyAiPowered                    (Main Web API)
 ├── NotifyAiPowered.Tests.Unit         (17 Unit Tests - 80ms execution)
 └── NotifyAiPowered.Tests.Integration  (4 Integration Tests - WebApplicationFactory)
```

### Run All Tests:

```powershell
dotnet test .\NotifyAiPowered.sln --logger "console;verbosity=normal"
```

### Run ONLY Unit Tests:

```powershell
dotnet test .\NotifyAiPowered.Tests.Unit
```

### Run ONLY Integration Tests:

```powershell
dotnet test .\NotifyAiPowered.Tests.Integration
```

---

### What the Unit Tests Cover (17 Tests)

- **Rate Limiter (`RateLimiterServiceTests.cs`)**:
  - First 10 calls succeed.
  - 11th call is rejected with `RetryAfterSeconds > 0`.
  - Concurrent multithreaded requests strictly enforce the 10-permit limit without race conditions.
- **Gemini AI Service (`GeminiLlmServiceTests.cs`)**:
  - Parses structured JSON responses correctly.
  - Strips markdown fences (\`\`\`json ... \`\`\`) if the model wraps JSON in code blocks.
  - Gracefully activates local fallback when HTTP 500 or network timeouts occur.
  - Gracefully activates fallback when API key is unconfigured.
- **Discord Dispatcher (`DiscordWebhookDispatcherTests.cs`)**:
  - Sets exact decimal colors: Amber (`16098851`) for Warning, Red (`15682852`) for Error, Dark Crimson (`8330525`) for Critical.
  - Returns `false` safely without calling the network when webhook URL is blank.
- **Controller (`NotificationsControllerTests.cs`)**:
  - Confirms `Info` severity exits with 200 without forwarding.
  - Confirms `Warning`, `Error`, and `Critical` request rate-limit permits, trigger AI enrichment, and forward to Discord.

---

### What the Integration Tests Cover (4 Tests)

Using `WebApplicationFactory<Program>`, the integration tests boot the entire ASP.NET Core web server in memory:

1. `Post_InfoNotification_Returns200_NotForwarded`: Verifies routing, JSON binding, and filtering.
2. `Post_WarningNotification_Returns200_ForwardedWithAiEnrichment`: Verifies end-to-end forward pipeline and response contract.
3. `Post_InvalidPayload_Returns400BadRequest`: Verifies ASP.NET Core validation filters (missing required fields return `400`).
4. `Post_ExceedingRateLimit_Returns429WithRetryAfterHeader`: Sends 11 requests in rapid succession; verifies the 11th returns `429` with the `Retry-After` header.

---

## 7. Response DTO Reference

Every response from `POST /api/notifications` follows a consistent schema:

| Property                | Type             | Description                                                            |
| :---------------------- | :--------------- | :--------------------------------------------------------------------- |
| **`status`**            | `string`         | Categorical status (`"Forwarded"`, `"Processed"`, or `"RateLimited"`). |
| **`forwarded`**         | `boolean`        | `true` if delivered to Discord; `false` if filtered or rate-limited.   |
| **`level`**             | `string`         | The severity level (`"Info"`, `"Warning"`, `"Error"`, `"Critical"`).   |
| **`message`**           | `string`         | Human-readable system explanation of the processing outcome.           |
| **`incidentType`**      | `string?`        | AI-classified problem category (e.g., `"Payment Service Failure"`).    |
| **`generatedMessage`**  | `string?`        | AI-generated 1-sentence executive summary.                             |
| **`recommendedAction`** | `string?`        | Actionable next step recommended by Gemini AI.                         |
| **`retryAfterSeconds`** | `int?`           | Seconds until quota replenishes (present only on HTTP 429).            |
| **`timestamp`**         | `DateTimeOffset` | High-precision UTC timestamp of completion.                            |
