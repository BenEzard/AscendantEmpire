# AscendantEmpire — Claude Instructions

## Project
This project contains a web API backend and a React frontend.

## Preferences
- Always use `{}` braces for `if` and `for` statements, even single-line bodies.
- Always ask questions if there's any uncertainty — don't assume.
- If I suggest an approach that isn't ideal, push back and offer the better alternative.

## Naming conventions (C#)
- **Private variables start with an underscore** — `_dbFactory`, `_service`, `_playerId`.
- **Constants are UPPER_SNAKE_CASE**, whatever their accessibility — `CODE_MAX_LENGTH`.
- The two rules compose, so a **private constant carries both**: `_MINIMUM_DIVISIONS`, `_MAX_TURN_TIME_HOURS`.
- Locals and parameters are unaffected — they stay `camelCase` with no underscore.
- This applies to test projects as well as production code.
- Parts of the codebase predate these rules and don't follow them. Bring a file into line when you're
  already changing it; don't raise a separate renaming pass for its own sake.

These are C# rules. The React frontend keeps its own idiom — `camelCase` throughout, no underscore
prefixes, and `SCREAMING_SNAKE_CASE` reserved for module-level constants.

## LINQ (C#)
- **Prefer method syntax** — `.Where(…).Select(…)` — over query syntax (`from … where … select …`).
- Two reasons to keep query syntax instead, and they're the only ones: the rewrite would **change the
  query**, or it would be **harder to read**. The last one covers a genuine join between two tables with
  no navigation between them, where `join … on … equals` beats a four-argument `.Join(…)`.
- **Speed is not a reason.** Query syntax compiles to the same method calls, so the two forms cannot
  differ in performance. What can differ is a careless rewrite: an explicit `join` is always an
  `INNER JOIN`, while a navigation projection follows the FK's nullability and gives a `LEFT JOIN` on a
  nullable one. Check the FK is required before swapping a join for a navigation.
- Before reaching for `.Join(…)` or a `join` clause, check whether the relationship is already a mapped
  navigation. If it is, project through it — `unlock.Asset.Code` — and let EF expand the join. That is
  usually what turns a query-syntax join into a plain method chain rather than an ugly one.
- Bring a query into line when you're already changing it; don't raise a separate pass for its own sake.

## API controllers
- **One route prefix, one controller.** A controller is the single source of control for its route prefix.
  Never add a second controller hanging off a prefix an existing controller already owns — no
  `MapGalleryRiversController` beside `MapGalleryController` — and never move endpoints out to a new
  controller because the file is getting long. Length is not a reason to split: put new endpoints on the
  controller that owns the prefix and separate them with a banner comment.
- Controllers declare `[Route("[controller]")]`. `KebabCaseParameterTransformer` (registered in
  `Program.cs`) kebab-cases the token, so `MapGalleryController` serves `/map-gallery`. Nested resources are
  action-level templates on that same controller — `[HttpPut("{id:int}/rivers")]` — never a hard-coded
  prefix on a new class.
- Action method names must be unique and descriptive across the whole controller, since they become the
  Swagger operation ids — `UpdateCampaignMapRiversAsync`, not `UpdateAsync`.

## Testing
- Follow the database-tier rules in [testing.md](docs/testing.md) when writing or reviewing tests
  (no database → in-memory → containerised SQL Server; always use the lightest tier that suffices).

## Technology tree
- When editing the technology catalogue — adding a technology, rewiring prerequisites, retiring the
  `Broken` placeholder root — follow [docs/technology-tree-repairs.md](docs/technology-tree-repairs.md).
  It covers the code and cost conventions, the mandatory/optional semantics, the rule that a real
  prerequisite **replaces** a `Broken` edge rather than joining it, and the graph checks to run before
  building.

## Task tracking
- Work on this project is tracked in the separate **Planner** app (`P:\Development\Planner`),
  which exposes a local REST API at `https://localhost:7207`. Ascendant Empire is project id 4.
- When I ask you to add, update or look up a task, follow
  [docs/project-task-management.md](docs/project-task-management.md) — it lists the endpoints,
  the component ids and abbreviations, the status/priority enums, and the workflow to use.
- Tasks are created against a **component**, never the project. Pick the component that matches
  the work (or General, id 11, if none fits), and ask if it's unclear.
- Task codes (e.g. `G-133`) are assigned by the server and never reused — never invent one.
  Report the returned code back to me.
- Use `POST /tasks/bulk` when adding more than one task; it's atomic and a single round-trip.
