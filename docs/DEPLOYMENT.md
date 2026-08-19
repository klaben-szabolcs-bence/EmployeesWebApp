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

Environment variables:

```
Database__Provider=Sqlite
ConnectionStrings__DefaultConnection=Data Source=/data/employees.db;Default Timeout=5
Storage__PhotosPath=/data/Photos
```

Leave `Cors__AllowedOrigins__0` until step 3.

### 2. Frontend

Put the API's URL in `src/environments/environment.prod.ts`, commit, then point
Cloudflare Pages at the repository:

| Setting | Value |
|---|---|
| Root directory | `Frontend/EmployeesExampleWebApp` |
| Build command | `npm ci && npm run build` |
| Output directory | `dist/employees-example-web-app` |
| `NODE_VERSION` | `18.20.8` |

Three things that will otherwise cost an afternoon:

- **Set the root directory precisely.** Pointing it at `Frontend` finds a
  different, near-empty project and "succeeds" while producing nothing.
- **Pin the Node version explicitly.** The current build image ignores
  `engines` in `package.json` and rejects codenames like `lts/hydrogen`.
  A literal version, or a `.node-version` file in the root directory.
- `src/_redirects` is already registered as a build asset. Without it every
  deep link 404s on refresh.

### 3. Close the loop

Add the resulting `https://<project>.pages.dev` to the API as
`Cors__AllowedOrigins__0` and let it redeploy.

Preview deployments get their own per-deployment subdomain, which a fixed
allow-list will not match. Either accept that previews cannot reach the API, or
match them with a `SetIsOriginAllowed` predicate — but do not reach for
`AllowAnyOrigin()` as the shortcut.

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

The project builds on Node 22 despite Angular 14 reporting it as "Unsupported" —
the CLI's version gate only rejects Node below 14.15 and the 15.x/early-16.x
range, and warns on odd-numbered releases. Node 22 is neither, so it passes. The
`ERR_OSSL_EVP_UNSUPPORTED` / `--openssl-legacy-provider` workaround often
suggested for old Angular does **not** apply here: Angular 14's builder already
pins webpack's hash function to `xxhash64`.

Cloudflare's build image is pinned to Node 18 anyway, as the conservative
choice for a build nobody watches.

## Checklist

- [ ] `podman build -f deploy/api.Dockerfile .` from a clean clone
- [ ] `grep -r "localhost" dist/` after a production build returns nothing
- [ ] Cloudflare root directory is `Frontend/EmployeesExampleWebApp`
- [ ] `NODE_VERSION` pinned
- [ ] `Cors__AllowedOrigins__0` set to the Pages origin
- [ ] Cold-start note in the README
- [ ] Live URL in the repository's About sidebar

## Verify before relying on this

Free-tier terms change often — Render, Cloudflare, Neon and Oracle have all
revised theirs within the last two years. The figures here were checked in
August 2026; confirm the current limits at signup rather than trusting this
document.
