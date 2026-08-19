# Employee Directory

A staff directory with departments, employee records and photo upload. I built
it to learn layered API design against a real relational schema: an ASP.NET Core
Web API over MS-SQL, with an Angular single page client.

**Live demo:** <https://employees.klaben.hu>
> The first request can take ~30 seconds, the free tier puts the API to sleep.

![The employee list, with seeded demo data](docs/screenshot.png)

## Stack

ASP.NET Core Web API · MS-SQL / SQLite · ADO.NET · Angular · TypeScript · Bootstrap

## Deployment

| Part | Host | Address |
|---|---|---|
| Angular client | Cloudflare Pages | `employees.klaben.hu` |
| ASP.NET Core API | Render (Docker, free) | `employees-api.klaben.hu` |

Both are on free tiers. The API hostname has the project name in it on purpose,
so a second project can get its own one later. The client is built against
`employees-api.klaben.hu`, not the Render URL, so the API can move to another
host with a DNS change and the frontend does not need a rebuild.

Details and the things that cost me time are in
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## What is interesting here

It is mostly CRUD. The parts worth a look are the ones where that stopped being
true.

**The data layer runs on two providers without a second set of queries.** It was
written against MS-SQL, but the public demo runs in a free container, and SQL
Server does not fit there. It wants around 2 GB of RAM and has no ARM64 build.
So the queries were normalised to the subset that MS-SQL and SQLite both
understand, and only three things stayed provider specific: the connection, the
DDL and one date expression. See [`WebAPI/WebAPI/Data/`](WebAPI/WebAPI/Data/).
The annoying part was `AddWithValue`. Every ADO.NET tutorial uses it, but it does
not exist on the `DbParameterCollection` base type, so going provider neutral
meant replacing all fourteen calls.

**The missing foreign key costs more than it looks.** `Employee.Department`
stores a free text copy of the department *name*, not a reference to its id.
Renaming a department orphans its employees, and deleting one leaves dangling
strings that still render. [`schema.sql`](schema.sql) is the original,
[`schema.sqlite.sql`](schema.sqlite.sql) was written fresh for the demo with the
keys and constraints the original does not have. The diff between them is the
fix.

**Ephemeral data is a design choice here.** Free container filesystems reset on
redeploy. Instead of fighting that, the database seeds itself on any boot where
it comes up empty, and the photos live in the same directory, so the two never
disagree. The demo is always populated, and nobody's experiments leak into the
next visitor's session.

## Running locally

```bash
podman-compose -f deploy/compose.yaml up --build
```

Frontend on `:4200`, API on `:5221`, SQLite seeded automatically. Without
containers, or against real MS-SQL, see
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## What I would do differently

I wrote this in 2023 while learning both halves of the stack, by following a
tutorial. I unarchived and reviewed it in 2026. The full review is in
[docs/CODE-REVIEW.md](docs/CODE-REVIEW.md). The things I would change, in the
order I would change them:

**Return DTOs, not `DataTable`.** Every action loads a `DataTable` and returns
it as JSON directly, which means the database column name is the API contract.
Rename a column and every client breaks silently. This was not theoretical: a
`SELECT *` returned `DepartmentId` while the Angular model read `DepartmentID`,
so the client's id was always `undefined` and delete was sending
`DELETE /api/Department/undefined`. I only found it during this review, three
years after I shipped it.

**Put the queries behind a repository.** They are built inline in the controller
actions, so there is no seam between HTTP and SQL. That is the real reason there
are no tests, not discipline. Extracting the layer and testing it are the same
work.

**Model the relationship properly.** A `DepartmentId` foreign key instead of a
copied name, plus the primary keys, `NOT NULL` and indexes that the original
schema does not declare anywhere. And `nvarchar`, so Hungarian names survive.

**Raise events instead of passing `this`.** Child components get the whole parent
as an `@Input()` (`[ShowEmpComp]="this"`) and call methods on it. `@Output()` with
an `EventEmitter` is the right direction, and it is what makes the child reusable
and testable. This is the clearest sign of the tutorial the project came from.

**Validate at the boundary.** Upload took the filename from the client and passed
it to `Path.Combine` after stripping only the quotes. That is a path traversal, a
crafted filename could overwrite files outside the photos directory, on an
endpoint with no authentication. It is fixed now, and the rule is general: do not
sanitise input you got from the client, replace it.

**Check what a write actually did.** Every `ExecuteNonQuery()` return value was
thrown away, so deleting a row that did not exist still reported success.

One thing I would keep: **every SQL statement is parameterised.** Fourteen
parameters, no string concatenation anywhere, including the endpoints that take
an id straight from the URL. That is the thing most likely to go wrong in a
project like this, and it did not.

## Repository layout

```
WebAPI/WebAPI/          ASP.NET Core Web API
  Controllers/          Employee and Department endpoints
  Data/                 connection factory, SQL dialect helper, seeding
Frontend/               Angular client
deploy/                 Dockerfile and compose
docs/                   code review, deployment notes
schema.sql              MS-SQL schema (original)
schema.sqlite.sql       SQLite schema (demo, with the constraints added)
```

## Dependencies

The frontend is on Angular 21 and `npm audit` reports 0 vulnerabilities.

It used to be on Angular 14, which is out of support. First I tried to patch only
the flagged transitive packages with npm `overrides`, and that got the tree from
79 flagged packages down to 23. But the last 13 were the XSS advisories in
`@angular/core`, `@angular/common` and `@angular/compiler` themselves, and there
is no patch for v14, so in the end the upgrade was the only way.

The client is small, 812 lines, so the jump was mostly config:

- the builder is `@angular/build:application` now, not
  `@angular-devkit/build-angular:browser`
- `polyfills.ts` is gone, `zone.js` and `@angular/localize/init` are listed in
  `angular.json` instead
- the NgModules are gone, the components are standalone and `main.ts` uses
  `bootstrapApplication` with `app.config.ts`
- `provideZoneChangeDetection()` is set explicitly, because it is not implicit
  any more and this app updates its views from plain `subscribe` callbacks, not
  from signals
- the `src/app/...` imports were rewritten to relative paths
- karma is replaced by the new `ng test`, but there are no spec files anyway

### Why 21 and not 22

I upgraded to 22 first. The app built and booted, but every table stayed empty.
The requests returned 200 and the subscribe callbacks ran, only the views never
re-rendered. Measured in the browser: `employeeList` had 5 items while the DOM
had 0 rows, 1.5 seconds after the data arrived.

That is [angular#69530](https://github.com/angular/angular/issues/69530), a
change detection regression in 22 which is still open. It is not an NgModule
problem, standalone components with `provideZoneChangeDetection()` behave the
same way. Angular 21 is not affected and has no advisories either, so the app
runs on 21. Worth trying 22 again after that issue is closed, and it will be a
version bump now, not a migration.

Angular 21 needs Node `^20.19.0 || ^22.12.0 || >=24`, so compose and the
Cloudflare pin are on Node 22, see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## AI usage

The original application was written by hand while I was following a tutorial.
No AI was involved in it.

The 2026 work was done with Claude Code: the code review, the dependency and
security work, the Angular 14 to 21 upgrade and the deployment. Those commits
carry a `Co-Authored-By` trailer, so `git log` shows exactly which ones. Nothing
before August 2026 had AI in it.

The decisions stayed with me. Which packages are safe to bump, staying on
Angular 21 instead of 22, which host to use, how the domains are named. Nothing
went in because a model suggested it.

Every change was verified before it was committed, not assumed: a production
build, the built bundle loaded in a headless browser against the real API, and
the Node version checked inside the same container image the deploy uses. That
is also how the Angular 22 problem was found. It compiled and it booted, so only
running it showed that the tables stay empty.

I write this down because the trailers are in the git log anyway, and "who wrote
this" is a fair question for a portfolio project.

---

Originally built following
[this tutorial](https://www.youtube.com/watch?v=Dpv6lUKNL9o).
The API is also documented as a
[Postman collection](https://www.postman.com/tudi20/workspace/employeeexamplewebapp).
