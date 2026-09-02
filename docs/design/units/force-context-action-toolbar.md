# Force Context Action Toolbar

When a Force is selected on the campaign map, the toolbar shows the actions appropriate to that Force
**in its current context**. The backend decides what is possible and why; the frontend decides how it
looks and what interaction follows.

This is the design for the toolbar as a whole. It is expected to accumulate many action types over
time; **TM-2** is the first slice, delivering the pipeline plus a single action — **Split Force**.

## Status

| Stage | Scope | Ticket | State |
|---|---|---|---|
| 1 | Rescale `UnitInstance.MovementPointsRemaining` to the Scale 1,000 fixed-point convention | TM-2 | **Done** — migration applied |
| 2 | Backend pipeline — `ForceActionType`, context, strategy interface, `SplitForceStrategy`, DI | TM-2 | **Done** |
| 3 | `GET /force-orders/units/{id}/actions` — contracts and endpoint | TM-2 | **Done** |
| 4 | Frontend — api module, query hook, action registry, disabled rendering, `useForceSelection` wiring | TM-2 | **Done** |
| 5 | Executing the split — split handler, options endpoint and parameter dialog | TM-4 | Not started |
| 6 | Replace the "Select Force" dialog with the always-visible force selection bar | TM-5 | **Done** — see [force-selection-toolbar.md](force-selection-toolbar.md) |
| 7 | Multi-Force selection — ctrl+click in the bar, and a toolbar that judges the whole selection | — | **Done** — see [Aggregating over a selection](#aggregating-over-a-selection) |

Stage 5 is deliberately **not** part of TM-2. TM-2 delivers the toolbar and the pipeline behind it; the
button that opens the split dialog currently raises a toast saying the dialog is not built yet. See
[Open questions](#open-questions) for what stage 5 has to settle first.

## Terminology

Per [Terminology.md](../../Terminology.md), a **Force** is whatever occupies a tile and moves as one:
either a single `Unit` (modelled by `UnitInstance`) or a `Command` of several. That is what the player
selects and what this toolbar acts on, so everything here is named `Force*`.

Force is a category rather than a type — there is no Force entity — so the pipeline works on flat
projections carrying a `RecordType` that says which kind it holds. Today only Units are ever fielded,
so every Force the toolbar sees is a Unit; the naming is what lets Commands arrive without a second
pipeline beside this one.

This document and its ticket were originally written when "Troop" was the word for the group. The
vocabulary was settled under **TM-6** and the names here follow it.

## Context: what exists today

**Frontend.** [`ContextActionToolbar.tsx`](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/components/ContextActionToolbar.tsx)
is a presenter over `{key, label, icon, onClick}`. Every decision lives in
[`useForceSelection.tsx`](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/hooks/useForceSelection.tsx),
which hard-codes two actions client-side: "Select Force" (a pure UI affordance that reopens the
multi-Force dialog) and "Hunt", parked behind `const canHunt = false`. There is no concept of a
*disabled* action — the toolbar renders exactly what it is handed.

> **Since TM-5:** the toolbar has no client-side actions left. "Select Force" was replaced by the
> [force selection bar](force-selection-toolbar.md), which is always visible when a tile holds several
> of the player's Forces, and "Hunt" was already gone. Every action on the toolbar now comes from the
> backend.

**Backend.** No Force-action concept exists yet, but one foundation is already in place:
`CompiledUnitTemplate.OrganisationTagId` resolves the military organisation tag at compile time
precisely so that "spawn and sizing rules read this single column rather than re-deriving from
components" — which is what the split rule needs.

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

New folder `Features/ForceOrders/ContextActions/`, mirroring the end-turn folder's layout.

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
the same sequence. The backend never emits a value from it. (TM-5 removed the only occupant the band
has ever had; the reservation stands for the next one.)

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
  second factor the ticket names, and what Combine/Merge will need.

Turn state is deliberately absent; see [open question 2](#2-should-the-toolbar-respect-whose-turn-it-is).
So is anything about enemy Forces — see [open question 3](#3-enemy-forces-on-the-tile-are-excluded-pending-vision-rules).

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

`IForceActionStrategy` stayed **per-Force**, deliberately. `Evaluate` still judges one
`ForceActionContext` and knows nothing about selections; everything multi-Force lives in
`ForceActionAggregator` instead. That is why widening the toolbar to a selection cost the rule layer no
changes at all — `SplitForceStrategy` and its 11 tests were untouched — and why a new action type still
only has to answer for one Force.

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
`MovementPointsRemaining` must still equal the template's `CampaignMovementPoints`. That comparison is
what forced Stage 1.

A tag that is `Unled` but has no authored base/max sizes is a configuration error, matching how
[`UnledSpawnRules`](../../../AscendantEmpire.Application/Features/Units/Classes/UnledSpawnRules.cs)
already treats it, rather than silently skipping the guardrail.

## Stage 1 — the movement point rescale (done)

`UnitInstance.MovementPointsRemaining` was a `decimal` (1.0 = one point), while
`CompiledUnitTemplate.CampaignMovementPoints` is a raw fixed-point `long` on the Scale 1,000
convention of [`ScaledAmount`](../../../AscendantEmpire.Application/Helpers/ScaledNumerics/ScaledAmount.cs)
(a stored `1000` means 1.0 MP). The split rule compares the two, so they had to share a representation.

`MovementPointsRemaining` is now a raw fixed-point `long`, matching every other movement, power and
hitpoint value in the system. Migration `TM-2_ScaleTroopMovementPointsRemaining` has been **applied**.
Four call sites moved with it:

| Site | Change |
|---|---|
| `MoveForceHandler` secondary validation | Comment only — "none left" is zero at any scale |
| `MovementProfileFactory.CreateState` | Now converts raw → decimal at the storage/engine boundary |
| `MovementProfileFactory.Create` | Magic `1000m` replaced with `ScaledAmount.Scale` |
| `BattleRemainingMovementCheck` | `>= 1m` → `>= ScaledAmount.Scale` |

`MovementState` deliberately stays `decimal`. `MovementProfileFactory` is documented as the "central
place for converting loaded Force and compiled design data into movement models", and it already
bridged scales for `BaseMovementPoints`, so the conversion belongs there — at one boundary — leaving
the movement engine's own models in whole/fractional MP throughout.

The migration's `ALTER` **truncates** rather than rescales (SQL Server drops the fraction converting
decimal to bigint, so `1.500` would arrive as `1`, not `1500`). That is safe only because nothing has
ever written the column; the migration carries a comment saying so.

### Known gap, deliberately out of scope

`MovementPointsRemaining` is **never written anywhere**: it is not set on spawn (`SpawnUnitHandler`
omits it) and nothing replenishes it at turn rollover, so every Force reads `0`. Until that is fixed,
the third split rule cannot distinguish "fresh" from "spent". Initialising on spawn and resetting at
end of turn are **deferred to a later ticket** (TM-3); values are adjusted directly in the database for
testing in the meantime.

Since TM-5 this gap has a second, louder symptom. The
[force selection bar](force-selection-toolbar.md) fades a Force with no movement left, and it reports
the column honestly, so **every Force in the bar renders greyed out** until TM-3 lands. That was a
deliberate choice over faking the value: the fade follows the data rather than hiding the gap.

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

That work is Stage 5 and is expected to become its own ticket.

## Testing

| Component | Tier | Count |
|---|---|---|
| `SplitForceStrategy` | 1 — no database | 11 — **unchanged** by the multi-select work, which is the point of keeping strategies per-Force |
| `ForceActionAggregator` | 1 — no database | 9 — the folding rules; hide/enable/disable, reason collection, dedupe, a selection of one |
| `ForceContextActionService` | 1 — no database | 12 — fake strategies; ordering, omission, fault isolation, re-validation, every Force judged |
| `ForceActionContextBuilder` | 2 — in-memory | 16 — the original 10 plus per-Force contexts, id ordering, tile-mates, all-or-nothing, duplicates |
| `ContextActionToolbar` | Vitest | 6 — disabled rendering, reason text, ordering, icon-only rendering |
| `ForceSelectionToolbar` | Vitest | 17 — ordering, troop count, fade, clickability when spent, ctrl/⌘+click, multiple pressed entries |
| `useForceSelection` | Vitest | 10 — own-forces-only filtering, default selection, the gesture trace, pinning, id-set identity |

Keeping the rules pure is what confined the tier question to a single class — and that class turned out
to sit on the in-memory tier after all. `CompiledUnitTemplate` owns EF complex types
(`Attack`/`Resistance`) which the in-memory provider cannot materialise, but the builder only ever
projects **scalars** off it and never reads a damage profile, so the provider is untroubled. That was
measured rather than assumed, and is now recorded in [testing.md](../../testing.md). If a later change
makes the builder read a damage profile, those tests will fail in the query shaper and must move to
tier 3.

## Open questions

Raised during implementation and deliberately left open.

### 1. Nothing initialises or replenishes movement points — the split rule cannot fire yet

`MovementPointsRemaining` is still never written: not on spawn, not at turn rollover. Every Force reads
`0`, so `HasFullMovement` is false and **Split always renders disabled** with "has already moved this
turn", regardless of the Force. The rule and its plumbing are correct and tested; there is simply no
data path feeding them yet. Setting the value directly in the database is the only way to see the
enabled state today. Raised as **TM-3**, and it blocks stage 5 having anything meaningful to do.

### 2. Should the toolbar respect whose turn it is?

Nothing in the pipeline checks the turn. That is consistent with the rest of the codebase — turn
enforcement isn't implemented anywhere yet (`MoveForceHandler` carries a `// TODO Check who's turn it
is`) — so adding it here would have invented new behaviour in a corner of the system rather than
solving it properly. When turn enforcement does land it belongs in the context, and probably as a
blanket rule ahead of the per-action strategies rather than repeated in each one.

### 3. Enemy Forces on the tile are excluded, pending vision rules

`ForceActionContextBuilder` gathers only the player's **own** Forces from the selected tile. Including
enemy Forces would let the toolbar confirm what is standing on a tile the player cannot see, so it
needs the vision rules applied first. Actions that turn on enemy presence (attack, ambush) will need
that resolved.

### 4. How does the split dialog learn its parameters? (stage 5)

A 100-strong settler with `UnledBaseSize` 25 can split into 2, 3 or 4 parts, or peel off a single group
of 25. The recommendation is a separate per-action detail endpoint fetched when the dialog opens
(`GET …/actions/split-force/options`), rather than a polymorphic `metadata` bag on the list endpoint —
which would make every action's contract the union of every action's needs. Carried into **TM-4**.

### 5. Does splitting produce a `ForceOrder`? (stage 5)

`ForceOrderType.SplitForce` exists, but the design here says splitting resolves immediately and costs
the turn, which needs no standing order. Either the order type is written as a completed audit record,
or it is unused and should be reconsidered. Worth settling deliberately rather than by accident; carried
into **TM-4**.

### 6. Hunt is still parked

The Hunt action was removed from the toolbar before this work (it was gated on a legacy unit design id
that no longer exists). It now wants to come back as its own strategy gated on a compiled capability tag
— see [tags-and-modifiers.md](../unit-design/tags-and-modifiers.md), concept 3. The registry carries a
`TODO` marking the spot.
