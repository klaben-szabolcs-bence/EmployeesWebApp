# Deployment

Frontend on Cloudflare Pages, API in a container on a free tier, SQLite for
data. Total cost: nothing.

```
Angular SPA  ──►  Cloudflare Pages      static, no cold start
     │
     └── HTTPS ──►  ASP.NET Core API    container, free tier, sleeps when idle
                          └── SQLite on the container's own disk
```

## Why SQLite and not MS-SQL

The project was written against MS-SQL and still runs against it locally. The
deployed demo cannot: SQL Server needs roughly 2 GB of RAM, which no free tier
offers, and there is no ARM64 image, which rules out the free ARM VMs that would
otherwise have the headroom.

The obvious alternative — a managed free Postgres — does not solve it either.
Render's free Postgres **expires after 30 days**, which is precisely the failure
mode a portfolio cannot afford: the demo works while you are building it and is
quietly dead by the time somebody clicks the link.

SQLite on the container's own filesystem has neither problem. The trade is that
the data is ephemeral, which the next section turns into a feature.

The data layer stays provider-selectable — `Database:Provider` is `SqlServer` or
`Sqlite` — so the MS-SQL path is real and exercised, not a historical claim.

## Ephemeral data is the design, not a defect

Free container filesystems are writable at runtime but reset on redeploy and on
spin-down. Rather than fight that:

- The database is **created and seeded on every boot where it comes up empty.**
  The demo is therefore always populated, and a visitor's experiments never
  persist into somebody else's session.
- **Photos live in the same directory as the database** (`/data`). Both reset
  together, so there are never rows referencing images that no longer exist.

Both paths are verified: a fresh container seeds itself, and a restart with data
already present skips seeding.

## Cold starts

Free tiers sleep after ~15 minutes of inactivity. The first request afterwards
waits for the container to be scheduled and started — on the order of a minute,
most of which is the platform rather than the application. (Measured locally at
Render's limits, `--memory=512m --cpus=0.1`, the app itself starts in about 3
seconds and settles at ~21 MB.)

Say so in the README rather than letting the demo look broken:

> First request may take ~30 seconds — free-tier cold start.

**Do not add a keep-warm pinger.** Render's free tier allows 750 instance-hours
per month across the whole workspace; a 31-day month is 744 hours. One
continuously-pinged service consumes essentially the entire allowance, and going
over suspends *every* free service on the account until the 1st.

## Deploying

The two sides reference each other, so the order matters.

### 1. API

Any host that runs a container works. Render is the path of least resistance
(no card required); it has no native .NET runtime, so Docker is the only route.

- Dockerfile: `deploy/api.Dockerfile`, build context the repository root
- The API binds `0.0.0.0` on `$PORT`, which the host injects

The client is built against `https://employees-api.klaben.hu`, not the host's
own URL, so point that name at whatever host you end up using with a CNAME. This
is the reason the API can move later without rebuilding and redeploying the
frontend.

The hostname carries the project name on purpose. `api.klaben.hu` would claim the
generic name for whichever project got there first, and the next project's API
would have nowhere natural to go. The pattern is `<project>.klaben.hu` for the
client and `<project>-api.klaben.hu` for its API. Both stay one level deep, which
matters: Cloudflare's Universal SSL covers `klaben.hu` and `*.klaben.hu` only, so
a nested `api.<project>.klaben.hu` would need the paid certificate add-on as soon
as it is proxied.

Environment variables:

```
Database__Provider=Sqlite
ConnectionStrings__DefaultConnection=Data Source=/data/employees.db;Default Timeout=5
Storage__PhotosPath=/data/Photos
```

Leave `Cors__AllowedOrigins__0` until step 3.

### 2. Frontend

`src/environments/environment.prod.ts` already points at
`https://employees-api.klaben.hu`,
so this step does not wait for the API. Point Cloudflare Pages at the repository:

| Setting | Value |
|---|---|
| Root directory | `Frontend/EmployeesExampleWebApp` |
| Build command | `npm ci && npm run build` |
| Output directory | `dist/employees-example-web-app` |
| Custom domain | `employees.klaben.hu` |
| `NODE_VERSION` | `22.23.2` (or rely on `.node-version`) |

Three things that will otherwise cost an afternoon:

- **Set the root directory precisely.** Pointing it at `Frontend` finds a
  different, near-empty project and "succeeds" while producing nothing.
- **Pin the Node version explicitly.** The build image ignores `engines` in
  `package.json` and rejects codenames like `lts/hydrogen`. There is a
  `.node-version` file in the root directory now, which keeps the version in
  git instead of only in the dashboard. Angular 21 needs Node 20.19+ or 22.12+.
- `src/_redirects` is already registered as a build asset. Without it every
  deep link 404s on refresh.

The project has build watch paths set, so a commit that only touches the API or
the docs does not rebuild the frontend:

```
includes: *
excludes: WebAPI/*, docs/*, deploy/*, *.sql, README.md
```

Excludes rather than includes, so a path I forget only costs a wasted build
instead of a change that never deploys. In Pages a single `*` also matches the
path separator, so `WebAPI/*` covers any depth and there is no `**`.

**A skipped build still creates a deployment row.** This is worth knowing before
you conclude the watch paths are broken, because I did exactly that. The row
appears in the dashboard and the API for every push, and a skipped one sits at
`queued` / `idle` with `started_on` null and never moves. A build that really
runs starts about a minute after its row appears. Check `started_on`, or check
which deployment is actually live, not whether a row exists.

The API field descriptions for `path_includes` and `path_excludes` say "preview
deployment", which suggests production is unaffected. On this project it does
affect production, verified on 19 August 2026 with a docs-only commit that never
built.

Render has the same feature under Build Filters, with the list inverted: it
ignores `Frontend/**` and `docs/**`.

### 3. Close the loop

Add `https://employees.klaben.hu` to the API as `Cors__AllowedOrigins__0` and
let it redeploy. Use the custom domain, not the `.pages.dev` one — CORS compares
the browser's `Origin` header literally, and the browser sends whichever host
the user typed. Add the `.pages.dev` URL as `Cors__AllowedOrigins__1` if you
want that one to keep working too.

Preview deployments get their own per-deployment subdomain, which a fixed
allow-list will not match. Either accept that previews cannot reach the API, or
match them with a `SetIsOriginAllowed` predicate — but do not reach for
`AllowAnyOrigin()` as the shortcut.

## Monitoring

`GET /api/health` returns the provider, row counts, photo count, version and
uptime. Three states:

| Status | HTTP | Meaning |
|---|---|---|
| `ok` | 200 | database answers, photos are there |
| `degraded` | 200 | still serving, but the photos directory is empty or gone |
| `unhealthy` | 503 | the database is unreachable |

Only `unhealthy` is a 503. A monitor reads the status code and nothing else, so
anything that can still serve has to stay 200 or it reports an outage that is not
happening. `degraded` exists because photos and the database share one directory
and are supposed to reset together. If the photos vanish while the rows are still
there, that stopped being true, and this endpoint is the only thing that notices.

The endpoint answers `HEAD` as well as `GET`. That is not decoration. The other
actions are marked `[HttpGet]` only, so they answer 405 to a `HEAD`, and every
uptime checker and badge service probes with `HEAD` first. shields.io reported
this API as down while it was warm and serving 200s in about a tenth of a second.

The response never contains the connection string, the photo path or exception
text. A failed check is logged and answered with a generic message. The API is
public and has no authentication.

### Why nothing polls the API

The dashboard cannot check the API directly, and neither can a badge.

Measured on 20 August 2026: a cold start takes about 34 seconds, a warm request
about a tenth of a second. Glance builds every widget request on a shared client
with `Timeout: 5 * time.Second` (`internal/glance/widget-utils.go`), and the
monitor widget uses that same client. The per site `timeout:` option only builds
a context, and Go applies both, so the shorter one wins. Setting `timeout: 60s`
still gives up after five seconds. There is no widget that can wait out a cold
start.

shields.io has the same problem plus a worse one. It sends `cache-control:
max-age=120`, so a live badge is rechecked every two minutes by whoever opens the
README, crawlers included. That is a keep-warm pinger driven by strangers, which
is the one thing the free tier cannot afford. The API badge is static for that
reason.

So a job on my own machine fetches `/api/health` every 6 hours with a 90 second
timeout and writes the JSON somewhere the dashboard can read instantly. Four
wakes a day, each keeping the container up for its 15 minute idle window, is
about 30 instance hours a month out of 750. Do not shorten that interval below
15 minutes, at which point the container never sleeps again.

Render's health check path is deliberately left empty for the same reason. There
is an endpoint to point it at now, but a platform probing it on a schedule is the
keep-warm problem again.

### Traffic numbers

Cloudflare already counts requests for `employees.klaben.hu` because it is
proxied. No beacon and no app code needed. Two limits are worth knowing before
building anything on it:

- The free plan refuses any GraphQL analytics query wider than one day. Not just
  short retention, it rejects the query. So the number is always "last 24 hours".
- `employees-api.klaben.hu` is DNS only, so that Render can issue its own
  certificate. Cloudflare never sees API traffic and cannot report on it.

## Running locally

### Containers

```bash
podman-compose -f deploy/compose.yaml up --build
```

Frontend on `:4200`, API on `:5221`, SQLite seeded automatically.

Under rootless podman on Fedora/Bazzite:

- Reach published ports on **`127.0.0.1`, not `localhost`.** `localhost` may
  resolve to `::1`, which pasta does not forward, and you get a connection reset
  from a container that is working perfectly. This is why
  `environment.ts` points the dev build at `http://127.0.0.1:5221` — with
  `localhost` there, the browser cannot reach a containerised API at all, and
  the app loads but shows empty tables.
- Bind mounts need `:z` so SELinux permits them. Already set in `compose.yaml`.
- Image names are fully qualified, since podman does not assume `docker.io`.
- Podman 5 shells out to an external compose provider. If you have none,
  `podman kube play` is the zero-install alternative.

### Without containers

```bash
# API, SQLite, seeded on first run
cd WebAPI/WebAPI
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://localhost:5221

# Frontend
cd Frontend/EmployeesExampleWebApp
npm ci && npm start
```

`appsettings.Development.json` selects SQLite. To develop against real MS-SQL,
run `schema.sql`, then set `Database:Provider` to `SqlServer` and point
`ConnectionStrings:DefaultConnection` at the instance.

### A note on Node

Angular 21 declares `^20.19.0 || ^22.12.0 || >=24`, so Node 18 is too old.
Compose and the Cloudflare build image are both on Node 22.

Older notes about `--openssl-legacy-provider` and the Angular 14 version gate do
not apply anymore, the project is on Angular 21.

## Checklist

- [x] `podman build -f deploy/api.Dockerfile .` from a clean clone
- [x] `grep -r "localhost" dist/` after a production build returns nothing
- [x] `NODE_VERSION` pinned (`.node-version`, 22.23.2)
- [x] Cold-start note in the README
- [x] Cloudflare root directory is `Frontend/EmployeesExampleWebApp`
- [x] `employees.klaben.hu` added as a custom domain on the Pages project
- [x] API deployed, `employees-api.klaben.hu` CNAME pointing at it
- [x] `Cors__AllowedOrigins__0` set to `https://employees.klaben.hu`
- [x] Live URL in the README
- [X] Live URL in the repository's About sidebar
- [x] `/api/health` answers `HEAD` as well as `GET`
- [x] Render health check path left empty on purpose

## Verify before relying on this

Free-tier terms change often — Render, Cloudflare, Neon and Oracle have all
revised theirs within the last two years. The figures here were checked in
August 2026; confirm the current limits at signup rather than trusting this
document.
