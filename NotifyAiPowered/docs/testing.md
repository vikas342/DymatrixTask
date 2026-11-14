# Endpoint & Payload Testing Guide: NotifyAiPowered

## 1. Overview
This guide provides complete payload specifications, endpoint testing instructions, expected responses, and test verification procedures for the **NotifyAiPowered** HTTP API based on the implementation documented in `Implementation.md`.

---

## 2. API Endpoints

| Method | Endpoint | Description | Expected Status Codes |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/notifications` | Ingests a notification event, verifies severity, rate limits, enriches via AI, and conditionally dispatches to Discord | `200 OK`, `400 Bad Request`, `429 Too Many Requests` |
| `GET` | `/swagger` | Interactive OpenAPI / Swagger UI | `200 OK` |

---

## 3. Request & Response Payload Specifications

### 3.1 Inbound Data Contracts

#### NotificationRequest (`POST /api/notifications`)
| Field | Type | Required | Description |
| :--- | :--- | :--- | :--- |
| `source` | `string` | **Yes** | Originating service name (e.g., `"PaymentGateway"`, `"DatabaseCluster"`) |
| `level` | `string` (enum) | **Yes** | Severity: `"Info"`, `"Warning"`, `"Error"`, or `"Critical"` |
| `title` | `string` | **Yes** | Short headline / subject of the incident |
| `message` | `string` | **Yes** | Detailed description of the error / event |
| `timestamp` | `string` (ISO 8601) | No | Event timestamp (defaults to current UTC time if omitted) |
| `metadata` | `object` | No | Arbitrary key-value diagnostics |

#### NotificationResponse (Returned to Client)
| Field | Type | Description |
| :--- | :--- | :--- |
| `status` | `string` | `"Processed"`, `"Forwarded"`, or `"RateLimited"` |
| `forwarded` | `boolean` | `true` if dispatched to Discord; `false` otherwise |
| `level` | `string` | Severity level of the notification |
| `incidentType` | `string?` | AI-classified category of the issue |
| `generatedMessage`| `string?` | AI-generated executive summary |
| `recommendedAction`| `string?`| AI-recommended immediate remediation step |
| `retryAfterSeconds`| `int?` | Seconds to wait when rate limited (status 429) |
| `timestamp` | `string` | Processing completion timestamp (UTC) |

---

## 4. Test Scenarios with Exact Payloads

### Scenario 1: Low Severity (`Info`) — Acknowledged, Not Forwarded
- **Behavior:** Logged for audit purposes. Does **not** consume rate limit permits; does **not** call Gemini or Discord.
- **Expected Status:** `200 OK`

#### Request Payload
```json
{
  "source": "OrderService",
  "level": "Info",
  "title": "Order #54210 Processed",
  "message": "Order #54210 was successfully placed and inventory deducted.",
  "metadata": {
    "orderId": "54210",
    "amount": 250.00
  }
}
```

#### Expected Response (`200 OK`)
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
  "timestamp": "2026-11-12T17:15:00.123Z"
}
```

---

### Scenario 2: Warning Severity (`Warning`) — Enriched & Forwarded
- **Behavior:** Consumes 1 rate limit permit. Enriched with Google Gemini AI analysis and dispatched to Discord as an **Amber** (`#F59E0B` / `16098851`) rich embed.
- **Expected Status:** `200 OK`

#### Request Payload
```json
{
  "source": "PaymentGateway",
  "level": "Warning",
  "title": "Stripe Gateway Elevated Latency",
  "message": "Average latency increased to 3200ms across 12 consecutive requests in us-east-1.",
  "metadata": {
    "provider": "Stripe",
    "avgLatencyMs": 3200,
    "environment": "production"
  }
}
```

#### Expected Response (`200 OK`)
```json
{
  "status": "Forwarded",
  "forwarded": true,
  "level": "Warning",
  "message": null,
  "incidentType": "Third-Party Service Degradation",
  "generatedMessage": "Stripe payment gateway latency is abnormally high (3200ms), which may slow checkout transactions.",
  "recommendedAction": "Monitor upstream Stripe status and verify timeout/retry configurations.",
  "retryAfterSeconds": null,
  "timestamp": "2026-11-12T17:15:01.450Z"
}
```

---

### Scenario 3: Error Severity (`Error`) — Enriched & Forwarded
- **Behavior:** Consumes 1 rate limit permit. Enriched with AI analysis and dispatched to Discord as a **Red** (`#EF4444` / `15682852`) rich embed.
- **Expected Status:** `200 OK`

#### Request Payload
```json
{
  "source": "AuthService",
  "level": "Error",
  "title": "JWT Signature Verification Failure",
  "message": "Public key rotation synchronization failed; 45 incoming requests rejected with 401 Unauthorized.",
  "metadata": {
    "issuer": "https://auth.company.com",
    "failedAttempts": 45
  }
}
```

#### Expected Response (`200 OK`)
```json
{
  "status": "Forwarded",
  "forwarded": true,
  "level": "Error",
  "message": null,
  "incidentType": "Authentication Key Mismatch",
  "generatedMessage": "AuthService failed to sync rotated JWT public keys, causing valid user requests to be rejected.",
  "recommendedAction": "Force reload public key cache from the Identity Provider JWKS endpoint.",
  "retryAfterSeconds": null,
  "timestamp": "2026-11-12T17:15:02.100Z"
}
```

---

### Scenario 4: Critical Severity (`Critical`) — Enriched & Forwarded
- **Behavior:** Consumes 1 rate limit permit. Enriched with AI analysis and dispatched to Discord as a **Dark Crimson** (`#7F1D1D` / `8330525`) rich embed.
- **Expected Status:** `200 OK`

#### Request Payload
```json
{
  "source": "DatabaseCluster",
  "level": "Critical",
  "title": "Postgres Primary Connection Pool Exhausted",
  "message": "Active connections reached 500/500 max limit. Incoming transactions are timing out with 504.",
  "metadata": {
    "cluster": "pg-primary-prod",
    "activeConnections": 500,
    "maxConnections": 500
  }
}
```

#### Expected Response (`200 OK`)
```json
{
  "status": "Forwarded",
  "forwarded": true,
  "level": "Critical",
  "message": null,
  "incidentType": "Database Connection Exhaustion",
  "generatedMessage": "Primary Postgres database connection pool reached 100% capacity (500/500), blocking transactions.",
  "recommendedAction": "Terminate idle client sessions immediately and increase max connections pool limit.",
  "retryAfterSeconds": null,
  "timestamp": "2026-11-12T17:15:03.210Z"
}
```

---

### Scenario 5: Rate Limit Exceeded (> 10 Forwarded Messages / Minute)
- **Behavior:** When an 11th elevated notification (`Warning`, `Error`, `Critical`) arrives within the rolling 60-second window, the limiter rejects the request. External calls (Gemini and Discord) are skipped to protect downstream systems.
- **Expected Status:** `429 Too Many Requests`
- **Expected Header:** `Retry-After: <seconds>`

#### Request Payload
```json
{
  "source": "BillingWorker",
  "level": "Warning",
  "title": "Rapid Rate Limit Test Alert",
  "message": "Simulated excessive alert."
}
```

#### Expected Response Headers
```http
HTTP/1.1 429 Too Many Requests
Content-Type: application/json
Retry-After: 48
```

#### Expected Response Body (`429 Too Many Requests`)
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
  "timestamp": "2026-11-12T17:15:05.000Z"
}
```

---

### Scenario 6: Schema Validation Failure (Invalid Payload)
- **Behavior:** Missing required fields (`source`, `title`, or `message`).
- **Expected Status:** `400 Bad Request`

#### Request Payload
```json
{
  "source": "",
  "level": "Warning"
}
```

#### Expected Response (`400 Bad Request`)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Source": ["The Source field is required."],
    "Title": ["The Title field is required."],
    "Message": ["The Message field is required."]
  }
}
```

---

## 5. Downstream Payload References

### 5.1 Outbound Discord Webhook Payload
Sent to Discord as a rich embed with color coding:
```json
{
  "username": "NotifyAI Incident Bot",
  "embeds": [
    {
      "title": "⚠️ [Warning] PaymentGateway: Stripe Gateway Elevated Latency",
      "description": "Stripe payment gateway latency is abnormally high (3200ms), which may slow checkout transactions.",
      "color": 16098851,
      "timestamp": "2026-11-12T17:15:01.450Z",
      "footer": {
        "text": "NotifyAiPowered • AI Incident Gateway"
      },
      "fields": [
        { "name": "Source", "value": "PaymentGateway", "inline": true },
        { "name": "Severity", "value": "Warning", "inline": true },
        { "name": "Incident Classification", "value": "Third-Party Service Degradation", "inline": false },
        { "name": "Recommended Action", "value": "Monitor upstream Stripe status and verify timeout/retry configurations.", "inline": false },
        { "name": "Metadata", "value": "`provider: Stripe, avgLatencyMs: 3200, environment: production`", "inline": false }
      ]
    }
  ]
}
```

### 5.2 Outbound Google Gemini Request & Response

#### Gemini Request
```json
{
  "contents": [
    {
      "parts": [
        {
          "text": "You are an expert Site Reliability Engineer...\nSource: PaymentGateway\nSeverity: Warning\nTitle: Stripe Gateway Elevated Latency\nMessage: Average latency increased to 3200ms..."
        }
      ]
    }
  ],
  "generationConfig": {
    "responseMimeType": "application/json",
    "temperature": 0.2
  }
}
```

#### Gemini Response (Parsed into LlmAnalysisResult)
```json
{
  "incidentType": "Third-Party Service Degradation",
  "summary": "Stripe payment gateway latency is abnormally high (3200ms), which may slow checkout transactions.",
  "actionRequired": "Monitor upstream Stripe status and verify timeout/retry configurations."
}
```

---

## 6. Manual Testing Commands

### cURL
```bash
# Test Warning Notification (Forwarded)
curl -X POST http://localhost:5052/api/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "source": "PaymentGateway",
    "level": "Warning",
    "title": "Stripe Gateway Elevated Latency",
    "message": "Average latency increased to 3200ms across 12 consecutive requests."
  }'

# Test Info Notification (Not Forwarded)
curl -X POST http://localhost:5052/api/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "source": "OrderService",
    "level": "Info",
    "title": "Order Processed",
    "message": "Order #54210 completed."
  }'
```

### PowerShell (`Invoke-RestMethod`)
```powershell
# Send Warning Notification
Invoke-RestMethod -Uri "http://localhost:5052/api/notifications" `
  -Method Post `
  -ContentType "application/json" `
  -Body (@{
    source = "PaymentGateway"
    level = "Warning"
    title = "Stripe Gateway Elevated Latency"
    message = "Average latency increased to 3200ms."
  } | ConvertTo-Json)

# Test Rate Limiting (Sends 12 requests in a loop)
1..12 | ForEach-Object {
  try {
    $res = Invoke-RestMethod -Uri "http://localhost:5052/api/notifications" -Method Post -ContentType "application/json" -Body (@{
      source = "ProbeService"
      level = "Warning"
      title = "Probe $_"
      message = "Testing rate limit"
    } | ConvertTo-Json)
    Write-Host "[$_] Status: $($res.status), Forwarded: $($res.forwarded)" -ForegroundColor Green
  } catch {
    Write-Host "[$_] HTTP Status: $($_.Exception.Response.StatusCode.value__) (Rate Limited)" -ForegroundColor Yellow
  }
}
```

---

## 7. Automated Test Suite Mapping

The implementation verified against these payloads with 100% passing tests:

| Project | Test File | Verified Scenarios |
| :--- | :--- | :--- |
| **`NotifyAiPowered.Tests.Integration`** | `NotificationsApiTests.cs` | `Post_InfoNotification_Returns200_NotForwarded`<br>`Post_WarningNotification_Returns200_ForwardedWithAiEnrichment`<br>`Post_InvalidPayload_Returns400BadRequest`<br>`Post_ExceedingRateLimit_Returns429WithRetryAfterHeader` |
| **`NotifyAiPowered.Tests.Unit`** | `NotificationsControllerTests.cs` | Routing, Status code mapping (200, 400, 429), and `Retry-After` header |
| **`NotifyAiPowered.Tests.Unit`** | `RateLimiterServiceTests.cs` | 10 permit boundary check, 11th rejection, and 30-thread concurrency |
| **`NotifyAiPowered.Tests.Unit`** | `GeminiLlmServiceTests.cs` | JSON prompt generation, markdown stripping, and resilient fallback |
| **`NotifyAiPowered.Tests.Unit`** | `DiscordWebhookDispatcherTests.cs` | Embed color mapping (`Warning` -> 16098851, `Error` -> 15682852, `Critical` -> 8330525) |
