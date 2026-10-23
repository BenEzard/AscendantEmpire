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

**Projecting only scalars off an entity that owns a complex type is *not* a tier-3 trigger either.**
Measured while building `ForceActionContextBuilder` (TM-2): a query that navigates through
`CompiledUnitTemplate` and selects only plain columns from it — `CampaignMovementPoints`,
`DisplayName`, and a nested projection of its `OrganisationTag` — runs fine on the in-memory provider,
because it never pulls `Attack` or `Resistance` into the shape. The rule above still stands as
written: it is *reading the damage profile* that throws, not merely touching the entity that owns one.
So check what a query actually projects before assuming tier 3, and if a change later makes such a
query read a damage profile, it will start failing in the query shaper and must move.

### 3. Containerised SQL Server

A containerised SQL Server database (Testcontainers, via `SqlServerTestBase`) should be used
**only** where an in-memory database will not suffice — i.e. the exercised code path depends
on one or more of the container-only behaviours listed above (real constraint enforcement,
`ExecuteUpdateAsync`/`ExecuteDeleteAsync`, raw T-SQL, transactions, reading an EF complex type, or
provider-specific mapping that only a real relational engine reproduces).

#### How a tier-3 database is provisioned

Each tier-3 test still gets its **own** database, created before it runs and dropped after, so tests
stay isolated and order-independent. What changed is how that database is built.

Applying the migration history costs seconds per database, and most of it is one migration —
`PopulateNumbersTable`, which writes 1,000,001 rows into `util.Numbers`. Paying that per test made the
containerised tier roughly 4.8 seconds per test regardless of what the test actually did.

Instead, `SqlServerTemplateDatabase` migrates a single **template** database the first time a tier-3
test runs, shrinks its log, and backs it up once. `SqlServerTestBase` then restores that backup under a
fresh GUID-named database for each test. A restore is a file copy inside the container, so the per-test
cost drops to roughly 0.7 seconds while each test still sees an identical pristine database — same
schema, same migration history, same seeded rows, same identity seeds.

Two consequences worth knowing:

- **Nothing needs to clean up after itself.** A test can write whatever it likes; the database is thrown
  away at teardown.
- **The template is built lazily**, on the first tier-3 test, so a run that selects only tier 1 and
  tier 2 tests never pays for it.

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

## What not to test

A test earns its place by being able to **fail for a reason nobody intended**. Before writing one, ask
what plausible edit it would catch. If the only edit that breaks it is the deliberate decision to change
that exact value — the edit whose author would update the test in the same breath — it is a restatement,
not a test, and it costs a line of maintenance for nothing.

### A restatement of a configuration literal

The usual shape is an assertion that copies a literal out of the code under test and compares it back:

```csharp
// Not worth writing: this says the same thing the configuration says, in a second place.
Assert.That(EntityType<AssetBank>().GetSchema(), Is.EqualTo("assets"));
Assert.That(EntityType<AssetBank>().GetTableName(), Is.EqualTo("AssetBank"));
```

Table and schema names, constant values, enum member numbers, a property's presence, a `DbSet` existing,
`[Required]` on a column — all of these are recorded by the thing that defines them, and a migration or
the compiler is the artifact that has to be right. A second copy in a test adds no safety.

### A value whose job is already finished

A related trap is asserting something that *was* load-bearing once and no longer is. The clearest case is
a column default that exists to backfill a migration:

```csharp
// Not worth writing: this mattered for exactly one migration, and that migration has run.
Assert.That(storageScope.GetDefaultValue(), Is.EqualTo(AssetStorageScope.SettlementBank));
```

`HasDefaultValue` on a new non-nullable column decides what lands on the rows that already exist when the
migration is applied. Once it has been applied the question is settled for good — the rows have their
values, and new rows get theirs from the entity's own initialiser, not from the database default. The test
guards a window that has already closed, and it will sit there being green for the life of the project.

Before writing a test about configuration, ask **when** it could fail. If the answer is "only before a
migration that has already run", it is not protecting anything.

### What is worth asserting instead

The narrow case that can be worth a test is a rule the mapping **enforces** that nothing in code enforces,
where a reasonable-looking future edit would quietly drop it — a composite key that is the whole of a
business rule, say, rather than a name or a default. Even then, prefer a test that provokes the rule
through the code that relies on it over one that reads the model back: a handler that refuses the second
write proves the rule and survives a refactor of how the rule is expressed. Where such a test is written,
the `[Description]` names the rule rather than the column.

The same question applies to behaviour, not just mapping. A test that asserts a getter returns what the
constructor was handed, or that a mapper copies a field across, is in this category too.

## Writing a test

Beyond picking the right tier, every new test follows these.

### Every test carries a `[Description]`

A test states in prose what it is proving, as an NUnit `Description`. Combine it with `Test` in a single
attribute list rather than stacking two:

```csharp
[Test, Description("A commissioned Force is not offered the unled split action at all, because no amount "
    + "of reinforcing or resting could ever make it apply.")]
public void ShouldHideTheActionWhenTheOrganisationIsCommissioned()
```

The description says **why the expected behaviour is correct**, not what the code does — a reader who
disagrees with the test should be able to tell from the description alone whether the test or the code
is wrong. It also surfaces in the runner's output, so a failure names the broken rule rather than a
method name.

### Test method names read `Should<Outcome>When<Condition>`

```
Should<Outcome>When<Condition>[Async]
```

- `<Outcome>` is what is expected to happen — `ShouldFail…`, `ShouldSucceed…`, `ShouldReturnNotFound…`,
  `ShouldHideTheAction…`.
- `<Condition>` is the circumstance that provokes it — `…WhenTheTroopBelongsToAnotherPlayer`.
- Suffix `Async` when, and only when, the test method itself is `async`.

Prefer naming the rule over naming the method under test: `ShouldBlockTheSplitWhenTheTroopHasAlreadyMoved`
beats `Evaluate_ReturnsBlocked_ForSpentMovement`.

Where there is no meaningful condition — a test asserting a fixed property rather than behaviour under
some circumstance — `Should<Outcome>` on its own is fine; don't pad it with an empty `When`.

> Plenty of existing tests predate both rules and use the older `Subject_Behaviour_Condition` style with
> no description. Bring them into line when you're already editing them; a sweeping rename for its own
> sake isn't worth the churn.

### Naming inside the fixture

The project-wide rules in [CLAUDE.md](../CLAUDE.md) apply to test code too: private fields start with an
underscore (`_service`), constants are `UPPER_SNAKE_CASE`, and a private constant is both
(`_UNLED_BASE_SIZE`).
