# Quorid

**Verified once. Trusted everywhere.**

Quorid is the verified operating system for business trust — a multi-tenant SaaS
platform combining document management, virtual data rooms, compliance engines,
and AI-powered extraction into a single verified business identity layer.

> This repository contains the **Phase 1 Foundation** plus **Module 1 — Capture &
> Extract**, **Module 2 — Identity Vault**, **Module 3 — Document Management**,
> **Module 5 — Sharing & Access**, and **Data Rooms** (6 room types, 4-step builder
> wizard, guest portal with NDA gate + watermarking + view analytics + Q&A), and
> **Module 6 — Compliance Engine**, and **Modules 7–9 — Projects / Vendors /
> Clients** (engagements with document checklists, progress tracking, and an
> external collection portal), and **Module 10 — Proposals & Business
> Development** (Kanban opportunity pipeline, proposal builder with ordered
> document packages, auto-generated cover letters, and win/loss analytics), and
> **Module 11 — Analytics** (a live KPI dashboard, the natural-language "Ask
> Quorid" assistant, and 18 pre-built reports over real tenant data), and
> **Module 4 — Document Creation** (a template library, an offline AI composer
> that drafts from a prompt, an editable studio with an "improve" pass, and
> finalize-to-vault promotion), and **Module 12 — Administration** (user
> management, role & permission editing, organization / entity configuration, an
> onboarding checklist, and billing with plan comparison, usage, and invoices).
> **All twelve feature modules are now implemented.**
> See [Roadmap](#roadmap).

---

## Tech stack

| Layer     | Technology                                   |
| --------- | -------------------------------------------- |
| Backend   | .NET 8 Minimal API (C# 12), Clean Architecture |
| ORM       | Entity Framework Core 8 + Pomelo MySQL       |
| Database  | MySQL 8.0 (UTF-8mb4)                          |
| Auth      | JWT bearer (access 15 min / refresh 7 d), bcrypt |
| Frontend  | React 18 + TypeScript + Vite, SAP Fiori tokens |
| Cache     | Redis 7 (wired in compose; used in later phases) |

## Repository layout

```
quorid/
├── backend/
│   ├── Quorid.sln
│   ├── Directory.Build.props          # shared net8.0 / nullable / implicit usings
│   ├── Dockerfile                     # API image
│   ├── src/
│   │   ├── Quorid.Domain/             # entities, enums, base types (no dependencies)
│   │   ├── Quorid.Application/        # interfaces, security constants, DTOs
│   │   ├── Quorid.Infrastructure/     # EF Core DbContext, JWT, bcrypt, multitenancy
│   │   └── Quorid.Api/                # Minimal API endpoints, middleware, DI wiring
│   └── tests/
│       └── Quorid.Domain.Tests/       # xUnit
├── frontend/                          # React + Vite + TS shell
├── docker-compose.yml                 # MySQL + Redis + Adminer for local dev
└── .github/workflows/ci.yml           # builds & tests backend + frontend
```

## Architecture highlights

- **Clean Architecture**: `Domain → Application → Infrastructure → Api`.
- **Multi-tenancy is enforced, not optional.** Every tenant-scoped entity
  implements `ITenantScoped`; a global EF Core query filter (built dynamically in
  `ApplicationDbContext`) restricts every read to the request's tenant, and a
  save interceptor stamps `TenantId` on insert.
- **Audit log is append-only.** The save interceptor throws if any code attempts
  to modify or delete an `audit_log` row.
- **snake_case schema.** Tables and columns are mapped to snake_case to match the
  spec DDL (`tenant_id`, `file_size_bytes`, `data_rooms`, …).
- **Enums stored as strings**, timestamps in UTC, GUID (CHAR(36)) primary keys.

## Getting started

### 1. Start backing services

```bash
docker compose up -d        # MySQL 8 on :3306, Redis on :6379, Adminer on :8081
```

### 2. Run the API

```bash
cd backend/src/Quorid.Api
dotnet run                  # http://localhost:5080  (Swagger at /swagger)
```

In `Development`, the API creates the schema from the EF model on first run
(`Database:EnsureCreatedOnStartup=true` in `appsettings.Development.json`).

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev                 # http://localhost:5173
```

Register a tenant from the login screen, then explore the Launchpad.

## API (Phase 1)

| Method | Route                | Auth  | Purpose                              |
| ------ | -------------------- | ----- | ------------------------------------ |
| POST   | `/api/auth/register` | anon  | Create tenant + first entity + owner |
| POST   | `/api/auth/login`    | anon  | Return access + refresh tokens       |
| POST   | `/api/auth/refresh`  | anon  | Rotate access token                  |
| GET    | `/api/users/me`      | bearer| Current user profile                 |
| GET    | `/api/documents`     | bearer| List vault documents (paged)         |
| GET    | `/api/documents/{id}`| bearer| Document detail + extracted fields   |
| POST   | `/api/documents/upload`        | bearer| Upload one file → classify + extract |
| POST   | `/api/documents/upload-batch`  | bearer| Upload up to 20 files                 |
| POST   | `/api/documents/{id}/confirm`  | bearer| Confirm/correct extracted fields      |
| POST   | `/api/documents/{id}/reclassify`| bearer| Override AI classification           |
| GET    | `/api/documents/{id}/extraction`| bearer| Extracted fields for a document      |
| PUT    | `/api/documents/{id}`          | bearer| Update metadata + tags               |
| DELETE | `/api/documents/{id}`          | bearer| Soft delete (archive)                |
| GET/POST | `/api/documents/{id}/versions`| bearer| Version history / upload new version |
| POST   | `/api/documents/{id}/versions/{v}/restore` | bearer| Restore a version        |
| GET    | `/api/documents/{id}/activity` | bearer| Document audit trail                 |
| GET    | `/api/documents/expiring`      | bearer| Documents expiring within N days     |
| GET    | `/api/vault`         | bearer| Vault profile: domain cards, tiers, health |
| POST   | `/api/vault/cross-validate` | bearer| Run the cross-validation engine       |
| POST   | `/api/documents/{id}/share`| bearer| Create a share link (8-level permission) |
| GET    | `/api/shares`        | bearer| List shares + view counts            |
| POST   | `/api/shares/{id}/revoke`| bearer| Revoke a share                       |
| GET    | `/api/shares/{id}/analytics`| bearer| Per-share view analytics            |
| GET    | `/api/share/{token}` | anon  | Public tokenized recipient view      |
| GET    | `/api/rooms/room-types`| bearer| 6 room types + folder templates    |
| GET/POST | `/api/rooms`       | bearer| List rooms / create (builder wizard) |
| GET/PUT/DELETE | `/api/rooms/{id}`| bearer| Room detail / settings / delete    |
| POST   | `/api/rooms/{id}/guests`| bearer| Invite guests                       |
| GET    | `/api/rooms/{id}/analytics`| bearer| Guest engagement analytics        |
| GET/PUT | `/api/rooms/{id}/qa`| bearer| List / answer questions              |
| GET    | `/api/guest/room/{token}`| anon | Guest room view (NDA-gated)         |
| POST   | `/api/guest/room/{token}/sign-nda` | anon| Sign the room NDA         |
| POST   | `/api/guest/room/{token}/view` | anon| Log a page view (analytics)     |
| GET/POST | `/api/guest/room/{token}/qa` | anon| Guest Q&A                      |
| GET    | `/api/compliance/overview`| bearer| All frameworks scored for an entity |
| GET    | `/api/compliance/frameworks`| bearer| List frameworks (6 pre-built)     |
| GET    | `/api/compliance/frameworks/{id}/status`| bearer| Gap analysis + score     |
| GET    | `/api/compliance/calendar`| bearer| Compliance renewal deadlines        |
| GET/POST | `/api/engagements`  | bearer| List (by type) / create projects·vendors·clients |
| GET/PUT/DELETE | `/api/engagements/{id}`| bearer| Engagement detail / update / delete |
| POST   | `/api/engagements/{id}/checklist`| bearer| Add checklist items             |
| PUT    | `/api/engagements/{id}/checklist/{itemId}/assign`| bearer| Satisfy an item with a doc |
| POST   | `/api/engagements/{id}/collection-request`| bearer| Mint the portal token      |
| GET    | `/api/portal/{token}`| anon  | Collection portal info (checklist)   |
| POST   | `/api/portal/{token}/upload`| anon| External upload → classify → attach  |
| GET    | `/api/rooms/{id}`    | bearer| Room detail + folders                |
| GET/POST | `/api/proposals`   | bearer| List pipeline / create a proposal    |
| GET    | `/api/proposals/analytics`| bearer| Win rate, pipeline & won value    |
| GET/PUT/DELETE | `/api/proposals/{id}`| bearer| Proposal detail / update / delete |
| POST/PUT | `/api/proposals/{id}/documents`| bearer| Add / reorder the doc package  |
| DELETE | `/api/proposals/{id}/documents/{docId}`| bearer| Remove a document from the package |
| POST   | `/api/proposals/{id}/cover-letter`| bearer| Auto-generate a cover letter     |
| GET    | `/api/analytics/dashboard`| bearer| KPI dashboard + breakdowns + activity |
| POST   | `/api/analytics/ask` | bearer| "Ask Quorid" natural-language query   |
| GET    | `/api/analytics/reports`| bearer| The 18-report catalog             |
| GET    | `/api/analytics/reports/{key}`| bearer| Run one report (columns + rows) |
| GET/POST | `/api/studio/templates`| bearer| Template library (seeded) / add custom |
| GET/POST | `/api/studio/drafts` | bearer| List drafts / generate a new draft   |
| GET/PUT/DELETE | `/api/studio/drafts/{id}`| bearer| Draft detail / edit / delete   |
| POST   | `/api/studio/drafts/{id}/improve`| bearer| AI polish/rewrite pass          |
| POST   | `/api/studio/drafts/{id}/finalize`| bearer| Promote the draft into the vault |
| GET/POST | `/api/admin/users` | bearer| List users / invite a user           |
| PUT    | `/api/admin/users/{id}`| bearer| Change a user's role, status, name  |
| GET/POST | `/api/admin/roles` | bearer| List roles / create a custom role    |
| GET    | `/api/admin/permissions`| bearer| The 15-permission catalog          |
| PUT/DELETE | `/api/admin/roles/{id}`| bearer| Edit permissions / delete role  |
| GET/PUT | `/api/admin/organization`| bearer| Tenant settings                   |
| GET/POST | `/api/admin/entities`| bearer| List / create business entities    |
| PUT    | `/api/admin/entities/{id}`| bearer| Update an entity profile         |
| GET    | `/api/admin/onboarding`| bearer| Setup checklist + completion %      |
| GET    | `/api/admin/billing` | bearer| Plan, usage, invoices                |
| PUT    | `/api/admin/billing/plan`| bearer| Change subscription plan          |
| GET    | `/health`            | anon  | Liveness                             |

## Configuration

Set these in production (environment variables shown; never commit secrets):

| Variable                        | Notes                                        |
| ------------------------------- | -------------------------------------------- |
| `ConnectionStrings__Default`    | MySQL connection string                      |
| `Jwt__SigningKey`               | ≥ 32-byte HMAC key (required)                |
| `Cors__Origins__0`              | Allowed frontend origin(s)                    |
| `Database__MigrateOnStartup`    | `true` in prod → apply EF migrations at boot |
| `Storage__Provider`             | `Local` (dev disk) or `S3`                   |
| `Storage__Bucket` / `Storage__Region` | S3 bucket + AWS region (Provider=S3)   |
| `Storage__AccessKey` / `Storage__SecretKey` | Optional; omit to use the AWS credential chain |
| `Anthropic__ApiKey`             | Set → Claude-backed AI; blank → offline heuristics |
| `VITE_API_URL` (frontend)       | API base URL (default `http://localhost:5080`) |

The committed `appsettings.Development.json` contains a **development-only**
signing key — replace it via env/user-secrets outside local dev.

## Production hardening

The three cross-cutting production concerns are wired and configuration-driven —
each has an offline default and a real adapter that drops in without touching
call sites.

### 1. EF Core migrations

`EnsureCreated` is the dev convenience; production uses EF Core migrations as the
schema source of truth. A design-time factory
(`Quorid.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`) lets the EF
tools build the context without booting the API, so generating the first
migration is one command (needs the .NET 8 SDK — run it locally or in CI):

```bash
cd backend
dotnet tool install --global dotnet-ef
./scripts/add-migration.sh InitialCreate      # wraps `dotnet ef migrations add`
```

Commit the generated `Persistence/Migrations` folder, then set
`Database__MigrateOnStartup=true` — the API runs `db.Database.Migrate()` at boot
(and `Database__EnsureCreatedOnStartup` is ignored when migrations are on).

### 2. Real Claude + S3 adapters

Every AI touchpoint sits behind an interface with an offline heuristic; setting
`Anthropic__ApiKey` registers the Claude-backed adapters instead — Haiku for
classification, Sonnet for extraction, document generation, and "Ask Quorid"
(`AnthropicDocumentAiService`, `AnthropicDocumentGenerator`,
`AnthropicAnalyticsAssistant`, all sharing one `AnthropicChatClient` over
`/v1/messages`). Each falls back to the heuristic if a call fails, so the app
never hard-errors on the AI path. Blob storage is the same shape: `IFileStorage`
is `LocalFileStorage` by default and `S3FileStorage` (AES-256 at rest) when
`Storage__Provider=S3`.

### 3. Permission-policy enforcement

The 15-permission role model (Module 12) is enforced at the API. Each permission
maps to a `perm:<key>` authorization policy; `PermissionAuthorizationHandler`
reads the caller's role permission map and grants or denies. Sensitive admin
routes carry the matching policy — user management (`ManageUsers`), role editing
(`ManagePermissions`), org/entity config (`ConfigureSettings`), and plan changes
(`ManageBilling`). Owner and Admin hold every permission, so existing accounts
are unaffected; a Viewer is correctly refused.

## Deploy to Render

The repo ships a Render **Blueprint** (`render.yaml`) that provisions the whole
stack. The app deploys as a **single web service**: the root `Dockerfile` builds
the React SPA and the .NET API into one image, and the API serves the SPA from
`wwwroot` — so it's one origin, no CORS, no cross-service URL wiring. MySQL 8 runs
as a private service with a persistent disk (Render's managed databases are
Postgres/Key-Value only).

**See [`DEPLOY.md`](DEPLOY.md) for the full click-by-click runbook** (both the
paid all-on-Render path and the free-tier external-MySQL path, with an Aiven /
TiDB Cloud setup and a troubleshooting table). The short version:

**Steps**

1. Push this repo to GitHub (Render deploys from a connected repo).
2. Render Dashboard → **New → Blueprint** → select the repo → **Apply**.
   Render reads `render.yaml` and creates `quorid-mysql` (private) and `quorid`
   (web). It generates the JWT signing key and the MySQL password, and wires the
   DB connection automatically.
3. Open the `quorid` service URL — the SPA loads and `/health` returns `ok`.

**Notes**

- Private services and disks require a **paid instance** (Starter+). For a
  free-tier deploy, use **`render.external-mysql.yaml`** instead — rename it to
  `render.yaml`, and set `ConnectionStrings__Default` in the dashboard to an
  external MySQL (PlanetScale, Aiven, RDS, …). That blueprint runs only the web
  service (free plan), no private service or disk.
- **Deploy on green (optional):** the `deploy` job in
  `.github/workflows/ci.yml` fires a Render **deploy hook** after the backend and
  frontend jobs pass on `main`. To enable it, create a deploy hook (Render →
  service → Settings → **Deploy Hook**), store the URL as the
  `RENDER_DEPLOY_HOOK_URL` GitHub secret, and set `autoDeploy: false` in
  `render.yaml` so you don't double-deploy. Without the secret the job is a no-op.
- First deploy uses `Database__EnsureCreatedOnStartup=true` to build the schema
  from the model. After generating EF migrations
  (`backend/scripts/add-migration.sh`), switch to `Database__MigrateOnStartup=true`.
- To turn on Claude-backed AI, set `Anthropic__ApiKey` on the `quorid` service in
  the dashboard (blank keeps the offline heuristics).
- The port is taken from Render's `PORT`; the DB bootstrap retries a few times so
  a still-booting MySQL settles on the first deploy.

> This session can't deploy for you (no Render account access, and Render pulls
> from GitHub) — the Blueprint makes it a one-click apply on your side.

## Data model (Phase 1)

25 entities across four areas:

- **Core** — tenants, entities, users, roles, groups, group_members, departments
- **Documents** — documents, document_versions, extracted_fields, document_tags,
  document_taxonomy, upload_jobs
- **Rooms** — room_types, room_templates, data_rooms, room_folders,
  room_documents, room_guests, guest_sessions, document_views, qa_questions,
  group_room_access
- **System** — audit_log, notifications

Compliance, approvals, sharing, and verification-rule tables arrive with their
modules in later phases.

## Module 1 — Capture & Extract

Upload a document and the pipeline classifies it (Domain → Category → Type),
extracts a field skeleton, and routes you to a **capture-review** screen with
color-coded confidence bars (green ≥85%, orange 60–84%, red <60%) to confirm or
correct before saving. Files are stored via `IFileStorage`; metadata and the
storage key go to the database.

Two integration points are deliberately behind interfaces so credentials aren't
required to run the flow locally:

- **`IFileStorage`** → `LocalFileStorage` (writes to `storage-data/`). Swap for an
  S3 implementation in production (AES-256 at rest, presigned URLs).
- **`IDocumentAiService`** → `HeuristicDocumentAiService`, an offline,
  deterministic stand-in that classifies from filename keywords and emits the
  expected field set. Replace with a Claude-backed implementation (Haiku for
  classification, Sonnet for extraction) — the endpoints and UI don't change.

## Module 2 — Identity Vault

Every extracted field is aggregated into one profile per entity. The vault
computes each field's **verification tier** (a value seen consistently across 2+
documents is elevated T1 → **T2 Cross-Referenced**; T3/T4 require external and
counter-party verification in later phases), an 8-card **domain view** with
completion percentages, a **cross-validation engine** (EIN / legal-name / revenue
consistency, formation-date logic, insurance expiry & coverage checks), and a
weighted **health score** (0–100) from completeness, tier distribution,
validation pass rate, and expiry.

The aggregation, rules, and scoring are pure functions in
`Quorid.Application/Vault` (unit-tested), driven by `VaultService`. `GET /api/vault`
returns the profile; `POST /api/vault/cross-validate` runs the engine.

## Module 3 — Document Management

Full lifecycle for every document. The **Document Detail** page is a tabbed
object page — **Metadata** (editable title/description/domain/privacy/expiry and
a tag editor), **Extracted Fields**, **Versions**, and **Activity**. **Version
history** creates an immutable version on every upload and supports one-click
**restore** (which creates a new version from the old content, preserving
history). The **File Browser** filters by domain and status with name search, and
an **expiring-soon** strip surfaces documents approaching their expiry date.
Soft delete archives a document and removes it from any rooms — nothing is ever
hard-deleted.

## Module 5 — Sharing & Access

Secure external sharing with the **8-level permission model** (None, Fence View,
View, Encrypted PDF, Print, PDF, Original, Upload). The **share wizard** captures
recipient, permission, expiry, watermark, and view-tracking, and mints a
cryptographically random access token. Recipients open a **tokenized public link**
(`/share/{token}`) — no login — which records a **view** (IP, user agent, time)
and increments the counter; **per-share analytics** list every view. Shares can be
**revoked**, and **Locked** documents are refused (privacy-level enforcement).

> Note: adding entities across modules changes the schema. In dev, `EnsureCreated`
> builds the full current schema on a fresh database — recreate the dev DB (or
> generate an EF migration) after pulling new modules.

## Data Rooms

The headline VDR feature. Six system **room types** (Investor DD, Audit, Tax,
Banking, Legal Discovery, Compliance) ship with default folder templates. The
**4-step builder wizard** (Room Info → Folders & Docs → Access → Review) creates a
room, references vault documents into it (never copied), and invites guests with
per-guest **8-level permissions**.

The **guest portal** is a tokenized, unauthenticated experience: an **NDA gate**
(legal name + agreement, IP/timestamp recorded) precedes access; documents open in
a **watermarked** viewer (guest name/email overlay); every open logs a **page
view**; and a **Q&A** module lets guests ask questions that hosts answer and
optionally publish. Admins get a room detail page with guest management, Q&A
answering, and **engagement analytics** (Cold/Warm/Hot per guest). OTP email
verification and MFA from the full guest flow are stubbed for later (they need
email/TOTP infrastructure); first access is auto-verified.

## Module 6 — Compliance Engine

Framework-based compliance tracking. Six **pre-built frameworks** (OSHA Safety,
SOC 2, PCI DSS, HIPAA, Construction Pre-Qualification, ISO 27001) seed per tenant,
each with requirements defined by accepted document domains and a minimum
verification tier; admins can also create custom frameworks. Scoring is
**real-time and computed live** — the evaluator (a pure, unit-tested function in
`Quorid.Application/Compliance`) matches an entity's documents against each
requirement and derives **present / expired / missing** status and a 0–100 score.
The overview scores every framework at once; framework detail is a **gap
analysis** showing each requirement's status and the satisfying document; and the
**calendar** lists satisfying documents by renewal date.

## Modules 7–9 — Projects, Vendors & Clients

These three share one **engagement** model (a type discriminator distinguishes
them), keeping the code DRY. Each engagement carries a **document checklist** with
live **progress tracking** (received / total). Internal **Projects** satisfy
checklist items by assigning existing vault documents (pre-qualification).
**Vendors** and **Clients** additionally get a **tokenized collection portal**
(`/portal/{token}`) — an unauthenticated page where the external party uploads
documents that are AI-classified, added to the host's vault, and auto-matched to a
pending checklist item (by domain). Three Launchpad tiles open the three
registries; a shared detail page manages the checklist, portal link, and status.

## Module 10 — Proposals & Business Development

A **Kanban opportunity pipeline** tracks deals across six stages (Lead →
Qualified → Proposal → Negotiation → Won / Lost); cards are moved with native
drag-and-drop, and each move persists the stage transition. The **proposal
builder** assembles an ordered document package drawn from the vault (add /
reorder / remove) and generates a **cover letter** from a template seeded with the
entity name, proposal title, and recipient — an offline stub behind the same
seam as the other AI touchpoints, so a real model drops in later. **Win/loss
analytics** (win rate, open count, pipeline value, and won value) are computed
live from the pipeline and surfaced both on the board and via
`/api/proposals/analytics`.

## Module 11 — Analytics & Reporting

Three surfaces over one live aggregation layer, all scoped by the tenant query
filter. The **dashboard** computes eight headline KPIs (documents, verified %,
expiring soon, compliance readiness, active rooms, active shares, engagements,
pipeline) plus status/stage breakdowns and a recent-activity feed from the audit
log. **"Ask Quorid"** is the natural-language assistant — the endpoint builds a
`MetricSnapshot` and hands it to an `IAnalyticsAssistant`; the offline
`HeuristicAnalyticsAssistant` matches question intent to the right metric and
returns a data-backed answer, keeping the feature working without API keys (the
production seam swaps in a real model). The **reports** catalog exposes **18
pre-built reports** across Documents, Compliance, Sharing, Data Rooms,
Engagements, and Proposals; each runs live and returns generic columns + rows,
rendered as a table. Compliance reports reuse the same real-time
`ComplianceEvaluator` as Module 6.

## Module 4 — Document Creation

The studio turns a prompt into a finished business document. A **template
library** ships ten seeded blueprints (NDA, engagement letter, SOW, capability
statement, cover letter, board resolution, and more) whose bodies carry
`{{token}}` placeholders; users can add their own. Generation runs through
`IDocumentGenerator` — the offline `HeuristicDocumentGenerator` fills tokens from
the entity profile and user input, expands the free-text prompt into the body,
and (for a blank draft) composes a clean structured document from scratch — the
same seam a real Claude Sonnet call drops into. Each **draft** is edited freely
in a studio editor with a one-click **improve** pass (polish / make concise /
more formal, all deterministic). **Finalize** writes the body through
`IFileStorage` and promotes the draft into the vault as a real `Document` with a
first version, so authored documents flow straight into capture, sharing, rooms,
and compliance.

## Module 12 — Administration

The admin console covers the platform's control plane. **User management** lists
the tenant's users with inline role and status editing and an invite flow that
mints a one-time temporary password (offline stand-in for the email invite).
**Roles & permissions** exposes the 15-permission catalog as editable toggles on
each role; system roles are read-only, custom roles are fully editable and
deletable (once no users hold them). **Organization** edits tenant name/domain
and manages the business entities (profile fields feed compliance and document
generation). An **onboarding checklist** computes six setup steps live from real
data (profile completeness, team, documents, verification, compliance, a data
room) with a completion percentage. **Billing** shows the four-tier plan
catalog, live usage against the current plan's limits (users, documents,
storage, rooms), plan switching, and generated invoices. Fine-grained permission
enforcement is modeled here and layers onto every module's `RequireAuthorization`.

## Roadmap

✅ Foundation · ✅ Capture · ✅ Identity Vault · ✅ Document Management · ✅ Sharing &
Access · ✅ Data Rooms · ✅ Compliance · ✅ Projects / Vendors / Clients · ✅ Proposals
· ✅ Analytics · ✅ Document Creation · ✅ Administration. **All twelve feature
modules are implemented.** See the build specification for the full plan; next
steps are production hardening — EF migrations, real Claude/S3 adapters behind
the existing seams, and permission-policy enforcement.

## CI

`.github/workflows/ci.yml` restores, builds, and tests the backend on .NET 8 and
type-checks + builds the frontend on Node 22 for every push and PR to `main`.
