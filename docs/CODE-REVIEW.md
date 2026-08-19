# Code review — August 2026

A review of this project as it stood when it was unarchived, roughly three years
after it was written. It was built by following a YouTube tutorial while
learning ASP.NET Core and Angular, and it is reviewed here as what it is: a
learning project, judged against what I would expect of myself now.

Findings are split into what was fixed and what was deliberately left alone.
The condensed version lives in the README under "What I'd do differently".

---

## Fixed

### 1. Path traversal in the photo upload — the one real security bug

`EmployeeController.SaveFile` took the filename from the client's
`Content-Disposition` header, stripped only the quotes, and handed it to
`Path.Combine`:

```csharp
var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName?.Trim('"');
var path = Path.Combine(Directory.GetCurrentDirectory(), "Photos", fileName);
using var stream = new FileStream(path, FileMode.Create);
```

Two independent problems. `Path.Combine` does not interpret or reject `..`, so
`../../appsettings.json` escapes the photos directory. And if the second
argument is *absolute*, `Path.Combine` discards the first entirely and returns
the absolute path — so a filename of `/app/WebAPI.dll` needs no `..` at all.
`FileMode.Create` truncates whatever it lands on, and the endpoint requires no
authentication.

Now: the client's name is not trusted at all — only a validated extension is
taken from it and the rest is generated server-side, with the resolved path
re-checked against the photos root before the stream is opened. There are also
guards for the empty-upload case (`Request.Form.Files[0]` was an unhandled
`ArgumentOutOfRangeException`, i.e. a 500) and a 2 MB size cap.

### 2. The frontend and backend did not agree on a port

`web-api.service.ts` hardcoded `http://localhost:5000`; `launchSettings.json`
served on `5221`. Cloning the repo and running both halves produced an app that
loaded and then silently failed every request.

### 3. `environment.prod.ts` was never actually used

Both environment files carried only `production: true/false`. The API URL had
never been moved into them, so a production build still pointed at localhost.
`angular.json`'s `fileReplacements` was configured correctly the whole time —
it simply had nothing to swap.

Both files are now typed against a shared `AppEnvironment` interface. Because
`fileReplacements` is a build-time file swap, TypeScript only ever checks the
file that is active, so a key added to one and forgotten in the other would
otherwise compile in development and fail only at production build time.

### 4. The client's department id was always `undefined`

`GET /api/Department` ran `SELECT * FROM Department`. Responses are a
`DataTable` serialised straight to JSON, so **the database column name is the
wire contract** — the response key was `DepartmentId`, while
`models/department.ts` declares `DepartmentID`.

Consequences: the ID column rendered blank, the add/edit modal's
`DepartmentID != 0` test always took the "update" branch, and delete issued
`DELETE /api/Department/undefined`.

Fixed by selecting explicit columns and aliasing `DepartmentId AS DepartmentID`,
which also makes the contract deliberate rather than accidental. The real fix is
a DTO — see "Left alone" below.

### 5. Writes reported success for rows that did not exist

Every `ExecuteNonQuery()` return value was discarded, so updating or deleting a
nonexistent id returned `200 {"Message": "... updated successfully"}`. The API
could not distinguish "done" from "matched nothing". Now returns 404.

### 6. Build output and IDE state were committed

`WebAPI/WebAPI/obj/` (11 files, including `project.assets.json` with absolute
machine paths), `WebAPI/.vs/**/.suo` (binary Visual Studio user state), a file
inside `Frontend/node_modules/`, and a stray `Frontend/package.json` containing
`{}` left by an npm command run one directory too high.

The per-project `.gitignore` files were correct; these predated them and stayed
tracked. Now untracked, with a root `.gitignore`.

### 7. CORS policy was registered and never applied

A named `"AllowOrigin"` policy was built in `AddCors`, and then
`app.UseCors(options => ...)` created and applied a *second*, inline policy —
the named one was dead code. Both were fully open. Now one named policy reading
its origins from configuration.

### 8. `schema.sql` could not be run

`CREATE DATABASE` and `CREATE TABLE` shared a single batch with no `GO`
separators. Run as written in sqlcmd or SSMS it fails.

### 9. Smaller things

- `Directory.GetCurrentDirectory()` used to locate the photos directory in two
  places. That is the process working directory, which is not guaranteed to be
  the content root in a container. Now one configured value.
- `AddNewtonsoftJson` chained twice, plus a redundant `AddControllers()`.
- `UseAuthorization()` with no authentication configured and no `[Authorize]`
  anywhere — a no-op.
- `WebAPIService` was both `providedIn: 'root'` and listed in
  `AppModule.providers`.
- `WeatherForecastController` and `WeatherForecast.cs` — template scaffolding,
  still routable at `GET /WeatherForecast`. `Properties/Resources.resx` was the
  empty default and unused.
- `<tbody>` opened and never closed in both `show.component.html` files.
- No default route and no wildcard, so the application's own root URL rendered
  an empty page below the navigation.
- The UI still called itself "Employees Example Web App" after the repository
  was renamed, and the subtitle read "Managment".

---

## Left alone, deliberately

These are the interesting ones. They are design decisions rather than defects,
and changing them would turn a finished 2023 project into a 2026 rewrite.

### Returning `DataTable` straight to JSON

Every action loads a `DataTable` and returns `new JsonResult(table)`. The API's
response shape is therefore whatever the `SELECT` happens to name its columns:
renaming a database column is a silent breaking change for every client, and
the PascalCase keys the Angular models depend on are an artifact of SQL Server
rather than a designed contract. Finding 4 above is this problem showing up as a
user-visible bug.

The fix is DTOs and an explicit mapping step. It is the single change I would
make first.

*(This is also why Newtonsoft cannot simply be swapped for `System.Text.Json`,
which refuses to serialise `DataTable`. That coupling is worth knowing about
before anyone "modernises" the JSON stack.)*

### No repository or service layer

Connections are opened and commands built inline in every controller action;
controllers took `IConfiguration` directly. Eight queries spread across two
controllers with no layer between them and HTTP, and consequently nothing that
can be tested without a running web host.

A connection factory has been introduced (it was needed to support SQLite for
the deployed demo), but the queries still live in the controllers. Moving them
behind a repository is the natural next step.

### The schema has no keys, and `Department` is a string

```sql
CREATE TABLE dbo.Employee (
    EmployeeId    int identity(1,1),
    EmployeeName  varchar(500),
    Department    varchar(500),   -- free text, not a foreign key
    DateOfJoining date,
    PhotoFileName varchar(500)
)
```

`Employee.Department` stores a *copy of the department's name*. Renaming a
department orphans its employees; deleting one leaves dangling strings that
still display. There are no primary keys, no `NOT NULL`, no uniqueness and no
indexes anywhere. Everything is `varchar` rather than `nvarchar`, so Hungarian
names do not round-trip.

`schema.sqlite.sql`, written fresh for the demo, declares the keys and
constraints the original lacks — the diff between the two files is the fix.
Applying it to the MS-SQL schema would need a data migration to convert the
name column into a `DepartmentId` reference.

### Child components receive their parent as an `@Input()`

```html
<app-add-edit-employee [ShowEmpComp]="this" ...>
```

The child takes the entire parent component instance and calls
`parent.closeModal()` and `parent.showSuccess()` on it directly. This is the
clearest single mark of the tutorial the project came from. `@Output()` with an
`EventEmitter` is the idiomatic inverse, and it is what keeps the child reusable
and independently testable.

### There are no tests

The infrastructure was entirely present and entirely inert: `karma.conf.js`,
`tsconfig.spec.json`, and a `src/test.ts` that globbed `./**/*.spec.ts` and
matched nothing. `npm test` launched Chrome and ran zero specs. Those files went
away with the Angular 21 upgrade, but there are still no specs, and there is no
.NET test project.

The honest reason nothing was ever tested is that there was no seam to test
against — see the repository-layer point above. Extracting one and adding
integration tests around it are the same piece of work.

### No authentication

Every endpoint is anonymous, including the file upload and both deletes. For a
public demo with self-resetting data that is an acceptable trade, but it is a
choice and not an oversight — and it is why hardening the upload mattered.

### `DateOfJoining` is a string

It is a `string` in the C# model, a `string` in the TypeScript model, and bound
to `<input type="date">`, which emits `YYYY-MM-DD`. The contract is an ISO
string end to end, which is coherent, but it means the type system enforces
nothing and the database's `date` type is reached only through an implicit
conversion.

This does have one upside worth noting: because the format was already ISO
everywhere, making the query portable between SQL Server and SQLite needed only
a small dialect helper rather than a second set of queries.

### Angular 14 is out of support — fixed

EOL November 2023. First the build-time advisories were patched with npm
`overrides`, which took the tree from 79 flagged packages to 23. The last 13
were XSS advisories in Angular itself with no patch for v14, so the app was
upgraded to Angular 21 and `npm audit` is clean now. See the README for why 21
rather than 22.

### Comments that restate the code

`DepartmentController` was 59 comment lines out of 144 — `// Open the
connection` above `connection.Open()`, for all eleven steps of every action —
while `EmployeeController` was 19 of 164. The two files did not agree on a
style, and the comments explained *what* rather than *why*. Thinned out during
the rework; the ones that remain explain decisions.

### Miscellaneous

- `GetAllDepartmentNames` lives on `EmployeeController` rather than
  `DepartmentController`, because that is where the form that needs it lives.
- No `trackBy` on `*ngFor`, so every list re-render rebuilds every row.
- Subscriptions are never unsubscribed and the `_success` Subject is never
  completed.
- `confirm()` for deletes.
- Bootstrap icons come from a CDN `@import` in `styles.css` while Bootstrap
  itself is a local npm dependency — an inconsistency, and an external runtime
  dependency the rest of the app does not have.
- The production bundle is 508 kB against a 500 kB budget, so every build warns.

---

## What the original got right

Worth stating, because it is the thing most likely to have gone wrong and
did not: **every SQL statement is parameterised.** Fourteen parameters across
eight queries, no string concatenation into a command anywhere, including in
the code paths that take an id straight from the URL. For a project written
while following a video tutorial, that is not a given.
