# AscendantEmpire — Claude Instructions

## Project
This project contains a web API backend and a React frontend.

## Preferences
- Always use `{}` braces for `if` and `for` statements, even single-line bodies.
- Always ask questions if there's any uncertainty — don't assume.
- If I suggest an approach that isn't ideal, push back and offer the better alternative.

## Testing
- Follow the database-tier rules in [testing.md](testing.md) when writing or reviewing tests
  (no database → in-memory → containerised SQL Server; always use the lightest tier that suffices).
