# AscendantEmpire — Claude Instructions

## Project
This project contains a web API backend and a React frontend.

## Preferences
- Always use `{}` braces for `if` and `for` statements, even single-line bodies.
- Always ask questions if there's any uncertainty — don't assume.
- If I suggest an approach that isn't ideal, push back and offer the better alternative.

## Testing
- Follow the database-tier rules in [testing.md](docs/testing.md) when writing or reviewing tests
  (no database → in-memory → containerised SQL Server; always use the lightest tier that suffices).

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
