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
- **EF complex types** (`ComplexProperty`) — see below. Any query that materialises *or* projects an
  entity owning one throws under the in-memory provider.
- Provider-specific mapping the in-memory provider cannot represent — migrations, and anything else
  that only a relational engine models.

### The complex-type limitation, measured

Verified against EF Core 10.0.9 (the version this solution pins) with a throwaway `InMemoryTestBase`
fixture, rather than inferred:

- **Writing works.** `Add` + `SaveChangesAsync` on an entity with a `ComplexProperty` succeeds.
- **Reading throws**, both when materialising (`Include(c => c.Weapon)`) and when projecting the
  complex type's properties (`.Select(c => new { c.Weapon.Attack.Slash })`):
  `KeyNotFoundException: The given key 'Property: UnitComponentWeapon.Attack#DamageProfile.Burn
  (long) Required' was not present in the dictionary`, thrown from
  `InMemoryShapedQueryCompilingExpressionVisitor.ShaperExpressionProcessingExpressionVisitor`.
  The failure is in the query shaper, so no query shape avoids it.
- The blast radius is per-query, not per-context: a query that never pulls in the owning entity is
  unaffected. `GetFullCatalogueHandler`'s bodies query is fine; its components query is not.

In this codebase that means any test reading `UnitComponentWeapon.Attack`,
`UnitComponentArmour.Resistance`, or `CompiledUnitTemplate`'s `Attack`/`Resistance` belongs in tier 3.

**Shared-primary-key 1:1 extensions are *not* a tier-3 trigger**, despite what this document and
several fixture remarks used to claim. The same spike confirmed that `UnitComponentBody` — keyed on
`UnitComponentId`, with a child collection — saves and reads back correctly in memory, including the
extension picking up the principal's generated key.

### 3. Containerised SQL Server

A containerised SQL Server database (Testcontainers, via `SqlServerTestBase`) should be used
**only** where an in-memory database will not suffice — i.e. the exercised code path depends
on one or more of the container-only behaviours listed above (real constraint enforcement,
`ExecuteUpdateAsync`/`ExecuteDeleteAsync`, raw T-SQL, transactions, reading an EF complex type, or
provider-specific mapping that only a real relational engine reproduces).

## Deciding which tier a test belongs to

Ask, in order:

1. Does the exercised code touch a real `DbContext` at all? **No →** Tier 1 (no database).
2. Does the exercised path need constraint enforcement, `ExecuteUpdate`/`ExecuteDelete`,
   raw SQL, transactions, a complex-type read, or unsupported mapping? **No →** Tier 2 (in-memory).
3. Otherwise **→** Tier 3 (containerised SQL Server).

> Note on terminology: "in-memory" here means the EF Core InMemory provider
> (`Microsoft.EntityFrameworkCore.InMemory`), reached through `InMemoryTestBase`. That
> provider cannot do `ExecuteUpdate`/`ExecuteDelete`, transactions, raw SQL, constraint
> checking, or complex-type reads, which is why those behaviours push a test up to the
> containerised tier.
