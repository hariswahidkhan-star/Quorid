# Deploying Quorid to Render

Quorid ships as a **single Docker image**: the root `Dockerfile` builds the React
SPA and the .NET 8 API together, and the API serves the SPA from `wwwroot`. That
means one web service, one origin, no CORS. MySQL is the only external dependency.

Two paths:

- **Option A — All on Render** (`render.yaml`): app + a MySQL private service with a
  disk. Simplest, but private services and disks need a **paid** instance (Starter+).
- **Option B — Free tier** (`render.external-mysql.yaml`): the app on Render's **free**
  web plan + a MySQL you host on a free external provider.

Pick one. Both use the same image and app code.

---

## 0. Prerequisites (once)

Render deploys from a connected GitHub repo, so the code has to be on GitHub first.

```bash
unzip quorid-*.zip && cd quorid
git push -u origin main         # origin is already set to your Quorid repo
```

Then, in Render, connect your GitHub account (Dashboard → **Account Settings →
GitHub**) so Render can see the repo.

---

## Option A — All on Render (paid)

1. Render Dashboard → **New → Blueprint**.
2. Select your `quorid` repo. Render detects **`render.yaml`**.
3. Review the plan — it creates two services:
   - `quorid-mysql` (private service, MySQL 8, 10 GB disk)
   - `quorid` (web, Docker)
   Render auto-generates the JWT signing key and the MySQL password and wires the
   DB connection between them.
4. Click **Apply**. First build takes a few minutes (Docker build + npm build).
5. When `quorid` is **Live**, open its URL. The login screen loads; `‹url›/health`
   returns `{"status":"ok"}`.

That's it — skip to **Post-deploy** below.

---

## Option B — Free tier (Render free + external MySQL)

### B1. Create a free MySQL database

You need a MySQL 8 database reachable over the internet with TLS. Two providers
with a genuine free tier (as of 2026):

**Aiven for MySQL** (managed MySQL)
1. Sign up at aiven.io → **Create service → MySQL** → pick the **Free** plan.
2. Once running, open the service page and note **Host**, **Port** (often *not*
   3306), **User** (`avnadmin`), and **Password**.
3. In the service's **Databases** tab, create a database named `quorid`.

**TiDB Cloud Serverless** (MySQL-compatible, generous free tier) — an alternative
if you prefer: create a free **Serverless** cluster, then a `quorid` database, and
read the connection params from **Connect**. Everything below works the same;
TiDB requires TLS too.

### B2. Build the connection string

Quorid uses the Pomelo/MySqlConnector format. Fill in your values:

```
server=HOST;port=PORT;database=quorid;user=USER;password=PASSWORD;SslMode=Required;
```

- `SslMode=Required` encrypts the connection (needed by Aiven/TiDB). For strict CA
  validation use `SslMode=VerifyFull;SslCa=/path/to/ca.pem` and mount the CA.
- Keep the trailing `;`.

### B3. Deploy the app

1. In the repo, **rename** `render.external-mysql.yaml` to `render.yaml` (Render's
   Blueprint reads that exact name), commit, and push:
   ```bash
   git mv render.external-mysql.yaml render.yaml && git commit -m "Use external MySQL blueprint" && git push
   ```
   (Or, if you keep both, point Render at the file when creating the Blueprint.)
2. Render Dashboard → **New → Blueprint** → select the repo → **Apply**. This
   creates just the `quorid` web service on the **free** plan.
3. Open the `quorid` service → **Environment** → set **`ConnectionStrings__Default`**
   to the string from B2 → **Save** (this redeploys).
4. When Live, open the service URL.

> Free web services **spin down after ~15 min idle** and cold-start on the next
> request (~30–60 s). Fine for demos; upgrade to Starter for always-on.

---

## Post-deploy

- **First login:** register a tenant from the login screen — the first user becomes
  the Owner with full permissions.
- **Schema:** the first deploy sets `Database__EnsureCreatedOnStartup=true`, which
  builds the schema from the model. Once you generate EF migrations, prefer those:
  ```bash
  cd backend
  dotnet tool install --global dotnet-ef
  ./scripts/add-migration.sh InitialCreate
  git add src/Quorid.Infrastructure/Persistence/Migrations && git commit && git push
  ```
  Then in Render set `Database__MigrateOnStartup=true` (and you can drop
  `Database__EnsureCreatedOnStartup`).
- **Claude-backed AI (optional):** set `Anthropic__ApiKey` on the `quorid` service.
  Blank keeps the offline heuristics; a real key switches on Claude for
  classification, extraction, drafting, and Ask Quorid.

---

## Deploy on green (optional CI trigger)

By default `render.yaml` has `autoDeploy: true` (every push to the connected branch
deploys). To deploy **only after CI passes** instead:

1. Render → `quorid` service → **Settings → Deploy Hook** → copy the URL.
2. GitHub repo → **Settings → Secrets and variables → Actions** → add
   `RENDER_DEPLOY_HOOK_URL` = that URL.
3. Set `autoDeploy: false` in `render.yaml` (so Render doesn't also auto-deploy).

The `deploy` job in `.github/workflows/ci.yml` then fires the hook after the
`backend` and `frontend` jobs pass on `main`. Without the secret it's a no-op.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Health check fails on first deploy | MySQL still booting — the app retries the DB bootstrap ~6× (18 s). If it still fails, redeploy the web service once the DB is Live. |
| `500`s right after deploy, DB errors in logs | Schema not created. Confirm `Database__EnsureCreatedOnStartup=true` (or a migration is applied) and the connection string/host are correct. |
| External MySQL: `SSL connection error` | Add `SslMode=Required;` to the connection string. |
| External MySQL: `Unknown database 'quorid'` | Create the `quorid` database in the provider first. |
| SPA loads but API calls 404 | Ensure you deployed the **root** `Dockerfile` (single image). API is same-origin at `/api/*`. |
| Blueprint didn't pick up changes | Render caches the blueprint; re-sync via the service's **Manual Deploy → Clear build cache & deploy**. |
