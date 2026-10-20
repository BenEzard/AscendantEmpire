# Tile ownership

Part of the [Campaign Map](README.md) design. What a tile *is* — its biome, geography and rivers —
is in [tiles.md](tiles.md); who holds it is here.

Ownership separates two different things a Player can have over a tile: a **claim**, which is a
political assertion, and **ownership**, which is real administrative control. Claims are cheap,
non-exclusive and revocable; ownership is exclusive and earned. Everything else in this document
follows from that split.

## The states

| State | What it means | Exclusive |
| --- | --- | --- |
| **Unclaimed** | Wilderness. No Player has asserted anything. | — |
| **Claimed** | One or more Players assert the tile. A political claim, not control. | **No** — several Players may claim one tile |
| **Owned** | A Player holds the tile administratively. Yields, construction and Settlement scoping all hang off this. | Yes |
| **Occupied** | A rival's **visible** Forces hold the tile alone, with no battle in progress. The owner keeps ownership. | Yes |
| **Disputed** | Both sides' Forces are on the tile — a battle. | Yes |
| **Abandoned** | Formerly owned, held by nobody. Reverts to Unclaimed after **5** turns and forgets its former owner. | Yes |

**Rebelling** is not a state. It is a *modifier* on Owned, applied to the tiles of a freshly
conquered Settlement — see [Conquest](#conquest-and-rebellion).

**Ownership trumps claim.** The two never coexist on one tile, so a tile becoming owned
**extinguishes every rival claim on it**. Founding a Settlement or finishing a structure therefore
clears rival claims inside its reach, which is the ordinary way a border pushes outwards.

## Becoming owned

A tile is owned by one of three routes:

1. It falls inside a **Settlement's territory**.
2. A **structure** asserting ownership stands on it.
3. It is **enclosed** by a complete ring of the Player's own owned territory — see
   [Enclosure](#enclosure).

A fourth route transfers existing ownership rather than creating it: the
[attrition timer](#the-attrition-timer) below, and conquest.

### Settlement territory

A Settlement starts with a **radius** of owned tiles. Beyond that, **growth is the mechanism and
selection is the direction**: growing a tier grants the Player a number of new tiles, and the Player
chooses where they go rather than the radius enlarging geometrically.

A tile owned through a Settlement carries that Settlement on the tile, which is what lets
Settlement-scoped modifiers reach it ([tiles.md](tiles.md#biome),
[replenishment.md](replenishment.md)). Owned land with no Settlement — won by a structure, by
enclosure or by attrition — carries none, and no Settlement-scoped modifier applies to it.

### Structures

What a structure asserts is a property of the structure itself, authored on the catalogue entry
beside its sight values, as `Structure.Asserts`:

| Asserts | Examples |
| --- | --- |
| **Claim** | Totem pole, border marker |
| **Owned** | Foraging camp, sentry tower |

Nothing a Player builds can assert Occupied, since Occupied is by definition a rival standing on
someone else's ground. `StructureAssertion` has exactly those two members and no zero, so that is
unrepresentable rather than merely forbidden, and a check constraint says the same in the schema.

`BuildingStructure` honours the column when a structure's construction finishes: it asserts over the
structure's own tile, and over the territory radius of its Settlement's tier where it has one
([settlements.md](../settlements.md#territory)). Every kind in the catalogue asserts **Owned** today —
the claim tier is there for the markers a scout plants, and nothing plants one yet.

An early scout may also assert a claim directly, either as a toggleable action or by building a cheap
one-turn marker. That is the one part of this section still to be built.

## Claims

Claims are held per Player per tile, so several Players may claim the same tile at once. Two rival
claims on an unowned tile are not a special state and have no mechanical consequence beyond being
visible: nothing is contested until Forces arrive.

A claim grants **no sustenance or MapResource values** — those follow ownership
([replenishment.md](replenishment.md), [map-resources.md](map-resources.md)).

### Claim supply range

Every claim must reach an **owned** tile within **3 hops** through the Player's own claimed or owned
land. A claim out of range at the end of a round **lapses**.

This is the whole of the anti-land-grab rule, and it does three jobs at once:

- a scout cannot plant a claim on a distant chokepoint and keep it — the claim holds only if real
  settlement follows it;
- it bounds how many claims a Player can hold at all, to roughly their owned perimeter × 3, which
  scales with how large they actually are;
- it caps how deep an [enclosed](#enclosure) interior can be held, so a pocket wider than about six
  tiles cannot be held in its middle.

Because that one number does all three, there is deliberately **no claim budget and no enclosed
region size cap**. If claim spam becomes a problem, the number to change is the hop count.

**Only a passable tile carries a hop.** That is the whole of it — the same impassability movement
refuses a Force on, and nothing else. Two things follow with no further rule:

- an **owned mountain is a dead end rather than a bridge**: it is not a source for a chain and is
  never walked through, so a claim on the far side of a range has to come round it;
- a **claimed mountain** could never be reached at all, so it would lapse the round it arrived —
  which is why in practice a mountain is only ever *owned*, never claimed, and the claimability rule
  simply refuses to claim impassable ground.

**Rivers do not cut the range.** Rivers shape enclosure and impassability shapes supply, and neither
rule carries the other's clause: a chain crosses a river of any width freely, and stops dead at a
mountain. Water is passable, so a chain runs across a strait of owned sea with no water clause
anywhere.

An ally's claimed and owned tiles count towards the range. There is no alliance model yet, so the
walk takes a set of friendly Players that currently holds one id.

## Enclosure

Unclaimed land surrounded by a Player's own territory becomes that Player's. The ring must be
**complete and the Player's own**:

- A **rival's** border never seals. A rival tile on the region's boundary blocks absorption outright,
  however little of the boundary it forms. So does **abandoned** ground, which is held by nobody but
  is not wilderness either — absorbing it would let a neighbour inherit a razed empire.
- The seal is tested on the **same adjacency as movement**, diagonals included, so a ring the rule
  calls closed cannot be walked through at a corner. That is what the adjacency is borrowed for: on
  four-way adjacency a ring touching only at a corner would count as closed and a Force could slip
  diagonally through the gap.

Two different things seal, and they work differently.

**Impassable terrain and void seal as boundary tiles.** They are excluded from the region and count
as neutral — neither a ring member nor a veto — so a boundary part mountain and part one Player's
ground still absorbs. The rule is keyed off the same impassability movement uses, which is why it is
one sentence rather than a list naming mountains: future impassable terrain seals for free, and a map
author who makes a mountain passable moves movement and borders together rather than making the two
disagree.

**A river edge of Medium or wider seals by removing the adjacency.** A river is a natural boundary for
an area to claim, so a pocket closed on three sides by territory and on the fourth by a river is
absorbed. Diagonals use the movement rules' own corner semantics: the corner is sealed only when
**both** ways round it cross a sealing river, at the easier route's width, so a river that merely ends
at a corner does not seal it and the seal agrees with what a diagonal step would pay.

Because a sealing edge removes the adjacency rather than contributing a boundary tile, **a rival on
the far bank of a sealing river does not veto**: as far as the rule is concerned they are not on the
region's edge at all. That is the point of treating a river as a boundary.

Only one Player can hold a complete ring around a given region, so there is no boundary-ratio or
majority rule to apply and no tie to resolve. Territory is never granted because a neighbour moved.

The interior takes the **weakest tier in the ring**: a ring of claims yields a claimed interior, and
owned all the way round is needed for an owned one. That is the same principle the
[attrition timer](#the-attrition-timer) uses — land is only held as firmly as the land behind it.

The tier is then applied **per tile**, because what the ring grants and what a given tile can take are
different questions. An impassable tile cannot be claimed, so an **owned** ring absorbs a mountain in
its interior as owned, while an **only claimed** ring leaves that mountain unclaimed rather than
writing a claim that would lapse the same round. A mountain on a frontier, with wilderness still
behind it, is not absorbed at all: you do not own the far slope of the wall that shelters you.

**The region must also be bounded.** Having no open boundary is not the same as having been
encircled, and the difference decides the whole rule. The complement of a small blob of territory is
the rest of the world, which has no open boundary anywhere — so without this, founding one village
takes the planet. Ownership has to be ground a Player encircled, not ground that merely happened to
have nothing beyond it.

A region fails to be bounded in one of two ways, which are the same fact on the two kinds of axis:

- on an axis that **does not wrap**, it reaches the map's edge, and the edge of the world is not
  anybody's territory to ring against;
- on an axis that **wraps**, it occupies every column or row on that axis, which means it has met
  itself the long way round — the torus's version of running off the edge.

So **map wrapping is still not a special case**: a wrapping axis has no edge, and the span test is
what stands in for one.

The price is paid twice, and knowingly. A pocket genuinely held against the world's edge is not
absorbed, and neither is a strip walled off by a band that girdles a wrapping world. In both the
legitimate case and the pathological one the region is the same shape, and only its size tells them
apart — so the rule refuses both rather than acquiring the region size cap this design has
deliberately done without. Walling a strip that wraps is a late-game feat; a lone village is the
first thing that happens in every game, and that is the case worth being strict for.

A ring that is owned the whole way round grants an owned interior of **any size**, and the interior
is not subject to the claim supply range because it is owned rather than claimed. The cost of owning
a complete perimeter is the limit on it.

### Water

**Water is ordinary terrain.** It is owned by exactly the same three routes land is — a Settlement's
radius, a structure's assertion, enclosure — and once owned it is as sticky as land. There is no
separate water mechanic, and that is deliberate: eventually something will be buildable on water, and
under this rule that needs no new one.

The two things a projection rule would have been for both fall out of the general rules:

- **A strait is crossable.** Water is passable, so an owned stretch of sea carries a claim supply hop
  like any other tile.
- **Open ocean belongs to nobody**, because nothing out there is owned to anchor a claim. A coastal
  village owns its adjacent sea through its radius, and a claim holds within 3 hops of owned ground,
  so a Player's reach into the ocean is about four tiles from their coast.

Water is also absorbable by [enclosure](#enclosure), so **a bay ringed by one Player's coast becomes
theirs**, while a pocket open to the sea grows into a region nobody rings and is never enclosed.

**Void** tiles can never be owned or claimed by anybody. They seal an enclosure ring, and they are the
one thing in the model with no route to ownership at all.

## Occupation

A tile is **Occupied** when a rival's **visible** Forces are on it and no battle is in progress. The
owner or claimant keeps the tile.

- The **occupier collects the yield, calculated at the owner's levels** — the owner's sustenance
  modifiers, tech and Settlement-scoped modifiers. Occupation changes *who collects*, never *the
  numbers*, and regrowth stays at the owner's values too ([replenishment.md](replenishment.md)).
- Occupation **can deny the owner's Settlement** the food it was drawing from the tile, which is what
  makes a ring of occupied tiles a siege.
- Occupying a merely **claimed** tile does nothing beyond starting the timer below. There is no yield
  on a claim to take.
- A Force present under **any organised agreement** — passage, harvesting, support — is **not**
  occupying. See [foreign-relations.md](../foreign-relations.md#diplomatic-arrangements).
- A **hidden** Force is not occupying anything. Hidden presence has no effect on a tile's state at
  all, even when the owner's own Forces are standing on the same tile.

When both sides' Forces are on the tile the state is **Disputed**, and **nobody** collects the
yield: the fighting interrupts the harvest. Forces on the tile still feed from its shared `Taken`
either way ([depletion.md](depletion.md)).

### The attrition timer

Unchallenged occupation eventually transfers the tile. **Unchallenged** means no battle on that tile;
an owner's Force on an adjacent tile does not challenge it.

| Tile was | Rounds of unchallenged visible presence |
| --- | --- |
| Owned or Claimed | **10** |
| Unclaimed or Abandoned | **3** |

Only **visible** Forces count, and visible means visible **to the owner or claimant of the tile, or
their allies**. A Force in ambush or recon stance, or otherwise hidden, never starts or advances the
count — hidden Forces can neither claim nor assert ownership.

The counter behaves as follows:

- the invader **leaves** → it resets to zero;
- the invader **goes hidden** → it stops counting, keeping its value;
- a **battle** starts → it holds its value awaiting the outcome, however many rounds the battle runs.
  A battle joined at 3 rounds resumes from 3.

When the count completes, the tile transfers **at the tier it already had**, and only if the invader
has an adjacent tile of that same tier:

| Tile was | Invader needs | Result |
| --- | --- | --- |
| Owned | an adjacent **owned** tile | Transfers as owned, joining that tile's Settlement |
| Owned | only adjacent **claimed** tiles | **No transfer** |
| Claimed | an adjacent claimed or owned tile | Transfers as claimed |
| Either | nothing adjacent | **No transfer** |

So taking owned land requires bringing owned land up behind it, and neither settlement-less owned
land nor a cross-map land grab can arise. A transfer **extinguishes the former claim or ownership**.
Where the adjacent owned tile has no Settlement of its own, the gained tile has none either.

The same adjacency rule governs a battle: a winner with no adjacent tile changes nothing about who
holds the ground.

## Conquest and rebellion

Conquering a Settlement's own tile immediately transfers **every tile assigned to that Settlement**
to the conqueror. A structure asserting ownership inside that territory follows the Settlement, not
the structure.

Those tiles arrive **Rebelling**, a modifier on Owned:

- it lasts a random **1 to 5 rounds** per tile, so tiles settle individually rather than all at once;
- a rebelling tile yields **50%**;
- a tile with a **military Force on it will not rebel**;
- the conqueror may still build on a rebelling tile;
- a rebelling tile can only ever settle to its new owner. The question is how long it takes, not
  where it ends up.

**Razing** a Settlement instead leaves its tiles **Abandoned**, with no rebellion period. An
abandoned tile reverts to Unclaimed after 5 turns and forgets its former owner, and can be claimed in
3 rounds by anyone who stands on it — against the rebellion period and the rebuilding, that is the
trade between taking a Settlement and destroying one.

## Allies

- An ally's tiles count for **enclosure** and for the **claim supply range**.
- An ally successfully defending a tile leaves it with its **current owner**.
- An attacker must defeat **every** allied Force on a tile to take it.
- Where three or more Players fight on one tile, the victor takes the spoils, subject to the
  adjacency rule above.

Non-allied Players on the same tile are each fighting for themselves.

## How it is stored

The split between exclusive and non-exclusive decides the shape:

| Concept | Where |
| --- | --- |
| Claims | `game.CampaignMapClaim`, one row per Player per tile — sparse, rows only where claimed |
| Any claims | `CampaignMap.HasClaims`, the map read's licence to skip the junction for a tile |
| Owned | `CampaignMap.OwnedByPlayerId`, `OwnedBySettlementId` |
| Occupied | `CampaignMap.OccupiedByPlayerId`, `OccupiedSinceTurnId` for the timer |
| Rebelling | `CampaignMap.RebellionPercentRemaining`, counted down by the per-round pass |
| Abandoned | `CampaignMap.LastOwnedByPlayerId`, `AbandonedOnTurnId`, both cleared on revert |
| Disputed | **derived** — a `BattleEngagement` on the tile. Never stored |

Ownership keys to **Player** throughout.

Every exclusive layer is a nullable Player reference rather than a state enum, because most
combinations of parallel state enums are illegal and would have to be policed. The states at the top
of this document are read *off* those columns.

**The state is read off the tile, never cached on it.** Claims are many-to-one, so there is no winner
to record and no single state column to keep up to date.

`TileOwnershipDerivation` is that resolution, and it is pure: it takes a tile's columns, its
claimants and whether a battle is on it, and gives back the state. `CampaignOwnershipReader` is the
query behind it, and the map read reports what it says **only for tiles an Observer can see**. A
border is current information rather than terrain — unlike the Biome and the rivers, which stay known
once explored, a border moves — so ground a Player merely remembers reports no ownership at all, and
the client draws a border only where someone is watching. A Player's map carries no ownership
columns, and deliberately so: there is no remembered border to read.

**The claimants are named, not counted.** A claim is asserted by something standing on the ground — a
totem pole, a border marker, a scout's action — so a Player who can see the tile can see whose marker
is on it, and withholding the name would hide what they could plainly observe. The read hands back
every claimant in Player id order, and the client draws the claim border in each claimant's own
faction colour, alternating them along its dashes, with the same list named in the tile's tooltip.
The order is stable because it is drawn: a shifting one would make the dashes swap colours between
reads.

**There is no viewer yet.** Resolving per Player is still the design — a hidden Force is visible to
some Players and not others, so two of them will correctly see one tile differently — but nothing
hides anything today. Ambush and recon wait on hiding and detection ([vision.md](vision.md#ambush-and-recon)),
and the claimants are named to anyone who can see the ground, so what the resolution returns is
currently the same for everybody and takes no Player. The viewer comes back as a parameter when there
is something for it to change.

`HasClaims` on the tile is what keeps that read cheap. Most of a map has never had a claim row, so
the flag already loaded with the tile decides which ids are worth asking the junction about, and a
read over wilderness never touches it. It is a flag rather than a count because all the read needs is
whether to look, and "any or none" is the one fact the per-round pass can keep right without
maintaining a total that could drift from the rows it describes.

A tile carries seven nullable integers and one bit for all of this, about 28 bytes, so roughly 1 MB
on a 40,000-tile map — the same reasoning that put the river widths on the tile
([tiles.md](tiles.md#rivers)). The claim junction only holds rows where something unusual is true.

**Rebellion is a countdown, not an expiry Turn.** Conquest rolls `RebellionPercentRemaining` — that
roll is where the random 1 to 5 rounds lives — and the per-round pass takes it down from there,
setting the column back to null once it runs out. A rate the pass controls is what lets a round
subtract nothing, which is how "a tile with a military Force on it will not rebel" is spent; a fixed
expiry Turn could not pause.

Neither the roll nor the size of the step is settled, and neither is pinned in the schema: a single
check constraint holds the column to 1–100, so a step that varies by round, by tile or by what is
standing on the tile is a change to the pass alone. 0 is excluded because a tile that has finished
rebelling is nulled out rather than left resting at 0.

What does cost is the **per-round pass**: claim lapse, enclosure and the attrition timers are graph
problems over a Player's territory. They live in a pure class run over an in-memory bitmap with
deltas written back, the way `FieldOfView` is shared between the map read and the witness service
([vision.md](vision.md)), not in SQL. `TileTerritory` is that class, `CampaignTerritoryReader` loads
the bitmap, `TileTerritoryService` writes the deltas, and `ResolveTileTerritory` is the end-of-round
process that runs it — after the tiles regrow, and before victory is judged on the borders it leaves.

**`HasClaims` moves in the same save as the claim rows**, because the pass holds the complete claim
set for the Game. It also **repairs** the flag where it disagrees with the junction: a tile flagged
true with no rows behind it comes back in the deltas with the flag cleared, even though nothing else
about it moved. That is the one thing `TileOwnershipDerivation` has to tolerate, and the pass is where
it gets put right.

**The pass is monotone, and therefore idempotent.** It only ever grants ownership or takes a claim
away, and it decides both from the tile columns and the claim rows rather than from anything a
previous run left behind — so running it twice on the same state writes nothing the second time.
That is what lets it sit in a turn pipeline which is not transactional and carries no per-round stamp,
unlike the accumulating processes beside it. The rest of the pass will not inherit that for free: the
rebellion countdown, the abandoned revert and the attrition timers all accumulate, so the first of
them to join needs the stamp the other processes are waiting on.

**Where two Players could want one tile, the earlier in the turn queue takes it** —
`TurnQueueParticipant.TurnOrder`. The per-turn writes settle it by themselves, since a Player's
territory is written at the end of their own Turn; ordering the round pass's Players the same way is
what stops any of it depending on which order a dictionary happened to enumerate in.

## Not yet implemented

**Claimed, Owned and Unclaimed are now all read and written.** `CampaignMap` carries every column in
[How it is stored](#how-it-is-stored) and `game.CampaignMapClaim` exists, so nothing below is blocked
on a migration.

The read side is in place. The map read resolves every state for the tiles in an Observer's sight and
the campaign map draws ownership and claim borders from it, and `BattleEngagementService` records
what a tile was when the fighting started through the same resolution. Callers that want the
effective holder read
`CampaignMap.EffectiveHolderPlayerId`, or `CampaignMap.EFFECTIVE_HOLDER_PROJECTION` where the
question is being asked of the database, rather than spelling out `OccupiedByPlayerId ??
OwnedByPlayerId` at each site.

The write side is in place for everything a Player does to a border on purpose, and for the two
graph rules. Structures assert over their ground per `Structure.Asserts`, a Settlement takes the
territory radius of its tier, ownership extinguishes every rival claim on the ground it takes,
enclosure absorbs wilderness a Player has ringed, and a claim that cannot reach owned land lapses at
the end of the round.

`OwnedBySettlementId` gained a real foreign key in CD-1, now that there is a `settlement.Settlements`
table for it to point at. `OccupiedSinceTurnId` and `AbandonedOnTurnId` still have none, matching the
Turn stamps already on the tile: the game purge removes `turns.PlayerTurn` before the tiles.

A tile owned through a Settlement is the one **cycle** in the game purge - the tile names the Settlement,
the Settlement names its Structure, and the Structure names the tile - so the purge releases the tile's
reference before deleting any of the three.

**Occupied, Disputed, Abandoned and Rebelling are still unreachable in play.** The resolution handles
all four and will start answering with them the moment something writes their columns. What each one
waits on:

- **A scout asserting a claim directly** — a toggleable action, or a cheap one-turn marker — needs a
  new Force action with its own order and frontend. `StructureAssertion.Claim` and the branch that
  writes claim rows are both built and waiting for it; nothing in the catalogue asserts Claim yet.
- **The attrition timers** need `OccupiedSinceTurnId` written, which needs the end-of-round context to
  carry a Turn id it currently has none of.
- **The rebellion countdown** and the **abandoned 5-turn revert** both need to join the per-round pass,
  and both accumulate, so the first of them brings the per-round stamp with it.
- **Settlement growth by selection** — a grown tier granting tiles the Player places — waits on
  Settlement growth being modelled at all; the grown tiers have no authored territory radius.
- **Allies** counting for enclosure and for the supply range wait on there being an alliance model.
  The supply walk already takes a set of friendly Players, so that is a change to its caller rather
  than to the rule.
- **Organised agreements** not counting as occupation waits on the diplomatic arrangements in
  [foreign-relations.md](../foreign-relations.md), which also need Settlement scoping.
- **Ambush and recon stances**, which the timer and the hidden-presence rules depend on, wait on
  hiding and detection — see [vision.md](vision.md#ambush-and-recon).

> TODO: The hop count (3), the attrition counts (10 and 3), the rebellion range (1–5), the abandoned
> revert (5 turns) are all first values, chosen to be tuned. So is the settlement territory radius
> (1 tile for a village) and the narrowest river width that seals an enclosure ring (Medium).
