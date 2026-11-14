# NotifyAiPowered - Project Overview

## 1. What Is NotifyAiPowered?
**NotifyAiPowered** is an intelligent notification and incident alert gateway built on **ASP.NET Core (.NET 9)**. It receives event notifications from applications, services, and background jobs over an HTTP API, determines the operational significance of each alert, enhances critical notifications with AI-generated analysis, and dispatches actionable alerts to external communication channels (Discord).

---

## 2. Core Capabilities & What the Project Does

### 1. Centralized HTTP Ingestion
- Exposes a unified HTTP endpoint (`POST /api/notifications`) to accept JSON payloads from distributed systems, microservices, and monitoring agents.
- Ingests structured notification data including `source`, `level`, `title`, `message`, `timestamp`, and arbitrary `metadata`.

### 2. Severity-Based Filtering
- Inspects the `level` of each notification against an operational threshold:
  - **Low Severity (`Info`):** Acknowledged and logged for audit purposes. This does **not** trigger external notifications or AI queries.
  - **Elevated Severity (`Warning`, `Error`, `Critical`):** Trigger the intelligent alert pipeline, forwarding enriched messages to incident responders.

### 3. AI-Powered Incident Enrichment (Google Gemini)
- Instead of forwarding raw error stacks or noisy alerts, the system consults **Google Gemini**.
- Gemini analyzes the context, determines the root category (e.g., Database, Network, Third-Party Outage), and drafts an executive summary with recommended immediate action items.
- If the AI service is unreachable or credentials are not supplied, an automated deterministic fallback ensures critical alerts are never lost.

### 4. Rate-Limiting Protection (Max 10 Messages / Minute)
- Enforces an outbound dispatch limit of **at most 10 forwarded messages per minute** (rolling window).
- Protects downstream notification channels (Discord) from alert fatigue, spamming, and API rate-limit penalties.
- Returns **HTTP 429 (Too Many Requests)** with a `Retry-After` header when this threshold is exceeded.

### 5. External Alert Dispatching (Discord Webhooks)
- Automatically dispatches enriched alerts directly to a configured Discord channel via Webhook.
- Uses Discord rich embeds with color-coded severity borders (Amber for `Warning`, Red for `Error`, Dark Crimson for `Critical`) for immediate visual prioritization.

---

## 3. High-Level Feature Summary

| Feature | Description |
| :--- | :--- |
| **Ingestion Protocol** | HTTP REST (`POST /api/notifications`) |
| **Forwarding Threshold** | `level >= Warning` |
| **AI Enrichment** | Google Gemini API (with resilient offline/mock fallback) |
| **Notification Channel** | Discord Webhooks with Rich Embeds |
| **Rate Limit** | 10 forwarded messages per minute (Sliding Window) |
| **Rate Limit Response** | HTTP 429 Too Many Requests (`Retry-After` header) |
| **Runtime & Language** | .NET 9 Web API in C# 13 |

---

## 4. Documentation Index

For detailed specifications, refer to the dedicated documents:
- [architecture.md](file:///d:/Projects/DymatrixTask/NotifyAiPowered/docs/architecture.md) — System architecture, technology stack, directory structure, and component design.
- [flow.md](file:///d:/Projects/DymatrixTask/NotifyAiPowered/docs/flow.md) — Request lifecycle, sequence diagrams, and end-to-end data flow.
- [testing.md](file:///d:/Projects/DymatrixTask/NotifyAiPowered/docs/testing.md) — Endpoint testing, payload specifications, expected HTTP status codes, and integration tests.
- [unittest.md](file:///d:/Projects/DymatrixTask/NotifyAiPowered/docs/unittest.md) — Unit test plan, boundary cases (e.g., 10 msgs/min), test suites, and mocking strategy.
