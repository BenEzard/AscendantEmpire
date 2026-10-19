# Force Context Action Toolbar

When a Force is selected on the campaign map, the toolbar shows the actions appropriate to that Force
**in its current context**. The backend decides what is possible and why; the frontend decides how it
looks and what interaction follows.

This is the design for the toolbar as a whole. It offers four actions — **Split Force**, **Create
Village**, **Establish Camp** and **Battle** — and is built to take more.

## Status

| Part | State |
|---|---|
| Backend pipeline — `ForceActionType`, context, strategy interface, aggregator, DI | Built |
| `GET /force-orders/actions` — contracts and endpoint | Built |
| Frontend — api module, query hook, action registry, disabled rendering, `useForceSelection` wiring | Built |
| Multi-Force selection — a toolbar that judges the whole selection | Built — see [Aggregating over a selection](#aggregating-over-a-selection) |
| Split Force — availability | Built — see [The Split Force strategy](#the-split-force-strategy) |
| Create Village and Establish Camp — availability, gated on capability tags | Built — see [The settlement strategies](#the-settlement-strategies--create-village-and-establish-camp-ud-56) |
| Battle — availability, and a placeholder dialog | Built — see [The Battle strategy](#the-battle-strategy-comm-1) |
| Executing a split — split handler, options endpoint and parameter dialog | Not built — **TM-4** |
| Executing a settlement founding — the settle endpoint, the name dialog and construction | Built — **CD-1**, see [settlements.md](../settlements.md#founding-one) |

Clicking an action whose execution is not built raises a toast saying so, which now leaves only Split
Force. See [Open questions](#open-questions) for what executing a split has to settle first.

## Terminology

Per [Terminology.md](../../Terminology.md), a **Force** is whatever occupies a tile and moves as one:
either a single `Unit` (modelled by `UnitInstance`) or a `Command` of several. That is what the player
selects and what this toolbar acts on, so everything here is named `Force*`.

Force is a category rather than a type — there is no Force entity — so the pipeline works on flat
projections carrying a `RecordType` that says which kind it holds. Today only Units are ever fielded,
so every Force the toolbar sees is a Unit; the naming is what lets Commands arrive without a second
pipeline beside this one.

## Context: the surrounding code

**Frontend.** [`ContextActionToolbar.tsx`](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/components/ContextActionToolbar.tsx)
is a presenter over `{key, label, icon, onClick}`, and the decisions live in
[`useForceSelection.tsx`](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/hooks/useForceSelection.tsx).
Every action on the toolbar comes from the backend; there are no client-side actions. Choosing between
stacked Forces is the [force selection bar](force-selection-toolbar.md)'s job, not an action's.

**Backend.** `CompiledUnitTemplate.OrganisationTagId` resolves the military organisation tag at compile
time precisely so that "spawn and sizing rules read this single column rather than re-deriving from
components" — which is what the split rule reads.

## The pattern

This is the Strategy pattern, and the codebase already has one instance of it:
`IPossibleActionCheck` + `EndCampaignTurnPossibleActionService` under
[`Features/Turns/Services/EndCampaignTurnPossibleActions/`](../../../AscendantEmpire.Application/Features/Turns/Services/EndCampaignTurnPossibleActions/) —
a DI-injected `IEnumerable<T>`, ordered, aggregated, with a `try`/`catch` per check so one broken
subsystem cannot take down the pipeline. This toolbar mirrors that structure so there is one
recognisable idiom for "ask every subsystem what is possible", with two deliberate departures.

### Departure 1 — strategies do not touch the database

Every end-turn check opens its own `DbContext`. That is fine when the pipeline runs once per turn; it
is wrong here, where it runs on every Force click. Instead a `ForceActionContext` is built **once**
per request and handed to every strategy, so the strategies are pure synchronous functions.

Two queries total, regardless of how many action types exist. It also puts the entire rule layer in
testing tier 1 (no database) under [testing.md](../../testing.md), leaving only the context builder
exposed to EF.

### Departure 2 — one strategy serves both the read and the write

This is the separation of responsibilities the ticket asks for. If `GET /actions` decides "you can
split" in one code path and the split handler re-validates in another, the two drift the moment a rule
changes. So a strategy exposes a single `Evaluate(context)`:

- the **list** endpoint calls it to render availability;
- the **execute** handler calls it again, against a freshly built context, before doing anything.

The frontend never decides legality. It renders a decision and echoes back an intent.

```
GET  /force-orders/actions?id=1&id=2 ─┐                                  ┌─► IForceActionStrategy.Evaluate()
                                       ├─► ForceActionContextBuilder ────┤     (pure, per Force)
POST /force-orders/split ──────────────┘   (2 queries, one ctx per Force)└─► ForceActionAggregator.Aggregate()
                                                                               (folds them into one verdict)
```

## Backend design

Lives in `Features/ForceOrders/ContextActions/`, mirroring the end-turn folder's layout.

### `ForceActionType` — a new enum, deliberately not `ForceOrderType`

`ForceOrderType` describes **orders that span multiple turns** — a persisted `ForceOrder` row with
progress, movement steps and a status the turn processor advances. A context action is something the
player does **now**. Executing one *may* raise a `ForceOrder` (choosing "Move" would), but it need not:
splitting resolves immediately and consumes the turn without ever becoming a standing order.

Conflating the two would force every immediate action to invent a spurious order type, and would tie
the toolbar's vocabulary to the turn processor's. So `ForceActionType` is its own enum in
`Domain/Enums/Forces/`, alongside `ForceOrderType`. Where a correspondence exists it is a mapping the
executing handler performs, not an identity — and the numeric values are deliberately *not* aligned
between the two enums, so no one is tempted to cast one to the other.

Values are spaced in hundreds, as `ForceOrderType`'s are, and **double as the static display order**
the ticket requires — so ordering cannot drift with DI registration order. A low band is reserved for
frontend-only affordances — actions with no backend rule behind them that still need a stable slot in
the same sequence. The backend never emits a value from it. The band is currently empty.

```
None        = 0
// 1–999 reserved for frontend-only affordances; the backend never emits these.
SplitForce = 1000
```

### `IForceActionStrategy`

```
ForceActionType ActionType { get; }
ForceActionAvailability? Evaluate(ForceActionContext context);
```

### `ForceActionContext`

An immutable snapshot carrying everything any strategy needs:

- the selected Force — size (healthy + injured), movement points remaining, tile, owner;
- its `CompiledUnitTemplate` with the resolved `OrganisationTag` (leadership model, unled sizes) and
  its `CampaignMovementPoints`;
- the **other Forces on the same tile**, each with its compiled template and movement points — the
  second factor the ticket names, and what Combine/Merge will need;
- the **capability tag codes** the compiled template grants, unioned across its components, so an action
  tied to a piece of equipment can ask whether the Force is carrying it (added in UD-56);
- the **tile's terrain** — its biome's water flag and its geography type — so an action can judge where
  the Force is standing without a second query (added in UD-56).

Turn state is deliberately absent; see [open question 1](#1-should-the-toolbar-respect-whose-turn-it-is).
So is anything about enemy Forces — see [open question 2](#2-enemy-forces-on-the-tile-are-excluded-pending-vision-rules).

### Three states, not two

The nullable return matters. There are three outcomes, not two:

| Result | Meaning | Example |
|---|---|---|
| `null` | Structurally irrelevant — omit from the list entirely | A commissioned Force can never split as unled |
| `IsEnabled: false` + reasons | Applicable, but blocked right now | "Unit has already moved this turn." |
| `IsEnabled: true` | Available | — |

Without the `null` case, every Force's toolbar becomes a wall of greyed-out buttons explaining actions
it will never have. The distinction is roughly "could this ever be true for this Force?" — structural
facts hide the action, mutable state disables it.

## Aggregating over a selection

The player may have several Forces selected at once — see
[the selection bar's multi-select](force-selection-toolbar.md#multi-select). An action offered over a
selection is **one order covering all of them**, so each action is judged against every selected Force
and the verdicts folded together.

`IForceActionStrategy` is **per-Force**, deliberately. `Evaluate` judges one `ForceActionContext` and
knows nothing about selections; everything multi-Force lives in `ForceActionAggregator` instead, so a new
action type only has to answer for one Force.

| Every selected Force's verdict | Toolbar shows |
|---|---|
| all `null` | nothing — the action is omitted |
| all available | enabled |
| anything else | disabled, with the collected reasons |

The three outcomes are not symmetrical, and each asymmetry is load-bearing:

* **Hidden only when no Force could ever take it.** Hiding as soon as *one* Force could not would make
  buttons vanish as the player adds Forces to the selection, with nothing said about why.
* **Enabled only when every Force can take it.** Pressing the button is one order over the selection; a
  partial order is not what the player asked for.
* **Everything else disabled with reasons**, because that is the only state that can explain itself.

A Force the action is structurally irrelevant to still contributes a reason —
`'Split Force' does not apply to Warband.`, matching what `EvaluateActionAsync` already says for the
single-Force case. Passing it over silently would leave the player looking at a disabled button whose
listed reasons all name *other* Forces.

Reasons are deduplicated. Most name their own Force ("Settler B is too small to split.") and so
distinguish themselves, but a configuration error such as an Unled organisation with no authored base
size is a fact about the **tag**, and every Force sharing that tag reports it word for word.

The aggregator is pure and static, and both the list endpoint and the execute-time re-check go through
it — the same reasoning as [Departure 2](#departure-2--one-strategy-serves-both-the-read-and-the-write).
If the two folded differently, an action could render enabled and then be refused on execution.

### The context builder is all-or-nothing

`BuildAsync` takes the whole selection and returns `null` if **any** Force in it is missing, wiped out
or another player's — which the service reports as a 404. Quietly dropping the bad id would render a
toolbar that judged fewer Forces than the player has selected, which is worse than an error.

It still costs **two queries** whatever the size of the selection: one for the selected Forces, one for
every own Force standing on the distinct tiles they occupy, then grouped in memory. The fixed cost the
pure-strategy design rests on is unchanged. The builder does *not* require the selection to share a
tile, even though the bar can only produce same-tile selections today — grouping by tile costs nothing
and leaves cross-tile selection free to arrive later.

### `ForceActionAvailability`

`IsEnabled` plus `DisabledReasons` — a **collection** of strings, so a Force failing both the size and
the movement rule is told both at once rather than fixing one to discover the other. A strategy
evaluates all of its rules and returns every failure.

## The Split Force strategy

| Rule | Read from | Outcome when it fails |
|---|---|---|
| Organisation tag's `LeadershipModel` is `Unled` | `Compiled.OrganisationTag` | **Hidden** (`null`) — structural, cannot change this turn |
| `Healthy + Injured >= 2 × UnledBaseSize` | tag + soldier counts | Disabled: *"…is too small to split — splitting requires at least 50 troops (2 × 25)."* |
| No campaign movement points spent this turn | Force vs. compiled template | Disabled: *"Unit has already moved this turn. Splitting requires a full turn."* |

Note the third rule is *full* movement, not *any* movement — splitting costs the whole turn, so
`CampaignMovementRemaining` must still be **at least** the template's `CampaignMovementPoints`. Because
unspent movement carries, that does not prove the Force has not moved — see
[unit-movement.md](unit-movement.md#movement-in-context-actions).

A tag that is `Unled` but has no authored base/max sizes is a configuration error, matching how
[`UnledSpawnRules`](../../../AscendantEmpire.Application/Features/Units/Classes/UnledSpawnRules.cs)
already treats it, rather than silently skipping the guardrail.

## The settlement strategies — Create Village and Establish Camp (UD-56)

Two actions, one for each settlement tier the player can reach at the start of the game. They are separate
`ForceActionType` members rather than one parameterised action, because the player is choosing between two
outcomes rather than configuring one.

| | Create Village | Establish Camp |
|---|---|---|
| Enum value / display order | `CreateVillage = 1100` | `EstablishCamp = 1200` |
| Capability that unlocks it | `create-settlement` | `create-early-camp` |
| Minimum troops | 100 — four settler base groups | 25 — one settler base group |

### Rules

| Rule | Read from | Outcome when it fails |
|---|---|---|
| Compiled template grants the capability | `Compiled.Capabilities` | **Hidden** (`null`) — a fielded Force cannot pick up camping gear |
| Tile's biome is not water | tile's `Biome.IsWater` | Disabled: *"…is on water, and a village must be founded on land."* |
| Tile's geography is not `Mountainous` | tile's `CampaignMapGeographyTypeId` | Disabled: *"…is in mountainous terrain, which cannot be settled."* |
| `CampaignMovementRemaining > 0` | Force | Disabled: *"…has no campaign movement left this turn."* |
| `Healthy + Injured >= minimum` | soldier counts | Disabled: *"…has 24 troops, and a camp needs at least 25."* |

The last three are shared, and live in `SettlementFoundingRules` rather than being written twice.

Three points worth keeping straight:

- **Water is the biome's flag only**, deliberately narrower than the movement layer's `MovementTargetTile.IsWater`,
  which also counts the DeepWater and ShallowWater geographies. Entering a tile and settling it are different
  questions, and sharing one definition would mean a later change to the movement rules quietly moved this one.
- **The mountain rule cannot currently fire.** `Mountainous` is seeded at
  `CampaignGeography.IMPASSABLE_MOVEMENT_COST`, so no Force can stand on one to be refused. It is written anyway,
  because the rule is about settling rather than walking and should already be right the day mountains become
  passable.
- **Movement is *any* remaining, not a full turn** — unlike Split Force. The value can legitimately be negative
  (a Force pays a tile's whole cost on entry and may go into debt), so a Force in movement debt is correctly
  refused.

### Gated on capabilities, not on a component

Both read `CompiledUnitTemplateCapability.TagCode`, not a component code and not a template id. The two tags are
`TagType.Ability` rows authored onto the `camping-materials` equipment component — the first concrete use of the
Capability Grants concept in [tags-and-modifiers.md](../unit-design/tags-and-modifiers.md). A second component
granting the same ability lights the action up with no code change, and the settler's organisation tag stays what
it is: a statement about how the formation is led, not about what it is carrying.

The consequence to remember: **these tags are catalogue data, not seed data.** They are authored through the
Entity Setup Tool and are absent from `Features/DatabaseSeeding/SeedData/`. On a fresh database nobody has
authored them on, no template compiles with them and both actions simply never appear. That is correct, not a
fault — but it is the first place to look if the buttons are missing.

### Executing them

UD-56 delivered **availability only**, because there was no Settlement entity to create. **CD-1 built the
rest.** Both buttons now open one dialog — a settlement needs a name, so neither can simply post — and the
dialog calls `POST /force-orders/settle` with the force, the tier and the name. See
[settlements.md](../settlements.md#founding-one) for what founding does.

Two things about it are worth knowing from here:

- **The handler re-runs this very strategy** through `IForceContextActionService.EvaluateActionAsync`
  before it founds anything, and refuses with the reasons it collects. So a refusal's wording is the
  wording the disabled button would have shown, and the rules live in exactly one place.
- **It founds through `IStructureLifecycleWriter`** rather than adding the row itself. That writer records
  the founding in `structure.StructureHistory` as it makes it, which is what lets another player watching
  the ground be told a village went up there — see
  [intelligence-and-sightings.md](../intelligence-and-sightings.md). A founding written any other way
  would be invisible to every other player, silently.

**One force, not a selection.** A settlement takes one name and one population, so the dialog is offered
only for a single-force selection; a larger one is turned away with a toast rather than pooled or founded
from an arbitrary member. This is the one place the toolbar's "an action over a selection is a single
order covering all of them" rule does not hold, and the frontend is what enforces it — the endpoint takes
one force id, so there is nothing for the backend to aggregate.

## The Battle strategy (COMM-1)

Offers a **Battle** button to a Force that is fighting in a battle. `Battle = 1300`, so it sits after the
settlement actions.

| Rule | Read from | Outcome when it fails |
|---|---|---|
| The Force is in battle | `ForceActionForce.IsInBattle`, projected as `BattleMapTileId != null` | **Hidden** (`null`) |

There is no disabled state: a Force in battle always has a battle to open. "In battle" is read off the
Force's battle map tile rather than by looking for an open `Battle` on its campaign tile, because the tile is
also what refuses the Force a campaign step, and the two must never disagree. With several Forces selected,
the aggregator's usual rules apply: the button is enabled only when every selected Force is in battle.

Clicking it opens `BattleDialog` for the first selected Force. What that dialog shows is still to be
designed, so today it is a placeholder.

A Force only has a battle map tile once a campaign step has started a battle — see
[unit-movement.md](unit-movement.md#battles-on-entry). A Force in battle stays on the campaign map
(`GetPlayerCampaignMapHandler` includes battle-map Forces), which is what lets it be clicked at all.

## Frontend design

### Contract

`GET /force-orders/actions?id=1&id=2`, game and player read from the token via `TryGetGameContext`,
returned under an `{ actions: [...] }` envelope to match the existing `{ ranks }` / `{ tags }`
convention. Enums serialise as their string names.

The query parameter is named simply **`id`**, and carries the whole selection as a repeated parameter.
It is deliberately not `unitInstanceId`: a Force is a *category*, not a type (see
[Terminology.md](../../Terminology.md)), so while every fielded Force is a Unit today and these are
`UnitInstance` ids, a Command is equally a Force and will arrive through the same parameter without a
rename. Read it throughout as a Force instance id.

```
actionType:      "SplitForce"
label:           "Split Force"
displayOrder:    1000
isEnabled:       false
disabledReasons: ["Unit has already moved this turn. Splitting requires a full turn."]
```

Fetched **per selection** and cached by React Query on `["forceContextActions", "1,2"]` — the ids
sorted and joined, so selecting the same Forces in a different order hits the same entry and the key
stays a primitive rather than a fresh array identity each render. Invalidated after anything that
changes a Force's size or movement.

The frontend builds the query string with `URLSearchParams` rather than axios's `params` option, which
is the obvious thing to reach for and is wrong here: axios serialises an array as `id[]=1&id[]=2`, and
`[FromQuery(Name = "id")]` binds only the repeated `id=1&id=2` form.

### Icons and labels are the frontend's job

The frontend keeps **one registry keyed by `actionType`** holding the icon, the label, and the
interaction to run. All three are presentation concerns the backend would otherwise have to know
about — including the fact that Split cannot simply fire a request.

The backend still sends `label` as a fallback name for an action type the registry doesn't know about
yet. Note what that fallback does and doesn't cover now the buttons are icon-only: such an action still
gets a correct tooltip and accessible name, but no icon, so it renders as an empty button. A new action
type is usable but not recognisable until the registry knows it.

### Icon-only buttons

The button draws **the icon alone**. The label is never rendered as text — it reaches the player through
the `Tooltip` on hover and through the button's `aria-label`, which is also what a screen reader
announces.

The toolbar floats over the campaign map, so its height is map the player can't see. Captioning every
button cost a second row of text for a name the tooltip was already giving, and made each button a wide
slab; dropping it roughly halves the bar's height. The tooltip is what carries the name, and it has to
be there regardless — it is the only place the `disabledReasons` can go.

### Disabled rendering

`ContextActionToolbar` gains a disabled state rather than filtering unavailable actions out: the
button is non-interactive, its icon renders greyscale (`filter: grayscale(1)` plus reduced opacity),
and its tooltip appends the `disabledReasons` to the label. This is the whole point of the backend
returning reasons — the player learns *why* an action is unavailable without having to guess.

The disabled button carries `pointer-events: none`. That looks backwards but is what makes the reasons
reachable: browsers don't dispatch mouse events over a disabled button, so without it the `Tooltip`'s
wrapping span never receives the `mouseenter` that shows the tooltip. Making the button transparent to
hit-testing lets the span take the hover instead; the span also carries the `not-allowed` cursor for the
same reason. The full tooltip text is duplicated into the button's `aria-label` because a disabled
button isn't focusable and so can't announce its own reason.

### Splitting needs parameters

A 100-strong settler with `UnledBaseSize` 25 can split into 2, 3 or 4 parts, or peel off a single
group of 25. So the button opens a dialog and only the dialog posts. The backend has no business
knowing a dialog exists, but it does own the numbers the dialog needs — fetched separately when the
dialog opens, rather than bloating the list endpoint with a per-action polymorphic payload.

That work is **TM-4**.

## Testing

| Component | Tier | Count |
|---|---|---|
| `SplitForceStrategy` | 1 — no database | 11 — per-Force only; selections are the aggregator's concern |
| `SettlementFoundingRules` | 1 — no database | 11 — the shared boundaries: exactly at the troop minimum, movement at zero and in debt, hills allowed |
| `CreateVillageStrategy` | 1 — no database | 8 — hidden without the capability, the 100-troop threshold, each rule's reason |
| `EstablishCampStrategy` | 1 — no database | 10 — the same, at 25 troops, plus that camp and village unlock separately |
| `BattleStrategy` | 1 — no database | 3 — its type and label, hidden out of battle, enabled in battle whatever its movement |
| `ForceActionAggregator` | 1 — no database | 9 — the folding rules; hide/enable/disable, reason collection, dedupe, a selection of one |
| `ForceContextActionService` | 1 — no database | 12 — fake strategies; ordering, omission, fault isolation, re-validation, every Force judged |
| `ForceActionContextBuilder` | 2 — in-memory | 20 — including capability codes, an empty capability set, the tile's terrain, and whether the Force is in battle |
| `ContextActionToolbar` | Vitest | 6 — disabled rendering, reason text, ordering, icon-only rendering |
| `forceActionRegistry` | Vitest | 4 — every known type gets its own distinct icon and this build's label; unknown types fall back |
| `ForceSelectionToolbar` | Vitest | 17 — ordering, troop count, fade, clickability when spent, ctrl/⌘+click, multiple pressed entries |
| `useForceSelection` | Vitest | 13 — own-forces-only filtering, default selection, the gesture trace, pinning, id-set identity, both settlement tiers opening the dialog, a multi-force selection turned away |

Executing a founding (CD-1) adds its own:

| Component | Tier | Count |
|---|---|---|
| `SettlementTierRules` | 1 — no database | 6 — each foundable tier's action and structure code, the growth tiers refused, codes unique and all seeded |
| `ConstructionRateCalculator` | 1 — no database | 16 — the anchor rate, each term's scaling, the movement cap, the floors, and work-to-turns rounding up |
| `StructureSeedData` | 1 — no database | 11 — codes unique, every row buildable with art and sight, and the village and camp calibrations asserted as arithmetic |
| `BuildingStructure` | 2 — in-memory | 13 — the per-turn step, each founder's own rate, completion, uneven division, no-work kinds, the tile claimed, only this player's, failures isolated |
| `SettleForceHandler` | **3 — containerised** | 14 — what founding leaves behind, the settler consumed, orders cleared, the Primary slot razed, and every refusal |
| `CreateSettlementDialog` | Vitest | 9 — the name required and trimmed, capped at 30, the refusal shown and dropped on edit, both tiers |
| `ObservedStructure` | Vitest | 6 — art by code, the name label, no label without a settlement, the completion bar |
| `structureImages` | Vitest | 4 — every seeded kind has its own distinct sprite, and an unknown or absent code has none |
| `settleForceApi` | Vitest | 4 — the body carries only force, tier and name; the tier goes as its name; failures pass through |

`SettleForceHandler` is the only fixture here on the containerised tier, and all three of `testing.md`'s
triggers apply at once: the founding runs in an explicit transaction, the lifecycle writers allocate their
sequences with raw SQL, and the name's uniqueness index is only a rule when a database is enforcing it.
`BuildingStructure` stays on the in-memory tier by substituting the writer, which is where the raw SQL is.

Keeping the rules pure confines the tier question to a single class, and that class sits on the
in-memory tier. `CompiledUnitTemplate` owns EF complex types (`Attack`/`Resistance`) which the in-memory
provider cannot materialise, but the builder only ever projects **scalars** off it and never reads a
damage profile, so the provider is untroubled. This is recorded in [testing.md](../../testing.md). If a later change
makes the builder read a damage profile, those tests will fail in the query shaper and must move to
tier 3.

## Open questions

Raised during implementation and deliberately left open.

### 1. Should the toolbar respect whose turn it is?

Nothing in the pipeline checks the turn, so the toolbar offers actions to a player outside their own
turn — while stepping a Force (`StepForceHandler`) does refuse out of turn. Turn enforcement belongs in the
context, probably as a blanket rule ahead of the per-action strategies rather than repeated in each one.

Still open, and now paid for twice: `SettleForceHandler` carries the same hand-written turn check as
`StepForceHandler`, because the toolbar would otherwise offer a player the Create Village button outside
their own turn and the founding has to refuse it somewhere. A third write path will copy it again.

### 2. Enemy Forces on the tile are excluded, pending vision rules

`ForceActionContextBuilder` gathers only the player's **own** Forces from the selected tile. Including
enemy Forces would let the toolbar confirm what is standing on a tile the player cannot see, so it
needs the vision rules applied first. Actions that turn on enemy presence (attack, ambush) will need
that resolved.

### 3. How does the split dialog learn its parameters? (TM-4)

A 100-strong settler with `UnledBaseSize` 25 can split into 2, 3 or 4 parts, or peel off a single group
of 25. The recommendation is a separate per-action detail endpoint fetched when the dialog opens
(`GET …/actions/split-force/options`), rather than a polymorphic `metadata` bag on the list endpoint —
which would make every action's contract the union of every action's needs. Carried into **TM-4**.

### 4. Does splitting produce a `ForceOrder`? (TM-4)

`ForceOrderType.SplitForce` exists, but the design here says splitting resolves immediately and costs
the turn, which needs no standing order. Either the order type is written as a completed audit record,
or it is unused and should be reconsidered. Worth settling deliberately rather than by accident; carried
into **TM-4**.

### 5. Hunt is parked

Hunt is not on the toolbar. It wants to come back as its own strategy gated on a compiled capability tag
— see [tags-and-modifiers.md](../unit-design/tags-and-modifiers.md), concept 3. The registry carries a
`TODO` marking the spot.
