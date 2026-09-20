# Player Unlocks

How a player comes to own a unit component or a unit template, and where the code that grants it lives.

The per-player *state* has existed since the unit design system was built — `PlayerUnlockedComponent` and
`PlayerUnlockedTemplate`, both described in
[unit-design-system.md](unit-design-system.md#per-player-unlock-state). What has never existed is the
**granting** side. This page designs it.

> **Scope.** This is the mechanism, not the sources. What *causes* an unlock — research, trade, espionage,
> battlefield capture, conquest — belongs to [technology.md](../technology.md). What the player is *offered*
> when an unlock supersedes something older belongs to
> [unit-design-supercessions.md](unit-design-supercessions.md). This page is the seam between them.

---

## Where things stand

`GameSetupService` is the only writer of unlock rows anywhere in the solution, and it writes them inline:

```csharp
// GameSetupService.SeedStarterUnitsAsync, per player
context.PlayerUnlockedComponents.AddRange(starterComponentIds.Select(componentId => new PlayerUnlockedComponent
{
    PlayerId = player.Id,
    UnitComponentId = componentId,
    UnlockedOnTurn = 0,
}));
```

Four read paths consume those rows, each re-expressing the same predicate for itself:

| Reader | What it filters |
| --- | --- |
| `GetUnlockedBodiesHandler` | Bodies the player may design on |
| `GetSlotCandidateComponentsHandler` | Components that may be fitted into a slot |
| `GetPlayerTemplatesHandler` | Global templates the player has, plus their own custom ones |
| `UnitTemplateAssembly.ResolveAsync` | Every component referenced by a save must be unlocked |

Three consequences follow, and they are what this design has to fix:

- **There is nowhere to call.** A research handler that wants to grant a component has to duplicate the
  `AddRange` and get the turn stamping right by hand.
- **The cascade is asserted but not implemented.** Both
  [unit-design-system.md](unit-design-system.md#the-building-blocks) and `PlayerUnlockedTemplate`'s own
  remarks state that a template unlocks once the player holds all of its components. Nothing does this;
  game setup sidesteps it by granting the template and its components together.
- **`PlayerId == x && ObsoleteOnTurn == null` is written out four times** and can drift.

---

## Design decisions

### 1. A static granter taking the caller's context — not a DI'd service

`PlayerUnlockGranter`, in `Features/UnitComponents/Services`, alongside `AffectedTemplateRecompiler` and
`UnitComponentReconciler`. Every class in that folder is a static class that takes an
`AscendantEmpireContext` and leaves the saving to its caller, and unlocks should not be the exception.

The reason is atomicity, not fashion. Every realistic caller is already mid-transaction on a context it
owns:

- game setup writes unlock rows **and** `UnitInstance` rows in one `SaveChangesAsync`;
- a future research handler will write research state **and** the unlocks it produces;
- a conquest handler will transfer settlements **and** the technology that comes with them.

A service that resolved its own context from `IDbContextFactory` and saved independently would split each
of those into two transactions, and a failure between them leaves a player holding units they cannot
design, or research they were never given the parts for. Taking the ambient context costs the caller one
line and keeps the guarantee.

> **The granter never calls `SaveChangesAsync`.** Its XML docs must say so, the way
> `AffectedTemplateRecompiler`'s say the opposite about *its* two-halves-around-the-save contract.
> `GameSetupService` is the counter-example to copy carefully: its private helpers are self-saving and say
> so ("The method is self-saving").

### 2. Granting means "ensure a row exists"

Both tables have a composite primary key — `(PlayerId, UnitComponentId)` and `(PlayerId, UnitTemplateId)` —
so a naive re-grant throws. Re-granting is not an edge case: trade, conquest and diffusion will all
routinely offer a player something they already hold.

So a grant reads the player's existing keys once, subtracts them, and adds only what is missing. It returns
the ids it **actually** granted, which the cascade then uses to narrow its work (see decision 4).

**A row that exists is left completely alone, including an obsolete one.** Re-granting a component the
player has already retired does *not* clear `ObsoleteOnTurn` and does not restamp `UnlockedOnTurn`.
Obsolescence is a deliberate player choice under
[unit-design-supercessions.md](unit-design-supercessions.md); silently undoing it because a trade deal
happened to include the old part would be a bug, not a courtesy. Reinstatement, if it is ever wanted, is a
separate explicit operation.

> Enforcing this **in code rather than leaning on the primary key** also keeps the tests in tier 2 — see
> [Testing](#testing).

### 3. The turn stamp is passed in, and `ICampaignTurnService` is where callers get it

`UnlockedOnTurn` is a plain `int` with no foreign key, matching `CreatedOnTurnId` and friends elsewhere in
the model. The canonical source is the one `AffectedTemplateRecompiler` already uses:

```csharp
int currentTurn = await campaignTurnService.GetCurrentRoundNumberAsync(gameId, cancelToken);
```

That method returns the current round of the game's active, non-battle campaign queue, and **`0` when there
is no such queue** — which is exactly the state during game setup. So the `0` that
`SeedStarterUnitsAsync` writes today is not a placeholder to be replaced; it is the correct value, and it
carries the same "before/outside the campaign" meaning it already carries on compiled template snapshots.

The granter takes the turn as a parameter rather than resolving it. Resolving inside would force a
`gameId` argument the granter has no other use for (a `Player` row is already game-scoped), and would put a
database round-trip inside a loop for callers granting to eight players at once.

### 4. The component-to-template cascade is evaluated on grant, and writes rows

The alternative — dropping `PlayerUnlockedTemplate` and deriving template availability from the component
set on every read — was considered and rejected:

- **Per-player facts need somewhere to live.** `HasBeenPrototyped` is mutable state keyed on
  (player, template). `UnlockedOnTurn` records *when*. `ObsoleteOnTurn` records a player's retirement
  decision. A derived view can hold none of these.
- **The reads are the hot path.** The designer's template list would become a group-by over
  `UnitTemplateComponent` with a subset test, on every load, for every player.
- **Unlocking is an event.** "You can now build the Legionary" is worth a turn notification. Derived state
  has no moment at which it becomes true.

Evaluation is narrowed by the ids a grant actually added, and mirrors the query
`AffectedTemplateRecompiler.FindAffectedTemplateIdsAsync` already uses — body component *or* fitted
component, backed by `IX_UnitTemplateComponent_Component`:

```csharp
// Only global templates are gated: a custom template is assembled from parts the player already holds
// (UnitTemplateAssembly enforces that on save), so it never needs an unlock row.
var candidates = await context.UnitTemplates
    .Where(template => template.PlayerId == null)
    .Where(template => grantedComponentIds.Contains(template.BodyComponentId)
        || template.Components.Any(fitted => grantedComponentIds.Contains(fitted.UnitComponentId)))
    .Where(template => !context.PlayerUnlockedTemplates
        .Any(unlocked => unlocked.PlayerId == playerId && unlocked.UnitTemplateId == template.Id))
    .Select(template => new
    {
        template.Id,
        template.BodyComponentId,
        FittedComponentIds = template.Components.Select(fitted => fitted.UnitComponentId),
    })
    .ToListAsync(cancelToken);
```

The subset test then runs in memory against the player's full unlocked set, once per batch rather than once
per component. Only a template that gained a component in *this* grant can have become complete, so a grant
of one component never scans the whole catalogue.

> **Templates whose components a player already held stay locked until something is granted.** A candidate
> is only reconsidered when one of its parts is newly granted, so seeding a player mid-game by writing
> component rows directly would leave the cascade unrun. That is the price of the narrowing, and it is why
> the granter is the only supported way in.

### 5. Obsolescence is exposed as a primitive, and the policy stays with supersession

The granter offers a method to stamp `ObsoleteOnTurn`, and stops there. It does **not** walk
`ComponentSupersession` / `TemplateSupersession` and retire predecessors automatically, because
[unit-design-supercessions.md](unit-design-supercessions.md) is explicit that retirement is *an offer the
player accepts*, and it already owns "the obsolete-offer handler" as a named follow-up. This page supplies
the write that handler will make; it does not pre-empt its policy.

### 6. One shared predicate

`PlayerUnlockQueries` (same folder, same static-class convention as `DesignComponentProjections`) exposes
the two filters as composable `IQueryable<int>`s, and the four readers use them instead of restating the
`ObsoleteOnTurn == null` condition:

```csharp
public static IQueryable<int> UnlockedComponentIds(AscendantEmpireContext context, int playerId);
public static IQueryable<int> UnlockedTemplateIds(AscendantEmpireContext context, int playerId);
```

They stay `IQueryable` so they compose into the existing `Contains(...)` subqueries rather than forcing a
second round-trip.

---

## The surface

All methods take the caller's `AscendantEmpireContext`, mutate the change tracker, and **do not save**.

| Method | Does | Returns |
| --- | --- | --- |
| `GrantComponentsAsync(context, playerId, componentIds, unlockedOnTurn, cancelToken)` | Adds a row per component the player lacks | The ids actually granted |
| `GrantTemplatesWithComponentsAsync(context, playerId, templateIds, unlockedOnTurn, cancelToken)` | Grants each template plus its body and every fitted component | The template ids actually granted |
| `ApplyTemplateCascadeAsync(context, playerId, grantedComponentIds, unlockedOnTurn, cancelToken)` | Unlocks global templates the grant completed | The template ids newly unlocked |
| `ObsoleteComponentAsync` / `ObsoleteTemplateAsync` | Stamps `ObsoleteOnTurn` on an existing row | Whether a row was stamped |
| `PlayerUnlockQueries.UnlockedComponentIds` / `.UnlockedTemplateIds` | The shared read filter | `IQueryable<int>` |

`GrantComponentsAsync` runs the cascade itself, so ordinary callers never call `ApplyTemplateCascadeAsync`
directly; it is public for the case where components arrive from several sources within one transaction and
the cascade should run once at the end.

`GrantTemplatesWithComponentsAsync` is the whole-design grant — what game setup needs, and equally what
conquest and tech-trade need when a player acquires a finished design rather than parts. It short-circuits
the cascade for the templates it was given explicitly, since they are being granted regardless of whether
the component set happens to complete them.

---

## Call sites that change

**`GameSetupService.SeedStarterUnitsAsync`** is the only existing writer, and becomes a caller. Its two
`AddRange` blocks and the `starterComponentIds` `SelectMany` collapse into one call per player, inside the
same loop and before the same `SaveChangesAsync`:

```csharp
await PlayerUnlockGranter.GrantTemplatesWithComponentsAsync(
    context, player.Id, starterTemplateIds, unlockedOnTurn: 0, cancellationToken);
```

Everything the method does either side of that — resolving the templates, validating that each is unled
with an authored `UnledDefaultSize`, checking start positions, spawning the `UnitInstance` rows — is
untouched, and so are the loud failures documented in
[starting-requirements.md](../starting-requirements.md#verifying-the-setup). `StarterUnitSpawnTests` must
pass unchanged; that is the proof the refactor was behaviour-preserving.

**The four readers** swap their inline predicate for `PlayerUnlockQueries`. No behaviour change.

---

## The starter set stays a code constant

`GameSetupService.StarterTemplateCodes` (`SETTLER`, `SCOUT`) stays where it is. The alternatives — a flag on
the global `UnitTemplate`, or a `DatabaseSeeding/SeedData` class beside `TagTypeSeedData` — were weighed and
declined for now:

- The seed does create the starter templates themselves (`StarterTemplateSeedData`, see
  [starting-requirements.md](../starting-requirements.md)), but **which codes are required** is a different
  question from **what those templates contain**. Moving the required list into seed data would make one
  array marginally more editable at the cost of the guarantee below.
- A flag on `UnitTemplate` lets a database silently disagree with the application. Today a missing or
  misconfigured starter fails game creation with a specific, actionable message; a data-driven set can
  instead be *empty*, which fails as a game with no units and no error.
- The current arrangement is documented end to end, including how to add one
  ([starting-requirements.md](../starting-requirements.md#adding-another-starting-unit-later)).

**Revisit when starter sets become per-faction or per-scenario.** At that point the set is genuinely data —
different players get different starters — and a single compiled array cannot express it. Until then this
is a constant that changes about once a year.

---

## Testing

Per [testing.md](../../testing.md), the granter's tests sit in **tier 2 (in-memory)**: the code path is
LINQ, `AddRange` and `SaveChanges`, touches no complex types, and — because decision 2 enforces idempotency
in code rather than leaning on the primary key — relies on no database constraint. A design that let the PK
throw and caught it would have forced tier 3 for every one of these tests.

Worth covering:

- granting a component the player already holds adds nothing and reports nothing granted;
- granting one that exists but is obsolete leaves `ObsoleteOnTurn` and `UnlockedOnTurn` untouched;
- granting the last missing component of a global template unlocks that template, and granting a
  non-final one does not;
- a custom (player-owned) template is never granted an unlock row;
- the turn stamp lands on both the component and the cascaded template rows.

`StarterUnitSpawnTests` continues to cover game setup end to end.

---

## Follow-ups

- **`PlayerUnitDesignState` is dead weight.** The prototype entity — `IsUnlocked` / `IsObsolete` booleans
  over `UnitDesign` — is still mapped alongside the pair that replaced it. Removing it is its own task.
- **`HasBeenPrototyped` is written by nothing.** `GetPlayerTemplatesHandler` projects it and it is always
  `false`. Either prototyping sets it or the field should go.
- **The obsolete-offer handler**, owned by
  [unit-design-supercessions.md](unit-design-supercessions.md#out-of-scope-explicit-follow-ups).
- **Unlock notifications** — the cascade returns newly-unlocked template ids precisely so a turn
  notification can be raised from them, but nothing consumes that yet.

## Open questions

- **Does an unlock ever need to be revoked outright?** Conquest can take territory; nothing in
  [technology.md](../technology.md) says knowledge can be taken back, so there is no delete path here. If
  one is ever needed, deciding between a hard delete and a third timestamp is a real decision, not a
  detail.
- **Should a template unlock when its components are held but one of them is obsolete?** The cascade as
  designed tests against the non-obsolete set, so it will not. That seems right — you should not be handed
  a new design built on a part you have retired — but it has not been play-tested.
