# Ascendant Empire

A turn-based 4X strategy game — a campaign map you explore, settle and fight over, with battles that
zoom into the tile they happen on.

> ### ⚠️ Not playable yet
>
> **Ascendant Empire is in active development and is not a game you can sit down and play.** There is
> no win condition, no AI opponent, and no tactical battle layer — selecting *Battle* opens a
> placeholder dialog. A great deal of the surrounding machinery is real and working (map generation,
> lobbies, turns, movement, vision and intelligence, settlement founding, the technology tree, the
> unit designer), but it does not yet add up to a complete game loop. Treat this repository as a work
> in progress rather than a release.

---

## What exists today

Enough to start a game and move around in it:

- Create a campaign map in the Map Editor, or generate one with the Perlin engine.
- Register an account, claim a player slot in a lobby, define a ruler and faction, and start the game.
- Take campaign turns in sequence, move Forces tile by tile, and watch fog of war open up.
- Receive sighting reports about what happened while you were not looking.
- Found a village or establish a camp, split a Force, and spend research in the technology tree.
- Assemble unit designs from components on the Unit Designer bench.

What is missing is the part that makes it a *game*: combat resolution, the battle map, production and
economy loops at full depth, and any opponent that is not another human taking a hot-seat turn.

---

## The five applications

The frontend is a **multi-page Vite app**: each tool has its own HTML entry and its own bundle, so a
browser tab only ever loads and mounts one of them. All five sit behind login, and the three tools are
additionally gated on a permission that matches the server-side policy on the endpoints they drive.

| Application | Served at | Permission | What it does |
|---|---|---|---|
| **Game** | `/game/` | — | The game client. Campaign map rendering, fog of war, Force selection and movement, the context action toolbar, asset panels, the Military and Science advisors, the Unit Designer, settlement founding, turn end, and the sighting-report notification feed. |
| **Match Maker** | `/matchmaker/` | — | Account registration and login, then claiming a player slot in an open game and defining the ruler and faction in it. The last player to define themselves is what starts the game. |
| **Map Editor** | `/mapeditor/` | `MapEditor` | Authoring campaign maps for the gallery: a tile brush over biome and geography, river edges, MapResource placement, map properties, preview rendering, and zip import/export of whole maps. |
| **System** | `/system/` | `SystemPanel` | Administration. User accounts and their permissions, the games list (including permanent deletion of a game, under a separate `DeleteGame` permission), and catalogue export. |
| **Entity Setup Tool** | `/entitysetuptool/` | `EntitySetupTool` | The authoring bench for shared catalogue data: unit components with their slots and attack/defence profiles, tags and tag types, biomes and geographies, MapResource types, the technology tree (with a laid-out graph view), and database seeding. |

A sixth entry, the root `index.html`, is a small landing page that links to the five.

---

## Architecture

```
AscendantEmpire.Api                ASP.NET Core Web API — controllers, JWT auth, Swagger, health checks
AscendantEmpire.Application        handlers, services, validators, map generation, background work, EF Core
AscendantEmpire.Domain             entities and enums, with no dependencies of its own
AscendantEmpire.BackendContracts   DTOs the Api and Application share
AscendantEmpire.FrontendContracts  DTOs that cross the wire to the browser
AscendantEmpire.Tests              NUnit suite over all of the above
AscendantEmpire.AscendantEmpireUI  React + TypeScript frontend (five apps, one Vite build)
```

Requests arrive at a thin controller, which dispatches a **request object** through a mediator to its
single handler. Validation and exception handling are pipeline behaviours around that dispatch, so a
handler only ever sees a request that has already passed its rules.

Long-running work — map generation, preview rendering, catalogue and map import/export, database
seeding, deleting a game — never runs on the request thread. It is queued onto a background processing
channel and tracked as a row in `system.Processing` that the client polls for status.

---

## Technologies

**Backend**

- **.NET 10** / C# — `net10.0` across every project
- **ASP.NET Core** Web API, controller-based routing
- **Entity Framework Core 10** on **SQL Server**, 76 migrations, with a schema-per-area layout
  (`game`, `auth`, `turns`, `mapgallery`, `unitdesigns`, `military`, `assets`, `technology`,
  `settlement`, `battle`, `system`, …)
- **JWT bearer authentication** (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Serilog** for structured logging, with a file sink
- **Swashbuckle / Swagger UI**, fed from XML doc comments, with a bearer-token *Authorize* button
- **SkiaSharp** for rendering campaign map previews to PNG
- **ASP.NET Core health checks** — liveness, plus a tag-selected readiness check over the database

**Frontend**

- **React 19** with **TypeScript 5.9**
- **Vite 7**, configured as a multi-page app
- **Tailwind CSS v4** via `@tailwindcss/vite` — no `tailwind.config.js`; the theme is declared in CSS
- **TanStack Query v5** for all server state, with devtools
- **axios**, **react-icons**, **recharts**

**Testing**

- **NUnit 4** with **FluentAssertions** and **NSubstitute** — roughly 2,600 tests
- **`WebApplicationFactory`** to host the real API in-process, so the HTTP pipeline itself is tested
- **EF Core InMemory** for tests that need a database but not a real one
- **Testcontainers for SQL Server** for the tests that genuinely need the engine
- **Vitest** with **Testing Library** and **jsdom** on the frontend

---

## Noteworthy design

### Two map tiers in continuous harmony

The campaign map is a grid of large tiles; when a battle occurs on one, that tile expands into a
higher-resolution battle map. The two are designed so the tactical terrain is a faithful zoom-in of the
strategic terrain beneath it, rather than a disconnected random layout.

Campaign maps exist twice over: a reusable **gallery** blueprint authored in the Map Editor
(`mapgallery` schema), and a per-game copy extended with runtime state (`game.CampaignMap`), populated
at game creation by a single bulk `INSERT … SELECT`.

Gallery tiles are stored **contiguously**, so a tile's primary key is arithmetic rather than a lookup:

```
tile(x, y).PK = CampaignMapOffsetId + (y * Width + x)
```

### Fog of war is two independent facts

A tile is **explored** (the player has seen it at some point) and/or **watched** (one of their Forces
or structures can see it *right now*). Explored is permanent and only ever grows; watched is live and
shrinks the moment you walk away. That split is the whole point: terrain is a fact about the ground and
survives losing sight of it, while **structures and Forces are intel** and do not.

### Intelligence: what you learn while you are not looking

Movement, structure lifecycle and combat each write their own activity records, and every record draws
a `TurnSequence` from a monotonic counter on the turn row. A shared counter is the only thing that can
interleave three separate feeds into one order — "rode past the village, then razed it" and "razed the
village, then rode past" are different intelligence, and the player is told which one happened.

### Parallel turn-based

The campaign map has one roster; each battlefield map gets its own. Players take campaign turns in
sequence, but battles run on their own rosters alongside, which makes the game *parallel* turn-based
rather than strictly sequential. Players can also plan moves in advance and have them actioned
automatically when their turn opens, provided nothing adverse has changed.

### Two-token authentication

A user may be in several games at once, so logging in cannot pick one for you. Auth is stateless and
happens in two steps: `POST /auth/login` issues a **user token** that proves who you are, and
`POST /auth/enter-game` exchanges it for a **game token** that also carries the game and player. Both
are signed from the same key; refresh sessions add reuse detection and pruning.

### The backend decides what a Force can do

When a Force is selected, the server evaluates strategy objects against that Force's current context
and returns which actions are available *and why the others are not*. The frontend decides how that
looks and what interaction follows, but never whether an action is legal. A multi-Force selection is
judged as a whole by the same aggregator.

### Three buckets of research

Each technology needs between one and three kinds of research filled: **directed** (deliberate study),
**experience-based** (a byproduct of doing things) and **diffusion-based** (a byproduct of trade and
proximity). A technology either uses a bucket or has no requirement for it at all, so "does this need
experience?" and "how much?" are the same question. Prerequisites are individually mandatory or
optional: all the mandatory ones, plus at least one of the optional ones.

### Hand-rolled mediator and validation

There is no MediatR and no FluentValidation. The solution carries its own small `IMediator`, a pipeline
behaviour abstraction, and a fluent validator with a rule-builder DSL, all auto-registered by assembly
scan. A deliberately shallow dependency surface under a pattern the codebase leans on heavily.

### Contracts split in two

Two separate contract assemblies: `BackendContracts` for DTOs the Api and Application share
internally, and `FrontendContracts` for the shapes that actually cross the wire. Enums serialise as
**names** rather than numbers, so the TypeScript side mirrors them as string unions.

### Routes derived from the class name

Controllers declare `[Route("[controller]")]` and a registered parameter transformer kebab-cases the
token, so `MapGalleryController` serves `/map-gallery`. One route prefix belongs to exactly one
controller; nested resources are action-level templates on that same class, never a second controller
hanging off the prefix.

### Design tokens, with accessibility as a gate

The frontend declares colour under Tailwind v4's `@theme` in the `--color-*` namespace, so a single
token serves both hand-written CSS (`var(--color-selection)`) and generated utilities
(`bg-selection`, `ring-selection/50`). Every colour, focus style and hover affordance has to clear the
WCAG 2.2 Level A and AA criteria catalogued in [`docs/WCAG.md`](docs/WCAG.md), against measured
contrast ratios rather than assumed ones.

### Tests pick the lightest tier that works

Three tiers, and a test must use the lowest one that suffices: **no database** at all, then **EF Core
InMemory**, then a **containerised SQL Server** only when the test genuinely needs constraint
enforcement, bulk `ExecuteUpdate`/`ExecuteDelete`, raw SQL, explicit transactions or EF complex types.
Standing up more than necessary makes the suite slower and more fragile for no benefit. The rules — and
the measured limits of the in-memory provider — are in [`docs/testing.md`](docs/testing.md).

### Vocabulary is enforced

A **Unit** is one fielded body of soldiers; a **Command** is several Units under one officer; a
**Force** is whichever of the two occupies a tile and moves as one — a category, not a type, carried in
code as a `RecordType` discriminator rather than a base class. Retired terms (`Formation`,
`Contingent`) are documented as retired so they cannot creep back. See
[`docs/Terminology.md`](docs/Terminology.md).

---

## Getting started

### Prerequisites

- .NET 10 SDK
- Node.js, with npm
- SQL Server — LocalDB, Express, or a container
- Docker, only if you want to run the containerised test tier

### Configure

The base `appsettings.json` deliberately carries **no** connection string and **no** signing key, so
that no single machine becomes the silent fallback for every environment. The API fails fast at startup
if either is missing. Set them with user-secrets:

```bash
dotnet user-secrets --project AscendantEmpire.Api set "ConnectionStrings:AscendantEmpireDatabase" "<your connection string>"
```

```bash
dotnet user-secrets --project AscendantEmpire.Api set "Jwt:SigningKey" "<32+ character secret>"
```

`Cors:AllowedOrigins` must list the origin the frontend is served from — the Vite dev server's
`http://localhost:5173` in development.

A database with no administrator can still be administered: name an account in the bootstrap setting
and it is granted every permission on startup, until somebody holds `SystemPanel`.

### Run

```bash
dotnet run --project AscendantEmpire.Api
```

```bash
npm install --prefix AscendantEmpire.AscendantEmpireUI
```

```bash
npm run dev --prefix AscendantEmpire.AscendantEmpireUI
```

The API exposes Swagger UI at `/swagger`; the frontend's landing page links to the five applications.

### Test

```bash
dotnet test
```

```bash
npm run test --prefix AscendantEmpire.AscendantEmpireUI
```

The containerised tier needs Docker. `docker-compose.yml` also stands up a SQL Server on port `14330`
for local use.

---

## Documentation

The `docs/` folder is substantial, and describes how things are now rather than how they got there.

| Area | Document |
|---|---|
| Game design, by topic | [`docs/design/`](docs/design/README.md) |
| Terminology | [`docs/Terminology.md`](docs/Terminology.md) |
| Campaign and battle maps | [`docs/campaign-and-battle-maps.md`](docs/campaign-and-battle-maps.md) |
| Fog of war pipeline | [`docs/fog-of-war.md`](docs/fog-of-war.md) |
| Authentication and authorisation | [`docs/authentication-and-authorisation.md`](docs/authentication-and-authorisation.md) |
| Refresh tokens | [`docs/refresh-token.md`](docs/refresh-token.md) |
| Match Maker | [`docs/matchmaker.md`](docs/matchmaker.md) |
| Database seeding | [`docs/db-seeding.md`](docs/db-seeding.md) |
| Testing tiers | [`docs/testing.md`](docs/testing.md) |
| UI look and feel | [`docs/look-and-feel.md`](docs/look-and-feel.md) |
| Accessibility | [`docs/WCAG.md`](docs/WCAG.md) |
| Technology tree maintenance | [`docs/technology-tree-repairs.md`](docs/technology-tree-repairs.md) |
