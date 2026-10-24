# Settlements

## Types of settlement

In rough order of growth:

- Camp
- Village
- Hamlet
- Town
- City
- Major City

Only the first two can be **founded**. The rest are reached by growth, which is not built. The tier is
stored on the settlement rather than inferred from the structure standing on its tile, because growth
moves the tier and the artwork together and the row has to say which it is without a join.
`SettlementTierRules` is the single place that maps a tier to the toolbar action whose rules judge it
and the `structure.Structures` code that stands for it.

## Founding one

A settler founds a settlement where it stands. The action is offered by the context-action toolbar —
Create Village and Establish Camp, gated on the `create-settlement` and `create-early-camp`
capabilities the settler's camping-materials component carries — and is taken through
`POST /force-orders/settle`, which carries the force, the tier and — for a tier that has one — the
player's name for it. Nothing else: the game and player come from the game token, and the tile is
wherever the force is standing.

**Only a village is named.** A camp is a place the settlers stopped rather than one they founded, so it
carries no name: Establish Camp founds outright, and only Create Village opens a dialog to ask for one.
`SettlementTierRules.IsNamed` is the single place that says which tiers are which; the server drops any
name sent for one that is not named, so the rule cannot be sidestepped by a client that ignores it.

**One settler, not a selection.** A settlement takes one name and one population, so there is nowhere to
put a second of either; a selection of several forces is turned away rather than pooled.

The order is judged by re-running the very strategy that rendered the button, so a refusal's wording is
the one the disabled button would have shown. The rules themselves — the capability, the troop minimum,
the terrain, the movement — are in
[units/force-context-action-toolbar.md](units/force-context-action-toolbar.md).

What founding does, in one transaction:

- razes whatever already held the tile's **Primary slot**, which keeps that structure's history and
  reports its loss to anyone watching. There is no confirmation yet: it is replaced silently.
- **takes the settlers out of the force** — see [What the founding costs](#what-the-founding-costs)
  below, which decides whether the force survives it;
- founds the settlement's structure at 0%, with the rate it will be built at;
- writes the settlement — its name, its tier, the round it was founded in, the player who founded it,
  and its population.

A name is **unique within the game**, trimmed, and at most 30 characters — short because it is drawn as
a label above the settlement, and a long one would cover its neighbours. A duplicate is refused by name
so the player knows which name to change. Uniqueness is enforced by an index filtered to the rows that
have a name, so any number of camps may stand unnamed in one game.

The dialog's own check is only that something was typed; it never trims. What a name may be is the
server's to judge, and it judges what it is sent, so the stored name is what its refusals are written
about. The button is never disabled — an empty name is answered with a message rather than a button that
will not press.

**Population is one figure**, seeded from the founding force's whole strength, healthy and injured
alike. Injury is a force concept; a settlement's health and sickness are their own mechanic and are not
modelled by splitting this number. This is where the Civilians figure under `Society.Population.MyPeople`
is to come from — see [assets-data-model.md](assets-data-model.md#what-the-figures-will-come-from).

Ownership of a settlement is the **structure's** `OwnedByPlayerId`, which a capture moves. The
settlement row records only who *founded* it, which never changes.

### What the founding costs

A **village** takes everyone who founds it, however many they are: the force is consumed and its whole
strength becomes the settlement's population.

A **camp** is a party that stopped rather than a force that ended, so a column strong enough to spare
them drops one off and marches on. When the force holds at least **twice the camp minimum** — 50, for a
minimum of 25 — exactly 25 are taken out of it and the force stays on the map with the rest. Below
that, the camp consumes it like a village does.

The threshold is twice the minimum because that is the same as saying *the founders only split off when
what remains could still found another camp*. That is what settles the boundary: at exactly 50 the
force survives, because the 25 left behind are still a legal founding party.

Either way the settlers are counted **once** — the settlement's population is exactly what the force
lost, never its whole strength alongside a force that kept it. The founders are drawn from the healthy
first, so a column does not settle its wounded and march on with the able-bodied.

The minimum is authored per tier in `SettlementTierRules`, and is the same number twice over: what a
force is refused for lacking, and what is deducted when it splits.

### The settlers go immediately, not when the building is finished

Deliberate, and it is a food decision. Settlers taken at founding leave the map before the
end-of-turn feed, so the people who are now a settlement's population never have to eat as a *force*
while their own settlement is being built — which they could not do, standing on one tile for several
turns stripping it.

It only solves the problem for the settlers themselves. **Any force that accompanied them still has to
feed itself** off the tile it is standing on, for as long as it stands there, and a half-built
settlement does nothing to help — including the remains of a force that split a camp off and chose to
stay. See [campaign-map/depletion.md](campaign-map/depletion.md).

The cost of taking the settlers off the map early is that they cannot be asked how fast the building
should go up on any later turn, which is why the rate is fixed at founding — below.

## Construction

A settlement goes up over the following turns rather than appearing finished. **How long it takes is not
a property of the building** — it is a property of who built it.

The `Structure` catalogue carries `BuildPointsRequired`, the work a kind takes. The founding force's own
figure, `BuildPointsPerTurn`, is worked out once and stored on the structure, and the `BuildingStructure`
end-of-turn process adds it to the structure's `BuildPointsProgress` each turn until the total is met:

```
BuildPointsPerTurn = Power × HealthyCount × MIN(1, CampaignMovementRemaining / CampaignMovementPoints)
```

`Power` comes off the force's compiled template, copied there from its body — the same value the
battlefield movement derivation uses, and its only other consumer (see
[units/unit-movement.md](units/unit-movement.md)). Each term is a lever:

- **A stronger body builds faster.** The human body is Power 7.0.
- **More hands build faster**, and **only the healthy count**. The injured are carried, not put to work
  — unlike the settlement's *population*, which counts every soldier, because injury is a force concept
  a settlement does not model.
- **A force that marched to the spot builds less** than one that was already standing there. The
  fraction is **capped at one whole turn**, because unspent movement carries over: without the cap a
  force opening its turn above its allowance would out-build a fresh one for no reason anybody designed.
  A force in movement debt contributes nothing.

### The calibration

**200 healthy humans on a full turn make 1,400 points.** Everything in the catalogue is anchored to
that:

| Kind | `BuildPointsRequired` | Anchored on |
| --- | --- | --- |
| Village | 2,800 | 200 healthy humans, full turn → **2 turns** |
| Camp | 175 | 25 healthy humans — the smallest force allowed one — full turn → **1 turn** |

Which makes a village cost:

| Force | Movement | Points/turn | Turns |
| --- | --- | --- | --- |
| 400 humans | full | 2,800 | 1 |
| 200 humans | full | 1,400 | 2 |
| 200 humans | half spent | 700 | 4 |
| 100 humans — the minimum | full | 700 | 4 |
| 100 humans | half spent | 350 | 8 |

The **owner's** turns, not rounds: the process runs at the end of one player's turn and touches only
that player's structures, so a player who has not taken a turn builds nothing. A village founded during
turn 4 by the anchor force is at 50% when turn 4 ends, reaches 100% when turn 5 ends, and is **complete
during turn 6** — a building finished at an end-of-turn is usable the turn after, so a build that needs
part of a fourth turn costs the whole of it. That is why the turn figure the player is quoted rounds up.

A build whose work does not divide evenly into the rate — three turns of 33.33 — finishes on its last
turn rather than needing an extra one to close the rounding gap.

### The rate is fixed at founding

Because the settlers become a settlement immediately, there is nobody left to ask on any later turn. So
"has this force spent its movement?" is a question asked **once**, at the moment of founding, and the
answer is stored. Where a camp splits off a larger force, the rate is worked out from the **settlers
who stayed**, not from the column that marched on — they are the ones raising it.

Two consequences worth knowing:

- **Nothing can reinforce a slow build.** A 100-strong settler commits to four turns, and marching
  another force onto the tile does not help. A force on the tile adding its labour is a natural
  follow-up, not something the model does today.
- **A structure placed rather than founded** — by game setup, say — carries no rate at all. The process
  floors it at one point per turn, so such a structure creeps rather than stalling unfinished for ever.

A kind with `BuildPointsRequired` of 0 needs no building and is finished at the end of the turn it was
founded in: a marker that is planted rather than raised, which is how the totem pole is authored. Its
progress never moves off 0, so `CompletedOnTurnId` is what says it is up.

**Work is stored, not a percentage.** `StructureInstance.BuildPointsProgress` is on the same raw
Scale-1,000 as the two figures above, so a turn's advance is a plain addition and being finished is an
exact comparison: a build takes exactly the turns `CalculateTurnsToComplete` promised the player when
they founded it, and a part turn at the end is still a whole turn of waiting. The percentage the UI
draws is derived on the way out. The intelligence snapshots keep their own `CompletionPercent`, because
an observer sees roughly how far along a structure is rather than its build points.

Every advance goes through `IStructureLifecycleWriter`, which writes the `Progressed` or `Completed`
row alongside the change. That is what puts construction into the intelligence feed: a watching player's
known-structure marker follows the progress, and the completion is reported to them, while progress
alone raises no report. See
[intelligence-and-sightings.md](intelligence-and-sightings.md#structures).

Construction is otherwise free. Nothing is *spent* to raise a structure — the work is the only cost.
A village is born with the bank such a cost would be drawn from, but no structure carries an
acquisition cost or an upkeep for it to meet.

**The settlement exists from the moment it is founded**, and is drawn on the map with its name, where it
has one, from then on — a progress bar on the sprite is how an unfinished one is told apart from a
finished one. What waits for completion is the tile ownership below.

## What a player sees

A named settlement is drawn on the campaign map with its name beneath it, by `SettlementLabelOverlay`,
which draws over the whole grid rather than inside the tiles: a name is wider than the 50px tile its
settlement stands on, and the tiles clip their own contents. The name is shown to **anyone who can see
the tile**: a settlement cannot be concealed, so withholding its name would hide what the player can
plainly observe. No rule enforces this — the map read reports structures only on tiles an observer can
see, so being in the response is the permission. A camp has no name and so draws no label.

The *sighting report* raised when another player witnesses a founding names the kind rather than the
settlement — "Red Empire founded a village at (4, 1)". Threading the name through the sighting fold is
not done.

## Territory

A settlement owns the tiles around it. It starts with a **radius** of owned tiles, and after that
**growth is the mechanism and selection is the direction**: growing a tier grants a number of new
tiles, and the player chooses where they go rather than the radius simply enlarging.

The radius is a property of the **tier**, authored in `SettlementTierRules`: a camp owns the ground it
stands on, and a village that tile plus a **1-tile radius** — the eight neighbours, on the same
eight-way adjacency movement and enclosure use. The grown tiers have no authored radius yet, and a
tier without one takes its own tile alone rather than a guessed amount of ground.

The territory is written when construction finishes, by the `BuildingStructure` end-of-turn process.
It takes every tile in the radius it can, and passes over the rest:

| Tile in the radius | What happens |
| --- | --- |
| Unclaimed | Owned, through the settlement |
| Claimed, by anyone | Owned, and **every claim on it is extinguished** — the two never coexist |
| Mountain or water | Owned. Both are ordinary terrain for ownership ([tile-ownership.md](campaign-map/tile-ownership.md#water)) |
| Void | Skipped — nothing can ever own it |
| A rival's owned or occupied ground | Skipped. Ownership is exclusive and earned, so building beside a neighbour is not a conquest of them |

So a village hemmed in by a rival, a coastline or the map's edge simply gets a smaller territory,
rather than the founding being refused for a reason the player cannot read off the map.

A tile owned through a settlement carries that settlement, which is what lets Settlement-scoped
modifiers reach it (see [campaign-map/tiles.md](campaign-map/tiles.md#biome)). Conquering a
settlement's own tile transfers its whole territory at once; razing it abandons that territory
instead. The states, the transfer rules and the rebellion period are in
[campaign-map/tile-ownership.md](campaign-map/tile-ownership.md).

## Asset banks

> **Partly built.** `AssetBank`, `AssetBankBalance` and `AssetBankSettlementMembership` are mapped, in
> the `assets` schema and keyed on the player. Founding a settlement of village tier or above creates
> its bank and its membership, empty, inside the settle transaction; an expense can be drawn from a
> bank. Nothing yet credits a balance, links or merges banks, or attaches a camp to the village whose
> bank it draws on, so the rest of the behaviour below is still the design.

The full rules are [asset-banks.md](asset-banks.md). What matters here is the shape:

- An **Asset Bank** is a pool of assets belonging to a player, and a settlement is joined to one by a
  **membership**. The bank is shared: one bank, many settlements.
- A settlement is **linked** when connected to another settlement via a transport network (road,
  river, railroad). Coastal settlements with a harbour and ship technology are automatically linked
  to other harboured settlements. Links count only between a player's **own** settlements.
- A settlement that is not linked to anything has a bank to itself. A settlement that becomes linked
  has its membership repointed at the bank it joins, so the two settlements then draw on one pool and
  there is nothing extra to reconcile between them.
- A settlement cut off — a pillaged road, a dammed river — gets a membership pointing at a **new, empty**
  bank. Restoring the link folds that bank's balances back into the shared one and deletes it.
- A settlement's membership is keyed on the settlement, so it has **exactly one** bank at any time,
  and that is a schema rule rather than one a service has to remember to check.
- An expense incurred **at a settlement** — a build order, an upkeep charge — is met from the one bank
  that settlement draws on. If the bank does not hold enough, the purchase cannot be made.
- An **Empire-wide** expense — a technology cost, an export — belongs to no settlement. It is met across
  every bank the player owns, spread by levelling so the fullest pools pay and a small one keeps the stock
  its own construction needs, and it carries no transport cost. See
  [asset-banks.md](asset-banks.md#drawing-down-an-asset).

> There is deliberately no second "asset bank group" table standing for a set of banks that share.
> Sharing *is* the bank, so a merge is folding one bank's balances into another and deleting it. The
> cost of that choice is accepted: per-settlement provenance is given up, so the game cannot say which
> settlement a given log came from and the "a mining settlement exporting stone should be considered
> richer" idea has nowhere to read from. Nothing uses it today, and it can be recovered from production
> records later rather than from balances.

Not everything a player holds lives in a bank. `Asset.StorageScope` says where a figure comes from:
most assets are `SettlementBank`, the two experience points are held per player for the whole game, and
the population figures are `Derived` — read from the settlements and rosters that already record them,
with no balance row anywhere.

## Buildings

Buildings are constructed within a settlement and provide outputs or capabilities.

| Building | Acquisition Cost | Upkeep | Requires | Provides | Description |
| --- | --- | --- | --- | --- | --- |
| Meeting Place | | | Built in: Camp | 10 units of craft materials per turn | Where the tribe gathers, and consequently where they output craft. |

> Infrastructure built on tiles *outside* a settlement is linked to the settlement in which it was
> created. See [campaign-map/infrastructure.md](campaign-map/infrastructure.md).
