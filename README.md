# Employee Directory

A staff directory with departments, employee records and photo uploads. Built
to learn layered API design against a real relational schema — an ASP.NET Core
Web API over MS-SQL, consumed by an Angular single-page client.

**Live demo:** <https://employees.klaben.hu>
> First request may take ~30 seconds — free-tier cold start.

![The employee list, with seeded demo data](docs/screenshot.png)

## Stack

ASP.NET Core Web API · MS-SQL / SQLite · ADO.NET · Angular · TypeScript · Bootstrap

## What's interesting here

Honestly? It's CRUD. The parts worth a look are the ones where that stopped
being true:

**The data layer is provider-portable without a second set of queries.** It was
written against MS-SQL, but the public demo has to run on a free container,
where SQL Server doesn't fit — it needs ~2 GB of RAM and has no ARM64 build. So
the queries were normalised to the subset MS-SQL and SQLite both understand,
leaving exactly three provider-specific things: the connection, the DDL, and one
date expression. See [`WebAPI/WebAPI/Data/`](WebAPI/WebAPI/Data/). The
interesting constraint was `AddWithValue`, which every ADO.NET tutorial uses and
which doesn't exist on the `DbParameterCollection` base type — so going
provider-neutral means replacing all fourteen calls.

**The schema's missing foreign key costs more than it looks.**
`Employee.Department` is a free-text copy of the department's *name* rather than
a reference to its id. Renaming a department orphans its employees; deleting one
leaves dangling strings that still render. [`schema.sql`](schema.sql) is the
original; [`schema.sqlite.sql`](schema.sqlite.sql) was written fresh for the demo
with the keys and constraints the original lacks, and the diff between them is
the fix.

**Ephemeral data as a design choice.** Free container filesystems reset on
redeploy. Rather than fight it, the database seeds itself on any boot where it
comes up empty, and photos live in the same directory so the two never disagree.
The demo is always populated and nobody's experiments leak into the next
visitor's session.

## Running locally

```bash
podman-compose -f deploy/compose.yaml up --build
```

Frontend on `:4200`, API on `:5221`, SQLite seeded automatically. Without
containers, or to run against real MS-SQL, see
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## What I'd do differently

This was written in 2022 while learning both halves of the stack, by following
a tutorial. It was unarchived and reviewed in 2026. The full review is in
[docs/CODE-REVIEW.md](docs/CODE-REVIEW.md); the things I'd change, in the order
I'd change them:

**Return DTOs, not `DataTable`.** Every action loads a `DataTable` and returns
it straight as JSON, which means the *database column name is the API contract*.
Rename a column and every client breaks silently. This wasn't theoretical: a
`SELECT *` returned `DepartmentId` while the Angular model read `DepartmentID`,
so the client's id was permanently `undefined` and delete was issuing
`DELETE /api/Department/undefined`. I found that during this review, four years
after shipping it.

**Put the queries behind a repository.** They're built inline in the controller
actions, so there's no seam between HTTP and SQL — which is the real reason
there are no tests, not lack of discipline. Extracting the layer and testing it
are the same piece of work.

**Model the relationship properly.** A `DepartmentId` foreign key instead of a
copied name, plus the primary keys, `NOT NULL` and indexes the original schema
declares nowhere. And `nvarchar`, so Hungarian names survive.

**Raise events instead of passing `this`.** Child components receive their whole
parent as an `@Input()` (`[ShowEmpComp]="this"`) and call methods on it
directly. `@Output()` with an `EventEmitter` is the inverse, and it's what makes
the child reusable and testable. This is the clearest fingerprint of the
tutorial the project came from.

**Validate at the boundary.** Uploads took the client's filename and passed it
to `Path.Combine` after stripping only quotes — a path traversal that let a
crafted filename overwrite files outside the photos directory, on an
unauthenticated endpoint. Fixed, but the lesson generalises: don't sanitise
attacker-controlled input, replace it.

**Check what a write actually did.** Every `ExecuteNonQuery()` return value was
discarded, so deleting a row that didn't exist reported success.

One thing I'd keep: **every SQL statement is parameterised** — fourteen
parameters, no concatenation anywhere, including the paths taking an id straight
from the URL. That's the thing most likely to have gone wrong here, and it
didn't.

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

It used to be on Angular 14, which is out of support. First I tried to patch
only the flagged transitive packages with npm `overrides`, and that got the tree
from 79 flagged packages to 23. But the last 13 were the XSS advisories in
`@angular/core`, `@angular/common` and `@angular/compiler` themselves, and for
v14 there is no patch, so in the end the upgrade was the only way.

The client is small, 812 lines, so the jump was mostly config:

- builder is `@angular/build:application` now, not
  `@angular-devkit/build-angular:browser`
- `polyfills.ts` is gone, `zone.js` and `@angular/localize/init` are listed in
  `angular.json` instead
- the NgModules are gone, the components are standalone and `main.ts` uses
  `bootstrapApplication` with `app.config.ts`
- `provideZoneChangeDetection()` is set explicitly, because it is not implicit
  any more and this app updates views from plain `subscribe` callbacks, not
  signals
- the `src/app/...` imports were rewritten to relative paths
- karma is replaced by the new `ng test`, but there are no spec files anyway

### Why 21 and not 22

I upgraded to 22 first, and the app built and booted but every table stayed
empty. The requests returned 200 and the subscribe callbacks ran, only the views
never re-rendered. Measured in the browser: `employeeList` had 5 items while the
DOM had 0 rows, 1.5 seconds after the data arrived.

That is [angular#69530](https://github.com/angular/angular/issues/69530), a
change detection regression in 22 that is still open. It is not specific to
NgModules, standalone components with `provideZoneChangeDetection()` behave the
same. Angular 21 is not affected, has no advisories either, and is what the app
runs on now. Worth retrying 22 once that issue is closed.

Angular 21 needs Node `^20.19.0 || ^22.12.0 || >=24`, so compose and the
Cloudflare pin are on Node 22, see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

---

Originally built following
[this tutorial](https://www.youtube.com/watch?v=Dpv6lUKNL9o).
The API is also documented as a
[Postman collection](https://www.postman.com/tudi20/workspace/employeeexamplewebapp).
