# MESA WhatsApp Business Automation

Production-ready WhatsApp Business outreach service for **MESA – Operating System for Restaurants** (UB Nexora Technologies), using the official **Meta WhatsApp Cloud API** (Graph API v20+).

No WhatsApp Web automation. No unofficial libraries.

## Folder structure

```
Mesa.WhatsApp.sln
├── src/
│   ├── Mesa.WhatsApp.Api/             # ASP.NET Core Web API, controllers, JWT, rate limits
│   ├── Mesa.WhatsApp.Application/     # Interfaces, options, content rules, keywords
│   ├── Mesa.WhatsApp.Domain/          # Entities, enums, PhoneNumber value object
│   └── Mesa.WhatsApp.Infrastructure/  # EF Core, WhatsApp client, Hangfire, services
├── tests/Mesa.WhatsApp.Tests/         # xUnit: signature, opt-out, pacing, auto-pause
├── admin-ui/                          # React + Vite admin console
├── docs/sample-leads.csv
├── postman/Mesa.WhatsApp.postman_collection.json
├── Dockerfile
├── docker-compose.yml
└── .env.example
```

## Data model (summary)

| Entity | Purpose |
|--------|---------|
| `Lead` | Business contact (E.164 phone, segment, status, opt-out/blocked flags) |
| `DoNotContactEntry` / `BlockedNumber` | Always skipped by campaigns |
| `PriorityRule` | Configurable priority (e.g. pincode `431001` first) |
| `MessageTemplate` + `TemplateVariable` | Approved Meta templates + `{{n}}` mappings |
| `SegmentMedia` | Per-segment image/PDF with cached WhatsApp media IDs |
| `Campaign` + `CampaignRecipient` | Schedule window, daily cap, pacing, idempotent sends |
| `ConversationMessage` | Full inbox history |
| `DemoBooking` | Pending human-approved demo slots (15:00–18:00 IST) |
| `AppUser` | JWT roles: Owner, Manager, SalesAgent, Viewer |
| `AuditLog` | Every send and admin action |

## Prerequisites

- .NET 8 SDK
- Node.js 20+ (admin UI)
- Meta Business / WhatsApp Cloud API app with a permanent system-user token
- Optional: SQL Server (SQLite is default for local/dev)
- ngrok (local webhook testing)

## Configuration

Copy `.env.example` and set values. Secrets are read from environment variables / `appsettings` and are **never logged**.

| Variable | Description |
|----------|-------------|
| `WHATSAPP_BUSINESS_ACCOUNT_ID` | WABA ID |
| `WHATSAPP_PHONE_NUMBER_ID` | Sending phone number ID |
| `WHATSAPP_ACCESS_TOKEN` | Permanent system-user token |
| `WHATSAPP_APP_SECRET` | App secret (webhook HMAC) |
| `WHATSAPP_VERIFY_TOKEN` | Webhook verify token you choose |
| `GRAPH_API_VERSION` | e.g. `v20.0` |
| `DEFAULT_LANGUAGE` | e.g. `en` |
| `JWT_SIGNING_KEY` | ≥ 32 characters |

## Meta / WABA setup

1. Create a Meta app → add **WhatsApp** product.
2. Attach a WhatsApp Business Account and a phone number.
3. Create a **System User**, grant WhatsApp permissions, generate a **permanent** token.
4. Note `WABA ID`, `Phone number ID`, App Secret.
5. Configure webhook:
   - Callback URL: `https://<public-host>/api/webhooks/whatsapp`
   - Verify token: same as `WHATSAPP_VERIFY_TOKEN`
   - Subscribe to `messages` (includes statuses).
6. For local testing: `ngrok http 5080` and use the HTTPS URL as the callback.

## Creating & submitting templates

In Meta Business Manager → WhatsApp → Message templates, create marketing/utility templates that match MESA content rules:

- Open with `Dear {{1}} team,` (map `{{1}}` to business name)
- Include: `Scan the QR or tap NFC, no app needed`
- Close with `Let's connect!` then `Team UB Nexora Technologies | +91 8855008116`
- Company name appears **once** (sign-off only)
- **No prices**
- Optional HEADER: IMAGE or DOCUMENT (segment media uploaded via API)

Seeded local names: `mesa_outreach_v1`, `mesa_followup_v1`. After Meta approval, run `POST /api/templates/sync`.

## Local run

```bash
# API
export PATH="$HOME/.dotnet:$PATH"
cp .env.example .env   # fill values
dotnet restore
dotnet run --project src/Mesa.WhatsApp.Api --launch-profile http
# Swagger: http://localhost:5080/swagger
# Hangfire: http://localhost:5080/hangfire

# Admin UI
cd admin-ui && npm install && npm run dev
# http://localhost:5173  (proxies /api → :5080)
```

### Seeded users

| Email | Password | Role |
|-------|----------|------|
| owner@ubnexora.local | ChangeMe!Owner1 | Owner |
| manager@ubnexora.local | ChangeMe!Manager1 | Manager |
| sales@ubnexora.local | ChangeMe!Sales1 | SalesAgent |
| viewer@ubnexora.local | ChangeMe!Viewer1 | Viewer |

## Docker

```bash
cp .env.example .env   # fill secrets
docker compose up --build
# API :8080  ·  Admin UI :5173
```

## Features implemented

1. **Leads** — CSV import, E.164 normalisation, dedupe, DNC/blocked lists, priority rules  
2. **Templates** — local registry, Meta sync, segment media upload + media ID cache  
3. **Campaigns** — segments, IST window (Mon–Sat 11:00–18:00 default), daily cap, pacing+jitter, idempotent sends, tier/quality auto-pause (Yellow/Red, 131048/131056/130429, block-rate threshold), transient retries  
4. **Follow-ups** — after N days, max 2, stop on reply/opt-out  
5. **Webhooks** — GET verify, POST `X-Hub-Signature-256`, statuses + inbound, STOP → DNC + confirmation, interest → hot lead + demo slot list, slot pick → pending booking  
6. **Inbox** — history, manual reply inside 24h window, human handover flag  
7. **Dashboard** — JWT roles, stats, CSV/XLSX export, React admin UI  
8. **Security** — env secrets, signature validation, rate limiting, audit log, masked phones in logs  

## Tests

```bash
dotnet test
```

Covers webhook signature validation, opt-out handling, pacing/window logic, and auto-pause.

## Postman

Import `postman/Mesa.WhatsApp.postman_collection.json`. Login first to populate `token`.

## Licence / product

MESA is a product of **UB Nexora Technologies**. Contact: **+91 8855008116**.
