# AscendantEmpire — Testing Guidelines

These rules govern how much database infrastructure a test is allowed to stand up.
The guiding principle: **use the lightest option that still exercises what the test needs.**
Standing up more than necessary (especially a containerised SQL Server) makes the suite
slower and more fragile for no benefit.

## The three tiers

Every test falls into exactly one of three tiers. Pick the lowest tier that suffices.

### 1. No database

If a test does not need a database, one must **not** be set up for it.

This covers:
- Pure logic / algorithm tests (no `DbContext` involved at all).
- Tests where every collaborator that would touch the database is mocked
  (e.g. a mocked `IDbContextFactory<AscendantEmpireContext>` or `IMediator`).
- Validator-only tests that instantiate the validator directly.

A test in this tier must not inherit from a base class that provisions a database
(e.g. `SqlServerTestBase`), because that would spin up infrastructure the test never uses.

### 2. In-memory database

If a test needs a database, but one that an **in-memory** database can satisfy, inherit from
`InMemoryTestBase` — never a container. It hands the test an `IDbContextFactory` over a
per-test EF Core InMemory store, so fixtures differ only by their base class.

An in-memory database suffices when the exercised code path uses only ordinary EF Core
operations: LINQ queries, `Add`/`AddRange`/`RemoveRange`, and `SaveChanges`. In-memory is
appropriate when the test does **not** rely on any of the following:
- **Constraint enforcement** by the database — unique indexes, foreign keys, check
  constraints, etc. (If the handler enforces the rule in code rather than leaning on a DB
  constraint, in-memory is fine.)
- **`ExecuteUpdateAsync` / `ExecuteDeleteAsync`** (bulk update/delete).
- **Raw SQL** — `FromSqlRaw`, `ExecuteSqlRaw`, `ExecuteSqlInterpolated`, and similar.
- **Explicit transactions** — `BeginTransactionAsync` and friends.
- Provider-specific mapping the in-memory provider cannot represent (e.g. EF complex
  types / owned shared-primary-key extensions, migrations).

### 3. Containerised SQL Server

A containerised SQL Server database (Testcontainers, via `SqlServerTestBase`) should be used
**only** where an in-memory database will not suffice — i.e. the exercised code path depends
on one or more of the container-only behaviours listed above (real constraint enforcement,
`ExecuteUpdateAsync`/`ExecuteDeleteAsync`, raw T-SQL, transactions, or provider-specific
mapping that only a real relational engine reproduces).

## Deciding which tier a test belongs to

Ask, in order:

1. Does the exercised code touch a real `DbContext` at all? **No →** Tier 1 (no database).
2. Does the exercised path need constraint enforcement, `ExecuteUpdate`/`ExecuteDelete`,
   raw SQL, transactions, or unsupported mapping? **No →** Tier 2 (in-memory).
3. Otherwise **→** Tier 3 (containerised SQL Server).

> Note on terminology: "in-memory" here means the EF Core InMemory provider
> (`Microsoft.EntityFrameworkCore.InMemory`), reached through `InMemoryTestBase`. That
> provider cannot do `ExecuteUpdate`/`ExecuteDelete`, transactions, raw SQL, or constraint
> checking, which is why those behaviours push a test up to the containerised tier.
