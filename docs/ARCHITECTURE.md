# Architecture & data model

## Clean architecture layers

```
Api  →  Application  →  Domain
 ↑           ↑
 └── Infrastructure ──┘
```

- **Domain**: entities, enums, `PhoneNumber` (E.164 + mask). No infrastructure references.
- **Application**: ports (`IWhatsAppCloudClient`, `ICampaignEngine`, …), options, content rules, keyword helpers.
- **Infrastructure**: EF Core, Meta Graph HTTP client, Hangfire jobs, JWT/BCrypt, CSV/XLSX export.
- **Api**: controllers, Serilog, rate limiting, Swagger, seed on startup.

## Campaign send pipeline

1. `POST /api/campaigns/{id}/start` → enqueue eligible leads (skip DNC/blocked/opt-out; apply priority rules; unique `IdempotencyKey`).
2. Hangfire `campaign-tick` (every minute) picks next queued recipient inside IST window / under daily cap.
3. Schedules `SendRecipientAsync` with delay = pacing + jitter.
4. `ProcessRecipientSendAsync` claims idempotency, validates content rules, attaches segment media, sends approved template only.
5. Webhook statuses update counters; quality/error/opt-out rate may auto-pause.
6. Follow-up job creates next recipient row (max 2) if no inbound reply.

## Content rules (enforced in code)

- No prices (`₹` / `Rs` / `INR` / `$` + digits)
- Opens with `Dear <Business Name> team,`
- Includes `Scan the QR or tap NFC, no app needed`
- Closes with `Let's connect!` + `Team UB Nexora Technologies | +91 8855008116`
- Company name exactly once (sign-off)
