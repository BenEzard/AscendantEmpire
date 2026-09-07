# AscendantEmpire — Full Design & Code Review

A rolling, multi-session review of the whole solution. This document is both the **plan** (so a
later session can pick up exactly where the last one stopped) and the **findings register**.

- **Started:** 2026-07-30
- **Last updated:** 2026-07-31 (session 3 — first full pass complete across all 15 slices)
- **Reviewed at commit:** `05f8644` (branch `master`, with an in-flight test-tier refactor in the
  working tree — see [Working-tree caveats](#working-tree-caveats))

---

## 1. How to resume this review

1. Read [§3 Slice register](#3-slice-register) and find the first slice whose **Status** is not
   `Done`.
2. Read that slice's row for its scope, then review only those files.
3. Append findings to [§5 Findings](#5-findings) under that slice's heading, using the ID scheme in
   [§2](#2-review-method). **Never renumber existing IDs** — they are referenced elsewhere.
4. Update the slice's Status / Session / Notes cells, and bump *Last updated* at the top.
5. Add anything that needs a decision from Ben to [§6 Open questions](#6-open-questions).

The matching task list is tracked in the session task tool as `S1`–`S15`, mirroring §3.

### Working-tree caveats

At the time of session 1 the working tree had uncommitted work that this review deliberately does
**not** treat as final:

- A test-tier refactor in progress: `SqlServerClassTestBase.cs`, `SnapshotSqlServerTestBase.cs` and
  `GameSnapshotSqlServerTestBase.cs` deleted; `InMemoryTestBase.cs` added; ~8 test fixtures
  re-pointed. `testing.md` (new) and `CLAUDE.md` describe the target state.
- `docs/tickets.md` staged-modified but deleted in the working tree.
- Staged edits to the Resources domain entities and `UpsertResourcesHandler`.

Slice **S12** (tests) should be reviewed *after* that refactor lands, or explicitly against the
working-tree state — flag which was used.

---

## 2. Review method

### Severity scale

| Severity | Meaning |
| --- | --- |
| **Critical** | Exploitable, or causes data loss / corruption. Fix before anything else. |
| **High** | Real defect or a design flaw that will actively cost time; users can hit it. |
| **Medium** | Genuine problem with a bounded blast radius, or a design decision that should be revisited. |
| **Low** | Correctness-neutral: naming, duplication, style, small cleanups. |
| **Info** | Observation, accepted trade-off, or something to confirm — no action implied. |

### Finding ID scheme

`AE-S<slice>-<nnn>` — e.g. `AE-S2-003` is the third finding in slice S2. IDs are permanent.

### Finding format

Each finding gives: severity, a one-line claim, the location, why it matters, and a suggested
direction. Where a finding is *latent* (a real defect on a path nothing currently reaches) it says
so explicitly, so it can be prioritised honestly.

### What this review covers

Bugs, security, design/architecture, code smells, consistency with the project's own stated
conventions (`CLAUDE.md`, `testing.md`, `docs/design/*`), missing tests, and tooling gaps.

### What it deliberately does not cover

- **Game-design balance.** Whether a rule is *fun* is out of scope; whether it is *implemented as
  documented* is in scope.
- **Generated EF migration bodies** (~84,000 of the Application project's ~106,000 lines). Reviewed
  only for ordering, drift against the model, and destructive operations — not line by line.
- `node_modules`, `bin`, `obj`, `dist`, `TestResults`, `_compilecheck` (the last two are untracked).

---

## 3. Slice register

Sizes exclude `bin`/`obj`. Backend totals: ~22k lines of hand-written C# in Application plus
~84k lines of generated migrations; ~3.2k Api; ~3.7k Domain; ~3.4k contracts; ~10.6k tests.
Frontend: ~23k lines of TS/TSX.

| # | Slice | Scope | Size | Status | Session |
| --- | --- | --- | --- | --- | --- |
| S1 | API host, composition root & config | `Api/Program.cs`, `ApplicationStartup.cs`, `Application/Options/*`, `appsettings*.json`, Serilog/CORS/Swagger setup | ~0.5k | **Done** | 1 |
| S2 | Authentication & authorisation | `Api/Authentication/*`, `Api/Utilities/{ClaimsPrincipal,GameToken}Extensions`, `Features/Authentication/**`, endpoint protection across all controllers | ~1.2k | **Done** | 1 |
| S3 | API surface — controllers & result mapping | all 15 controllers, `Common/ServiceResultBase.cs`, `Api/Utilities/ServiceResultExtensions.cs`, `DTApiErrorResult`, `ValidationBehaviour`, BackendContracts / FrontendContracts | ~6.1k | **Done** | 1–2 |
| S4 | Application layer conventions | MediatR request/handler conventions across `Features/*`, validator coverage, error/logging patterns, `Helpers/*`, `Common/*` | ~1k + cross-cutting | **Done** | 3 |
| S5 | Persistence — DbContext, migrations, EF usage | `Data/AscendantEmpireContext.cs`, `Data/AscendantEmpireContextFactory.cs`, `Migrations/*`, indexes, concurrency, tracking, N+1, transactions, raw SQL | ~1.9k + 84k gen. | **Done** | 1–2 |
| S6 | Domain model | `Domain/Entities/**`, `Domain/Enums/**`, `Domain/Util` | ~3.7k | **Done** | 3 |
| S7 | Background processing pipeline | `BackgroundServices/*` and all 7 `IProcessExecutor`s | ~0.2k + executors | **Partial** — pipeline done, the 7 executors deferred to S8/S9/S11 | 1 |
| S8 | Maps & map generation | `Features/Maps/**` (61 files), `MapGeneration/*`, `Helpers/MapMethods.cs`, map export/import/preview executors | ~7.3k | **Targeted pass** — see scope note in §5 | 3 |
| S9 | UnitComponents / unit design | `Features/UnitComponents/**` (31 files), `Features/Units/*`, `Helpers/ScaledNumerics/*`, catalog export/import executors | ~4.7k | **Targeted pass** — see scope note in §5 | 3 |
| S10 | Turns & troop orders | `Features/Turns/**` (35 files), `Features/TroopOrders/**` | ~3k | **Targeted pass** — see scope note in §5 | 3 |
| S11 | Remaining backend features | `Features/{Game,Players,MatchMaking,Military,Resources,Structures,Troops,System,DatabaseSeeding}` | ~3.3k | **Targeted pass** — see scope note in §5 | 3 |
| S12 | Test suite health & coverage | `AscendantEmpire.Tests/**` (48 files) against `testing.md`; itemise untested high-risk paths | ~10.6k | **Partial** — per-fixture tier audit still blocked on the in-flight refactor | 1, 3 |
| S13 | Frontend architecture & shared layers | `src/api`, `AppProviders.tsx`, `src/entries`, `src/hooks`, `src/components`, `src/types`, `src/util`, `src/styles`, Vite/tsconfig/eslint | ~4k | **Partial** — `src/hooks`, `components/ui` and types-vs-contracts alignment remain | 1, 3 |
| S14 | Frontend features | `src/features/*`: game (5.4k), mapgallery (4.5k), entitysetuptool (3.0k), unitdesign (3.0k), system, map, notifications, processing, auth, matchmaker | ~19k | **Targeted pass** — see scope note in §5 | 3 |
| S15 | Build, tooling, CI & docs | csprojs, `.github/**`, `docker-compose.yml`, side projects (`MapGenerationPlay`, `NanoBananaConsole`, `WeightedRandom`), `docs/**` vs implementation | — | **Partial** — `docs/design/*` cross-check remains | 1, 3 |

### What a further session should pick up

Every slice has been through at least one pass. What remains is depth, not breadth:

1. **Per-slice deep reads of the algorithmic cores** (S8-S11): `PathFinder`,
   `GeneratePerlinNoiseCampaignMap`, `CompileUnitTemplateHandler`, `GameSetupService`,
   `GetPlayerCampaignMapHandler`, `BulkUpsertUnitComponentsHandler`.
2. **S12** per-fixture tier audit — after the in-flight test refactor lands.
3. **S13** `src/hooks` / `components/ui`, and a field-by-field `src/types` ↔ C# contracts check.
4. **S15** cross-check the 17 `docs/design/*.md` files against the implementation.

---

## 4. Architecture summary (as observed)

Recording this so later sessions don't have to re-derive it.

```
AscendantEmpire.Api            ASP.NET Core 10, controllers → IMediator, JWT bearer
  └── AscendantEmpire.Application    MediatR handlers + services, EF Core 10 (SQL Server),
      │                              in-process background job pipeline, SkiaSharp map rendering
      ├── AscendantEmpire.BackendContracts    request/response records between Api and handlers
      └── AscendantEmpire.Domain              EF entities + enums (no behaviour)
AscendantEmpire.FrontendContracts   interfaces the API response bodies implement
AscendantEmpire.AscendantEmpireUI   React 19 + Vite (5 separate entry points), react-query, axios,
                                    Tailwind 4  — not part of the .sln
```

Conventions in force:

- **CQRS-ish via MediatR.** One request record + one handler per operation, grouped by feature
  folder. Handlers return `ServiceResult<T>`, which controllers translate to `IActionResult`.
- **`IDbContextFactory` over injected `DbContext`.** Handlers and services create their own context
  per unit of work.
- **An in-house validation system** (`Application/Common/Validation/`) run by a MediatR pipeline
  behaviour, converting failures into a `Fail` result rather than throwing.
- **Enums on the wire as string names** (`JsonStringEnumConverter`), matched by TS string unions.
- **Two token kinds**: a *user token* (identity only) and a *game token* (identity + `gameId` +
  `playerId`), backed by a revocable DB-side refresh session.

---

## 5. Findings

**Running tally:** 3 Critical, 15 High, 62 Medium, 41 Low, 11 Info — **132 items**.
(Session 1: 62. Session 2: +29, S3 and S5. Session 3: +41, all remaining slices.)

### If you only fix ten things

Ordered by (damage × cheapness to fix), not by severity alone.

| # | Finding | Why first |
| --- | --- | --- |
| 1 | **AE-S4-001** — `EndCampaignTurnHandler` has no `IRequestHandler<,>` | Ending a turn 500s every time. One line, plus a return-type decision. |
| 2 | **AE-S2-001** — `FallbackPolicy` missing → ~45 anonymous endpoints | One block in `Program.cs` makes the default safe. |
| 3 | **AE-S3-016** — five endpoints take identity from the request body | Anonymous *and* impersonatable. Delete the params, use `TryGetGameContext`. |
| 4 | **AE-S15-001** — no CI | Nothing above can regress silently once this exists. |
| 5 | **AE-S12-004** — the three tests that pin 1, 2, 3 and AE-S4-002 | Cheap, permanent, catch the whole class of bug. |
| 6 | **AE-S1-001** — no global exception handler | Turns a dozen findings from "bare 500" into "handled error"; enables 7. |
| 7 | **AE-S5-009** — exception messages returned to the client | Leaks SQL schema detail; a one-line change once 6 exists. |
| 8 | **AE-S4-003** — five blocking `.Result` calls in the fog-of-war read | Thread starvation and fake parallelism on the hottest query. |
| 9 | **AE-S5-002** — migrations never applied, never tested | Schema drift is already real (G-20). |
| 10 | **AE-S3-005** — un-nest `ServiceResult<T>` | Collapses AE-S3-002, 003, and the `using static` in 16 files. |

> **Correction (session 2):** AE-S5-001 as first written said `OnModelCreating` was "one ~1,580-line
> method". That was wrong — it delegates to 17 private `Configure*Schema` methods. The finding has
> been rewritten and downgraded accordingly.

### S1 — API host, composition root & configuration

**AE-S1-001 · High · No global exception handler.**
`Api/Program.cs` builds no exception-handling middleware (`Api/Middleware/` contains only
`Validators/`), and no controller wraps `mediator.Send` in a try/catch. Any exception that escapes a
handler therefore produces the framework default: a `text/plain` 500 with a full stack trace in
Development, and an empty 500 in Production. Neither matches the `IApiErrorResult` `{ errors: [] }`
shape that `src/api/http.ts` parses, so the frontend surfaces `error.message` ("Request failed")
instead of anything useful. Add `app.UseExceptionHandler` (or an `IExceptionHandler`) that logs and
emits `DTApiErrorResult`, and consider a `ProblemDetails`-shaped alternative.

**AE-S1-002 · Medium · CORS origin hard-coded.**
[Program.cs:85](../../AscendantEmpire.Api/Program.cs) pins `WithOrigins("http://localhost:5173")`.
Any non-local deployment needs a code change. Move the origin list to configuration and bind it
alongside the other options.

**AE-S1-003 · Medium · Options are registered ad hoc and never validated.**
Four `builder.Services.Configure<...>` calls sit in `Program.cs` with fully-qualified type names
([Program.cs:101-111](../../AscendantEmpire.Api/Program.cs)) rather than inside
`AddApplicationServices`, which is where the rest of the Application wiring lives — so adding an
option means touching the Api project. None of them use `ValidateDataAnnotations`/`ValidateOnStart`,
so e.g. a missing `ExportSettings.TempDirectory` surfaces as a failed background job much later
rather than as a startup failure, mirroring the fail-fast treatment `Jwt:SigningKey` already gets.

**AE-S1-004 · Medium · No HTTP request logging.**
Serilog is configured, but `UseSerilogRequestLogging()` is never called and
`appsettings.json` overrides `Microsoft.AspNetCore` to `Warning`. Net effect: successful requests
leave no trace in the log at all, which will make diagnosing anything reported from the UI
guesswork.

**AE-S1-005 · Medium · No health endpoint.**
No `AddHealthChecks`/`MapHealthChecks`. `/system/status` is background-job status, not liveness, so
there is nothing for a container orchestrator or uptime check to hit — and nothing that proves the
database connection is good before traffic arrives.

**AE-S1-006 · Low · Absolute developer-machine paths are the committed defaults.**
`appsettings.json` ships `ExportSettings.TempDirectory` and `MapGenerationSettings.OutputDirectory`
as `P:\Development\AscendantEmpire_Files\...`, and the connection string as
`Server=CoreOS\SQLExpress;...`. These are base (all-environment) settings, not
`appsettings.Development.json` overrides, so they are the fallback everywhere. Move machine-specific
values into the Development file or user-secrets and leave the base file with no default.

**AE-S1-007 · Low · `AllowedHosts: "*"`.** No host filtering. Fine locally; worth tightening
whenever this is hosted anywhere.

**AE-S1-008 · Low · Dead local in `Program.cs`.**
[Program.cs:152](../../AscendantEmpire.Api/Program.cs) — `IServiceCollection services =
builder.Services;` is assigned and never read.

**AE-S1-009 · Low · `KebabCaseParameterTransformer` is culture-sensitive and allocates per call.**
[Program.cs:194](../../AscendantEmpire.Api/Program.cs) uses `.ToLower()` (culture-sensitive — under a
Turkish culture `I` maps to `ı`, quietly changing route generation) and a non-cached `Regex.Replace`.
Use `ToLowerInvariant()` and a `[GeneratedRegex]` partial method.

**AE-S1-010 · Low · Swagger XML comments only exist for the Api project.**
`IncludeXmlComments` is wired for the executing assembly only, and only `AscendantEmpire.Api.csproj`
sets `GenerateDocumentationFile`. Every documented type that actually appears in the schema
(contracts, enums, DTOs) contributes nothing to the generated docs. Also, `IncludeXmlComments`
throws if the file is absent, so the Api project can never drop that property without breaking
startup.

**AE-S1-011 · Info · `AddSwaggerGen` runs in every environment** but `UseSwagger` is Development-only.
Harmless, just wasted startup work outside Development.

---

### S2 — Authentication & authorisation

**AE-S2-001 · Critical · Most mutating endpoints are unauthenticated.**
`builder.Services.AddAuthorization()` is called with no `FallbackPolicy`
([Program.cs:150](../../AscendantEmpire.Api/Program.cs)), so ASP.NET's default for an action with no
`[Authorize]` is **anonymous**. Only `AuthController`, `MatchMakingController`, `PlayerController`,
`MilitaryController` and 6 of `UnitDesignController`'s 22 actions are protected. Everything below is
reachable with **no token at all**:

| Controller | Unprotected actions |
| --- | --- |
| `BiomesController` | `POST`/`PUT`/`GET campaign-map` — mutates shared biome reference data |
| `GeographyController` | `POST`/`PUT`/`GET campaign-map` — same, for geography |
| `CampaignMapController` | `POST player`, `POST player/clear` |
| `GamesController` | `POST setup`, `POST open` |
| `MapGalleryController` | all 14, including `update-tiles`, `publish`, `import`, `export`, `export/download` |
| `ResourcesController` | `PUT` |
| `SystemController` | `GET status`, **`POST seed`** — kicks off a database-wide seed job |
| `TroopOrdersController` | `POST move` |
| `TurnController` | `GET campaign`, `POST end-campaign` — advances the game turn |
| `UnitsController` | `POST temporary-dev-only/spawn`, `GET {gameId}/display` |
| `UnitDesignController` | `components/bulk`, `catalog`, `components/{code}`, `templates/global` (POST+PUT), `templates/recompile-all`, `tags` (GET/POST/PUT), `tag-types` (GET/PUT), `export`, `export/download`, `import` |

Two of these are worse than "unauthenticated": `POST /system/seed` and
`POST /unit-design/templates/recompile-all` are unbounded server-side work anyone can trigger
repeatedly, and `GET /units/{gameId}/display` takes the game id from the **route** rather than the
token, so it discloses another game's units by id.

Fix at the composition root rather than per action, so the default is safe:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

then add `[AllowAnonymous]` to exactly `register`, `login` and `refresh` (they already have it) and
audit anything that then starts returning 401. Endpoints that are game-scoped should additionally go
through `TryGetGameContext` — see AE-S2-002 and AE-S3-007.

**AE-S2-002 · High · There is no authorisation model beyond "is authenticated".**
No roles, no policies, no ownership checks at the boundary. Concretely:

- `GET /auth/users` ([AuthController.cs:32](../../AscendantEmpire.Api/Controllers/AuthController.cs))
  returns every account to any logged-in user.
- `POST /auth/users/inactive` lets **any** logged-in user deactivate **any** other user — the target
  `userId` comes from the request body with no check that the caller is an administrator or the
  subject.
- The admin-ish tools (`EntitySetupTool` endpoints: tags, tag types, global templates, seeding,
  recompile-all) are indistinguishable from player endpoints.

The token already carries `userId`/`gameId`/`playerId`, so the mechanism exists; what's missing is a
role/claim and policies (e.g. `RequireAdmin`, `RequireGameContext`) to hang the checks on.

**AE-S2-003 · High · Refresh-token rotation has no reuse detection.**
`RefreshSessionService.RotateAsync`
([RefreshSessionService.cs:50](../../AscendantEmpire.Application/Features/Authentication/Services/RefreshSessionService.cs))
overwrites `TokenHash` in place. A stolen refresh token is therefore fully interchangeable with the
legitimate one: whoever refreshes first keeps the session, and the loser simply gets a 401 and is
logged out with no signal that anything was wrong. The standard mitigation is to keep the previous
hash (or a token family) and, on presentation of an already-rotated token, revoke the whole family
and log it as a probable compromise.

**AE-S2-004 · Medium · `RotateAsync` has a lost-update race.**
Two concurrent refreshes with the same token both pass the `IsActive` check, both generate a new
token, and both `SaveChangesAsync`. `RefreshSession` has no concurrency token, so the second write
wins silently and the first client holds a refresh token that will never work. The frontend's
single-flight guard (`src/api/http.ts`) reduces but does not remove this — two browser tabs are two
independent guards. Add a `rowversion` to `RefreshSession` and treat
`DbUpdateConcurrencyException` as "refresh failed, retry/401".

**AE-S2-005 · Medium · `RotateAsync` throws on a deleted user, and ignores user state.**
[RefreshSessionService.cs:72-75](../../AscendantEmpire.Application/Features/Authentication/Services/RefreshSessionService.cs)
uses `FirstAsync` to read the username. If the `User` row has gone while a session row survives,
this throws `InvalidOperationException` — which, given AE-S1-001, becomes an unhandled 500 rather
than the intended 401. Separately, the method never checks the user's active/inactive state, so
`SetUserInactive` does not stop the target refreshing: they keep minting access tokens for up to
`RefreshTokenDays` (14). `SetUserInactiveHandler` should revoke sessions
(`IRefreshSessionService.RevokeByUserAsync` already exists) *and* `RotateAsync` should re-check.

**AE-S2-006 · Medium · No throttling or lockout on the auth endpoints.**
`/auth/login`, `/auth/register` and `/auth/refresh` are anonymous with no rate limiter
(ASP.NET's `AddRateLimiter` isn't used anywhere) and no failed-attempt counter on `User`. Password
guessing and refresh-token brute force are both unbounded.

**AE-S2-007 · Medium · The signing key's length is never validated.**
`Program.cs` fails fast on a blank `Jwt:SigningKey` but not a short one, while
`JwtSettings.SigningKey`'s own doc comment says "Must be long (32+ chars)". HMAC-SHA256 requires a
256-bit key, so a 20-character key passes startup and then throws on the first token issued — i.e.
at first login, not at boot. Validate the byte length next to the existing null check.

**AE-S2-008 · Medium · `session.CreatedAt` is overwritten on every rotation.**
[RefreshSessionService.cs:81](../../AscendantEmpire.Application/Features/Authentication/Services/RefreshSessionService.cs)
reassigns `CreatedAt`, so the column no longer records when the session began — it records the last
refresh. That destroys the only audit trail of session age (`ExpiresAt` is absolute, so it isn't
recoverable from it either). Add a separate `LastRotatedAt` and leave `CreatedAt` immutable.

**AE-S2-009 · Medium · `RefreshSession` rows are never pruned.**
Expired and revoked sessions accumulate forever; every login adds one. There's no cleanup job, and
`UX_RefreshSession_TokenHash` keeps growing. Add a periodic delete of rows past `ExpiresAt`.

**AE-S2-010 · Low · No `jti` claim.**
`JwtTokenService.BuildToken` emits no token identifier, so an individual access token can't be
identified in logs or blacklisted. Revocation latency is bounded by `AccessTokenMinutes` (5) by
design, which is a reasonable trade-off — but the absence of `jti` also means two tokens issued in
the same second to the same session are byte-identical, which is a nuisance when debugging.

**AE-S2-011 · Low · Malformed XML doc comment.**
`Api/Authentication/JwtSettings.cs` — the `<remarks>` on `SigningKey` is never closed and the
following `///` line is missing, so the property's documentation silently swallows into the next
element. (Not caught, because `NoWarn 1591` is set — see AE-S15-004.)

**AE-S2-012 · Low · `EnterGameAsync` mutates the session before minting the token, with no rollback.**
[AuthController.cs:167-177](../../AscendantEmpire.Api/Controllers/AuthController.cs) calls
`UpdateGameContextAsync` and *then* builds the token. If token creation throws, the session is left
pointing at a game the caller never received a token for; the next refresh then silently hands them
a game token they didn't ask for.

**AE-S2-013 · Low · `ClockSkew = TimeSpan.Zero` with a 5-minute access token.**
Deliberate and commented, but the combination is unforgiving: a client whose clock is 30 seconds
fast will reject tokens it should accept for the first 30 seconds of every token's life. Worth a
small non-zero skew (e.g. 30s) rather than zero.

**AE-S2-014 · Info · The dev signing key is committed.**
`appsettings.Development.json` carries a real, working key, clearly labelled dev-only. Acceptable
convenience, with the caveat that anything accidentally started with
`ASPNETCORE_ENVIRONMENT=Development` accepts tokens forged by anyone with repo access.

**AE-S2-015 · Info · Game-scope enforcement is per-handler, and unverified.**
`GameTokenExtensions.TryGetGameContext` is the intended chokepoint, but nothing forces a handler to
use it, and two controllers can't (AE-S3-007). Whether every game-scoped handler actually filters by
the token's `gameId`/`playerId` — rather than trusting a body/route value — needs checking in S8/S10/S11.
Recorded here so it isn't lost. See also the `GET /units/{gameId}/display` case in AE-S2-001.

---

### S3 — API surface, result mapping & validation

**AE-S3-001 · High · The generic `ToActionResult` returns a body on 204, and can null-deref the mapper.**
[ServiceResultExtensions.cs:62-69](../../AscendantEmpire.Api/Utilities/ServiceResultExtensions.cs)
groups `SuccessNoContent` with `Success`/`Created` and builds an `ObjectResult` carrying
`contactMapping(serviceResult.SuccessValue)` at status 204. Two problems in one arm:

1. HTTP 204 must not have a body. ASP.NET will suppress it, so the mapper's work is discarded — and
   any client that expects a body on a documented 204 sees nothing.
2. `SuccessValue` is `TSuccess?`, and on a `SuccessNoContent()` result it is `null` (or `default`).
   It is passed into a `Func<TDataContract, TFrontendContract>` whose parameter is non-nullable, so
   every mapping lambda that dereferences its argument throws `NullReferenceException` — which,
   given AE-S1-001, becomes a bare 500.

Split the 204 case out and return `new NoContentResult()`; make the mapper only run when
`SuccessValue is not null`.

**AE-S3-002 · High · Validation failures are routed through a string-keyed reflection lookup.**
`Api/Middleware/Validators/ValidationBehaviour.cs` finds the failure factory with
`typeof(ServiceResultBase).GetNestedType("ServiceResult`1", ...)`. The type name is a string
literal, so renaming or moving `ServiceResult<T>` compiles cleanly and breaks **all** validation at
runtime: the lookup returns null, control falls through, and the behaviour throws
`ValidationException` — which nothing handles, so a 400 validation error becomes a 500. Use
`typeof(ServiceResultBase.ServiceResult<>)` so the reference is checked by the compiler. (The root
cause is AE-S3-005.)

**Closed by G-135.** The rewritten behaviour constrains `TResponse` to `ServiceResultBase`, so a
failure is an ordinary object initializer over the `StatusCode` and `Errors` properties every result
shape shares. There is no lookup left to fail.

**AE-S3-003 · Medium · Latent `InvalidCastException` in `ValidationBehaviour` for two-arg results.**
When `TResponse` is `ServiceResult<TSuccess, TError>`, `GetGenericArguments()[0]` yields only
`TSuccess`, so the reflected instance is a `ServiceResult<TSuccess>` — not assignable to
`TResponse`. The `failResult is TResponse` test fails, and control reaches
`return (TResponse)(object)ServiceResultBase.Fail(messages)`, which throws `InvalidCastException`
because `ServiceResultBase` is not a `ServiceResult<T,E>`. **Latent:** no handler currently returns
the two-arg form (verified by search), so nothing reaches it today. It becomes live the moment one
does — and would be resolved for free by AE-S3-004.

**Closed by G-135.** With the reflection gone, `new TResponse { ... }` builds whichever closed type
the response actually is, the two-arg form included. Pinned by a test.

**AE-S3-004 · Medium · `ServiceResult<TSuccess, TError>` is dead code.**
~180 lines of `ServiceResultBase.cs` (lines 197-376) plus `ErrorValue`,
`HasPartiallySucceeded` and `FromBase` are never used — no handler returns the two-arg form. Worse,
`ToActionResult` has no overload for it, so `ErrorValue` could not reach a client even if one did.
Delete it, or finish it deliberately; leaving it invites AE-S3-003.

**AE-S3-005 · Medium · `ServiceResult<T>` is nested inside `ServiceResultBase`.**
This forces `using static AscendantEmpire.Application.Common.ServiceResultBase;` in every consumer
(16 controllers and handlers), forces the `new` keyword on almost every static factory (hiding, not
overriding — `ServiceResultBase.Fail` and `ServiceResult<T>.Fail` are unrelated methods that look
related), and is the direct cause of the reflection in AE-S3-002. Promote both to top-level types in
`AscendantEmpire.Application.Common`.

**AE-S3-006 · Medium · `PartialSuccess` is mapped to HTTP 202.**
`ServiceResultStatus.PartialSuccess = 202` with the comment "not a standard HTTP status code" — but
202 *is* standard, and means **Accepted** (async processing started). Both `ToActionResult` overloads
return it as a 2xx, so a partial failure reaches the frontend as a success with a truncated body and
no error channel; `src/api/http.ts` will not raise. Either use 200 with an envelope that carries the
per-item failures, or 207 Multi-Status, and give the frontend something to branch on.

**AE-S3-007 · Medium · `HasSucceeded` and `ToActionResult` disagree on what "success" means.**
`ServiceResultBase.HasSucceeded` excludes `PartialSuccess`
([ServiceResultBase.cs:27](../../AscendantEmpire.Application/Common/ServiceResultBase.cs)), while
`ToActionResult` maps it to a 2xx with a success body. Any caller that branches on `HasSucceeded`
(e.g. `AuthController.IssueLoginResultAsync`) treats a partial success as a failure and returns the
error body instead. One definition, used in both places.

**AE-S3-008 · Medium · `MapGalleryController` and `SystemController` don't derive from `ControllerBase`.**
Unlike the other 13 controllers. `[ApiController]` still binds them, but they have no `User`, no
`Ok()`/`NotFound()`, and — critically — cannot call `TryGetGameContext`, which is an extension on
`ControllerBase`. That is very likely *why* `MapGalleryController`'s 14 actions have no game-scope
check at all (AE-S2-001). Make them derive from `ControllerBase`.

**AE-S3-009 · Medium · Dependencies are injected per-action via `[FromServices]` throughout.**
Every one of ~60 actions repeats `[FromServices] IMediator mediator` and friends
(`AuthController.RegisterAsync` takes four). The effect is that no controller declares its
dependencies in one place, signatures are 4-6 parameters before the actual request body, and the
list has to be maintained action by action. Constructor injection (primary constructors) would
remove the repetition; `[FromServices]` is worth keeping only for the rare service one action needs.

**AE-S3-010 · Medium · No API versioning and no route prefix.**
Every controller is `[Route("[controller]")]`, so routes are `/auth`, `/map-gallery`, `/turn` at the
web root, mixed in with any future static content, and with no `/api` segment and no version. There
is nowhere to put a breaking change. Introduce `/api/v1` (or `Asp.Versioning`) before the surface
gets any larger.

**AE-S3-011 · Medium · `ValidationBehaviour` lives in the wrong project.**
It is a MediatR pipeline behaviour over Application-layer requests, but it sits in
`AscendantEmpire.Api/Middleware/Validators/` — so the Application layer's validation is only applied
when the Api project composes the container. The test project doesn't reference Api (AE-S12-002),
which is exactly why this behaviour has no tests. Move it to
`AscendantEmpire.Application/Common/Behaviours/` and register it in `AddApplicationServices`. The
folder name `Middleware` is also misleading — it isn't ASP.NET middleware.

**Closed by G-135.** Moved to exactly that folder and registered there, and it has tests now that
the test project can reach it.

**AE-S3-012 · Low · Duplicated switch arms in both `ToActionResult` overloads.**
The `PartialSuccess` arm is byte-identical to the success arm above it in both methods, and the six
error arms all fall through to the same `default`. Collapse to `HasSucceeded ? ... : ...` (once
AE-S3-007 is settled) or a single expression switch.

**AE-S3-013 · Low · Typo `contactMapping` should be `contractMapping`** — parameter name and doc
comment in `ServiceResultExtensions.ToActionResult<,>`.

**AE-S3-014 · Low · `DTApiErrorResult.Errors` is a mutable `IEnumerable<string>`.**
Public setter on a response DTO, no `IReadOnlyCollection`, no `[JsonPropertyName]`. Relatedly, the
frontend defensively parses both `Errors` and `errors`
([http.ts](../../AscendantEmpire.AscendantEmpireUI/src/api/http.ts)) — with ASP.NET's default
camelCase policy only `errors` is ever emitted, so the PascalCase branch is dead code born of
uncertainty about the wire format. Pin the contract and drop the branch.

**AE-S3-015 · Low · Two controllers use namespace-block style.**
`BiomesController.cs` and `GeographyController.cs` use `namespace X { ... }` with 4-space-indented
members; the other 13 use file-scoped namespaces. Also `AuthController` mixes `Ok(body)` (in
`RefreshAsync`) with the `ServiceResult`→`ToActionResult` convention used everywhere else.

#### Session 2 additions — endpoint semantics and the contracts projects

**AE-S3-016 · Critical · Five controllers take the caller's identity from the request, with no
authentication.**
This is the concrete, exploitable form of AE-S2-001 and deserves separating from it. Only 3 of 15
controllers (`Player`, `Military`, `UnitDesign`) use `TryGetGameContext`. Five take `GameId` and
`PlayerId` straight from the request body or route **and have no `[Authorize]`**:

| Endpoint | Identity source | Effect |
| --- | --- | --- |
| `POST /turn/end-campaign` | `request.GameId`, `request.PlayerId` ([TurnController.cs:63](../../AscendantEmpire.Api/Controllers/TurnController.cs)) | Anonymous caller ends any player's turn in any game |
| `GET /turn/campaign?gameId=` | `[FromQuery] int gameId` | Reads any game's turn state |
| `POST /troop-orders/move` | `request.GameId`, `request.PlayerId` | Anonymous caller moves any player's troops |
| `POST /units/temporary-dev-only/spawn` | `request.GameId`, `request.PlayerId` | Anonymous unit spawning into any game |
| `POST /campaign-map/player`, `/player/clear` | `request.GameId`, `request.PlayerId` | Reads/clears any player's fog of war |
| `POST /games/open` | `request.GameId`, `request.PlayerId` | Reads any game's open state |

`PlayerController` shows the intended pattern and even comments it ("Game and player come from the
token, so the caller can't ask for someone else's data"). These five predate or ignore it. Fixing
AE-S2-001 stops the anonymous access; these endpoints additionally need their `GameId`/`PlayerId`
parameters **deleted** and replaced with `TryGetGameContext`, otherwise an authenticated player can
still act as any other. `MatchMakingController.ClaimPlayerSlot` also takes both from the body but
combines them with a token-derived `userId` — that one is plausibly legitimate, but the handler
needs checking in S11.

**AE-S3-017 · Medium · `[ProducesResponseType]` documents types that do not exist and statuses that
are not returned.**
Spot checks found the attribute sets are copied and stale:

- `TurnController.cs:54` declares `[ProducesResponseType(typeof(IEndCampaignTurnOutcome), 204)]` —
  a **body type on a 204**, naming an interface that **has no implementation anywhere in the
  solution**. The action returns a bodiless `ServiceResultBase`. Its XML doc says
  `<response code="200">` while the attribute says 204.
- `PlayerController.cs:73` declares `IPlayerTroopCollection` (also unimplemented); the action
  actually returns a bare `List<DTPlayerTroop>` — not a collection wrapper at all.
- `PlayerController.cs:32` declares `IPlayerStructureCollection` but returns
  `DTPlayerStructureCollection`, which is at least shape-compatible.
- Neither `PlayerController` action declares 401 despite `[Authorize]`.
- `TurnController.cs:29` has a stray empty `///` line splitting the doc block.

Since Swagger is the only published description of this API and the frontend is hand-written against
it, these are actively misleading. Consider generating the frontend types from the OpenAPI document
so drift becomes a build error rather than a runtime surprise.

**AE-S3-018 · Medium · Three declarations of the same shape: interface, contract record, and DTO.**
`ICampaignMapTile` (FrontendContracts) → `CampaignMapTile` (BackendContracts record, `init`) →
`DTCampaignMapTile` (Application class, `set`) all declare the same five properties and the last two
both implement the first. There are 31 `DT*` classes in Application alongside 120 BackendContracts
files. The interface layer is not pure overhead — System.Text.Json serialises interface-typed
properties using the *declared* type, so `IReadOnlyCollection<ICampaignMapTile>` genuinely acts as a
projection — but that same behaviour is a footgun: **adding a property to the concrete record and
forgetting the interface silently drops it from the response**, with no compiler error and no test.
Either collapse `DT*` into the BackendContracts records, or document the projection role explicitly.

**AE-S3-019 · Medium · `FrontendContracts` depends on `AscendantEmpire.Domain`.**
`AscendantEmpire.FrontendContracts.csproj` references the Domain project, and contracts such as
`ICampaignMapTile` expose `BiomeType` / `CampaignMapGeographyType` directly. The outermost
wire-format layer is therefore coupled to the innermost domain layer: renaming a domain enum member
is a **breaking API change** that nothing flags, and the enums serialise by name (AE-S1 note), so the
wire format changes silently. Either mirror the enums in the contracts layer, or accept the coupling
explicitly and add a test that pins the serialised names.

**AE-S3-020 · Low · Four `FrontendContracts` interfaces have no implementation.**
`IAddCampaignMapDto`, `IEndCampaignTurnOutcome`, `IPossibleAction` and `IPlayerTroopCollection` are
implemented nowhere. Two are referenced only by the inaccurate `[ProducesResponseType]` attributes in
AE-S3-017; two are referenced nowhere at all. Delete or implement.

**AE-S3-021 · Low · `CampaignMapTileCollection.Tiles` is an uninitialised non-nullable mutable
property.**
`public IReadOnlyCollection<ICampaignMapTile> Tiles { get; set; }` under `Nullable: enable` with no
initialiser — a CS8618 warning that ships because warnings aren't errors (AE-S15-002). It also uses
`set` where its sibling `CampaignMapStartPositionCollection` uses `init` with `= []`. Match the
sibling.

**AE-S3-022 · Low · The mapping lambda's parameter is ignored throughout.**
`PlayerController` (both actions) writes `response.ToActionResult(contract => response.SuccessValue?...)`
— capturing the outer `response` and ignoring the `contract` argument it was handed. It works, and
the `?.` is why AE-S3-001's null-deref isn't hit here, but it means the overload's entire purpose
(handing the mapper a non-null value) is bypassed by convention. Either use the parameter, or drop
it and take a `Func<TFrontendContract>`.

**AE-S3-023 · Low · `CampaignMapGalleryIndex` carries two status representations.**
`required MapCreationStatus CreationStatus` (enum) *and* `required string Status` on the same record,
plus `required string ErrorMessage` — a non-nullable required error message, which forces `""` as a
sentinel on every success. Make `ErrorMessage` nullable and pick one status representation.

**AE-S3-024 · Info · `IEndCampaignTurnOutcome.PossibleActions` is a mutable `List<IPossibleAction>`**
on an otherwise read-only contract interface (every other member is get-only). `IReadOnlyList<T>`
would match the intent. Currently harmless — the interface is unimplemented (AE-S3-020).

---

### S5 — Persistence

**What is good here, so it doesn't get lost:** 84 indexes (31 unique) with explicit names, schema
separation (`game`, `mapgallery`, `util`, `troops`, …), blanket decimal precision, `AsNoTracking` on
57 read paths, `rowversion` on exactly the four genuinely contended entities, and batched
bulk-copy work in the publish path. The findings below are real, but this layer has clearly had
thought put into it.

**AE-S5-001 · Low · `AscendantEmpireContext` is 1,844 lines in one file.** *(Corrected — see the
note at the top of §5.)* `OnModelCreating` is **not** monolithic: it delegates to 17 private
`Configure*Schema` methods, the largest being `ConfigureUnitComponentCatalog` (~305 lines) and
`ConfigureUnitTemplates` (~216). That is a reasonable structure. The remaining point is only that
one 1,844-line file is a merge-conflict magnet and that EF's `IEntityTypeConfiguration<T>` +
`ApplyConfigurationsFromAssembly` would give the same organisation as one file per entity, for free.
Low priority.

**AE-S5-002 · High · Migrations are never applied by the application, and never exercised by tests.**
There is no `Database.Migrate()` anywhere in the Api or Application. Schema changes land only when a
developer remembers to run `dotnet ef database update` — and project memory records the G-20
migration as scaffolded but *not applied*, so the drift is already real. Meanwhile every
containerised test calls `Database.EnsureCreatedAsync()`, which **builds the schema from the current
model and bypasses migrations entirely**. Net effect: a broken, missing, or out-of-order migration
is invisible to the entire test suite and only surfaces against a real database. Either migrate on
startup (with a guard), or add one test that applies migrations to an empty container and asserts no
pending model changes.

**AE-S5-003 · High · `GenerateBlankCampaignMap` takes an exclusive lock on the whole gallery table
to allocate ids.**
[GenerateBlankCampaignMap.cs:28-35](../../AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/GenerateBlankCampaignMap.cs)
hand-rolls identity allocation:

```sql
DECLARE @StartId int = ISNULL((SELECT MAX(CampaignMapGalleryId)
    FROM [mapgallery].[CampaignMapGallery] WITH (HOLDLOCK, TABLOCKX)), 0) + 1;
```

`TABLOCKX` + `HOLDLOCK` takes an exclusive lock on the **entire** `CampaignMapGallery` table — which
holds one row per tile of every map in the gallery — for the duration of the insert. Every reader and
writer of every map blocks behind one map generation. Since generation runs on the background
pipeline (which is already strictly serial, AE-S7-005), this is a second, wider serialisation point.
Use an `IDENTITY`/sequence on `CampaignMapGalleryId` and delete the lock.

**AE-S5-004 · High · The same raw SQL opens a transaction it cannot roll back.**
The same string is `BEGIN TRAN; … COMMIT TRAN;` with **no `SET XACT_ABORT ON`, no `TRY/CATCH`, and no
`ROLLBACK`**. If any statement inside fails, the batch aborts with the transaction still open,
holding the `TABLOCKX` from AE-S5-003. It is eventually cleaned up when the pooled connection is
reset, but until then the gallery table is exclusively locked by an orphaned transaction. The
transaction is also invisible to EF, so it composes with nothing. Add `SET XACT_ABORT ON`, or drop
the T-SQL transaction and use `BeginTransactionAsync` like the other nine call sites do.

**AE-S5-005 · Medium · Game ids are string-concatenated into raw SQL.**
[PublishCampaignMapUpdatesHandler.cs](../../AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/PublishCampaignMapUpdatesHandler.cs)
builds `string gameIdsCsv = string.Join(",", gameIds);` and interpolates it into
`WHERE cm.GameId IN ({gameIdsCsv})`, executed via `ExecuteSqlRawAsync`. **Not exploitable today** —
`gameIds` is an `int[]` read from `db.GameIndex.Select(g => g.Id)`, so the values cannot carry
anything but digits. Two reasons it still matters: it is the exact shape that becomes an injection
the moment the id source changes to anything caller-influenced, and every distinct batch of game ids
produces a distinct SQL text, so SQL Server caches a new plan per batch. Join against the temp table
that is already in scope, or pass a table-valued parameter.

**AE-S5-006 · Medium · A partial publish leaves the database half-updated with no signal.**
`PublishCampaignMapUpdatesHandler` commits **one transaction per game batch** in a loop. If batch 4
of 9 throws, batches 1-3 are already committed and permanent, batches 5-9 never run, and the caller
receives a flat 500 with no indication of how far it got. There is no resume token and no
compensation. Either make the publish idempotent and resumable (record progress per game), or
document that a failed publish must be re-run in full — which the current code does not guarantee is
safe either.

**AE-S5-007 · Medium · Handlers swallow cancellation and convert it into a 500.**
There are 96 `catch (Exception)` blocks across the Application layer, and exactly one mention of
`OperationCanceledException` anywhere in the project. So when a client disconnects or a request is
cancelled, the resulting `OperationCanceledException` is caught by the generic handler, logged as an
error, and returned as `InternalServerError` — polluting the logs with fake failures and hiding real
ones. Add `catch (OperationCanceledException) { throw; }` ahead of the generic catch, or filter with
`when (ex is not OperationCanceledException)`.

**AE-S5-008 · Medium · Every handler re-implements the same try/catch → 500.**
Related to the above and to AE-S1-001: with 96 broad catches across 74 handlers, the "log it and
return `InternalServerError`" boilerplate *is* the error-handling strategy. A single MediatR
exception-handling behaviour plus the missing global exception handler would replace nearly all of
it, and would stop each handler deciding independently how much of the exception to leak into
`Errors` (`ServiceResult.InternalServerError(Exception, string)` puts `ex.Message` and the inner
message straight into the response body — see AE-S5-009).

**AE-S5-009 · Medium · Exception messages are returned to the client.**
`ServiceResult<T>.InternalServerError(Exception ex, string context)`
([ServiceResultBase.cs:182](../../AscendantEmpire.Application/Common/ServiceResultBase.cs)) puts
`ex.Message` **and** `ex.InnerException.Message` into `Errors`, which `ToActionResult` serialises to
the caller. For an EF/SqlException that leaks table names, column names, constraint names and
connection details to an unauthenticated caller (given AE-S2-001). Log the detail; return a
correlation id.

**AE-S5-010 · Medium · `GenerateBlankCampaignMap` holds mutable state on the handler.**
The class has `private BiomeType _defaultBiomeType;` and `_defaultGeographyType` fields that
`ValidateRequestProperties` writes and `GenerateCampaignMapAsync` reads. The two methods must be
called in the right order, from the same instance, and the type is not thread-safe. Return the parsed
values instead of stashing them.

**AE-S5-011 · Medium · `ValidateRequestProperties` cannot fail, and throws on bad input.**
Same file. It always returns `ServiceResultBase.Succeed()` regardless of what it finds — so the name
is a lie. It calls `JsonSerializer.Deserialize<Dictionary<string, string>>(index.PropertiesJson)`
with no try/catch on a property that is `string?`: malformed JSON throws `JsonException` and a null
throws `ArgumentNullException`, both of which become an unhandled 500. And when
`propertyDictionary` is null the defaults are never assigned at all, so generation silently proceeds
with `default(BiomeType)`/`default(CampaignMapGeographyType)` — whatever enum member happens to be 0.

**AE-S5-012 · Medium · `AscendantEmpireContextFactory` hard-codes a connection string.**
[AscendantEmpireContextFactory.cs](../../AscendantEmpire.Application/Data/AscendantEmpireContextFactory.cs)
pins `Server=CoreOS\SQLExpress;…` with the comment "Use your real connection string here". It is
design-time only, but it means `dotnet ef` always targets one developer's machine — anyone else has
to edit a tracked file to scaffold a migration, and risks committing it. Read from configuration or
an environment variable.

**AE-S5-013 · Medium · No concurrency token on `RefreshSession` (cross-ref AE-S2-004).**
`IsRowVersion` is applied to exactly four entities — `Battle`, `BattleParticipant`, `TurnQueue`,
`TurnQueueParticipant` — which is a well-judged choice for the contended game state. `RefreshSession`
is the notable omission: it is written concurrently by design (every tab refreshing at once) and has
a documented lost-update race. Add it there.

**AE-S5-014 · Low · `DeleteBehavior.NoAction` on 75 of 86 relationships, with no delete paths.**
Only 4 `Cascade` and 7 `Restrict`. Consistent with an append-only design — and there are no
game/player delete handlers anywhere, so nothing currently trips it. But it does mean a game can
never be removed, which contradicts `RefreshSessionService.UpdateGameContextAsync`'s comment that
sessions can be revoked "when the game is deleted" and the existence of `RevokeByGameAsync`: that
capability is built and unreachable. Decide whether games are deletable and make the model say so.

**AE-S5-015 · Low · Map size is silently capped by a seeded helper table.**
Blank-map generation cross-joins `util.Numbers`, seeded 0..1,000,000 by
`20260614094425_PopulateNumbersTable`. `AddOrUpdateCampaignMapIndexValidator` does bound Width/Height,
so the two agree today — but nothing links them. Raise `MAX_WIDTH` past the seeded range and map
generation quietly produces a **smaller map than requested** with no error. Add a comment on both
sides, or derive the validator's bound from the seed.

**AE-S5-016 · Low · One generator resolves its context differently from everywhere else.**
`GenerateBlankCampaignMap` uses `IServiceScopeFactory` → `GetRequiredService<AscendantEmpireContext>()`
and disposes it with `using`, while the other ~70 handlers use `IDbContextFactory` with `await using`.
Synchronous disposal of an async-disposable context blocks on connection teardown.

**AE-S5-017 · Low · `GenerateCampaignMapAsync` accepts no `CancellationToken`,** so the longest
database operation in the codebase cannot be cancelled — including by the background dispatcher's
shutdown token (AE-S7-003). Twelve `mediator.Send(...)` calls also omit the token
(e.g. [CampaignMapController.cs:99](../../AscendantEmpire.Api/Controllers/CampaignMapController.cs),
[GamesController.cs:57](../../AscendantEmpire.Api/Controllers/GamesController.cs)).

**AE-S5-018 · Low · Blanket `decimal(18,3)` for every decimal in the model.**
`SetDecimalPrecision(modelBuilder, 18, 3)` walks all entity types and applies one precision to all 10
decimal properties. It removes the "decimal without precision" warning uniformly, which is good, but
3 decimal places is an arbitrary fit for values that are probably a mix of ratios, multipliers and
UI positions (`UD-15_SlotUIPositionToDecimal`). Confirm 3 is right for each, or set them explicitly.

**AE-S5-019 · Info · The publish path mixes EF and raw ADO.NET on one connection.**
`UpdateCampaignMapTilesAsync` creates `#GalleryTiles` via a raw `SqlCommand`, fills it with
`SqlBulkCopy`, then queries it through `db.Database.ExecuteSqlRawAsync` — relying on EF reusing the
same already-open, transaction-enlisted connection so the temp table is visible. It works, and the
`finally` drop is correct, but it is the most fragile code in the persistence layer and has no test.
Worth a comment explaining the connection-sharing assumption.

**AE-S5-020 · Info · 25 migrations, sensibly named and ticket-prefixed** (`UD-27_…`, `G-20_…`,
`ES-1_…`) with a clean chronological order. No destructive operations spotted in the migration names
beyond the deliberate `Drop`/`Remove` ones, each of which pairs with a documented ticket.

---

### S7 — Background processing pipeline

**AE-S7-001 · High · Queued jobs are lost on restart.**
The queue is an in-process `Channel<int>` (`BackgroundProcessingChannel`). A `Processing` row is
persisted and its id written to the channel, but nothing reconciles the two: if the process stops
before the dispatcher drains the id, the row stays `Queued` forever and the UI polls
`/system/status` indefinitely. There is no startup sweep for orphaned `Queued`/`Running` rows and no
retry. A startup recovery pass that re-enqueues `Queued` rows and fails (or re-enqueues) `Running`
ones is the minimum; a durable queue is the real fix if jobs matter.

**AE-S7-002 · Medium · A full queue blocks the HTTP request that's enqueuing.**
`FullMode = BoundedChannelFullMode.Wait` with `ChannelCapacity: 50`. The comment frames this as
"backpressure … rather than dropping jobs", but the caller is a web request: once 50 jobs are
queued, `WriteAsync` awaits until a slot frees, so the user's POST hangs (up to the frontend's 30s
axios timeout) instead of being told the system is busy. `TryWrite` + a 503/429 result gives the
client something actionable.

**AE-S7-003 · Medium · Shutdown cancels in-flight jobs and skips the drain.**
`DispatchAsync` passes `stoppingToken` straight to `executor.ExecuteAsync`, so a deploy or restart
aborts a running map generation mid-write. And because `ReadAllAsync(stoppingToken)` throws
`OperationCanceledException` out of `ExecuteAsync`, the `"BackgroundProcessingService stopped."` log
line is unreachable. Separate "stop accepting work" from "cancel the running job", and give the
current job a bounded grace period.

**AE-S7-004 · Medium · Every executor is constructed for every job.**
`scope.ServiceProvider.GetServices<IProcessExecutor>().FirstOrDefault(e => e.ProcessType == ...)`
instantiates all seven executors — and their whole dependency graphs — to use one. It also silently
picks the first when two executors claim the same `ProcessType`, with no startup check for
duplicates. Keyed services (`AddKeyedScoped<IProcessExecutor>(ProcessType.MapExport, ...)`) or a
`ProcessType`→factory dictionary fixes both.

**AE-S7-005 · Medium · Strictly serial execution with no priority.**
`SingleReader = true` plus one sequential `await foreach` means a long map generation blocks every
other job behind it, including trivial ones. Fine as a documented constraint; a problem the first
time two users are in the tool at once. Consider a small degree of parallelism, or at least a
separate lane for cheap jobs.

**AE-S7-006 · Medium · No per-job timeout.**
Nothing bounds `executor.ExecuteAsync`. Combined with AE-S7-005, one hung executor stalls the
pipeline permanently with no diagnostic beyond a row stuck in `Running`. Wrap each dispatch in a
`CancellationTokenSource` with a configured budget.

**AE-S7-007 · Medium · A second instance would double-process every job.**
`Processing` rows are never claimed — there's no owner/lease column and no atomic status transition
from `Queued` to `Running` at dispatch. Single-instance-only is a real constraint that isn't recorded
anywhere; note it in the class docs at minimum, and add a claim before it stops being true.

**AE-S7-008 · Low · The failure backstop reuses the executor's context.**
`DispatchAsync`'s `catch` writes `Status = Failed` through the same `AscendantEmpireContext` the
executor may have just left with tracked, partially-applied changes — so `SaveChangesAsync` can
persist unintended entities alongside the status, or fail for an unrelated reason. Use a fresh
context (or `ExecuteUpdate`) for the status write.

**AE-S7-009 · Low · `DateTime` vs `DateTimeOffset` inconsistency.**
`processing.CompletedAt = DateTime.UtcNow` here, while `RefreshSession` uses `DateTimeOffset`. Pick
one representation for the whole domain — see S6.

**AE-S7-010 · Info · `IProcessExecutor` is `internal`** yet forms the extension point for the whole
pipeline. Fine while every executor is in the Application assembly; worth noting if that changes.

*Still to do:* the seven executors themselves are deferred to their owning feature slices
(map export/import/generate/preview → S8, catalog export/import → S9, database seed → S11).

---

### S4 — Application layer conventions

**AE-S4-001 · Critical · `POST /turn/end-campaign` is broken at runtime — no handler is registered.**
`EndCampaignTurnHandler`
([EndCampaignTurnHandler.cs:61](../../AscendantEmpire.Application/Features/Turns/Handlers/EndCampaignTurnHandler.cs))
declares no base type at all:

```csharp
internal class EndCampaignTurnHandler(
    ILogger<EndCampaignTurnHandler> logger, …, IOpenGameService openGameService)
{
    public async Task<ServiceResult<EndCampaignTurnOutcome>> Handle(EndCampaignTurnRequest request, …)
```

It is the **only** handler in the solution missing `IRequestHandler<,>` (verified across all 74).
MediatR discovers handlers by that interface, so nothing is registered for `EndCampaignTurnRequest`,
and `TurnController.EndPlayerTurnAsync`'s `mediator.Send(new EndCampaignTurnRequest(...))` throws
`InvalidOperationException: No service for type 'IRequestHandler<EndCampaignTurnRequest,
ServiceResultBase>' has been registered`. With no global exception handler (AE-S1-001) that is a bare
500 on **every attempt to end a campaign turn** — the core action of the game loop.

The root cause is a signature mismatch that makes the interface impossible to add as-is: the request
declares `IRequest<ServiceResultBase>` but `Handle` returns `ServiceResult<EndCampaignTurnOutcome>`.
One of the two has to change (returning the outcome is presumably the intent, given AE-S3-017).

**Why no test caught it:** `EndCampaignTurnHandlerTests` constructs the handler directly
(`new EndCampaignTurnHandler(...)`) and calls `.Handle(...)`, so all its tests pass against a type
that is never wired into the application. This is precisely the gap AE-S12-002 describes — a single
HTTP-level test, or one assertion that every `IRequest<T>` has a registered handler, would have
caught it immediately.

**AE-S4-002 · High · Two thirds of requests have no validator.**
75 `IRequest<>` types, 26 `AbstractValidator<>` types. `ValidationBehaviour` short-circuits when no
validator exists (`if (validators.Any() == false) return await next(cancel);`), so ~50 requests
reach their handler with entirely unchecked input — including mutating ones:
`AddCampaignMapRequest`, `UpdateCampaignMapTilesRequest`, `PutCampaignMapStartPositionsRequest`,
`CreateUnitTemplateRequest`, `UpdateUnitTemplateRequest`, `CreateGlobalUnitTemplateRequest`,
`BulkUpsertUnitComponentsRequest`, `UpsertResourcesRequest`, `UpsertTagTypesRequest`,
`SaveOfficerRanksRequest`, `SaveOrganisationStructureRequest`, `PublishCampaignMapUpdatesRequest`
and `RecompileAllUnitTemplatesRequest`. Combined with AE-S2-001 (most of these endpoints are
anonymous) this is a wide unvalidated surface. A test that enumerates `IRequest<>` implementations
and asserts a validator exists would close the gap permanently and cheaply.

**AE-S4-003 · High · `GetPlayerCampaignMapHandler` blocks on `.Result` in five places.**
[GetPlayerCampaignMapHandler.cs](../../AscendantEmpire.Application/Features/Maps/CampaignMaps/Handlers/GetPlayerCampaignMapHandler.cs)
lines 103, 104, 199, 307, 343. Lines 103/104 follow a `Task.WhenAll` and are harmless, but 307 and
343 are the damaging ones:

```csharp
Task<ServiceResult<…>> fetchStructures = mediator.Send(new GetPlayerStructuresRequest(…), cancelToken);
ServiceResult<…> structuresResult = fetchStructures.Result;   // blocks the thread
```

Three consequences. It **blocks a thread-pool thread** for the whole database round-trip on the
single most expensive read in the game (the fog-of-war map). It **wraps any exception in
`AggregateException`**, so the enclosing `catch (Exception ex)` reports "One or more errors
occurred" instead of the real failure. And because `GetStructureObserversAsync`/
`GetTroopObserversAsync` have no `await` before the blocking call, they run **synchronously to
completion** when invoked — so the `Task.WhenAll(fetchStructureObservers, fetchTroopObservers)` at
the call site is parallelism in appearance only; the two run strictly one after the other. Replacing
`.Result` with `await` fixes all three and makes the `WhenAll` real.

**AE-S4-004 · Medium · Ownership checks compare a resource's owner to a caller-supplied id.**
`MoveTroopHandler` does check ownership
([MoveTroopHandler.cs:259](../../AscendantEmpire.Application/Features/TroopOrders/Handlers/MoveTroopHandler.cs)):
`if (troop.PlayerId != request.PlayerId)`. But `request.PlayerId` comes from the request body
(AE-S3-016), so the check is self-referential — a caller who supplies the victim's player id passes
it. The same shape recurs across the game-scoped handlers. The checks are not wrong, they are
checking the wrong thing: they must compare against the **token-derived** player id. Worth
re-auditing every handler that takes a `PlayerId`/`GameId` parameter once AE-S3-016 is fixed.

**AE-S4-005 · Medium · `EndCampaignTurnHandler` validates the request manually, three times over.**
Lines 77-85 do `new EndCampaignTurnRequestValidator()` and then call `.Validate(request)` **twice**
(once for the `is not { IsValid: true }` pattern, again to read `.Errors`) — while the same validator
is also auto-registered and run by `ValidationBehaviour`. So the request is validated three times per
call, and the handler duplicates a cross-cutting concern it should be able to rely on.

**AE-S4-006 · Medium · `EndCampaignTurnOutcome.PossibleActions` is a public field, not a property.**
`public List<PossibleAction> PossibleActions = [];` on a `record`. System.Text.Json **ignores fields
by default**, so this collection would silently serialise as absent — the one piece of information
the endpoint exists to return. Currently masked by AE-S4-001 (the endpoint never runs) and AE-S3-017
(the controller returns a bodiless `ServiceResultBase` anyway). Make it a property.

**AE-S4-007 · Medium · Handlers call `IMediator` to reach other handlers.**
`GetPlayerCampaignMapHandler` sends `GetPlayerStructuresRequest`/`GetPlayerTroopsRequest`;
`MoveTroopHandler` and `GetCampaignMapTileRadiusHandler` send `GetOpenGamesRequest`. Nested
dispatch makes the call graph invisible to the compiler, runs the whole validation pipeline again per
inner call, and means a change to an inner request's shape breaks callers with no compile error.
Extract the shared reads into services (the codebase already does this well elsewhere —
`ICampaignTurnService`, `IOpenGameService`, `IMovementProfileFactory`).

**AE-S4-008 · Low · 33 `TODO`s, several in the core turn logic.**
`EndCampaignTurnHandler` alone carries TODOs saying the end-turn eligibility rules are not
implemented ("`TODO: Implement logic to determine if the turn can be ended based on the EndTurnMode`")
and that the post-auto-action movement flow needs redesign. Worth converting to tickets so they are
visible outside the file.

**AE-S4-009 · Low · Naming drift on constants.**
`EndCampaignTurnHandler._MAX_TURN_TIME_HOURS` uses an underscore-prefixed field convention for a
`const`, whereas the domain entities use plain `SCREAMING_CASE` (`TOKEN_HASH_MAX_LENGTH`,
`USERNAME_MAX_LENGTH`). Cosmetic, but it is the kind of thing an analyzer (AE-S15-002) settles.

---

### S6 — Domain model

**AE-S6-001 · Medium · The Domain project is a persistence schema, not a domain model.**
67 entity files; exactly **two** contain any behaviour (`RefreshSession.IsActive`, and one file inside
`_not_used_yet`). Every rule — turn eligibility, movement cost, fog of war, template compilation,
resource upkeep — lives in handlers. That is a legitimate choice for EF-centric CQRS and it is
applied consistently, so this is not a defect. It is worth naming explicitly, because the project has
a substantial `docs/design/` (1,317 lines across 17 documents) describing rules that currently have
no home in the type system: nothing stops a handler constructing an invalid entity, and the design
docs cannot be traced to code. If the rules stay in handlers, say so in `CLAUDE.md` so nobody
half-migrates them later.

**AE-S6-002 · Medium · Six files carry a namespace from an abandoned rename.**
Everything under `Entities/Resources/_not_used_yet/` declaring a namespace uses
`AscendantEmpire.Domain.Entities.**Assets**._not_used_yet` while living in the `Resources` folder —
`context-changes.cs`, `FactionResourcePriority.cs`, `FactionResourceState.cs`, `ResourceBank.cs`,
`ResourceBankBalance.cs`, `WorldResourceSnapshot.cs`. A `Resources` ← `Assets` rename stopped
halfway. (Related to the memory note about avoiding entity names as feature-folder names.)

**AE-S6-003 · Medium · `User` can hold two contradictory representations of "inactive".**
`InactiveAt` is `DateTimeOffset?` and `InactiveReasonType` is a non-nullable enum with an `Active`
member. Nothing prevents `InactiveAt = null` with `InactiveReasonType = Banned`, or a timestamp with
`Active`. Whichever one the read paths trust, the other can silently disagree. Derive one from the
other, or add a check constraint.

**AE-S6-004 · Low · `Processing` is the sole `DateTime` holdout.**
`StartedAt`/`CompletedAt` are `DateTime` while all 16 other temporal properties in the domain are
`DateTimeOffset`. Cross-ref AE-S7-009. Also `ProgressPercent` is a `byte` documented as "between 0
and 100" with no constraint enforcing it, and the entity's two enums come from two different
namespaces (`Enums.Processing.ProcessingStatus`, `Enums.System.ProcessType`).

**AE-S6-005 · Low · Three namespace/folder mismatches beyond AE-S6-002.**
`Entities/System/Processing.cs` declares `Entities.BackgroundProcessing`;
`Enums/System/ProcessingStatus.cs` declares `Enums.Processing`. So "processing" lives under three
different names across two projects. Pick one.

**AE-S6-006 · Low · `_not_used_yet/context-changes.cs` is committed scratch work.**
A class literally named `context_changes` whose entire body is commented-out `ValueConverter`
snippets. It ships in the Domain assembly. Move it to a scratch file outside the solution or delete
it — the useful part is a two-line note in the DbContext.

**AE-S6-007 · Info · `= null!` on required navigations, `required` on required scalars.**
Twenty files mix the two idioms (`RefreshSession.User = null!` alongside `required string TokenHash`).
Both are correct EF practice for what they express; noting it so it isn't "fixed" into inconsistency
later.

---

### S8 / S9 / S10 / S11 — feature slices *(targeted, not exhaustive)*

> **Scope note, stated honestly:** these four slices cover ~18,000 lines. This pass was a targeted
> hunt — largest files, transaction/SQL/concurrency sites, authorisation paths, and the request→
> handler wiring — rather than a line-by-line read. The findings below are real; **absence of a
> finding in a file here is not evidence it is clean.** The highest-value follow-up is a dedicated
> session per slice on the algorithmic cores: `PathFinder`, `GeneratePerlinNoiseCampaignMap`,
> `CompileUnitTemplateHandler` and `GameSetupService`.

Findings already recorded elsewhere that belong to these slices: AE-S4-001 and AE-S4-005/006 (S10,
turns), AE-S4-003 and AE-S5-003/004/005/006/010/011 (S8, maps), AE-S4-004 (S10, troop orders),
AE-S7-001…009 (the executors these features enqueue).

**AE-S8-001 · Medium · `GetPlayerCampaignMapHandler` is 834 lines in one file** and
`BulkUpsertUnitComponentsHandler` is 849 (S9). Both mix orchestration, querying, projection and
domain rules in a single type. These are the two files where a defect is most likely to hide and the
hardest to test in pieces; they are the natural first targets for extraction into services.

**AE-S9-001 · Medium · `UpdateCampaignMapTilesHandler`, `CompileUnitTemplateHandler`,
`BulkUpsertGlobalUnitTemplatesHandler` and `CatalogExportExecutor` all issue queries inside loops.**
Whether each is an actual N+1 depends on the collection size, which this pass did not measure — for
the catalog and template paths those collections are the whole component catalog, so it is worth
measuring. Flagged for the per-slice follow-up rather than asserted as a defect.

**AE-S10-001 · Medium · The turn pipeline's abstractions are in place but the rules are not.**
`ITurnServicePipeline<IEndCampaignTurnProcess>` and its Start/End Round/Turn siblings, plus the four
`IPossibleActionCheck` implementations, are cleanly registered and structured. But
`EndCampaignTurnHandler` carries TODOs saying the eligibility rules themselves are unimplemented, and
the handler is unreachable anyway (AE-S4-001). The scaffolding is good; treat the turn loop as
unfinished rather than as working code with bugs.

**AE-S11-001 · Medium · `POST /units/temporary-dev-only/spawn` is an anonymous, permanent-looking
"temporary" endpoint.** Named `temporary-dev-only` in the route, present on `master`, with no
`[Authorize]` (AE-S2-001) and no environment guard. Gate it behind `IWebHostEnvironment.IsDevelopment()`
at minimum, or delete it.

---

### S12 — Test suite *(partial — structural only)*

**AE-S12-001 · High · The frontend is effectively untested.**
5 test files for ~23,000 lines of TS/TSX
(`DescriptiveSettingDropDown`, `ResourceSliderCollection`, `ResourceTicker`, `slotImagePosition`,
`UnitComponent`). The whole `src/api` layer — including the refresh-and-retry interceptor, which is
subtle enough to have its own concurrency guard — has no tests at all, as do all 10 feature areas.

**AE-S12-002 · High · The HTTP layer has zero test coverage.**
`AscendantEmpire.Tests.csproj` references Application, BackendContracts and FrontendContracts — but
not `AscendantEmpire.Api`, and doesn't use `Microsoft.AspNetCore.Mvc.Testing`. So nothing tests:
authorisation attributes (AE-S2-001 would have been caught instantly by one test asserting 401 on an
unauthenticated `POST /system/seed`), JWT issuance/validation, `ValidationBehaviour` (AE-S3-002/003),
`ToActionResult` mapping (AE-S3-001), or the `ClaimsPrincipal`/`GameToken` extensions. A
`WebApplicationFactory`-based fixture plus a table-driven "every endpoint requires auth unless
allow-listed" test would cover the highest-risk gap cheaply.

**AE-S12-003 · High · 86 tests for ~127,000 lines of C#.**
Across 48 files. For scale: the four slices S8-S11 alone are ~18,000 lines of game logic. The suite
is not close to being a safety net for a refactor, and AE-S4-001 is the proof — the single most
important action in the game (ending a turn) is dead at runtime while its seven tests pass.

**AE-S12-004 · Medium · Tests bypass the very thing that would have caught the live bugs.**
Two structural habits, both visible in `EndCampaignTurnHandlerTests`: fixtures construct handlers
with `new Handler(...)` rather than resolving them through the container, and containerised fixtures
call `Database.EnsureCreatedAsync()` rather than applying migrations (AE-S5-002). So the suite
verifies handler *bodies* while never exercising handler *registration*, the MediatR pipeline, or the
migration set. Three cheap tests would close most of this:

1. Enumerate every `IRequest<T>` in the Application assembly and assert the container resolves an
   `IRequestHandler<,>` for it. (Catches AE-S4-001, permanently.)
2. Enumerate every `IRequest<T>` and assert a validator exists. (Catches AE-S4-002.)
3. A `WebApplicationFactory` test that hits every route unauthenticated and asserts 401 unless
   allow-listed. (Catches AE-S2-001 and AE-S3-016, permanently.)

**AE-S12-005 · Medium · The tier distribution still skews heavy.**
After the in-flight refactor: 10 fixtures on `SqlServerTestBase`, 3 on `GameSqlServerTestBase`, 3 on
`CampaignTurnServiceTestBase` — versus 6 on `InMemoryTestBase`. That is 16 containerised fixtures to
6 in-memory, which sits awkwardly against `testing.md`'s "use the lightest option that still
exercises what the test needs". Some are certainly justified (`ExecuteUpdate`, raw SQL, constraint
enforcement all appear in the code under test), but the ratio suggests the tier decision is being
made by copying the nearest fixture. Worth a pass with the `testing.md` decision tree applied
per fixture, once the refactor lands.

**AE-S12-006 · Medium · FluentAssertions 8.x is referenced but barely used — and is now
commercially licensed.**
10 `.Should()` calls against 955 NUnit `Assert.` calls. FluentAssertions changed to a paid licence
for commercial use at v8, and `AscendantEmpire.Tests.csproj` pins `8.10.0`. For 10 assertions that is
a licensing question with no upside — drop the package and convert them, or adopt it properly and
resolve the licence. Worth deciding deliberately rather than by default.

**AE-S12-007 · Low · An empty `Handlers\Turns\` placeholder folder** in the test csproj (see
AE-S15-006) sits next to a real `Features\Turns\Handlers\` — two conventions for the same area,
one of them empty.

*Still to do in S12:* per-fixture tier compliance against the `testing.md` decision tree, assertion
quality, shared fixture/seed-data design, and Testcontainers flakiness. Best done after the in-flight
test-tier refactor lands.

---

### S13 — Frontend architecture & shared layers *(partial)*

**AE-S13-001 · Medium · Logout is `window.location.reload()`, called from an interceptor.**
`src/api/http.ts` responds to an unrecoverable 401 with `clearAuth(); window.location.reload();`.
Three consequences: any unsaved UI state is destroyed; the reload lands on whatever page the user
was on rather than a login screen (each Vite entry point is a separate HTML page, so behaviour
differs per entry); and because the handler is per-request, N concurrent 401s trigger N reloads.
Route the session-expired event through React (a context/`queryClient` invalidation) instead.

**AE-S13-002 · Medium · The single-flight refresh guard is cleared by whichever caller resumes first.**
`refreshPromise = refreshPromise ?? refreshAccessToken(); await refreshPromise; refreshPromise = null;`
— the `null` assignment runs in every awaiting caller, and the shared promise is discarded by the
first one to resume rather than by the refresh completing. On failure, every joined caller also runs
`clearAuth()` and `window.location.reload()`. Attach the reset to the promise itself
(`.finally(() => { refreshPromise = null; })`) so the guard's lifetime matches the operation's.

**AE-S13-003 · Medium · `updateTokens` drops the game context returned by `/auth/refresh`.**
The server's refresh response includes `gameId`/`playerId` (`AuthController.RefreshAsync`), and the
session is the authority on which game is entered — but `RefreshResult` in `http.ts` types only
`{ token, refreshToken }`, and `updateTokens` preserves the *client's* stored `gameId`/`playerId`. If
the server-side game context ever differs (another tab entered a different game, or the game was
closed), the client keeps stale values while its token says otherwise, and `hasGameContext()` lies.

**AE-S13-004 · Low · The API base URL falls back to a hard-coded `https://localhost:7242`.**
`import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7242"` — a missing env var silently
produces a build that points at a developer's machine rather than failing. Fail loudly in
production builds.

**AE-S13-005 · Info · Access *and* refresh tokens live in `localStorage`.**
Documented as a known trade-off in `authToken.ts`, with the httpOnly-cookie alternative spelled out.
Worth restating that the refresh token is the more serious half: an XSS gets 14 days of access, not
5 minutes. Recorded as accepted, not as a recommendation to change now.

#### Session 3 additions

**What is good here:** `tsconfig.app.json` is genuinely strict (`strict`, `noUnusedLocals`,
`noUnusedParameters`, `noFallthroughCasesInSwitch`, `verbatimModuleSyntax`), and it holds — **zero**
`any`, `as any`, `@ts-ignore` or `@ts-expect-error` in 23,000 lines. 174 `aria-`/`role` attributes
and zero `onClick` on a bare `<div>`. The multi-page Vite setup is deliberate and documented. This is
the most disciplined part of the codebase.

**AE-S13-006 · Medium · No error boundary anywhere in the app.**
No `ErrorBoundary` or `componentDidCatch` in any of the 253 source files. React 19 unmounts the whole
tree on an unhandled render error, so any exception in any of the 96 `useEffect`s or their components
blanks the entire page with no message and no recovery — and because each Vite entry is its own
document, the user's only route back is a manual reload. One boundary per entry point in
`AppProviders` would cover all five.

**AE-S13-007 · Medium · React Query Devtools ship to production.**
`AppProviders.tsx` renders `<ReactQueryDevtools />` unconditionally, so the devtools bundle is in
every production build and the full query cache — including anything fetched for the logged-in user —
is inspectable from the deployed page. Gate on `import.meta.env.DEV`.

**AE-S13-008 · Low · Dev-server settings disable every cache Vite has.**
`vite.config.ts` sets `server.watch.usePolling: true` with `interval: 100` (polls the filesystem ten
times a second on a 23k-line project) and `optimizeDeps.force: true` (re-bundles all dependencies on
every start, defeating the dep cache). Both are described as safety measures for change detection;
both cost startup time and CPU on every dev run. Worth re-testing whether they are still needed.

**AE-S13-009 · Low · No accessibility linting.**
`eslint-plugin-jsx-a11y` is not in `package.json` or `eslint.config.js`. The hand-written markup is
better than average (see above), which is exactly the position from which the plugin is cheap to
adopt and will stay green.

**AE-S13-010 · Low · 13 `console.log`/`console.debug` calls left in `src`,** with no `no-console`
rule to catch them.

---

### S14 — Frontend features *(targeted, not exhaustive)*

> **Scope note:** ~19,000 lines across 10 feature areas. This pass looked at structure, component
> size, state handling and test coverage rather than reading every component. As with S8-S11, absence
> of a finding is not evidence of correctness.

**AE-S14-001 · Medium · The largest components carry too much local state.**
`EntitySetupTool.tsx` (512 lines) holds **23** `useState` hooks in one component;
`UnitDesigner.tsx` (500 lines) holds 11. At that density the valid combinations of state are
untrackable and effects start depending on each other's ordering — which is where the 96 `useEffect`s
become a risk. A `useReducer` (or splitting the component) makes the state machine explicit and
testable, which matters most here because these two files have no tests at all.

**AE-S14-002 · Medium · Feature test coverage is effectively zero where the logic is.**
Of five test files, three cover `game/resources` and two cover small `unitdesign` helpers. The four
largest features — `game` (5,367 lines), `mapgallery` (4,524), `entitysetuptool` (2,977),
`unitdesign` (2,977) — have no component or hook tests. The testing infrastructure is already set up
and working (`vitest`, `jsdom`, Testing Library, a `setup.ts`), so the cost here is writing tests, not
building a harness. Cross-ref AE-S12-001.

**AE-S14-003 · Low · Several dialogs exceed 400 lines** (`NewCampaignMapDialog` 494,
`BiomeManagerDialog` 440, `MapGalleryEditor` 422, `useMapGalleryEditorToolbar` 414,
`SlotsSection` 423). Not defects, but they are where AE-S14-001's state density concentrates.

**AE-S14-004 · Info · `ResourceDummyData.ts` (379 lines) ships placeholder data in `src`.**
Consistent with the memory note that resource cost/upkeep are placeholder stand-ins. Worth a
`TODO`/ticket link in the file so it isn't mistaken for real data later.

---

*S13 remaining:* per-hook review of `src/hooks`, `src/components/ui`, and a field-by-field alignment
check between `src/types` and the C# contracts (AE-S3-019 makes that check worth doing properly).

---

### S15 — Build, tooling, CI & solution hygiene *(partial)*

**AE-S15-001 · High · There is no CI.**
`.github/workflows/` exists and is **empty**. Nothing builds the solution, runs the 48 test fixtures,
runs `tsc -b`, `eslint` or `vitest` on push or PR. Every finding in this document is a finding that
CI could have surfaced continuously; AE-S2-001 in particular is the kind of thing a single
authorisation test in CI would have blocked. A minimal workflow (`dotnet build` + `dotnet test` +
`npm ci && npm run build && npm run lint && npm test`) is the highest-leverage item in this slice.

**AE-S15-002 · Medium · No shared build configuration and no analyzers.**
Six csprojs each repeat `TargetFramework`/`Nullable`/`ImplicitUsings`, with no
`Directory.Build.props`. `TreatWarningsAsErrors` is off everywhere, no analyzer package is
referenced, and there is no `.editorconfig`-driven style enforcement — so the conventions in
`CLAUDE.md` (braces on `if`/`for`, etc.) are unenforced and the codebase drifts (AE-S3-015).

**AE-S15-003 · Medium · The frontend is not in the solution and has no shared build step.**
`AscendantEmpire.AscendantEmpireUI` appears nowhere in `AscendantEmpire.sln`, so a solution build
never type-checks the 23k lines of TS, and the contract drift between `FrontendContracts` and
`src/types` has nothing watching it.

**AE-S15-004 · Low · `NoWarn 1591` on the Api project.**
The one project that *does* generate XML documentation (for Swagger) suppresses missing-comment
warnings — so undocumented public API surface is invisible, and malformed doc comments like
AE-S2-011 go unnoticed.

**AE-S15-005 · Low · `NanoBananaConsole` is a csproj outside the solution; `WeightedRandom` is a
project with no csproj.**
`NanoBananaConsole/NanoBananaConsole.csproj` exists but isn't referenced by `AscendantEmpire.sln`
(only `MapGenerationPlay` is), so it is never built and can rot silently. `WeightedRandom/` contains
only `bin`/`obj` — its sources appear to be gone. Decide: adopt into the solution, or delete.

**AE-S15-006 · Low · Four csprojs carry empty placeholder `<Folder Include>` entries.**
`Application` → `Features\Turns\Classes\`, `BackendContracts` → `Biomes\AddCampaignMapBiome\`,
`Domain` → `Entities\Faction\`, `Tests` → `Handlers\Turns\`. Dead scaffolding from abandoned starts;
each is a small false signal about where code lives.

**AE-S15-007 · Info · `docker-compose.yml` only defines the test SQL Server**, with the SA password
inline. Fine for a Testcontainers-adjacent convenience; just note it is not an app-hosting compose
file, which the filename implies.

#### Session 3 additions — docs vs implementation

**AE-S15-008 · Medium · The authentication doc contradicts the implementation on its central claim.**
`docs/authentication-and-authorisation.md` states: "Auth is **stateless** and based on **JWT bearer
tokens**. There is no server-side session; everything the API needs is signed into the token."

There *is* a server-side session. `RefreshSession` is a database table whose own XML doc says the
opposite — "Because the session lives in the database it can be revoked … which the stateless access
token alone cannot be" — and `RefreshSessionService` is built entirely around it. The access token is
stateless; the session behind it is not, and that distinction is the whole point of the design. As
written, the doc would lead a reader to conclude that revocation is impossible.

**AE-S15-009 · Medium · The same doc describes an authorisation model that does not exist.**
It documents the User/Player/Game concepts and the two-token flow accurately, but a reader would
reasonably infer from it that game-scoped endpoints are enforced. They are not (AE-S2-001,
AE-S3-016). Given S2's findings, this document is the highest-priority one to reconcile — and the
place to record whichever decisions come out of open questions 1 and 2.

**AE-S15-010 · Low · `docs/tickets.md` is staged-modified but deleted in the working tree.**
Either the deletion is intended (in which case commit it) or it is accidental (in which case restore
it) — but as it stands the index and working tree disagree about whether the project's ticket list
exists.

**AE-S15-011 · Low · `.github/copilot-instructions.md` exists alongside `CLAUDE.md`.**
Two sets of assistant instructions that can drift apart. If both are wanted, have one reference the
other rather than restating conventions twice.

*S15 remaining:* the 17 `docs/design/*.md` files (1,317 lines) have **not** been cross-checked
against the implementation. Given AE-S10-001 (the turn rules are scaffolding, not rules) that check
is likely to find substantial doc-ahead-of-code drift, which is normal for a design document set but
worth recording explicitly so the docs aren't read as a description of current behaviour.

---

## 6. Open questions for Ben

1. **Is this ever deployed anywhere other than localhost?** The severity of AE-S2-001 (anonymous
   mutating endpoints), AE-S1-002 (hard-coded CORS) and AE-S1-006 (machine paths as defaults) depends
   entirely on the answer. I've rated them as if the answer is "yes, eventually".
2. **Is there meant to be an admin/role concept?** Several endpoints are clearly dev/admin tools
   (`/system/seed`, `templates/global`, `tag-types`, `recompile-all`) served from the same API as
   player endpoints. Knowing whether roles are planned changes whether AE-S2-002 is "add policies" or
   "split the surface".
3. **Do background jobs need to survive a restart?** (AE-S7-001) If a lost map generation is just
   "click it again", the fix is a startup sweep. If not, this needs a durable queue.
4. **Is `ServiceResult<TSuccess, TError>` (AE-S3-004) intended for something?** I'd delete it, but
   if there's a planned use I'll review it as a design instead of dead code.
5. **Should `PartialSuccess` exist at all?** (AE-S3-006) Which operations are genuinely partial?
   That determines whether the fix is 207, an envelope, or removing the status.
6. **How are migrations meant to reach a database?** (AE-S5-002) Manual `dotnet ef database update`
   only, or should the app migrate on startup? The answer also decides whether the test suite should
   stop using `EnsureCreated`.
7. **Are games ever deletable?** (AE-S5-014) `RevokeByGameAsync` and 75 `NoAction` relationships
   point in opposite directions. If games are permanent, `RevokeByGameAsync` is dead code; if not,
   the delete behaviours need revisiting.
8. **Is `CampaignMapGalleryId` deliberately not an IDENTITY column?** (AE-S5-003) The hand-rolled
   `MAX(id)+1` under `TABLOCKX` looks like it exists to make ids contiguous per map. If contiguity
   matters, there's a cheaper way; if it doesn't, this can just be an identity column.
9. **Should `EndCampaignTurnRequest` return the outcome?** (AE-S4-001) Fixing the missing interface
   needs a decision: change the request to `IRequest<ServiceResult<EndCampaignTurnOutcome>>` and
   return the possible-actions list to the client (which is what `EndCampaignTurnOutcome` and the
   frontend appear to want), or change `Handle` to return `ServiceResultBase` and drop the outcome.
   I'd take the former.
10. **Do the game rules belong in the Domain project?** (AE-S6-001) Right now it holds EF entities and
    essentially no behaviour, while `docs/design/` describes 1,317 lines of rules. Either is fine —
    but the choice should be written down in `CLAUDE.md`, because a half-migration is the bad outcome.
11. **Keep FluentAssertions?** (AE-S12-006) It's used 10 times against 955 NUnit asserts and now needs
    a commercial licence at the pinned v8. Cheapest path is to drop it.

---

## 7. Themes emerging so far

Recorded to avoid re-discovering them per slice.

1. **Safe-by-default is missing at the boundaries.** Authorisation defaults to anonymous, exception
   handling defaults to the framework, options default to unvalidated. Each individual finding is
   small; the pattern is that nothing is enforced at the composition root, so correctness depends on
   every author remembering. Most of S1/S2's Critical/High items are one `FallbackPolicy` and one
   `UseExceptionHandler` away from being structurally impossible.
2. **`ServiceResultBase`'s nesting is load-bearing in a bad way.** One design decision (nesting the
   generics inside the base record) produces reflection in the validation pipeline (AE-S3-002), a
   latent cast exception (AE-S3-003), `using static` in every consumer, and `new`-hidden statics that
   look like overrides. Fixing AE-S3-005 collapses several findings at once.
3. **Nothing verifies the API contract end to end.** No CI, no HTTP-level tests, no frontend tests
   worth the name — and the defensive dual-casing in `http.ts` shows the wire format isn't actually
   pinned by anything. This is why the S2 findings survived to be found by a manual read.
4. **In-process infrastructure is treated as durable.** The job pipeline persists a row and then
   relies on process-local state to act on it. It works, single-instance, until a restart.
5. **Consistency is good within a slice and drifts between them.** Handler/request naming, XML doc
   quality and the `ServiceResult` convention are strong; the exceptions cluster in the older or
   more experimental corners (`Biomes`/`Geography` file style, `MapGallery`/`System` not deriving
   from `ControllerBase`, `Ok()` in `RefreshAsync`). Enforcement (AE-S15-002) would hold the line.
6. *(session 2)* **The right pattern usually exists — it just isn't the default.** `TryGetGameContext`,
   `IDbContextFactory`, `AsNoTracking`, `rowversion`, `BeginTransactionAsync`, parameterised SQL are
   all present and used well *somewhere*. The findings are overwhelmingly places that didn't adopt
   the pattern: 3 of 15 controllers use the game-context helper, 1 of ~70 handlers resolves its
   context differently, 4 of 54 entities have concurrency tokens, 1 of 4 raw-SQL sites concatenates.
   This is a codebase where the fix is nearly always "apply the existing convention here", not
   "invent one" — which is a good position to be in, and an argument for the analyzer/CI enforcement
   in AE-S15-001/002 over case-by-case cleanup.
7. *(session 2)* **Error handling is duplicated 96 times and leaks.** The broad `catch (Exception)` →
   `InternalServerError(ex, context)` idiom appears in nearly every handler, converts client
   cancellations into logged 500s (AE-S5-007), and puts raw exception messages in the response body
   (AE-S5-009). One MediatR exception behaviour plus the missing global handler (AE-S1-001) replaces
   essentially all of it.
8. *(session 3)* **The wiring between layers is what's unverified, not the code inside them.**
   The three worst findings in the whole review are all seams: a handler that is never registered
   (AE-S4-001), endpoints that are never authorised (AE-S2-001), requests that are never validated
   (AE-S4-002). In each case the code on both sides is fine and the connection between them is
   missing — and in each case *nothing in the build or test suite looks at connections*. Tests
   construct handlers directly; there is no CI; there are no HTTP-level tests. Three reflection-based
   tests (AE-S12-004) would convert this entire category from "found by reading" to "impossible to
   merge".
9. *(session 3)* **Quality is uneven by area, and the newest work is the best.** The frontend's
   TypeScript is strict with zero escape hatches; `RefreshSession`/`User` are carefully documented;
   the turn pipeline's abstractions are clean; `rowversion` is on exactly the right four entities.
   The problems concentrate in the oldest paths (`GenerateBlankCampaignMap`'s table lock,
   `GetPlayerCampaignMapHandler`'s `.Result`) and in anything touching a boundary. This is a
   trajectory worth protecting with enforcement rather than a codebase needing rework.
